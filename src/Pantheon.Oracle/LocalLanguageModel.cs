using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Game.Minds;

namespace Pantheon.Oracle;

/// <summary>
/// A language model on the player's own computer (ADR-065), served by Ollama: its chat API is asked
/// for JSON that follows the question's schema. Nothing leaves the computer unless the player points
/// the address elsewhere. Failures become answers without JSON, in words the player can act on.
/// </summary>
public sealed class LocalLanguageModel : ILanguageModel, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _address;
    private readonly string _model;

    /// <summary>Creates the model.</summary>
    /// <param name="address">Where the model is served, for example <c>http://localhost:11434</c>.</param>
    /// <param name="model">The model's name, for example <c>llama3.2</c>.</param>
    /// <param name="timeout">How long a question may take.</param>
    /// <param name="http">The HTTP client to send with, for tests; a new one when <c>null</c>.</param>
    public LocalLanguageModel(Uri address, string model, TimeSpan timeout, HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        // The API's paths are taken relative to the address, which must end in a slash to keep its own path.
        _address = address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
        _model = model.Trim();
        _ownsHttp = http is null;
        _http = http ?? new HttpClient { Timeout = timeout };
    }

    /// <inheritdoc />
    public string Name => $"{_model} (on this computer)";

    /// <summary>The request a question becomes, for tests.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The request's JSON body.</returns>
    public JsonObject Request(ModelQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = question.Instructions } };
        foreach (var turn in question.Turns)
        {
            messages.Add(new JsonObject { ["role"] = turn.FromModel ? "assistant" : "user", ["content"] = turn.Text });
        }

        return new JsonObject
        {
            ["model"] = _model,
            ["messages"] = messages,
            ["stream"] = false,
            ["format"] = JsonNode.Parse(question.Schema),
            ["options"] = new JsonObject { ["num_predict"] = question.MaxTokens },
        };
    }

    /// <inheritdoc />
    public async Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(question);
        try
        {
            using var content = new StringContent(Request(question).ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(new Uri(_address, "api/chat"), content, cancel).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? Read(body) : Failed(ErrorOf(body) ?? $"the local model answered {(int)response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            return Failed($"no model is served at {_address}: is Ollama running?");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return Failed("the local model took too long to answer");
        }
    }

    /// <summary>The models the server has, for the settings to offer.</summary>
    /// <param name="cancel">Cancels the question.</param>
    /// <returns>Their names, or why there are none.</returns>
    public async Task<(IReadOnlyList<string> Models, string? Problem)> Installed(CancellationToken cancel)
    {
        try
        {
            using var response = await _http.GetAsync(new Uri(_address, "api/tags"), cancel).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ([], $"the server at {_address} answered {(int)response.StatusCode}");
            }

            var models = JsonNode.Parse(body)?["models"]?.AsArray().Select(m => m?["name"]?.GetValue<string>()).OfType<string>().Order(StringComparer.Ordinal).ToList() ?? [];
            return (models, models.Count == 0 ? "the server has no models yet: try `ollama pull llama3.2`" : null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            return ([], $"no model is served at {_address}: is Ollama running?");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return ([], "the server took too long to answer");
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

    /// <summary>The answer in a chat response: its text, or why there is none.</summary>
    /// <param name="body">The response's JSON.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Read(string body)
    {
        try
        {
            var root = JsonNode.Parse(body);
            if (root?["done_reason"]?.GetValue<string>() == "length")
            {
                return ModelAnswer.Without(ModelOutcome.CutShort, "the local model's answer ran too long");
            }

            var text = root?["message"]?["content"]?.GetValue<string>() ?? string.Empty;
            long Count(string name) => root?[name] is JsonValue value && value.TryGetValue<long>(out var n) ? n : 0;
            return text.Trim().Length == 0
                ? ModelAnswer.Without(ModelOutcome.Failed, "the local model's answer was empty")
                : ModelAnswer.Of(text, Count("prompt_eval_count"), Count("eval_count"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return ModelAnswer.Without(ModelOutcome.Failed, "the local model's answer could not be read");
        }
    }

    /// <summary>The error a server's body names, if any.</summary>
    private static string? ErrorOf(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["error"]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A failure, in words.</summary>
    private static ModelAnswer Failed(string problem) => ModelAnswer.Without(ModelOutcome.Failed, problem);
}
