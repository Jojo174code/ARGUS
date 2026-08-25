using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Application.Validation;
using Argus.Domain.Entities;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Argus.Application.Services;

public sealed class IncidentInvestigationService : IIncidentInvestigationService
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IIncidentAnalysisRepository _incidentAnalysisRepository;
    private readonly IEvidenceFileStore _evidenceFileStore;
    private readonly IEmailParser _emailParser;
    private readonly IPhishingAnalyzer _phishingAnalyzer;
    private readonly ILogger<IncidentInvestigationService> _logger;

    public IncidentInvestigationService(
        IIncidentRepository incidentRepository,
        IIncidentAnalysisRepository incidentAnalysisRepository,
        IEvidenceFileStore evidenceFileStore,
        IEmailParser emailParser,
        IPhishingAnalyzer phishingAnalyzer,
        ILogger<IncidentInvestigationService> logger)
    {
        _incidentRepository = incidentRepository;
        _incidentAnalysisRepository = incidentAnalysisRepository;
        _evidenceFileStore = evidenceFileStore;
        _emailParser = emailParser;
        _phishingAnalyzer = phishingAnalyzer;
        _logger = logger;
    }

    public async Task<EvidenceItemDto> UploadEmailEvidenceAsync(
        Guid incidentId,
        Stream emailStream,
        string fileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken)
    {
        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            throw new KeyNotFoundException("Incident was not found.");
        }

        var validationErrors = EmailEvidenceValidator.Validate(fileName, fileSize);
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", validationErrors));
        }

        var storedFile = await _evidenceFileStore.SaveEmailAsync(
            emailStream,
            fileName,
            contentType,
            fileSize,
            cancellationToken);

        var evidenceItem = new EvidenceItem(
            incident.Id,
            storedFile.SanitizedFileName,
            storedFile.ContentType,
            EvidenceType.EmailFile,
            storedFile.FileSize,
            storedFile.StoredPath,
            storedFile.Sha256);

        await _incidentRepository.AddEvidenceAsync(evidenceItem, cancellationToken);
        await _incidentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Evidence uploaded for incident {IncidentId}: {FileName} ({FileSize} bytes)",
            incident.Id,
            evidenceItem.FileName,
            evidenceItem.FileSize);

        return new EvidenceItemDto(
            evidenceItem.Id,
            evidenceItem.IncidentId,
            evidenceItem.FileName,
            evidenceItem.ContentType,
            evidenceItem.EvidenceType,
            evidenceItem.FileSize,
            evidenceItem.Sha256,
            evidenceItem.UploadedAt);
    }

    public async Task<PhishingAnalysisResult> AnalyzeAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            throw new KeyNotFoundException("Incident was not found.");
        }

        var emailEvidence = incident.EvidenceItems
            .Where(evidence => evidence.EvidenceType == EvidenceType.EmailFile)
            .OrderByDescending(evidence => evidence.UploadedAt)
            .FirstOrDefault();

        if (emailEvidence is null)
        {
            incident.SetStatus(IncidentStatus.AwaitingInformation);
            await _incidentRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("An .eml evidence file must be uploaded before analysis.");
        }

        incident.SetStatus(IncidentStatus.Analyzing);
        await _incidentRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Analysis started for incident {IncidentId}", incidentId);

        try
        {
            await using var emailStream = await _evidenceFileStore.OpenReadAsync(emailEvidence.StoredPath, cancellationToken);
            var parsedEmail = await _emailParser.ParseAsync(emailStream, cancellationToken);

            _logger.LogInformation(
                "Email parsed for incident {IncidentId}: sender={Sender}, subject={Subject}, urls={UrlCount}, attachments={AttachmentCount}",
                incidentId,
                parsedEmail.FromAddress,
                parsedEmail.Subject,
                parsedEmail.Urls.Count,
                parsedEmail.Attachments.Count);

            var analysisResult = await _phishingAnalyzer.AnalyzeAsync(parsedEmail, incidentId, cancellationToken);
            await _incidentAnalysisRepository.UpsertAsync(analysisResult, cancellationToken);
            await _incidentAnalysisRepository.SaveChangesAsync(cancellationToken);

            incident.SetStatus(IncidentStatus.Completed);
            await _incidentRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Analysis completed for incident {IncidentId} with risk score {RiskScore} and level {RiskLevel}",
                incidentId,
                analysisResult.RiskScore,
                analysisResult.RiskLevel);

            return analysisResult;
        }
        catch (Exception ex)
        {
            incident.SetStatus(IncidentStatus.Failed);
            await _incidentRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Analysis failed for incident {IncidentId}", incidentId);
            throw;
        }
    }

    public async Task<PhishingAnalysisResult?> GetAnalysisAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var analysis = await _incidentAnalysisRepository.GetByIncidentIdAsync(incidentId, cancellationToken);
        return analysis is null ? null : IncidentAnalysisMapper.Map(analysis);
    }
}