using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Argus.Application.Services;

public sealed class EducationChatService : IEducationChatService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentAnalysisRepository _incidentAnalysisRepository;
    private readonly IInvestigatorRunRepository _investigatorRunRepository;
    private readonly IResponseEducationRunRepository _responseEducationRunRepository;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<EducationChatService> _logger;

    public EducationChatService(
        IIncidentRepository incidentRepository,
        IIncidentAnalysisRepository incidentAnalysisRepository,
        IInvestigatorRunRepository investigatorRunRepository,
        IResponseEducationRunRepository responseEducationRunRepository,
        ILlmClient llmClient,
        ILogger<EducationChatService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentAnalysisRepository = incidentAnalysisRepository;
        _investigatorRunRepository = investigatorRunRepository;
        _responseEducationRunRepository = responseEducationRunRepository;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<EducationChatResponseDto> ChatAsync(Guid incidentId, EducationChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException("Question is required.", nameof(request));
        }

        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException("Incident was not found.");

        var analysisEntity = await _incidentAnalysisRepository.GetByIncidentIdAsync(incidentId, cancellationToken);
        var investigatorRun = await _investigatorRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);
        var responseRun = await _responseEducationRunRepository.GetLatestSuccessfulByIncidentIdAsync(incidentId, cancellationToken);

        var analysis = analysisEntity is null ? null : IncidentAnalysisMapper.Map(analysisEntity);
        var report = investigatorRun is null || string.IsNullOrWhiteSpace(investigatorRun.OutputJson)
            ? null
            : JsonSerializer.Deserialize<InvestigationReport>(investigatorRun.OutputJson, SerializerOptions);
        var education = responseRun is null || string.IsNullOrWhiteSpace(responseRun.OutputJson)
            ? null
            : JsonSerializer.Deserialize<ResponseEducationPackage>(responseRun.OutputJson, SerializerOptions);

        var safeHistory = (request.History ?? [])
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(6)
            .Select(message => new { role = message.Role, content = message.Content })
            .ToList();

        var context = new
        {
            incident = new
            {
                incidentId,
                incident.OrganizationName,
                incident.OrganizationType,
                incident.TechnicalSkillLevel,
                incident.Status,
                incident.Description
            },
            analysis = analysis is null ? null : new
            {
                analysis.RiskLevel,
                analysis.RiskScore,
                analysis.Summary,
                indicatorCount = analysis.Indicators.Count
            },
            investigator = report is null ? null : new
            {
                report.Classification,
                report.Severity,
                report.Confidence,
                findingCount = report.Findings.Count,
                findings = report.Findings.Take(5).Select(finding => new
                {
                    finding.Title,
                    finding.Description,
                    finding.Severity
                }).ToList()
            },
            education = education is null ? null : new
            {
                education.PlainLanguageSummary,
                education.OverallPriority,
                immediateActions = education.ImmediateActions.Take(5).Select(action => action.Title).ToList(),
                warningSigns = education.Education.WarningSigns.Take(5).Select(warning => warning.Title).ToList(),
                takeaways = education.Education.Takeaways.Take(5).ToList()
            },
            hasEducationPackage = education is not null
        };

        var userPrompt = $$"""
        You are a calm, plain-language cybersecurity coach for non-technical users.

        CONTEXT JSON:
        {{JsonSerializer.Serialize(context, SerializerOptions)}}

        CHAT HISTORY JSON:
        {{JsonSerializer.Serialize(safeHistory, SerializerOptions)}}

        USER QUESTION:
        {{request.Question}}

        Return only valid JSON:
        {
          "answer": "short plain-language response",
          "suggestedNextStep": "optional short next step, or null"
        }

        Rules:
        - Use simple words and short sentences.
        - Do not invent facts outside the context JSON.
        - If data is missing, say what is missing clearly.
        - Give practical next steps non-technical staff can follow.
        - Keep answer under 170 words.
        """;

        var llmRequest = new LlmRequest(
            incidentId,
            "You are ARGUS Education Assistant. Be factual, concise, and easy for non-technical users.",
            userPrompt,
            "EducationChatResponse");

        try
        {
            _logger.LogInformation("Education chat requested for incident {IncidentId}", incidentId);
            var response = await _llmClient.GenerateAsync(llmRequest, cancellationToken);
            var synthesis = JsonSerializer.Deserialize<EducationChatSynthesis>(response.Content, SerializerOptions);

            if (synthesis is null || string.IsNullOrWhiteSpace(synthesis.Answer))
            {
                throw new InvalidOperationException("Education assistant returned an invalid response.");
            }

            return new EducationChatResponseDto(
                synthesis.Answer,
                string.IsNullOrWhiteSpace(synthesis.SuggestedNextStep) ? null : synthesis.SuggestedNextStep,
                string.IsNullOrWhiteSpace(response.Model) ? "unknown" : response.Model,
                DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Education assistant is temporarily unavailable.", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException("Education assistant timed out. Please try again.", ex);
        }
    }
}
