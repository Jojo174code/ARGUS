using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Repositories;

public sealed class AgenticWorkflowRunRepository : IAgenticWorkflowRunRepository
{
    private readonly ArgusDbContext _dbContext;

    public AgenticWorkflowRunRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(AgenticWorkflowRun workflowRun, CancellationToken cancellationToken)
    {
        return _dbContext.Set<AgenticWorkflowRun>().AddAsync(workflowRun, cancellationToken).AsTask();
    }

    public Task<AgenticWorkflowRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<AgenticWorkflowRun>()
            .Where(run => run.IncidentId == incidentId)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}