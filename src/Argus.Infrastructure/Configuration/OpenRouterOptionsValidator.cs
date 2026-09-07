using Microsoft.Extensions.Options;

namespace Argus.Infrastructure.Configuration;

public sealed class OpenRouterOptionsValidator : IValidateOptions<OpenRouterOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenRouterOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("OpenRouter API key is required. Set OPENROUTER_API_KEY in .env or OpenRouter:ApiKey in User Secrets.");
        }
        else if (!options.ApiKey.StartsWith("sk-or-", StringComparison.Ordinal))
        {
            failures.Add("OpenRouter API key must begin with 'sk-or-'.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("OpenRouter model is required.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("OpenRouter base URL must be an absolute HTTPS URL.");
        }

        if (options.TimeoutSeconds is < 10 or > 300)
        {
            failures.Add("OpenRouter timeout must be between 10 and 300 seconds.");
        }

        if (options.MaxRetries is < 0 or > 3)
        {
            failures.Add("OpenRouter retry count must be between 0 and 3.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}