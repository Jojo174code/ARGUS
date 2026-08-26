using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Argus.Application.Services;

public sealed class AgenticWorkflowService : IAgenticWorkflowService
{
    private const string DeterministicAnalysisStage = "DeterministicAnalysis";
    private const string CoordinatorStage = "Coordinator";
    private const string InvestigatorStage = "Investigator";
    private const string ResponseEducationStage = "ResponseEducation";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentInvestigationService _incidentInvestigationService;
    private readonly ICoordinatorService _coordinatorService;
    private readonly IInvestigatorService _investigatorService;
    private readonly IResponseEducationService _responseEducationService;
    private readonly ICoordinatorRunRepository _coordinatorRunRepository;
    private readonly IInvestigatorRunRepository _investigatorRunRepository;
    private readonly IResponseEducationRunRepository _responseEducationRunRepository;
    private readonly IAgenticWorkflowRunRepository _workflowRunRepository;
    private readonly ILogger<AgenticWorkflowService> _logger;

    public AgenticWorkflowService(
        IIncidentRepository incidentRepository,
        IIncidentInvestigationService incidentInvestigationService,
        ICoordinatorService coordinatorService,
        IInvestigatorService investigatorService,
        IResponseEducationService responseEducationService,
        ICoordinatorRunRepository coordinatorRunRepository,
        IInvestigatorRunRepository investigatorRunRepository,
        IResponseEducationRunRepository responseEducationRunRepository,
        IAgenticWorkflowRunRepository workflowRunRepository,
        ILogger<AgenticWorkflowService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentInvestigationService = incidentInvestigationService;
        _coordinatorService = coordinatorService;
        _investigatorService = investigatorService;
        _responseEducationService = responseEducationService;
        _coordinatorRunRepository = coordinatorRunRepository;
        _investigatorRunRepository = investigatorRunRepository;
        _responseEducationRunRepository = responseEducationRunRepository;
        _workflowRunRepository = workflowRunRepository;
        _logger = logger;
    }

    public async Task<AgenticWorkflowResultDto> RunAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException("Incident was not found.");

        var latestWorkflow = await _workflowRunRepository.GetLatestByIncidentIdAsync(incidentId, cancellationToken);
        if (latestWorkflow is not null && latestWorkflow.Status == WorkflowRunStatus.Completed)
        {
            return ToDto(latestWorkflow);
        }

        var workflow = latestWorkflow ?? new AgenticWorkflowRun(
            incidentId,
            DeterministicAnalysisStage,
            JsonSerializer.Serialize(CreateDefaultStages(), SerializerOptions),
            DateTimeOffset.UtcNow);

        if (latestWorkflow is null)
        {
            await _workflowRunRepository.AddAsync(workflow, cancellationToken);
        }

