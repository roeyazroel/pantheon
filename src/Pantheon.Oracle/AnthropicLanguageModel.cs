using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Pantheon.Game.Minds;

namespace Pantheon.Oracle;

/// <summary>
/// Claude, through Anthropic's SDK (ADR-065), with the player's own key. Every question asks for JSON
/// that follows its schema (structured output) at the question's effort, with the instructions cached;
/// a model that declines for policy reasons hands the question to Anthropic's default fallback model.
/// Failures become answers without JSON, in words the player can act on.
/// </summary>
public sealed class AnthropicLanguageModel : ILanguageModel, IDisposable
{
    /// <summary>The beta that lets a declined question fall back to another model.</summary>
    public const string FallbackBeta = "server-side-fallback-2026-07-01";

    private readonly IAnthropicClient _client;
    private readonly string _model;

    /// <summary>Creates the model.</summary>
    /// <param name="apiKey">The player's Anthropic key; when empty, the <c>ANTHROPIC_API_KEY</c> environment variable's.</param>
    /// <param name="model">The model's id, for example <c>claude-opus-5-5</c>.</param>
    /// <param name="name">The model's name, for the player.</param>
    /// <param name="timeout">How long a question may take.</param>
    /// <param name="http">The HTTP client to send with, for tests; the SDK's own when <c>null</c>.</param>
    public AnthropicLanguageModel(string? apiKey, string model, string name, TimeSpan timeout, HttpClient? http = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _model = model;
        Name = name;
        var client = string.IsNullOrWhiteSpace(apiKey) ? new AnthropicClient() : new AnthropicClient { ApiKey = apiKey.Trim() };
        _client = client.WithOptions(options => options with { Timeout = timeout, MaxRetries = 2, HttpClient = http ?? options.HttpClient });
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The request a question becomes, for tests.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The request's parameters.</returns>
    public MessageCreateParams Request(ModelQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        return new MessageCreateParams
        {
            Model = _model,
            MaxTokens = question.MaxTokens,
            System = new List<BetaTextBlockParam> { new() { Text = question.Instructions, CacheControl = new BetaCacheControlEphemeral() } },
            Messages = question.Turns.Select(t => new BetaMessageParam { Role = t.FromModel ? Role.Assistant : Role.User, Content = t.Text }).ToList(),
            OutputConfig = new BetaOutputConfig
            {
                Effort = question.Effort switch
                {
                    ModelEffort.Low => Effort.Low,
                    ModelEffort.Medium => Effort.Medium,
                    ModelEffort.High => Effort.High,
                },
                Format = new BetaJsonOutputFormat { Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(question.Schema)! },
            },
            Betas = [FallbackBeta],
            Fallbacks = new Default(),
        };
    }

    /// <inheritdoc />
    public async Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(question);
        try
        {
            var message = await _client.Beta.Messages.Create(Request(question), cancel).ConfigureAwait(false);
            return Read(message);
        }
        catch (AnthropicUnauthorizedException)
        {
            return Failed("Anthropic did not accept the key: check it in Settings");
        }
        catch (AnthropicForbiddenException)
        {
            return Failed("the key may not use this model");
        }
        catch (AnthropicNotFoundException)
        {
            return Failed($"Anthropic does not know the model {_model}");
        }
        catch (AnthropicRateLimitException)
        {
            return Failed("too many questions at once: Anthropic asks to wait a little");
        }
        catch (Anthropic5xxException)
        {
            return Failed("Anthropic is having trouble; try again later");
        }
        catch (AnthropicBadRequestException exception)
        {
            return Failed("Anthropic refused the question: " + exception.Message);
        }
        catch (AnthropicApiException exception)
        {
            return Failed("Anthropic answered with an error: " + exception.Message);
        }
        catch (AnthropicIOException)
        {
            return Failed("Anthropic could not be reached: is this computer online?");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return Failed("Claude took too long to answer");
        }
        catch (Exception exception) when (exception is AnthropicException or HttpRequestException or JsonException)
        {
            return Failed(exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose() => (_client as IDisposable)?.Dispose();

    /// <summary>The answer in a message: its text, or why there is none.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Read(BetaMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var stop = message.StopReason?.Raw();
        if (stop == "refusal")
        {
            return ModelAnswer.Without(ModelOutcome.Refused, "Claude declined to answer");
        }

        if (stop == "max_tokens")
        {
            return ModelAnswer.Without(ModelOutcome.CutShort, "Claude's answer ran too long");
        }

        var text = string.Concat(message.Content.Select(b => b.TryPickText(out var block) ? block.Text : string.Empty));
        return text.Length == 0
            ? ModelAnswer.Without(ModelOutcome.Failed, "Claude's answer was empty")
            : ModelAnswer.Of(text, message.Usage.InputTokens, message.Usage.OutputTokens);
    }

    /// <summary>A failure, in words.</summary>
    private static ModelAnswer Failed(string problem) => ModelAnswer.Without(ModelOutcome.Failed, problem);
}
