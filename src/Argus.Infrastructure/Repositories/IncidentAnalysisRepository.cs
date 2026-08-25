using Argus.Application.Interfaces;
using Argus.Domain.Entities;
using Argus.Domain.Models;
using Argus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Argus.Infrastructure.Repositories;

public sealed class IncidentAnalysisRepository : IIncidentAnalysisRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ArgusDbContext _dbContext;

    public IncidentAnalysisRepository(ArgusDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IncidentAnalysis?> GetByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<IncidentAnalysis>()
            .SingleOrDefaultAsync(analysis => analysis.IncidentId == incidentId, cancellationToken);
    }

    public async Task UpsertAsync(PhishingAnalysisResult analysisResult, CancellationToken cancellationToken)
    {
        var existing = await GetByIncidentIdAsync(analysisResult.IncidentId, cancellationToken);
        var parsedEmailJson = JsonSerializer.Serialize(analysisResult.Email, SerializerOptions);
        var ruleResultsJson = JsonSerializer.Serialize(analysisResult.RuleResults, SerializerOptions);
        var mitreMappingsJson = JsonSerializer.Serialize(analysisResult.MitreAttackMappings, SerializerOptions);

        if (existing is null)
        {
            var entity = new IncidentAnalysis(
                analysisResult.IncidentId,
                analysisResult.RiskScore,
                analysisResult.RiskLevel,
                analysisResult.Summary,
                parsedEmailJson,
                ruleResultsJson,
                mitreMappingsJson,
                analysisResult.AnalyzedAt);

            await _dbContext.AddAsync(entity, cancellationToken);
            return;
        }

        existing.Update(
            analysisResult.RiskScore,
            analysisResult.RiskLevel,
            analysisResult.Summary,
            parsedEmailJson,
            ruleResultsJson,
            mitreMappingsJson,
            analysisResult.AnalyzedAt);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}