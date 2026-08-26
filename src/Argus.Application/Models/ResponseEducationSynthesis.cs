using Argus.Domain.Models;

namespace Argus.Application.Models;

public sealed record ResponseEducationSynthesis(
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