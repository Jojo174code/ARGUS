using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Argus.Infrastructure.AI;

public sealed class OpenAiLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLlmClient> _logger;

    public OpenAiLlmClient(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAiLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var model = SelectModel(request.ResponseSchemaName);

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            }
        };

        message.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Invoking ARGUS LLM for incident {IncidentId}, schema {SchemaName}, model {Model}, systemPromptChars {SystemPromptChars}, userPromptChars {UserPromptChars}",
            request.IncidentId,
            request.ResponseSchemaName,
            model,
            request.SystemPrompt.Length,
            request.UserPrompt.Length);

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var detail = responseContent.Length > 500
                ? responseContent[..500]
                : responseContent;
            throw new HttpRequestException($"OpenAI request failed with status {(int)response.StatusCode}. Response: {detail}");
        }

        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        var responseModel = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : model;
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HttpRequestException("OpenAI returned an empty coordinator response.");
        }

        var normalizedContent = NormalizeJsonContent(content);
        _logger.LogInformation(
            "ARGUS LLM completed for incident {IncidentId}, schema {SchemaName}, duration {ElapsedMs} ms, responseChars {ResponseChars}",
            request.IncidentId,
            request.ResponseSchemaName,
            stopwatch.ElapsedMilliseconds,
            normalizedContent.Length);

        return new LlmResponse(responseModel ?? model, normalizedContent);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("Coordinator LLM provider is not configured. Set OPENAI_API_KEY or LITELLM_API_KEY, and OPENAI_MODEL or LITELLM_MODEL.");
        }
    }

    private string SelectModel(string schemaName)
    {
        var configuredModel = schemaName switch
        {
            "InvestigationPlan" => _options.CoordinatorModel,
            "InvestigationReportSynthesis" => _options.InvestigatorModel,
            "ResponseEducationPackage" => _options.ResponseModel,
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(configuredModel) ? _options.Model : configuredModel;
    }

    private static string NormalizeJsonContent(string content)
    {
        var trimmed = content.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var lines = trimmed.Split('\n');
        if (lines.Length < 3)
        {
            return trimmed;
        }

        var body = lines.Skip(1).ToList();
        if (body.Count > 0 && body[^1].Trim().StartsWith("```", StringComparison.Ordinal))
        {
            body.RemoveAt(body.Count - 1);
        }

        return string.Join('\n', body).Trim();
    }
}