using Argus.Domain.Entities;
using Argus.Domain.Models;
using System.Text.Json;

namespace Argus.Application.Services;

internal static class IncidentAnalysisMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static PhishingAnalysisResult Map(IncidentAnalysis analysis)
    {
        var email = JsonSerializer.Deserialize<ParsedEmail>(analysis.ParsedEmailJson, SerializerOptions)
            ?? throw new InvalidOperationException("Stored parsed email payload could not be deserialized.");

        var ruleResults = JsonSerializer.Deserialize<IReadOnlyList<PhishingRuleResult>>(analysis.RuleResultsJson, SerializerOptions)
            ?? Array.Empty<PhishingRuleResult>();

        var mitreMappings = JsonSerializer.Deserialize<IReadOnlyList<MitreAttackTechnique>>(analysis.MitreMappingsJson, SerializerOptions)
            ?? Array.Empty<MitreAttackTechnique>();

        return new PhishingAnalysisResult(
            analysis.IncidentId,
            email,
            analysis.RiskScore,
            analysis.RiskLevel,
            analysis.Summary,
            ruleResults,
            ruleResults.Where(result => result.Triggered).ToList(),
            mitreMappings,
            analysis.AnalyzedAt);
    }
}