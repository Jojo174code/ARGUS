using Argus.Domain.Entities;

namespace Argus.Application.Interfaces;

public interface IAgenticWorkflowRunRepository
{
    Task AddAsync(AgenticWorkflowRun workflowRun, CancellationToken cancellationToken);

    Task<AgenticWorkflowRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}