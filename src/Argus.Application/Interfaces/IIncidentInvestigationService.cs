using Argus.Application.DTOs;
using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IIncidentInvestigationService
{
    Task<EvidenceItemDto> UploadEmailEvidenceAsync(
        Guid incidentId,
        Stream emailStream,
        string fileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken);

    Task<PhishingAnalysisResult> AnalyzeAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<PhishingAnalysisResult?> GetAnalysisAsync(Guid incidentId, CancellationToken cancellationToken);
}