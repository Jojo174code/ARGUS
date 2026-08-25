using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface ICoordinatorService
{
    Task<CoordinatorPlanDto> GeneratePlanAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<CoordinatorPlanDto?> GetLatestPlanAsync(Guid incidentId, CancellationToken cancellationToken);
}