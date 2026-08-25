using Argus.Domain.Entities;

namespace Argus.Application.Interfaces;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);

    Task AddEvidenceAsync(EvidenceItem evidenceItem, CancellationToken cancellationToken);

    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}