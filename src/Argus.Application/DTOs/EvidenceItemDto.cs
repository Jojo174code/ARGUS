using Argus.Domain.Enums;

namespace Argus.Application.DTOs;

public sealed record EvidenceItemDto(
    Guid Id,
    Guid IncidentId,
    string FileName,
    string ContentType,
    EvidenceType EvidenceType,
    long FileSize,
    string Sha256,
    DateTimeOffset UploadedAt);