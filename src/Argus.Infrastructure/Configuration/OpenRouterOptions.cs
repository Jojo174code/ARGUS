namespace Argus.Infrastructure.Configuration;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek/deepseek-v4-pro-0813";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string? SiteUrl { get; set; }

    public string AppName { get; set; } = "ARGUS";

    public int TimeoutSeconds { get; set; } = 120;

    public int MaxRetries { get; set; } = 2;
}