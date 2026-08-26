using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IAgenticWorkflowService
{
    Task<AgenticWorkflowResultDto> RunAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<AgenticWorkflowResultDto?> GetLatestAsync(Guid incidentId, CancellationToken cancellationToken);
}