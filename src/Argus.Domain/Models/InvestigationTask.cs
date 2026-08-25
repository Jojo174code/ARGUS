namespace Argus.Domain.Models;

public sealed record InvestigationTask(
    string Id,
    string TaskType,
    string Title,
    string Description,
    int Priority,
    IReadOnlyList<string> RequiredEvidence,
    string AssignedCapability,
    string CompletionCondition);