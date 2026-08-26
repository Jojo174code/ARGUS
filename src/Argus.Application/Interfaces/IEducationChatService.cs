using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IEducationChatService
{
    Task<EducationChatResponseDto> ChatAsync(Guid incidentId, EducationChatRequestDto request, CancellationToken cancellationToken);
}
