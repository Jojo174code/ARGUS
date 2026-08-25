using Argus.Domain.Enums;

namespace Argus.Domain.Models;

public sealed record PhishingAnalysisResult(
    Guid IncidentId,
    ParsedEmail Email,
    int RiskScore,
    RiskLevel RiskLevel,
    string Summary,
    IReadOnlyList<PhishingRuleResult> RuleResults,
    IReadOnlyList<PhishingRuleResult> Indicators,
    IReadOnlyList<MitreAttackTechnique> MitreAttackMappings,
    DateTimeOffset AnalyzedAt);