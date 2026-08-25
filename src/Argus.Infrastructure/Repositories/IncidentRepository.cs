using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Repositories;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly ArgusDbContext _dbContext;

    public IncidentRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Incident incident, CancellationToken cancellationToken)
    {
        return _dbContext.Incidents.AddAsync(incident, cancellationToken).AsTask();
    }

    public Task AddEvidenceAsync(EvidenceItem evidenceItem, CancellationToken cancellationToken)
    {
        return _dbContext.EvidenceItems.AddAsync(evidenceItem, cancellationToken).AsTask();
    }

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Incidents
            .Include(incident => incident.EvidenceItems)
            .Include(incident => incident.Analysis)
            .SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}