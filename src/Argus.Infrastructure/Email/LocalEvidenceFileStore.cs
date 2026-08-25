using Argus.Application.DTOs;
using Argus.Application.Interfaces;
using Argus.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Argus.Infrastructure.Email;

public sealed class LocalEvidenceFileStore : IEvidenceFileStore
{
    private readonly UploadOptions _uploadOptions;
    private readonly string _rootPath;
    private readonly ILogger<LocalEvidenceFileStore> _logger;

    public LocalEvidenceFileStore(
        IOptions<UploadOptions> uploadOptions,
        IHostEnvironment hostEnvironment,
        ILogger<LocalEvidenceFileStore> logger)
    {
        _uploadOptions = uploadOptions.Value;
        _rootPath = Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, _uploadOptions.RootDirectory));
        _logger = logger;
    }

    public async Task<StoredEmailFile> SaveEmailAsync(
        Stream emailStream,
        string originalFileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken)
    {
        if (fileSize > _uploadOptions.MaxFileSizeBytes)
        {
            throw new ArgumentException($"File exceeds the maximum size of {_uploadOptions.MaxFileSizeBytes} bytes.");
        }

        Directory.CreateDirectory(_rootPath);

        var sanitizedFileName = SanitizeFileName(originalFileName);
        var storageName = $"{Guid.NewGuid():N}.eml";
        var fullPath = GetSafeFullPath(storageName);

        await using (var targetStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await emailStream.CopyToAsync(targetStream, cancellationToken);
        }

        var actualFileSize = new FileInfo(fullPath).Length;
        if (actualFileSize == 0)
        {
            File.Delete(fullPath);
            throw new ArgumentException("Uploaded files must not be empty.");
        }

        var sha256 = await ComputeSha256Async(fullPath, cancellationToken);

        _logger.LogInformation(
            "Stored email evidence at {StoredPath} with SHA-256 {Sha256}",
            storageName,
            sha256);

        return new StoredEmailFile(
            sanitizedFileName,
            string.IsNullOrWhiteSpace(contentType) ? "message/rfc822" : contentType,
            actualFileSize,
            storageName,
            sha256);
    }

    public Task<Stream> OpenReadAsync(string storedPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetSafeFullPath(storedPath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private string GetSafeFullPath(string storedPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storedPath));
        if (!fullPath.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }

    private static async Task<string> ComputeSha256Async(string fullPath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(fullPath);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SanitizeFileName(string originalFileName)
    {
        var fileName = Path.GetFileName(originalFileName);
        var builder = new StringBuilder(fileName.Length);

        foreach (var character in fileName)
        {
            builder.Append(Path.GetInvalidFileNameChars().Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}