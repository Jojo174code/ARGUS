using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class IncidentAnalysis
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public int RiskScore { get; private set; }

    public RiskLevel RiskLevel { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string ParsedEmailJson { get; private set; } = string.Empty;

    public string RuleResultsJson { get; private set; } = string.Empty;

    public string MitreMappingsJson { get; private set; } = string.Empty;

    public DateTimeOffset AnalyzedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Incident? Incident { get; private set; }

    private IncidentAnalysis()
    {
    }

    public IncidentAnalysis(
        Guid incidentId,
        int riskScore,
        RiskLevel riskLevel,
        string summary,
        string parsedEmailJson,
        string ruleResultsJson,
        string mitreMappingsJson,
        DateTimeOffset analyzedAt)
    {
        IncidentId = incidentId;
        RiskScore = riskScore;
        RiskLevel = riskLevel;
        Summary = summary;
        ParsedEmailJson = parsedEmailJson;
        RuleResultsJson = ruleResultsJson;
        MitreMappingsJson = mitreMappingsJson;
        AnalyzedAt = analyzedAt;
    }

    public void Update(
        int riskScore,
        RiskLevel riskLevel,
        string summary,
        string parsedEmailJson,
        string ruleResultsJson,
        string mitreMappingsJson,
        DateTimeOffset analyzedAt)
    {
        RiskScore = riskScore;
        RiskLevel = riskLevel;
        Summary = summary;
        ParsedEmailJson = parsedEmailJson;
        RuleResultsJson = ruleResultsJson;
        MitreMappingsJson = mitreMappingsJson;
        AnalyzedAt = analyzedAt;
    }
}