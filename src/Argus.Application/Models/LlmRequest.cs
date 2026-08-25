namespace Argus.Application.Models;

public sealed record LlmRequest(
    Guid IncidentId,
    string SystemPrompt,
    string UserPrompt,
    string ResponseSchemaName);