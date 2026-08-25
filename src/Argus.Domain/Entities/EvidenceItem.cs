using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class EvidenceItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid IncidentId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public EvidenceType EvidenceType { get; private set; }

    public long FileSize { get; private set; }

    public string StoredPath { get; private set; } = string.Empty;

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Incident? Incident { get; private set; }

    private EvidenceItem()
    {
    }

    public EvidenceItem(
        Guid incidentId,
        string fileName,
        string contentType,
        EvidenceType evidenceType,
        long fileSize,
        string storedPath,
        string sha256)
    {
        IncidentId = incidentId;
        FileName = fileName;
        ContentType = contentType;
        EvidenceType = evidenceType;
        FileSize = fileSize;
        StoredPath = storedPath;
        Sha256 = sha256;
    }
}