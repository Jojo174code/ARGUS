using Argus.Domain.Models;

namespace Argus.Application.Models;

public sealed record ResponseEducationInput(
    Guid IncidentId,
    string TechnicalSkillLevel,
    string Classification,
    string Severity,
    IReadOnlyList<InvestigationFinding> Findings,
    IReadOnlyList<string> Uncertainties);