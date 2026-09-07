using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Argus.Infrastructure.AI;

public sealed class OpenRouterLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterLlmClient> _logger;

    public OpenRouterLlmClient(HttpClient httpClient, IOptions<OpenRouterOptions> options, ILogger<OpenRouterLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var message = CreateChatRequest(request);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return ParseCompletion(responseContent, request.IncidentId);
            }

            if (IsTransient(response.StatusCode) && attempt < _options.MaxRetries)
            {
                var delay = GetRetryDelay(response, attempt);
                _logger.LogWarning("OpenRouter request for incident {IncidentId} returned {StatusCode}; retrying in {DelayMs} ms", request.IncidentId, (int)response.StatusCode, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            throw CreateProviderException(response.StatusCode, response.Headers.RetryAfter, responseContent);
        }
    }

    public async Task<OpenRouterDiagnosticResult> CheckAuthenticationAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "auth/key");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return new OpenRouterDiagnosticResult("OpenRouter", _options.Model, response.IsSuccessStatusCode, (int)response.StatusCode, response.IsSuccessStatusCode ? null : SanitizeError(content));
    }

    private HttpRequestMessage CreateChatRequest(LlmRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.SiteUrl))
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", _options.SiteUrl);
        }
        message.Headers.TryAddWithoutValidation("X-OpenRouter-Title", _options.AppName);
        message.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = _options.Model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = $"{request.SystemPrompt} Return only a JSON object." },
                new { role = "user", content = request.UserPrompt }
            }
        }, SerializerOptions), Encoding.UTF8, "application/json");
        return message;
    }

    private LlmResponse ParseCompletion(string responseContent, Guid incidentId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                throw new OpenRouterException("OpenRouter returned an invalid completion: no choices were present.");
            }
            var choice = choices[0];
            if (choice.TryGetProperty("finish_reason", out var finishReason) && string.Equals(finishReason.GetString(), "length", StringComparison.OrdinalIgnoreCase))
            {
                throw new OpenRouterException("OpenRouter returned a truncated completion.");
            }
            if (!choice.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var contentElement))
            {
                throw new OpenRouterException("OpenRouter returned an invalid completion: message content was missing.");
            }
            var content = NormalizeJsonContent(contentElement.GetString());
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new OpenRouterException("OpenRouter returned an empty completion.");
            }
            using var _ = JsonDocument.Parse(content);
            var model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : _options.Model;
            _logger.LogInformation("OpenRouter completion received for incident {IncidentId}, model {Model}, responseChars {ResponseChars}", incidentId, model, content.Length);
            return new LlmResponse(model ?? _options.Model, content);
        }
        catch (JsonException ex)
        {
            throw new OpenRouterException("OpenRouter returned invalid JSON completion content.", ex);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable || (int)statusCode is 524 or 529;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        return retryAfter ?? TimeSpan.FromMilliseconds(Math.Min(4000, 400 * Math.Pow(2, attempt)) + Random.Shared.Next(0, 150));
    }

    private static OpenRouterException CreateProviderException(HttpStatusCode statusCode, RetryConditionHeaderValue? retryAfter, string content)
    {
        var message = (int)statusCode switch
        {
            400 => "OpenRouter rejected the request.",
            401 => "OpenRouter rejected the API key. Create or verify the key at the OpenRouter keys page.",
            402 => "OpenRouter has insufficient credits.",
            403 => "OpenRouter denied access to this resource.",
            404 => "OpenRouter could not find the configured model or resource.",
            429 => $"OpenRouter rate limited the request{FormatRetryAfter(retryAfter)}.",
            502 or 503 or 524 or 529 => "OpenRouter or its upstream provider is temporarily unavailable.",
            _ => $"OpenRouter request failed with HTTP {(int)statusCode}."
        };
        return new OpenRouterException($"{message} {SanitizeError(content)}".Trim());
    }

    private static string FormatRetryAfter(RetryConditionHeaderValue? retryAfter) => retryAfter?.Delta is { } delay ? $" Retry after {Math.Ceiling(delay.TotalSeconds)} seconds" : string.Empty;

    private static string SanitizeError(string content)
    {
        var compact = content.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length == 0 ? string.Empty : compact.Length > 300 ? compact[..300] : compact;
    }

    private static string NormalizeJsonContent(string? content)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && lastFence > firstNewLine ? trimmed[(firstNewLine + 1)..lastFence].Trim() : trimmed;
    }
}

public sealed class OpenRouterException : HttpRequestException
{
    public OpenRouterException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public sealed record OpenRouterDiagnosticResult(string Provider, string Model, bool AuthenticationSucceeded, int HttpStatus, string? Error);