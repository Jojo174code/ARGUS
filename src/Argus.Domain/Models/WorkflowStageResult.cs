namespace Argus.Domain.Models;

public sealed record WorkflowStageResult(
    string Stage,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Summary,
    string? Error);