using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Argus.IntegrationTests;

internal static class TestJsonExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<T?> ReadArgusJsonAsync<T>(this HttpContent content)
        => content.ReadFromJsonAsync<T>(Options);
}