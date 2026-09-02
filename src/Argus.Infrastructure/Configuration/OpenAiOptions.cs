namespace Argus.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string CoordinatorModel { get; init; } = string.Empty;

    public string InvestigatorModel { get; init; } = string.Empty;

    public string ResponseModel { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
}