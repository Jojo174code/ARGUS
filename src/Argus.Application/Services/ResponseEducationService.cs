using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Argus.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Argus.Application.Services;

public sealed class ResponseEducationService : IResponseEducationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentAnalysisRepository _incidentAnalysisRepository;
    private readonly ICoordinatorRunRepository _coordinatorRunRepository;
    private readonly IInvestigatorRunRepository _investigatorRunRepository;
    private readonly IResponseEducationRunRepository _responseEducationRunRepository;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<ResponseEducationService> _logger;

    public ResponseEducationService(
        IIncidentRepository incidentRepository,
        IIncidentAnalysisRepository incidentAnalysisRepository,
        ICoordinatorRunRepository coordinatorRunRepository,
        IInvestigatorRunRepository investigatorRunRepository,
        IResponseEducationRunRepository responseEducationRunRepository,
        ILlmClient llmClient,
        ILogger<ResponseEducationService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentAnalysisRepository = incidentAnalysisRepository;
        _coordinatorRunRepository = coordinatorRunRepository;
        _investigatorRunRepository = investigatorRunRepository;
        _responseEducationRunRepository = responseEducationRunRepository;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<ResponseEducationPackageDto> GenerateAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Response and education generation requested for incident {IncidentId}", incidentId);

        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException("Incident was not found.");

        var analysisEntity = await _incidentAnalysisRepository.GetByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new ResponseEducationPrerequisiteException("Deterministic analysis must be completed before generating response and education guidance.");

        var coordinatorRun = await _coordinatorRunRepository.GetLatestCompletedByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new ResponseEducationPrerequisiteException("A completed coordinator plan is required before generating response and education guidance.");

        if (string.IsNullOrWhiteSpace(coordinatorRun.OutputJson))
        {
            throw new ResponseEducationPrerequisiteException("The completed coordinator run has no persisted plan output.");
        }

        var investigatorRun = await _investigatorRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new ResponseEducationPrerequisiteException("A completed investigator report is required before generating response and education guidance.");

        if (string.IsNullOrWhiteSpace(investigatorRun.OutputJson))
        {
            throw new ResponseEducationPrerequisiteException("The completed investigator run has no persisted report output.");
        }

        var coordinatorPlan = JsonSerializer.Deserialize<InvestigationPlan>(coordinatorRun.OutputJson, SerializerOptions)
            ?? throw new ResponseEducationPrerequisiteException("The persisted coordinator plan could not be loaded.");

        var investigatorReport = JsonSerializer.Deserialize<InvestigationReport>(investigatorRun.OutputJson, SerializerOptions)
            ?? throw new ResponseEducationPrerequisiteException("The persisted investigator report could not be loaded.");

        var responseInput = new ResponseEducationInput(
            incidentId,
            incident.TechnicalSkillLevel.ToString(),
            investigatorReport.Classification,
            investigatorReport.Severity,
            investigatorReport.Findings,
            investigatorReport.Uncertainties);

        var inputJson = JsonSerializer.Serialize(responseInput, SerializerOptions);
        var run = new ResponseEducationRun(
            incidentId,
            investigatorRun.Id,
            string.Empty,
            ResponseEducationPromptBuilder.PromptVersion,
            inputJson,
            DateTimeOffset.UtcNow);

        await _responseEducationRunRepository.AddAsync(run, cancellationToken);
        await _responseEducationRunRepository.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Response and education LLM invocation started for incident {IncidentId}", incidentId);

            var knownFindingIds = investigatorReport.Findings
                .Select(finding => finding.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var (package, model) = await GenerateValidatedPackageAsync(responseInput, knownFindingIds, cancellationToken);

            run.SetModel(model);
            run.MarkCompleted(JsonSerializer.Serialize(package, SerializerOptions), DateTimeOffset.UtcNow);
            await _responseEducationRunRepository.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Response and education run completed for incident {IncidentId} in {ElapsedMs} ms with {ActionCount} actions and {QuestionCount} questions",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                package.ImmediateActions.Count + package.RecoveryActions.Count + package.PreventionActions.Count,
                package.Education.Questions.Count);

            return ToDto(run, package);
        }
        catch (ResponseEducationValidationException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _responseEducationRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Response and education output validation failed for incident {IncidentId} after {ElapsedMs} ms: {Message}",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        catch (ResponseEducationUnavailableException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _responseEducationRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                ex,
                "Response and education run failed due to LLM availability for incident {IncidentId} after {ElapsedMs} ms",
                incidentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            run.MarkFailed("Response and education run failed.", DateTimeOffset.UtcNow);
            await _responseEducationRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Response and education run failed for incident {IncidentId} after {ElapsedMs} ms", incidentId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<ResponseEducationPackageDto?> GetLatestSuccessfulPackageAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var run = await _responseEducationRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var package = string.IsNullOrWhiteSpace(run.OutputJson)
            ? null
            : JsonSerializer.Deserialize<ResponseEducationPackage>(run.OutputJson, SerializerOptions);

        return ToDto(run, package);
    }

    private async Task<(ResponseEducationPackage Package, string Model)> GenerateValidatedPackageAsync(
        ResponseEducationInput input,
        IReadOnlyCollection<string> knownFindingIds,
        CancellationToken cancellationToken)
    {
        var request = ResponseEducationPromptBuilder.BuildRequest(input);
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
                throw new ResponseEducationUnavailableException("Response and education LLM provider is not configured.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ResponseEducationUnavailableException("Response and education LLM provider is unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new ResponseEducationUnavailableException("Response and education LLM provider timed out.", ex);
            }

            ResponseEducationSynthesis? synthesis = null;
            try
            {
                synthesis = JsonSerializer.Deserialize<ResponseEducationSynthesis>(response.Content, SerializerOptions);
            }
            catch (JsonException ex)
            {
                lastError = ex;
            }

            if (synthesis is not null)
            {
                if (synthesis.IncidentId == input.IncidentId
                    && synthesis.ImmediateActions is not null
                    && synthesis.RecoveryActions is not null
                    && synthesis.PreventionActions is not null
                    && synthesis.EscalationRecommendations is not null
                    && synthesis.Education is not null
                    && synthesis.Assumptions is not null
                    && synthesis.Limitations is not null)
                {
                    var package = new ResponseEducationPackage(
                        input.IncidentId,
                        synthesis.IncidentClassification,
                        synthesis.OverallPriority,
                        synthesis.PlainLanguageSummary,
                        synthesis.ImmediateActions,
                        synthesis.RecoveryActions,
                        synthesis.PreventionActions,
                        synthesis.EscalationRecommendations,
                        synthesis.Education,
                        synthesis.Assumptions,
                        synthesis.Limitations);

                    var validationErrors = ResponseEducationPackageValidator.Validate(package, input.IncidentId, knownFindingIds);
                    if (validationErrors.Count == 0)
                    {
                        return (package, string.IsNullOrWhiteSpace(response.Model) ? "unknown" : response.Model);
                    }

                    lastError = new ResponseEducationValidationException("Response and education output failed validation.", validationErrors);
                }
                else
                {
                    lastError = new InvalidOperationException("Response and education synthesis did not pass basic shape checks.");
                }
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
            }
        }

        if (lastError is ResponseEducationValidationException validationException)
        {
            throw new ResponseEducationValidationException(
                "Response and education output failed validation after retry.",
                validationException.ValidationErrors);
        }

        throw new ResponseEducationValidationException(
            "Response and education output failed validation after retry.",
            [lastError?.Message ?? "Response and education output was invalid."]);
    }

    private static ResponseEducationPackageDto ToDto(ResponseEducationRun run, ResponseEducationPackage? package)
    {
        return new ResponseEducationPackageDto(
            run.IncidentId,
            run.Status.ToString(),
            string.IsNullOrWhiteSpace(run.Model) ? "unknown" : run.Model,
            run.PromptVersion,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            package);
    }
}