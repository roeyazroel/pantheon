using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Pantheon.Oracle;

/// <summary>Posting JSON to a model's web API and reading what comes back (ADR-067).</summary>
internal static class JsonHttp
{
    /// <summary>Posts a JSON body.</summary>
    /// <param name="http">The client.</param>
    /// <param name="address">Where to.</param>
    /// <param name="body">The body.</param>
    /// <param name="headers">Headers to send, such as the key.</param>
    /// <param name="cancel">Cancels the post.</param>
    /// <returns>The status code and the response's text.</returns>
    /// <exception cref="HttpRequestException">The server could not be reached.</exception>
    public static async Task<(int Status, string Body)> Post(HttpClient http, Uri address, JsonObject body, IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
    }

    /// <summary>A JSON document, or <c>null</c> when the text is not JSON.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The document's root.</returns>
    public static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string member, or <c>null</c> when it is missing or not a string.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The string.</returns>
    public static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A whole-number member, or 0.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The number.</returns>
    public static long Count(JsonNode? node) => node is JsonValue value && value.TryGetValue<long>(out var count) ? count : 0;
}
