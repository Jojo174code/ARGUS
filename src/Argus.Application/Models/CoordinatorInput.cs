namespace Argus.Application.Models;

public sealed record CoordinatorInput(
    Guid IncidentId,
    IncidentContext Incident,
    DeterministicFindings Findings,
    IReadOnlyList<EvidenceSnapshot> EvidenceItems);

public sealed record IncidentContext(
    string OrganizationName,
    string OrganizationType,
    string Description,
    string ReportedBy,
    string TechnicalSkillLevel,
    string IncidentStatus,
    DateTimeOffset CreatedAt);

public sealed record DeterministicFindings(
    int RiskScore,
    string RiskLevel,
    string Summary,
    IReadOnlyList<IndicatorSnapshot> Indicators,
    IReadOnlyList<string> MitreTechniques,
    EmailSnapshot Email);

public sealed record IndicatorSnapshot(
    string RuleId,
    string Name,
    string Description,
    string Severity,
    string? Evidence);

public sealed record EmailSnapshot(
    string? DisplayName,
    string? FromAddress,
    string? ReplyToAddress,
    string? ReturnPath,
    string? Subject,
    string? Date,
    string? MessageId,
    IReadOnlyList<string> ReceivedHeaders,
    IReadOnlyList<string> AuthenticationResults,
    IReadOnlyList<EmailUrlSnapshot> Urls,
    IReadOnlyList<AttachmentSnapshot> Attachments);

public sealed record EmailUrlSnapshot(
    string Url,
    string? DisplayText,
    string Source);

public sealed record AttachmentSnapshot(
    string FileName,
    string ContentType,
    long Size);

public sealed record EvidenceSnapshot(
    string FileName,
    string ContentType,
    long FileSize,
    string Sha256);