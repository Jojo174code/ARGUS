using Argus.Domain.Enums;
using Argus.Domain.Models;

namespace Argus.Application.DTOs;

public sealed record CoordinatorPlanDto(
    Guid IncidentId,
    string Status,
    string Model,
    string PromptVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    InvestigationPlan? Plan);