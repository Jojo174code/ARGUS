using Argus.Domain.Enums;

namespace Argus.Application.DTOs;

public sealed record CreateIncidentRequest(
    string OrganizationName,
    OrganizationType OrganizationType,
    string Description,
    string ReportedBy,
    TechnicalSkillLevel TechnicalSkillLevel);