using Argus.Domain.Entities;

namespace Argus.Application.Interfaces;

public interface IInvestigatorRunRepository
{
    Task AddAsync(InvestigatorRun investigatorRun, CancellationToken cancellationToken);

    Task<InvestigatorRun?> GetLatestByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<InvestigatorRun?> GetLatestSuccessfulByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}