using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class CoordinatorRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public string InputJson { get; private set; } = string.Empty;

    public string? OutputJson { get; private set; }

    public CoordinatorRunStatus Status { get; private set; } = CoordinatorRunStatus.NotStarted;

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public Incident? Incident { get; private set; }

    private CoordinatorRun()
    {
    }

    public CoordinatorRun(Guid incidentId, string model, string promptVersion, string inputJson, DateTimeOffset startedAt)
    {
        IncidentId = incidentId;
        Model = model;
        PromptVersion = promptVersion;
        InputJson = inputJson;
        StartedAt = startedAt;
        Status = CoordinatorRunStatus.Running;
    }

    public void MarkCompleted(string outputJson, DateTimeOffset completedAt)
    {
        OutputJson = outputJson;
        CompletedAt = completedAt;
        Status = CoordinatorRunStatus.Completed;
        ErrorMessage = null;
    }

    public void SetModel(string model)
    {
        Model = model;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset completedAt)
    {
        OutputJson = null;
        CompletedAt = completedAt;
        Status = CoordinatorRunStatus.Failed;
        ErrorMessage = errorMessage;
    }
}