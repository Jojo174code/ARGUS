namespace Argus.Domain.Models;

public sealed record TaskExecutionResult(
    string TaskId,
    string TaskType,
    string Status,
    IReadOnlyList<string> ToolsUsed,
    IReadOnlyList<string> EvidenceReferences,
    string Summary);