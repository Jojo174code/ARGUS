using Argus.Domain.Enums;

namespace Argus.Domain.Models;

public sealed record PhishingRuleResult(
    string RuleId,
    string Name,
    string Description,
    RuleSeverity Severity,
    int ScoreContribution,
    string? Evidence,
    bool Triggered);