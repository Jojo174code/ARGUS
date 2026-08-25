namespace Argus.Infrastructure.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    public string RootDirectory { get; init; } = "App_Data/uploads";

    public long MaxFileSizeBytes { get; init; } = 2 * 1024 * 1024;
}