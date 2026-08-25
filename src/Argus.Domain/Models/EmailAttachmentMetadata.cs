namespace Argus.Domain.Models;

public sealed record EmailAttachmentMetadata(
    string FileName,
    string ContentType,
    long Size);