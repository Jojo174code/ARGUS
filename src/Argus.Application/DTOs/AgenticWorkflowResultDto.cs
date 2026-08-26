using Argus.Domain.Models;

namespace Argus.Application.DTOs;

public sealed record AgenticWorkflowResultDto(
    Guid IncidentId,
    Guid WorkflowRunId,
    string Status,
    IReadOnlyList<WorkflowStageResult> Stages,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureStage,
    string? FailureMessage,
    IReadOnlyList<MissingInformationItem> BlockingMissingInformation,
    WorkflowSummaryMetrics Metrics);