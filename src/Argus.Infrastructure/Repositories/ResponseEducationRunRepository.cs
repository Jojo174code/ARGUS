using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Repositories;

public sealed class ResponseEducationRunRepository : IResponseEducationRunRepository
{
    private readonly ArgusDbContext _dbContext;

    public ResponseEducationRunRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ResponseEducationRun responseEducationRun, CancellationToken cancellationToken)
    {
        return _dbContext.Set<ResponseEducationRun>().AddAsync(responseEducationRun, cancellationToken).AsTask();
    }

    public Task<ResponseEducationRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<ResponseEducationRun>()
            .Where(run => run.IncidentId == incidentId)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ResponseEducationRun?> GetLatestSuccessfulByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<ResponseEducationRun>()
            .Where(run => run.IncidentId == incidentId && run.Status == ResponseEducationRunStatus.Completed)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}