namespace Argus.Domain.Models;

public sealed record WorkflowSummaryMetrics(
    int CoordinatorTaskCount,
    int InvestigatorFindingCount,
    int UnsupportedTaskCount,
    int ResponseActionCount,
    int QuizQuestionCount,
    double DurationMilliseconds);