namespace Argus.Domain.Models;

public sealed record ResponseAction(
    string Id,
    string Title,
    string Description,
    string Reason,
    int Priority,
    string ActionType,
    bool RequiresHumanApproval,
    string? ResponsibleRole,
    IReadOnlyList<string> SupportingFindingIds);