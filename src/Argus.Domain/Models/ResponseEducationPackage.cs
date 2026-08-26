namespace Argus.Domain.Models;

public sealed record ResponseEducationPackage(
    Guid IncidentId,
    string IncidentClassification,
    string OverallPriority,
    string PlainLanguageSummary,
    IReadOnlyList<ResponseAction> ImmediateActions,
    IReadOnlyList<ResponseAction> RecoveryActions,
    IReadOnlyList<ResponseAction> PreventionActions,
    IReadOnlyList<EscalationRecommendation> EscalationRecommendations,
    EducationModule Education,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations);