using Argus.Domain.Models;

namespace Argus.Application.DTOs;

public sealed record InvestigatorReportDto(
    Guid IncidentId,
    string Status,
    string Model,
    string PromptVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    InvestigationReport? Report);