using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IPhishingAnalyzer
{
    Task<PhishingAnalysisResult> AnalyzeAsync(ParsedEmail email, Guid incidentId, CancellationToken cancellationToken);
}