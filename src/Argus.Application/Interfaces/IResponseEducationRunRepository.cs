using Argus.Domain.Entities;

namespace Argus.Application.Interfaces;

public interface IResponseEducationRunRepository
{
    Task AddAsync(ResponseEducationRun responseEducationRun, CancellationToken cancellationToken);

    Task<ResponseEducationRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<ResponseEducationRun?> GetLatestSuccessfulByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}