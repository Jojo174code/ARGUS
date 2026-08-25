using Argus.Application.DTOs;

namespace Argus.Application.Interfaces;

public interface IEvidenceFileStore
{
    Task<StoredEmailFile> SaveEmailAsync(
        Stream emailStream,
        string originalFileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storedPath, CancellationToken cancellationToken);
}