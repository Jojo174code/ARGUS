using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Repositories;

public sealed class InvestigatorRunRepository : IInvestigatorRunRepository
{
    private readonly ArgusDbContext _dbContext;

    public InvestigatorRunRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(InvestigatorRun investigatorRun, CancellationToken cancellationToken)
    {
        return _dbContext.Set<InvestigatorRun>().AddAsync(investigatorRun, cancellationToken).AsTask();
    }

    public Task<InvestigatorRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<InvestigatorRun>()
            .Where(run => run.IncidentId == incidentId)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<InvestigatorRun?> GetLatestSuccessfulByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<InvestigatorRun>()
            .Where(run => run.IncidentId == incidentId && run.Status == InvestigatorRunStatus.Completed)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}