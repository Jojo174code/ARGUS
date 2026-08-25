using Argus.Application.Models;

namespace Argus.Application.Interfaces;

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken);
}