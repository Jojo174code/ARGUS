namespace Argus.Domain.Models;

public sealed record InvestigationReport(
    Guid IncidentId,
    string Classification,
    string Severity,
    double Confidence,
    IReadOnlyList<InvestigationFinding> Findings,
    IReadOnlyList<AttackTechniqueFinding> AttackTechniques,
    IReadOnlyList<string> PossibleImpact,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<TaskExecutionResult> TaskResults);