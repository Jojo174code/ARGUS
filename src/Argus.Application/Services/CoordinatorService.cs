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

public sealed class CoordinatorService : ICoordinatorService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentAnalysisRepository _incidentAnalysisRepository;
    private readonly ICoordinatorRunRepository _coordinatorRunRepository;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<CoordinatorService> _logger;

    public CoordinatorService(
        IIncidentRepository incidentRepository,
        IIncidentAnalysisRepository incidentAnalysisRepository,
        ICoordinatorRunRepository coordinatorRunRepository,
        ILlmClient llmClient,
        ILogger<CoordinatorService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentAnalysisRepository = incidentAnalysisRepository;
        _coordinatorRunRepository = coordinatorRunRepository;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<CoordinatorPlanDto> GeneratePlanAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Coordinator plan requested for incident {IncidentId}", incidentId);

        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException("Incident was not found.");

        var deterministicAnalysisEntity = await _incidentAnalysisRepository.GetByIncidentIdAsync(incidentId, cancellationToken)
            ?? throw new CoordinatorPrerequisiteException("Deterministic analysis must be completed before generating a coordinator plan.");

        var deterministicAnalysis = IncidentAnalysisMapper.Map(deterministicAnalysisEntity);
        var coordinatorInput = CoordinatorPromptBuilder.BuildInput(incident, deterministicAnalysis);
        var inputJson = JsonSerializer.Serialize(coordinatorInput, SerializerOptions);
        var startTime = DateTimeOffset.UtcNow;
        var run = new CoordinatorRun(incidentId, string.Empty, CoordinatorPromptBuilder.PromptVersion, inputJson, startTime);

        await _coordinatorRunRepository.AddAsync(run, cancellationToken);
        await _coordinatorRunRepository.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var (generatedPlan, model) = await GenerateValidatedPlanAsync(incidentId, coordinatorInput, cancellationToken);
            var outputJson = JsonSerializer.Serialize(generatedPlan, SerializerOptions);

            run.SetModel(model);
            var completedAt = DateTimeOffset.UtcNow;
            run.MarkCompleted(outputJson, completedAt);
            await _coordinatorRunRepository.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Coordinator plan completed for incident {IncidentId} in {ElapsedMs} ms with {TaskCount} tasks",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                generatedPlan.Tasks.Count);

            return ToDto(run, generatedPlan);
        }
        catch (CoordinatorPlanValidationException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _coordinatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Coordinator output validation failed for incident {IncidentId} after {ElapsedMs} ms: {Message}",
                incidentId,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        catch (CoordinatorUnavailableException ex)
        {
            stopwatch.Stop();
            run.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
            await _coordinatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                ex,
                "Coordinator invocation failed for incident {IncidentId} after {ElapsedMs} ms",
                incidentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            run.MarkFailed("Coordinator invocation failed.", DateTimeOffset.UtcNow);
            await _coordinatorRunRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Coordinator invocation failed for incident {IncidentId} after {ElapsedMs} ms", incidentId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<CoordinatorPlanDto?> GetLatestPlanAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var latestRun = await _coordinatorRunRepository.GetLatestByIncidentIdAsync(incidentId, cancellationToken);
        if (latestRun is null)
        {
            return null;
        }

        InvestigationPlan? plan = null;
        if (!string.IsNullOrWhiteSpace(latestRun.OutputJson))
        {
            plan = JsonSerializer.Deserialize<InvestigationPlan>(latestRun.OutputJson, SerializerOptions);
        }

        return ToDto(latestRun, plan);
    }

    private async Task<(InvestigationPlan Plan, string Model)> GenerateValidatedPlanAsync(
        Guid incidentId,
        CoordinatorInput coordinatorInput,
        CancellationToken cancellationToken)
    {
        var request = CoordinatorPromptBuilder.BuildRequest(incidentId, coordinatorInput);
        Exception? lastValidationException = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            LlmResponse response;
            try
            {
                response = await _llmClient.GenerateAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new CoordinatorUnavailableException("Coordinator LLM provider is not configured.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new CoordinatorUnavailableException("Coordinator LLM provider is unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new CoordinatorUnavailableException("Coordinator LLM provider timed out.", ex);
            }

            InvestigationPlan? plan = null;
            try
            {
                plan = JsonSerializer.Deserialize<InvestigationPlan>(response.Content, SerializerOptions);
            }
            catch (JsonException ex)
            {
                lastValidationException = ex;
            }

            if (plan is null)
            {
                lastValidationException ??= new JsonException("Coordinator output could not be deserialized.");
            }
            else
            {
                var validationErrors = CoordinatorPlanValidator.Validate(plan, incidentId);
                if (validationErrors.Count == 0)
                {
                    var normalizedPlan = new InvestigationPlan(
                        plan.IncidentId,
                        plan.IncidentType.Trim(),
                        plan.Priority.Trim(),
                        plan.ReadyForInvestigation,
                        plan.Tasks.OrderBy(task => task.Priority).ToList(),
                        plan.MissingInformation.ToList(),
                        plan.Assumptions.ToList(),
                        plan.SafetyNotes.ToList());

                    return (normalizedPlan, string.IsNullOrWhiteSpace(response.Model) ? "unknown" : response.Model);
                }

                lastValidationException = new CoordinatorPlanValidationException(
                    "Coordinator output failed validation.",
                    validationErrors);
            }

            if (attempt == 1)
            {
                request = request with
                {
                    UserPrompt = $$"""
                    The previous coordinator response was invalid. Return only schema-valid JSON and correct the issues.

                    ORIGINAL REQUEST:
                    {{request.UserPrompt}}
                    """
                };
                continue;
            }
        }

        throw new CoordinatorPlanValidationException(
            "Coordinator output failed validation after retry.",
            lastValidationException is CoordinatorPlanValidationException validationException
                ? validationException.ValidationErrors
                : [lastValidationException?.Message ?? "The model output was invalid."]);
    }

    private static CoordinatorPlanDto ToDto(CoordinatorRun run, InvestigationPlan? plan)
    {
        return new CoordinatorPlanDto(
            run.IncidentId,
            run.Status.ToString(),
            string.IsNullOrWhiteSpace(run.Model) ? "unknown" : run.Model,
            run.PromptVersion,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            plan);
    }
}