using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Game.Minds;

namespace Pantheon.Oracle;

/// <summary>
/// Google's Gemini, through the Gemini API's <c>generateContent</c> (ADR-067), with the player's own
/// API key. Every question asks for JSON that follows its schema at the question's thinking level;
/// the model's thoughts are left out of the answer. Failures become answers without JSON, in words
/// the player can act on.
/// </summary>
public sealed class GeminiLanguageModel : ILanguageModel, IDisposable
{
    /// <summary>The Gemini API.</summary>
    public static readonly Uri DefaultAddress = new("https://generativelanguage.googleapis.com/v1beta/");

    // Why a candidate stopped that means it would not answer.
    private static readonly string[] Withheld = ["SAFETY", "RECITATION", "BLOCKLIST", "PROHIBITED_CONTENT", "SPII", "LANGUAGE"];

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _address;
    private readonly string _key;
    private readonly string _model;

    /// <summary>Creates the model.</summary>
    /// <param name="apiKey">The player's Gemini API key.</param>
    /// <param name="model">The model's id, for example <c>gemini-3.8-flash</c>.</param>
    /// <param name="name">The model's name, for the player.</param>
    /// <param name="timeout">How long a question may take.</param>
    /// <param name="http">The HTTP client to send with, for tests; a new one when <c>null</c>.</param>
    /// <param name="address">The API's address; Google's when <c>null</c>.</param>
    public GeminiLanguageModel(string apiKey, string model, string name, TimeSpan timeout, HttpClient? http = null, Uri? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        (_key, _model, Name, _address) = (apiKey.Trim(), model, name, address ?? DefaultAddress);
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = timeout };
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The request a question becomes, for tests.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The request's JSON body.</returns>
    public JsonObject Request(ModelQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var contents = new JsonArray();
        foreach (var turn in question.Turns)
        {
            contents.Add(new JsonObject { ["role"] = turn.FromModel ? "model" : "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = turn.Text }) });
        }

        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = question.Instructions }) },
            ["contents"] = contents,
            ["generationConfig"] = new JsonObject
            {
                ["maxOutputTokens"] = question.MaxTokens,
                ["responseFormat"] = new JsonObject
                {
                    ["text"] = new JsonObject { ["mimeType"] = "APPLICATION_JSON", ["schema"] = JsonNode.Parse(question.Schema) },
                },
                ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = Level(question.Effort) },
            },
        };
    }

    /// <inheritdoc />
    public async Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(question);
        try
        {
            var address = new Uri(_address, $"models/{Uri.EscapeDataString(_model)}:generateContent");
            var (status, body) = await JsonHttp.Post(_http, address, Request(question), [new("x-goog-api-key", _key)], cancel).ConfigureAwait(false);
            return status is >= 200 and < 300 ? Read(body) : Failed(Problem(status, body));
        }
        catch (HttpRequestException)
        {
            return Failed("Gemini could not be reached: is this computer online?");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return Failed("Gemini took too long to answer");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    /// <summary>The answer in a response: its first candidate's text without thoughts, or why there is none.</summary>
    /// <param name="body">The response's JSON.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Read(string body)
    {
        var root = JsonHttp.Parse(body);
        if (root is null)
        {
            return Failed("Gemini's answer could not be read");
        }

        if (JsonHttp.Text(root["promptFeedback"]?["blockReason"]) is { } blocked)
        {
            return ModelAnswer.Without(ModelOutcome.Refused, $"Gemini declined the question ({blocked.ToLowerInvariant()})");
        }

        var candidate = (root["candidates"] as JsonArray)?.FirstOrDefault();
        var finish = JsonHttp.Text(candidate?["finishReason"]);
        if (finish == "MAX_TOKENS")
        {
            return ModelAnswer.Without(ModelOutcome.CutShort, "Gemini's answer ran too long");
        }

        if (finish is not null && Withheld.Contains(finish, StringComparer.Ordinal))
        {
            return ModelAnswer.Without(ModelOutcome.Refused, $"Gemini declined to answer ({finish.ToLowerInvariant()})");
        }

        var text = new StringBuilder();
        foreach (var part in candidate?["content"]?["parts"] as JsonArray ?? [])
        {
            if (part?["thought"] is not JsonValue thought || !thought.TryGetValue<bool>(out var isThought) || !isThought)
            {
                text.Append(JsonHttp.Text(part?["text"]));
            }
        }

        return text.Length == 0
            ? Failed("Gemini's answer was empty")
            : ModelAnswer.Of(text.ToString(), JsonHttp.Count(root["usageMetadata"]?["promptTokenCount"]), JsonHttp.Count(root["usageMetadata"]?["candidatesTokenCount"]));
    }

    /// <summary>A failed response in words.</summary>
    private string Problem(int status, string body)
    {
        var error = JsonHttp.Parse(body)?["error"];
        var message = JsonHttp.Text(error?["message"]) ?? $"the answer {status}";
        var keyRefused = message.Contains("API key", StringComparison.OrdinalIgnoreCase) || body.Contains("API_KEY_INVALID", StringComparison.Ordinal);
        return status switch
        {
            400 when keyRefused => "Google did not accept the key: check it in Settings",
            401 or 403 => keyRefused ? "Google did not accept the key: check it in Settings" : "the key may not use this model",
            404 => $"Gemini does not know the model {_model}",
            429 => "too many questions, or the free quota is spent: Google asks to wait a little",
            >= 500 => "Gemini is having trouble; try again later",
            _ => "Gemini refused the question: " + message,
        };
    }

    /// <summary>The thinking level a question's effort asks.</summary>
    private static string Level(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "low",
        ModelEffort.Medium => "medium",
        ModelEffort.High => "high",
    };

    /// <summary>A failure, in words.</summary>
    private static ModelAnswer Failed(string problem) => ModelAnswer.Without(ModelOutcome.Failed, problem);
}
