using Argus.Domain.Models;

namespace Argus.Application.Models;

public sealed record InvestigationContext(
    Guid IncidentId,
    IncidentContext Incident,
    DeterministicFindings Findings,
    IReadOnlyList<EvidenceSnapshot> EvidenceItems,
    InvestigationPlan CoordinatorPlan);

public sealed record ToolObservation(
    string Key,
    string Value,
    string? EvidenceReference);

public sealed record ToolResult(
    string ToolName,
    bool Success,
    IReadOnlyList<ToolObservation> Observations,
    IReadOnlyList<string> EvidenceReferences,
    string? Error);

public sealed record InvestigationTaskExecution(
    InvestigationTask Task,
    TaskExecutionResult Result,
    IReadOnlyList<ToolResult> ToolResults);

public sealed record InvestigationSynthesisInput(
    Guid IncidentId,
    InvestigationPlan CoordinatorPlan,
    DeterministicFindings DeterministicFindings,
    IReadOnlyList<TaskExecutionResult> TaskResults,
    IReadOnlyList<ToolResult> ToolResults);

public sealed record InvestigationReportSynthesis(
    Guid IncidentId,
    string Classification,
    string Severity,
    double Confidence,
    IReadOnlyList<InvestigationFinding> Findings,
    IReadOnlyList<AttackTechniqueFinding> AttackTechniques,
    IReadOnlyList<string> PossibleImpact,
    IReadOnlyList<string> Uncertainties);