        await _workflowRunRepository.SaveChangesAsync(cancellationToken);

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["WorkflowRunId"] = workflow.Id,
            ["IncidentId"] = incidentId
        });

        _logger.LogInformation("Unified workflow requested for incident {IncidentId}", incidentId);

        var stages = DeserializeStages(workflow.StageResultsJson);
        var blockingMissingInformation = DeserializeBlockingItems(workflow.BlockingMissingInformationJson);
        WorkflowSummaryMetrics metrics;

        try
        {
            stages = MarkStageRunning(stages, DeterministicAnalysisStage);
            workflow.SetRunning(DeterministicAnalysisStage, SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            var analysis = await _incidentInvestigationService.GetAnalysisAsync(incidentId, cancellationToken);
            if (analysis is null)
            {
                analysis = await _incidentInvestigationService.AnalyzeAsync(incidentId, cancellationToken);
                stages = MarkStageCompleted(stages, DeterministicAnalysisStage, $"Generated deterministic analysis with risk {analysis.RiskLevel} and score {analysis.RiskScore}.");
            }
            else
            {
                stages = MarkStageCompleted(stages, DeterministicAnalysisStage, $"Reused deterministic analysis with risk {analysis.RiskLevel} and score {analysis.RiskScore}.");
            }

            workflow.SetStageResults(SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            stages = MarkStageRunning(stages, CoordinatorStage);
            workflow.SetRunning(CoordinatorStage, SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            var coordinatorRun = await _coordinatorRunRepository.GetLatestCompletedByIncidentIdAsync(incidentId, cancellationToken);
            CoordinatorPlanDto coordinatorPlanDto;
            InvestigationPlan coordinatorPlan;

            if (coordinatorRun is not null && !string.IsNullOrWhiteSpace(coordinatorRun.OutputJson))
            {
                coordinatorPlan = JsonSerializer.Deserialize<InvestigationPlan>(coordinatorRun.OutputJson, SerializerOptions)
                    ?? throw new InvalidOperationException("The persisted coordinator plan could not be loaded.");
                coordinatorPlanDto = new CoordinatorPlanDto(
                    coordinatorRun.IncidentId,
                    coordinatorRun.Status.ToString(),
                    string.IsNullOrWhiteSpace(coordinatorRun.Model) ? "unknown" : coordinatorRun.Model,
                    coordinatorRun.PromptVersion,
                    coordinatorRun.StartedAt,
                    coordinatorRun.CompletedAt,
                    coordinatorRun.ErrorMessage,
                    coordinatorPlan);
                stages = MarkStageCompleted(stages, CoordinatorStage, $"Reused coordinator plan with {coordinatorPlan.Tasks.Count} tasks.");
            }
            else
            {
                coordinatorPlanDto = await _coordinatorService.GeneratePlanAsync(incidentId, cancellationToken);
                coordinatorPlan = coordinatorPlanDto.Plan ?? throw new InvalidOperationException("Coordinator returned no plan.");
                coordinatorRun = await _coordinatorRunRepository.GetLatestCompletedByIncidentIdAsync(incidentId, cancellationToken)
                    ?? throw new InvalidOperationException("Coordinator run was not persisted.");
                stages = MarkStageCompleted(stages, CoordinatorStage, $"Generated coordinator plan with {coordinatorPlan.Tasks.Count} tasks.");
            }

            workflow.LinkCoordinatorRun(coordinatorRun.Id);

            blockingMissingInformation = coordinatorPlan.MissingInformation
                .Where(item => item.BlocksInvestigation)
                .ToList();

            workflow.SetBlockingMissingInformation(blockingMissingInformation.Count == 0
                ? null
                : JsonSerializer.Serialize(blockingMissingInformation, SerializerOptions));
            workflow.SetStageResults(SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            if (blockingMissingInformation.Count > 0)
            {
                stages = MarkStageAwaitingInformation(stages, CoordinatorStage, "Blocking information is still required before investigation can continue.");
                workflow.SetAwaitingInformation(
                    CoordinatorStage,
                    SerializeStages(stages),
                    JsonSerializer.Serialize(blockingMissingInformation, SerializerOptions),
                    "ARGUS needs more information before continuing.");
                metrics = BuildMetrics(workflow, coordinatorPlan, null, null);
                workflow.SetSummaryMetrics(JsonSerializer.Serialize(metrics, SerializerOptions));
                await _workflowRunRepository.SaveChangesAsync(cancellationToken);
                return ToDto(workflow);
            }

            stages = MarkStageRunning(stages, InvestigatorStage);
            workflow.SetRunning(InvestigatorStage, SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            var investigatorRun = await _investigatorRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);
            InvestigatorReportDto investigatorReportDto;
            InvestigationReport investigatorReport;

            if (investigatorRun is not null
                && investigatorRun.CoordinatorRunId == coordinatorRun.Id
                && !string.IsNullOrWhiteSpace(investigatorRun.OutputJson))
            {
                investigatorReport = JsonSerializer.Deserialize<InvestigationReport>(investigatorRun.OutputJson, SerializerOptions)
                    ?? throw new InvalidOperationException("The persisted investigator report could not be loaded.");
                investigatorReportDto = new InvestigatorReportDto(
                    investigatorRun.IncidentId,
                    investigatorRun.Status.ToString(),
                    string.IsNullOrWhiteSpace(investigatorRun.Model) ? "unknown" : investigatorRun.Model,
                    investigatorRun.PromptVersion,
                    investigatorRun.StartedAt,
                    investigatorRun.CompletedAt,
                    investigatorRun.ErrorMessage,
                    investigatorReport);
                var unsupportedTaskCount = investigatorReport.TaskResults.Count(result => string.Equals(result.Status, "Unsupported", StringComparison.OrdinalIgnoreCase));
                stages = MarkStageCompleted(stages, InvestigatorStage, $"Reused investigator report with {investigatorReport.Findings.Count} findings and {unsupportedTaskCount} unsupported tasks.");
            }
            else
            {
                investigatorReportDto = await _investigatorService.RunAsync(incidentId, cancellationToken);
                investigatorReport = investigatorReportDto.Report ?? throw new InvalidOperationException("Investigator returned no report.");
                investigatorRun = await _investigatorRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken)
                    ?? throw new InvalidOperationException("Investigator run was not persisted.");
                var unsupportedTaskCount = investigatorReport.TaskResults.Count(result => string.Equals(result.Status, "Unsupported", StringComparison.OrdinalIgnoreCase));
                stages = MarkStageCompleted(stages, InvestigatorStage, $"Generated investigator report with {investigatorReport.Findings.Count} findings and {unsupportedTaskCount} unsupported tasks.");
            }

            workflow.LinkInvestigatorRun(investigatorRun.Id);
            workflow.SetStageResults(SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            stages = MarkStageRunning(stages, ResponseEducationStage);
            workflow.SetRunning(ResponseEducationStage, SerializeStages(stages));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            var responseRun = await _responseEducationRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);
            ResponseEducationPackageDto responsePackageDto;
            ResponseEducationPackage responsePackage;

            if (responseRun is not null
                && responseRun.InvestigatorRunId == investigatorRun.Id
                && !string.IsNullOrWhiteSpace(responseRun.OutputJson))
            {
                responsePackage = JsonSerializer.Deserialize<ResponseEducationPackage>(responseRun.OutputJson, SerializerOptions)
                    ?? throw new InvalidOperationException("The persisted response and education package could not be loaded.");
                responsePackageDto = new ResponseEducationPackageDto(
                    responseRun.IncidentId,
                    responseRun.Status.ToString(),
                    string.IsNullOrWhiteSpace(responseRun.Model) ? "unknown" : responseRun.Model,
                    responseRun.PromptVersion,
                    responseRun.StartedAt,
                    responseRun.CompletedAt,
                    responseRun.ErrorMessage,
                    responsePackage);
                stages = MarkStageCompleted(stages, ResponseEducationStage, $"Reused response package with {responsePackage.ImmediateActions.Count + responsePackage.RecoveryActions.Count + responsePackage.PreventionActions.Count} actions and {responsePackage.Education.Questions.Count} questions.");
            }
            else
            {
                responsePackageDto = await _responseEducationService.GenerateAsync(incidentId, cancellationToken);
                responsePackage = responsePackageDto.Package ?? throw new InvalidOperationException("Response and education returned no package.");
                responseRun = await _responseEducationRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken)
                    ?? throw new InvalidOperationException("Response and education run was not persisted.");
                stages = MarkStageCompleted(stages, ResponseEducationStage, $"Generated response package with {responsePackage.ImmediateActions.Count + responsePackage.RecoveryActions.Count + responsePackage.PreventionActions.Count} actions and {responsePackage.Education.Questions.Count} questions.");
            }

            workflow.LinkResponseEducationRun(responseRun.Id);
            metrics = BuildMetrics(workflow, coordinatorPlan, investigatorReport, responsePackage);
            workflow.SetCompleted(
                ResponseEducationStage,
                SerializeStages(stages),
                JsonSerializer.Serialize(metrics, SerializerOptions));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Unified workflow completed for incident {IncidentId} with {TaskCount} coordinator tasks, {FindingCount} findings, and {ActionCount} response actions",
                incidentId,
                metrics.CoordinatorTaskCount,
                metrics.InvestigatorFindingCount,
                metrics.ResponseActionCount);

            return ToDto(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unified workflow failed for incident {IncidentId} at stage {Stage}", incidentId, workflow.CurrentStage);
            stages = MarkStageFailed(stages, workflow.CurrentStage, ex.Message);
            workflow.SetFailed(workflow.CurrentStage, SerializeStages(stages), ex.Message);
            workflow.SetSummaryMetrics(JsonSerializer.Serialize(BuildMetrics(workflow, null, null, null), SerializerOptions));
            await _workflowRunRepository.SaveChangesAsync(cancellationToken);
            return ToDto(workflow);
        }
    }

    public async Task<AgenticWorkflowResultDto?> GetLatestAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var workflow = await _workflowRunRepository.GetLatestByIncidentIdAsync(incidentId, cancellationToken);
        return workflow is null ? null : ToDto(workflow);
    }

    private static IReadOnlyList<WorkflowStageResult> CreateDefaultStages()
    {
        return
        [
            new WorkflowStageResult(DeterministicAnalysisStage, WorkflowRunStatus.Pending.ToString(), null, null, null, null),
            new WorkflowStageResult(CoordinatorStage, WorkflowRunStatus.Pending.ToString(), null, null, null, null),
            new WorkflowStageResult(InvestigatorStage, WorkflowRunStatus.Pending.ToString(), null, null, null, null),
            new WorkflowStageResult(ResponseEducationStage, WorkflowRunStatus.Pending.ToString(), null, null, null, null)
        ];
    }

    private static List<WorkflowStageResult> DeserializeStages(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? CreateDefaultStages().ToList()
            : JsonSerializer.Deserialize<List<WorkflowStageResult>>(json, SerializerOptions) ?? CreateDefaultStages().ToList();
    }

    private static List<MissingInformationItem> DeserializeBlockingItems(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<MissingInformationItem>>(json, SerializerOptions) ?? [];
    }

    private static string SerializeStages(IReadOnlyList<WorkflowStageResult> stages)
    {
        return JsonSerializer.Serialize(stages, SerializerOptions);
    }

    private static List<WorkflowStageResult> MarkStageRunning(IReadOnlyList<WorkflowStageResult> stages, string stageName)
    {
        return UpdateStage(stages, stageName, WorkflowRunStatus.Running.ToString(), null, null, setCompletedAt: false);
    }

    private static List<WorkflowStageResult> MarkStageCompleted(IReadOnlyList<WorkflowStageResult> stages, string stageName, string summary)
    {
        return UpdateStage(stages, stageName, WorkflowRunStatus.Completed.ToString(), summary, null, setCompletedAt: true);
    }

    private static List<WorkflowStageResult> MarkStageAwaitingInformation(IReadOnlyList<WorkflowStageResult> stages, string stageName, string summary)
    {
        return UpdateStage(stages, stageName, WorkflowRunStatus.AwaitingInformation.ToString(), summary, null, setCompletedAt: true);
    }

    private static List<WorkflowStageResult> MarkStageFailed(IReadOnlyList<WorkflowStageResult> stages, string stageName, string error)
    {
        return UpdateStage(stages, stageName, WorkflowRunStatus.Failed.ToString(), null, error, setCompletedAt: true);
    }

    private static List<WorkflowStageResult> UpdateStage(
        IReadOnlyList<WorkflowStageResult> stages,
        string stageName,
        string status,
        string? summary,
        string? error,
        bool setCompletedAt)
    {
        var now = DateTimeOffset.UtcNow;
        return stages
            .Select(stage =>
            {
                if (!string.Equals(stage.Stage, stageName, StringComparison.Ordinal))
                {
                    return stage;
                }

                return stage with
                {
                    Status = status,
                    StartedAt = stage.StartedAt ?? now,
                    CompletedAt = setCompletedAt ? now : null,
                    Summary = summary,
                    Error = error
                };
            })
            .ToList();
    }

    private static WorkflowSummaryMetrics BuildMetrics(
        AgenticWorkflowRun workflow,
        InvestigationPlan? coordinatorPlan,
        InvestigationReport? investigatorReport,
        ResponseEducationPackage? responsePackage)
    {
        var responseActionCount = responsePackage is null
            ? 0
            : responsePackage.ImmediateActions.Count + responsePackage.RecoveryActions.Count + responsePackage.PreventionActions.Count;

        var unsupportedTaskCount = investigatorReport?.TaskResults.Count(result => string.Equals(result.Status, "Unsupported", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var duration = (workflow.CompletedAt ?? DateTimeOffset.UtcNow) - workflow.StartedAt;

        return new WorkflowSummaryMetrics(
            coordinatorPlan?.Tasks.Count ?? 0,
            investigatorReport?.Findings.Count ?? 0,
            unsupportedTaskCount,
            responseActionCount,
            responsePackage?.Education.Questions.Count ?? 0,
            Math.Max(0, duration.TotalMilliseconds));
    }

    private static AgenticWorkflowResultDto ToDto(AgenticWorkflowRun workflow)
    {
        var stages = DeserializeStages(workflow.StageResultsJson);
        var blockingItems = DeserializeBlockingItems(workflow.BlockingMissingInformationJson);
        var metrics = string.IsNullOrWhiteSpace(workflow.SummaryMetricsJson)
            ? new WorkflowSummaryMetrics(0, 0, 0, 0, 0, Math.Max(0, ((workflow.CompletedAt ?? DateTimeOffset.UtcNow) - workflow.StartedAt).TotalMilliseconds))
            : JsonSerializer.Deserialize<WorkflowSummaryMetrics>(workflow.SummaryMetricsJson, SerializerOptions)
                ?? new WorkflowSummaryMetrics(0, 0, 0, 0, 0, Math.Max(0, ((workflow.CompletedAt ?? DateTimeOffset.UtcNow) - workflow.StartedAt).TotalMilliseconds));

        return new AgenticWorkflowResultDto(
            workflow.IncidentId,
            workflow.Id,
            workflow.Status.ToString(),
            stages,
            workflow.StartedAt,
            workflow.CompletedAt,
            workflow.FailureStage,
            workflow.FailureMessage,
            blockingItems,
            metrics);
    }
}