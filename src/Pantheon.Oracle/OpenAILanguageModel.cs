using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Game.Minds;

namespace Pantheon.Oracle;

/// <summary>
/// OpenAI's GPT, through the Responses API (ADR-067), with the player's own key. Every question asks
/// for JSON that follows its schema exactly (strict structured output) at the question's reasoning
/// effort; nothing is stored on OpenAI's side. Failures become answers without JSON, in words the
/// player can act on.
/// </summary>
public sealed class OpenAILanguageModel : ILanguageModel, IDisposable
{
    /// <summary>OpenAI's API.</summary>
    public static readonly Uri DefaultAddress = new("https://api.openai.com/v1/");

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _address;
    private readonly string _key;
    private readonly string _model;

    /// <summary>Creates the model.</summary>
    /// <param name="apiKey">The player's OpenAI key.</param>
    /// <param name="model">The model's id, for example <c>gpt-6.1-sol</c>.</param>
    /// <param name="name">The model's name, for the player.</param>
    /// <param name="timeout">How long a question may take.</param>
    /// <param name="http">The HTTP client to send with, for tests; a new one when <c>null</c>.</param>
    /// <param name="address">The API's address; OpenAI's when <c>null</c>.</param>
    public OpenAILanguageModel(string apiKey, string model, string name, TimeSpan timeout, HttpClient? http = null, Uri? address = null)
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
        var input = new JsonArray();
        foreach (var turn in question.Turns)
        {
            input.Add(new JsonObject { ["role"] = turn.FromModel ? "assistant" : "user", ["content"] = turn.Text });
        }

        return new JsonObject
        {
            ["model"] = _model,
            ["instructions"] = question.Instructions,
            ["input"] = input,
            ["max_output_tokens"] = question.MaxTokens,
            ["reasoning"] = new JsonObject { ["effort"] = Effort(question.Effort) },
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject { ["type"] = "json_schema", ["name"] = "answer", ["strict"] = true, ["schema"] = JsonNode.Parse(question.Schema) },
            },
            ["store"] = false,
        };
    }

    /// <inheritdoc />
    public async Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(question);
        try
        {
            var (status, body) = await JsonHttp.Post(_http, new Uri(_address, "responses"), Request(question), [new("Authorization", "Bearer " + _key)], cancel).ConfigureAwait(false);
            return status is >= 200 and < 300 ? Read(body) : Failed(Problem(status, body));
        }
        catch (HttpRequestException)
        {
            return Failed("OpenAI could not be reached: is this computer online?");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return Failed("GPT took too long to answer");
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

    /// <summary>The answer in a response: its text, or why there is none.</summary>
    /// <param name="body">The response's JSON.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Read(string body)
    {
        var root = JsonHttp.Parse(body);
        if (root is null)
        {
            return Failed("OpenAI's answer could not be read");
        }

        var incomplete = JsonHttp.Text(root["incomplete_details"]?["reason"]);
        if (incomplete == "max_output_tokens")
        {
            return ModelAnswer.Without(ModelOutcome.CutShort, "GPT's answer ran too long");
        }

        if (incomplete == "content_filter")
        {
            return ModelAnswer.Without(ModelOutcome.Refused, "GPT's answer was filtered");
        }

        var text = new StringBuilder();
        foreach (var part in (root["output"] as JsonArray ?? []).Where(o => JsonHttp.Text(o?["type"]) == "message").SelectMany(o => o?["content"] as JsonArray ?? []))
        {
            switch (JsonHttp.Text(part?["type"]))
            {
                case "refusal":
                    return ModelAnswer.Without(ModelOutcome.Refused, "GPT declined to answer");
                case "output_text":
                    text.Append(JsonHttp.Text(part?["text"]));
                    break;
            }
        }

        return text.Length == 0
            ? Failed("GPT's answer was empty")
            : ModelAnswer.Of(text.ToString(), JsonHttp.Count(root["usage"]?["input_tokens"]), JsonHttp.Count(root["usage"]?["output_tokens"]));
    }

    /// <summary>A failed response in words.</summary>
    private string Problem(int status, string body)
    {
        var error = JsonHttp.Parse(body)?["error"];
        var message = JsonHttp.Text(error?["message"]) ?? $"the answer {status}";
        return status switch
        {
            401 => "OpenAI did not accept the key: check it in Settings",
            403 => "the key may not use this model",
            404 => $"OpenAI does not know the model {_model}",
            429 => JsonHttp.Text(error?["code"]) == "insufficient_quota" ? "the OpenAI account has no credit left" : "too many questions at once: OpenAI asks to wait a little",
            >= 500 => "OpenAI is having trouble; try again later",
            _ => "OpenAI refused the question: " + message,
        };
    }

    /// <summary>The reasoning effort a question's effort asks.</summary>
    private static string Effort(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "low",
        ModelEffort.Medium => "medium",
        ModelEffort.High => "high",
    };

    /// <summary>A failure, in words.</summary>
    private static ModelAnswer Failed(string problem) => ModelAnswer.Without(ModelOutcome.Failed, problem);
}
