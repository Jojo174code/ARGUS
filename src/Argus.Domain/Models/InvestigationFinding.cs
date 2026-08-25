namespace Argus.Domain.Models;

public sealed record InvestigationFinding(
    string Id,
    string Title,
    string Description,
    string Severity,
    double Confidence,
    string EvidenceSource,
    string EvidenceReference,
    string Evidence,
    string FindingType,
    string? TaskId);