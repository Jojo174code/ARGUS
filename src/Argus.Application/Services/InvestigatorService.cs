using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Argus.Application.Services;

public sealed class InvestigatorService : IInvestigatorService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentAnalysisRepository _incidentAnalysisRepository;
    private readonly ICoordinatorRunRepository _coordinatorRunRepository;
    private readonly IInvestigatorRunRepository _investigatorRunRepository;
    private readonly IEnumerable<IInvestigationTool> _investigationTools;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<InvestigatorService> _logger;

    public InvestigatorService(
        IIncidentRepository incidentRepository,
        IIncidentAnalysisRepository incidentAnalysisRepository,
        ICoordinatorRunRepository coordinatorRunRepository,
        IInvestigatorRunRepository investigatorRunRepository,
        IEnumerable<IInvestigationTool> investigationTools,
        ILlmClient llmClient,
        ILogger<InvestigatorService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentAnalysisRepository = incidentAnalysisRepository;
        _coordinatorRunRepository = coordinatorRunRepository;
        _investigatorRunRepository = investigatorRunRepository;
        _investigationTools = investigationTools;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<InvestigatorReportDto> RunAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Investigator run requested for incident {IncidentId}", incidentId);

        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException("Incident was not found.");

        var analysisEntity = await _incidentAnalysisRepository.GetByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new InvestigatorPrerequisiteException("Deterministic analysis must be completed before running investigator.");

        var coordinatorRun = await _coordinatorRunRepository.GetLatestCompletedByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new InvestigatorPrerequisiteException("A completed coordinator plan is required before running investigator.");

        if (string.IsNullOrWhiteSpace(coordinatorRun.OutputJson))
        {
            throw new InvestigatorPrerequisiteException("The completed coordinator run has no investigation plan output.");
        }

        var coordinatorPlan = JsonSerializer.Deserialize<InvestigationPlan>(coordinatorRun.OutputJson, SerializerOptions)
            ?? throw new InvestigatorPrerequisiteException("The persisted coordinator plan could not be loaded.");

        var deterministicAnalysis = IncidentAnalysisMapper.Map(analysisEntity);
        var context = CoordinatorPromptBuilder.BuildInput(incident, deterministicAnalysis);
        var investigationContext = new InvestigationContext(
            incidentId,
            context.Incident,
            context.Findings,
            context.EvidenceItems,
            coordinatorPlan);

        var executionResults = await ExecuteTasksAsync(investigationContext, cancellationToken);

        var synthesisInput = new InvestigationSynthesisInput(
            incidentId,
            coordinatorPlan,
            context.Findings,
            executionResults.Select(result => result.Result).ToList(),
            executionResults.SelectMany(result => result.ToolResults).ToList());

        var inputJson = JsonSerializer.Serialize(synthesisInput, SerializerOptions);
        var run = new InvestigatorRun(
            incidentId,
            coordinatorRun.Id,
            string.Empty,
            InvestigatorPromptBuilder.PromptVersion,
            inputJson,
            DateTimeOffset.UtcNow);

        await _investigatorRunRepository.AddAsync(run, cancellationToken);
        await _investigatorRunRepository.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Investigator LLM invocation started for incident {IncidentId}", incidentId);

            var (synthesis, model) = await GenerateValidatedSynthesisAsync(synthesisInput, coordinatorPlan, cancellationToken);
            var report = new InvestigationReport(
                incidentId,
                synthesis.Classification,
                synthesis.Severity,
                synthesis.Confidence,
                synthesis.Findings,
                synthesis.AttackTechniques,
                synthesis.PossibleImpact,
                synthesis.Uncertainties,
                executionResults.Select(result => result.Result).ToList());

            report = NormalizeEvidenceReferences(report);

            var evidenceReferences = executionResults
                .SelectMany(result => result.Result.EvidenceReferences)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var validationErrors = InvestigationReportValidator.Validate(report, incidentId, coordinatorPlan, evidenceReferences);
            if (validationErrors.Count > 0)
            {
                throw new InvestigatorValidationException("Investigator output failed validation.", validationErrors);
            }

            run.SetModel(model);
            run.MarkCompleted(JsonSerializer.Serialize(report, SerializerOptions), DateTimeOffset.UtcNow);
            await _investigatorRunRepository.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Investigator run completed for incident {IncidentId} in {ElapsedMs} ms with {FindingCount} findings",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                report.Findings.Count);

            return ToDto(run, report);
        }
        catch (InvestigatorValidationException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _investigatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Investigator output validation failed for incident {IncidentId} after {ElapsedMs} ms: {Message}",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        catch (InvestigatorUnavailableException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _investigatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                ex,
                "Investigator run failed due to LLM availability for incident {IncidentId} after {ElapsedMs} ms",
                incidentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            run.MarkFailed("Investigator run failed.", DateTimeOffset.UtcNow);
            await _investigatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Investigator run failed for incident {IncidentId} after {ElapsedMs} ms", incidentId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<InvestigatorReportDto?> GetLatestSuccessfulReportAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var run = await _investigatorRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var report = string.IsNullOrWhiteSpace(run.OutputJson)
            ? null
            : JsonSerializer.Deserialize<InvestigationReport>(run.OutputJson, SerializerOptions);

        return ToDto(run, report);
    }

    private async Task<IReadOnlyList<InvestigationTaskExecution>> ExecuteTasksAsync(
        InvestigationContext context,
        CancellationToken cancellationToken)
    {
        var executions = new List<InvestigationTaskExecution>();

        foreach (var task in context.CoordinatorPlan.Tasks.OrderBy(task => task.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Task execution started for incident {IncidentId}: task {TaskId} ({TaskType})", context.IncidentId, task.Id, task.TaskType);

            var matchingTools = _investigationTools.Where(tool => tool.CanExecute(task)).ToList();
            if (matchingTools.Count == 0)
            {
                _logger.LogInformation("Unsupported task encountered for incident {IncidentId}: task {TaskId} ({TaskType})", context.IncidentId, task.Id, task.TaskType);
                executions.Add(new InvestigationTaskExecution(
                    task,
                    new TaskExecutionResult(
                        task.Id,
                        task.TaskType,
                        InvestigationTaskExecutionStatus.Unsupported.ToString(),
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        "No supported deterministic investigation tool is available for this task type."),
                    Array.Empty<ToolResult>()));
                continue;
            }

            var toolResults = new List<ToolResult>();
            foreach (var tool in matchingTools)
            {
                var result = await tool.ExecuteAsync(context, task, cancellationToken);
                toolResults.Add(result);
                _logger.LogInformation(
                    "Tool executed for incident {IncidentId}: task {TaskId}, tool {ToolName}, success {Success}",
                    context.IncidentId,
                    task.Id,
                    result.ToolName,
                    result.Success);
            }

            var taskStatus = toolResults.All(result => result.Success)
                ? InvestigationTaskExecutionStatus.Completed
                : InvestigationTaskExecutionStatus.Failed;

            var evidenceReferences = toolResults.SelectMany(result => result.EvidenceReferences)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var summary = taskStatus == InvestigationTaskExecutionStatus.Completed
                ? "Task completed using deterministic tools."
                : "Task executed but one or more tool operations failed.";

            executions.Add(new InvestigationTaskExecution(
                task,
                new TaskExecutionResult(
                    task.Id,
                    task.TaskType,
                    taskStatus.ToString(),
                    toolResults.Select(result => result.ToolName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    evidenceReferences,
                    summary),
                toolResults));
        }

        return executions;
    }

    private async Task<(InvestigationReportSynthesis Synthesis, string Model)> GenerateValidatedSynthesisAsync(
        InvestigationSynthesisInput input,
        InvestigationPlan coordinatorPlan,
        CancellationToken cancellationToken)
    {
        var request = InvestigatorPromptBuilder.BuildRequest(input);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            LlmResponse response;
            try
            {
                response = await _llmClient.GenerateAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvestigatorUnavailableException("Investigator LLM provider is not configured.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new InvestigatorUnavailableException("Investigator LLM provider is unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvestigatorUnavailableException("Investigator LLM provider timed out.", ex);
            }

            InvestigationReportSynthesis? synthesis = null;
            try
            {
                synthesis = JsonSerializer.Deserialize<InvestigationReportSynthesis>(response.Content, SerializerOptions);
            }
            catch (JsonException ex)
            {
                lastError = ex;
            }

            if (synthesis is not null)
            {
                if (synthesis.IncidentId == input.IncidentId
                    && synthesis.Confidence is >= 0 and <= 1
                    && synthesis.Findings is not null
                    && synthesis.AttackTechniques is not null
                    && synthesis.PossibleImpact is not null
                    && synthesis.Uncertainties is not null)
                {
                    return (synthesis, string.IsNullOrWhiteSpace(response.Model) ? "unknown" : response.Model);
                }

                lastError = new InvalidOperationException("Investigator synthesis did not pass basic shape checks.");
            }

            if (attempt == 1)
            {
                request = request with
                {
                    UserPrompt = $$"""
                    The prior output failed validation. Return only schema-valid JSON.

                    ORIGINAL REQUEST:
                    {{request.UserPrompt}}
                    """
                };
                continue;
            }
        }

        throw new InvestigatorValidationException(
            "Investigator output failed validation after retry.",
            [lastError?.Message ?? "Investigator output was invalid."]);
    }

    private static InvestigatorReportDto ToDto(InvestigatorRun run, InvestigationReport? report)
    {
        return new InvestigatorReportDto(
            run.IncidentId,
            run.Status.ToString(),
            string.IsNullOrWhiteSpace(run.Model) ? "unknown" : run.Model,
            run.PromptVersion,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            report);
    }

    private static InvestigationReport NormalizeEvidenceReferences(InvestigationReport report)
    {
        var evidenceReferences = report.TaskResults
            .SelectMany(task => task.EvidenceReferences)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var taskEvidenceReferences = report.TaskResults
            .Where(task => !string.IsNullOrWhiteSpace(task.TaskId))
            .ToDictionary(
                task => task.TaskId,
                task => task.EvidenceReferences
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var normalizedFindings = report.Findings.Select(finding =>
        {
            var normalizedReference = ResolveEvidenceReference(
                finding.EvidenceReference,
                finding.TaskId,
                evidenceReferences,
                taskEvidenceReferences);

            return string.Equals(normalizedReference, finding.EvidenceReference, StringComparison.Ordinal)
                ? finding
                : finding with { EvidenceReference = normalizedReference };
        }).ToList();

        return normalizedFindings.SequenceEqual(report.Findings)
            ? report
            : report with { Findings = normalizedFindings };
    }

    private static string ResolveEvidenceReference(
        string evidenceReference,
        string? taskId,
        HashSet<string> knownEvidenceReferences,
        Dictionary<string, List<string>> taskEvidenceReferences)
    {
        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            if (!string.IsNullOrWhiteSpace(taskId)
                && taskEvidenceReferences.TryGetValue(taskId, out var taskReferences)
                && taskReferences.Count > 0)
            {
                return taskReferences[0];
            }

            return evidenceReference;
        }

        if (knownEvidenceReferences.Contains(evidenceReference))
        {
            return evidenceReference;
        }

        if (taskEvidenceReferences.TryGetValue(evidenceReference, out var matchingTaskReferences)
            && matchingTaskReferences.Count > 0)
        {
            return matchingTaskReferences[0];
        }

        foreach (var token in evidenceReference.Split([',', ';', '|', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (knownEvidenceReferences.Contains(token))
            {
                return token;
            }
        }

        if (!string.IsNullOrWhiteSpace(taskId)
            && taskEvidenceReferences.TryGetValue(taskId, out var referencesForTask)
            && referencesForTask.Count > 0)
        {
            return referencesForTask[0];
        }

        return evidenceReference;
    }
}