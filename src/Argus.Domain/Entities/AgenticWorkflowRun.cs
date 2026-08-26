using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class AgenticWorkflowRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public WorkflowRunStatus Status { get; private set; } = WorkflowRunStatus.Pending;

    public string CurrentStage { get; private set; } = string.Empty;

    public string StageResultsJson { get; private set; } = "[]";

    public string? BlockingMissingInformationJson { get; private set; }

    public string? SummaryMetricsJson { get; private set; }

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureStage { get; private set; }

    public string? FailureMessage { get; private set; }

    public Guid? CoordinatorRunId { get; private set; }

    public Guid? InvestigatorRunId { get; private set; }

    public Guid? ResponseEducationRunId { get; private set; }

    public Incident? Incident { get; private set; }

    private AgenticWorkflowRun()
    {
    }

    public AgenticWorkflowRun(Guid incidentId, string currentStage, string stageResultsJson, DateTimeOffset startedAt)
    {
        IncidentId = incidentId;
        CurrentStage = currentStage;
        StageResultsJson = stageResultsJson;
        StartedAt = startedAt;
        Status = WorkflowRunStatus.Running;
    }

    public void SetRunning(string currentStage, string stageResultsJson)
    {
        CurrentStage = currentStage;
        StageResultsJson = stageResultsJson;
        Status = WorkflowRunStatus.Running;
        CompletedAt = null;
        FailureStage = null;
        FailureMessage = null;
    }

    public void SetAwaitingInformation(string currentStage, string stageResultsJson, string blockingMissingInformationJson, string? message)
    {
        CurrentStage = currentStage;
        StageResultsJson = stageResultsJson;
        BlockingMissingInformationJson = blockingMissingInformationJson;
        Status = WorkflowRunStatus.AwaitingInformation;
        CompletedAt = DateTimeOffset.UtcNow;
        FailureStage = currentStage;
        FailureMessage = message;
    }

    public void SetCompleted(string currentStage, string stageResultsJson, string? summaryMetricsJson)
    {
        CurrentStage = currentStage;
        StageResultsJson = stageResultsJson;
        SummaryMetricsJson = summaryMetricsJson;
        BlockingMissingInformationJson = null;
        Status = WorkflowRunStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        FailureStage = null;
        FailureMessage = null;
    }

    public void SetFailed(string currentStage, string stageResultsJson, string message)
    {
        CurrentStage = currentStage;
        StageResultsJson = stageResultsJson;
        Status = WorkflowRunStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        FailureStage = currentStage;
        FailureMessage = message;
    }

    public void SetStageResults(string stageResultsJson)
    {
        StageResultsJson = stageResultsJson;
    }

    public void SetBlockingMissingInformation(string? blockingMissingInformationJson)
    {
        BlockingMissingInformationJson = blockingMissingInformationJson;
    }

    public void SetSummaryMetrics(string? summaryMetricsJson)
    {
        SummaryMetricsJson = summaryMetricsJson;
    }

    public void LinkCoordinatorRun(Guid coordinatorRunId)
    {
        CoordinatorRunId = coordinatorRunId;
    }

    public void LinkInvestigatorRun(Guid investigatorRunId)
    {
        InvestigatorRunId = investigatorRunId;
    }

    public void LinkResponseEducationRun(Guid responseEducationRunId)
    {
        ResponseEducationRunId = responseEducationRunId;
    }
}