using Argus.Domain.Enums;

namespace Argus.Application.DTOs;

public sealed record IncidentDto(
    Guid Id,
    string OrganizationName,
    OrganizationType OrganizationType,
    string Description,
    string ReportedBy,
    DateTimeOffset CreatedAt,
    IncidentStatus Status,
    TechnicalSkillLevel TechnicalSkillLevel,
    IReadOnlyList<EvidenceItemDto> EvidenceItems);