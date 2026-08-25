using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class InvestigatorRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public Guid CoordinatorRunId { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public string InputJson { get; private set; } = string.Empty;

    public string? OutputJson { get; private set; }

    public InvestigatorRunStatus Status { get; private set; } = InvestigatorRunStatus.NotStarted;

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    public Incident? Incident { get; private set; }

    private InvestigatorRun()
    {
    }

    public InvestigatorRun(
        Guid incidentId,
        Guid coordinatorRunId,
        string model,
        string promptVersion,
        string inputJson,
        DateTimeOffset startedAt)
    {
        IncidentId = incidentId;
        CoordinatorRunId = coordinatorRunId;
        Model = model;
        PromptVersion = promptVersion;
        InputJson = inputJson;
        StartedAt = startedAt;
        Status = InvestigatorRunStatus.Running;
    }

    public void SetModel(string model)
    {
        Model = model;
    }

    public void MarkCompleted(string outputJson, DateTimeOffset completedAt)
    {
        OutputJson = outputJson;
        CompletedAt = completedAt;
        Status = InvestigatorRunStatus.Completed;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset completedAt)
    {
        OutputJson = null;
        CompletedAt = completedAt;
        Status = InvestigatorRunStatus.Failed;
        ErrorMessage = errorMessage;
    }
}