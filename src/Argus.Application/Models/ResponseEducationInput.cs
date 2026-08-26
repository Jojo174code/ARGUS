using Argus.Domain.Models;

namespace Argus.Application.Models;

public sealed record ResponseEducationInput(
    Guid IncidentId,
    IncidentContext Incident,
    DeterministicFindings DeterministicFindings,
    InvestigationReport InvestigatorReport,
    IReadOnlyList<string> CoordinatorAssumptions,
    IReadOnlyList<string> CoordinatorSafetyNotes);