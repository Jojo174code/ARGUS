using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Repositories;

public sealed class CoordinatorRunRepository : ICoordinatorRunRepository
{
    private readonly ArgusDbContext _dbContext;

    public CoordinatorRunRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(CoordinatorRun coordinatorRun, CancellationToken cancellationToken)
    {
        return _dbContext.Set<CoordinatorRun>().AddAsync(coordinatorRun, cancellationToken).AsTask();
    }

    public Task<CoordinatorRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<CoordinatorRun>()
            .Where(run => run.IncidentId == incidentId)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CoordinatorRun?> GetLatestCompletedByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<CoordinatorRun>()
            .Where(run => run.IncidentId == incidentId && run.Status == Domain.Enums.CoordinatorRunStatus.Completed)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}