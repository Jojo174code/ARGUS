namespace Argus.Application.Models;

public sealed record LlmResponse(
    string Model,
    string Content);