using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IResponseEducationService
{
    Task<ResponseEducationPackageDto> GenerateAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<ResponseEducationPackageDto?> GetLatestSuccessfulPackageAsync(Guid incidentId, CancellationToken cancellationToken);
}