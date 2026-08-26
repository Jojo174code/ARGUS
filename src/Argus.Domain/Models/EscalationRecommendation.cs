namespace Argus.Domain.Models;

public sealed record EscalationRecommendation(
    string Level,
    string Reason,
    string RecommendedContact,
    bool Urgent);