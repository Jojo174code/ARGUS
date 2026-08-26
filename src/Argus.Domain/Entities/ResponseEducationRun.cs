using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class ResponseEducationRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public Guid InvestigatorRunId { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public string InputJson { get; private set; } = string.Empty;

    public string? OutputJson { get; private set; }

    public ResponseEducationRunStatus Status { get; private set; } = ResponseEducationRunStatus.NotStarted;

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public Incident? Incident { get; private set; }

    private ResponseEducationRun()
    {
    }

    public ResponseEducationRun(
        Guid incidentId,
        Guid investigatorRunId,
        string model,
        string promptVersion,
        string inputJson,
        DateTimeOffset startedAt)
    {
        IncidentId = incidentId;
        InvestigatorRunId = investigatorRunId;
        Model = model;
        PromptVersion = promptVersion;
        InputJson = inputJson;
        StartedAt = startedAt;
        Status = ResponseEducationRunStatus.Running;
    }

    public void SetModel(string model)
    {
        Model = model;
    }

    public void MarkCompleted(string outputJson, DateTimeOffset completedAt)
    {
        OutputJson = outputJson;
        CompletedAt = completedAt;
        Status = ResponseEducationRunStatus.Completed;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset completedAt)
    {
        OutputJson = null;
        CompletedAt = completedAt;
        Status = ResponseEducationRunStatus.Failed;
        ErrorMessage = errorMessage;
    }
}