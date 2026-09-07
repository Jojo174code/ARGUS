using Argus.Application.Models;
using Argus.Infrastructure.AI;
using Argus.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Argus.UnitTests;

public sealed class OpenRouterLlmClientTests
{
    [Fact]
    public async Task GenerateAsync_sends_openrouter_json_request_and_returns_content()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(async request =>
        {
            captured = await CloneRequestAsync(request);
            return JsonResponse("{\"model\":\"deepseek/deepseek-v4-pro-0813\",\"choices\":[{\"message\":{\"content\":\"{\\\"result\\\":true}\"}}]}");
        });
        var client = CreateClient(handler);

        var response = await client.GenerateAsync(new LlmRequest(Guid.NewGuid(), "schema", "system", "user"), CancellationToken.None);

        Assert.Equal("{\"result\":true}", response.Content);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/api/v1/chat/completions", captured.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("sk-or-test-key", captured.Headers.Authorization.Parameter);
        Assert.Equal("http://localhost", captured.Headers.GetValues("HTTP-Referer").Single());
        Assert.Equal("ARGUS", captured.Headers.GetValues("X-OpenRouter-Title").Single());
        using var body = JsonDocument.Parse(await captured.Content!.ReadAsStringAsync());
        Assert.Equal("deepseek/deepseek-v4-pro-0813", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("json_object", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GenerateAsync_does_not_include_key_in_rejected_key_error()
    {
        const string key = "sk-or-sensitive-test-key";
        var client = CreateClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("key rejected")
        })), key);

        var exception = await Assert.ThrowsAsync<OpenRouterException>(() => client.GenerateAsync(new LlmRequest(Guid.NewGuid(), "schema", "system", "user"), CancellationToken.None));

        Assert.Contains("rejected the API key", exception.Message);
        Assert.DoesNotContain(key, exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_retries_transient_failures()
    {
        var requests = 0;
        var client = CreateClient(new StubHandler(_ =>
        {
            requests++;
            return Task.FromResult(requests == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse("{\"choices\":[{\"message\":{\"content\":\"```json\\n{\\\"ok\\\":true}\\n```\"}}]}"));
        }));

        var response = await client.GenerateAsync(new LlmRequest(Guid.NewGuid(), "schema", "system", "user"), CancellationToken.None);

        Assert.Equal(2, requests);
        Assert.Equal("{\"ok\":true}", response.Content);
    }

    private static OpenRouterLlmClient CreateClient(HttpMessageHandler handler, string key = "sk-or-test-key")
    {
        return new OpenRouterLlmClient(new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.ai/api/v1/") }, Options.Create(new OpenRouterOptions
        {
            ApiKey = key,
            SiteUrl = "http://localhost",
            MaxRetries = 1
        }), NullLogger<OpenRouterLlmClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content is null ? null : new StringContent(await request.Content.ReadAsStringAsync())
        };
        foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responseFactory(request);
    }
}