namespace Argus.Domain.Models;

public sealed record InvestigationPlan(
    Guid IncidentId,
    string IncidentType,
    string Priority,
    bool ReadyForInvestigation,
    IReadOnlyList<InvestigationTask> Tasks,
    IReadOnlyList<MissingInformationItem> MissingInformation,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> SafetyNotes);