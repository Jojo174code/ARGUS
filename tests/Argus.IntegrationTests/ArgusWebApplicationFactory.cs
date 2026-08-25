using Argus.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Argus.IntegrationTests;

public sealed class ArgusWebApplicationFactory : WebApplicationFactory<Program>
{
    private const long DefaultMaxUploadSizeBytes = 2 * 1024 * 1024;

    private readonly string _uploadDirectory = Path.Combine(Path.GetTempPath(), $"argus-integration-{Guid.NewGuid():N}");
    private readonly string _databaseName = $"argus-integration-{Guid.NewGuid():N}";
    private readonly string? _originalProvider = Environment.GetEnvironmentVariable("Database__Provider");
    private readonly string? _originalDatabaseName = Environment.GetEnvironmentVariable("Database__DatabaseName");
    private readonly string? _originalUploadRoot = Environment.GetEnvironmentVariable("Uploads__RootDirectory");
    private readonly string? _originalUploadMaxFileSize = Environment.GetEnvironmentVariable("Uploads__MaxFileSizeBytes");

    public string UploadDirectory => _uploadDirectory;

    public long MaxUploadSizeBytes => DefaultMaxUploadSizeBytes;

    public ArgusWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Database__Provider", "InMemory");
        Environment.SetEnvironmentVariable("Database__DatabaseName", _databaseName);
        Environment.SetEnvironmentVariable("Uploads__RootDirectory", _uploadDirectory);
        Environment.SetEnvironmentVariable("Uploads__MaxFileSizeBytes", DefaultMaxUploadSizeBytes.ToString());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILlmClient, TestCoordinatorLlmClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Environment.SetEnvironmentVariable("Database__Provider", _originalProvider);
        Environment.SetEnvironmentVariable("Database__DatabaseName", _originalDatabaseName);
        Environment.SetEnvironmentVariable("Uploads__RootDirectory", _originalUploadRoot);
        Environment.SetEnvironmentVariable("Uploads__MaxFileSizeBytes", _originalUploadMaxFileSize);

        if (disposing && Directory.Exists(_uploadDirectory))
        {
            Directory.Delete(_uploadDirectory, recursive: true);
        }
    }
}