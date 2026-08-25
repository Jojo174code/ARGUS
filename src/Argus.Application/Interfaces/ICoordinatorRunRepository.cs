using Argus.Domain.Entities;

namespace Argus.Application.Interfaces;

public interface ICoordinatorRunRepository
{
    Task AddAsync(CoordinatorRun coordinatorRun, CancellationToken cancellationToken);

    Task<CoordinatorRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<CoordinatorRun?> GetLatestCompletedByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}