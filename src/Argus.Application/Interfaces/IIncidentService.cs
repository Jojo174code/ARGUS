using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IIncidentService
{
    Task<IncidentDto> CreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken);

    Task<IncidentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}