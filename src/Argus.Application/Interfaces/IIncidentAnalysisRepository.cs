using Argus.Domain.Entities;
using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IIncidentAnalysisRepository
{
    Task<IncidentAnalysis?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken);

    Task UpsertAsync(PhishingAnalysisResult analysisResult, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}