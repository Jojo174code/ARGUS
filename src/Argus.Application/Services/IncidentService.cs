using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Argus.Application.Services;

public sealed class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(IIncidentRepository incidentRepository, ILogger<IncidentService> logger)
    {
        _incidentRepository = incidentRepository;
        _logger = logger;
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var errors = IncidentValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }

        var incident = new Incident(
            request.OrganizationName.Trim(),
            request.OrganizationType,
            request.Description.Trim(),
            request.ReportedBy.Trim(),
            request.TechnicalSkillLevel);

        await _incidentRepository.AddAsync(incident, cancellationToken);
        await _incidentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Incident created for organization {OrganizationName} with incident ID {IncidentId}",
            incident.OrganizationName,
            incident.Id);

        return Map(incident);
    }

    public async Task<IncidentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _incidentRepository.GetByIdAsync(id, cancellationToken);
        return incident is null ? null : Map(incident);
    }

    private static IncidentDto Map(Incident incident)
    {
        return new IncidentDto(
            incident.Id,
            incident.OrganizationName,
            incident.OrganizationType,
            incident.Description,
            incident.ReportedBy,
            incident.CreatedAt,
            incident.Status,
            incident.TechnicalSkillLevel,
            incident.EvidenceItems.Select(evidence => new EvidenceItemDto(
                evidence.Id,
                evidence.IncidentId,
                evidence.FileName,
                evidence.ContentType,
                evidence.EvidenceType,
                evidence.FileSize,
                evidence.Sha256,
                evidence.UploadedAt)).ToList());
    }
}