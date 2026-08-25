namespace Argus.Application.DTOs;

public sealed record StoredEmailFile(
    string SanitizedFileName,
    string ContentType,
    long FileSize,
    string StoredPath,
    string Sha256);