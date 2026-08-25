using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IInvestigatorService
{
    Task<InvestigatorReportDto> RunAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<InvestigatorReportDto?> GetLatestSuccessfulReportAsync(Guid incidentId, CancellationToken cancellationToken);
}