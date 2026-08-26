using Argus.Domain.Models;

namespace Argus.Application.DTOs;

public sealed record ResponseEducationPackageDto(
    Guid IncidentId,
    string Status,
    string Model,
    string PromptVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    ResponseEducationPackage? Package);