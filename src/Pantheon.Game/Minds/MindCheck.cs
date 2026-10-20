using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pantheon.Game.Minds;

/// <summary>The settings' test of a model (ADR-065): a short question, to show the player it answers.</summary>
public static class MindCheck
{
    /// <summary>The question: a god's one-line greeting.</summary>
    public static ModelQuestion Question { get; } = new()
    {
        Instructions = "You are a god in PANTHEON, a god game. Greet a newly risen rival god in one short, in-character sentence. Answer only with the JSON asked for.",
        Turns = [new ModelTurn(false, "Greet me.")],
        Schema = """{"type":"object","properties":{"greeting":{"type":"string"}},"required":["greeting"],"additionalProperties":false}""",
        Effort = ModelEffort.Low,
        MaxTokens = 2000,
    };

    /// <summary>Asks the model the question.</summary>
    /// <param name="model">The model.</param>
    /// <param name="cancel">Cancels the question.</param>
    /// <returns>What the settings say: the model's greeting, or why there is none.</returns>
    public static async Task<(bool Answered, string Text)> Run(ILanguageModel model, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(model);
        var answer = await model.Ask(Question, cancel).ConfigureAwait(false);
        return Read(model.Name, answer);
    }

    /// <summary>What the settings say of an answer.</summary>
    /// <param name="name">The model's name.</param>
    /// <param name="answer">Its answer.</param>
    /// <returns>Whether it answered, and the words.</returns>
    public static (bool Answered, string Text) Read(string name, ModelAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Outcome != ModelOutcome.Answered)
        {
            return (false, $"{name} did not answer: {answer.Problem}.");
        }

        try
        {
            using var document = JsonDocument.Parse(answer.Json);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("greeting", out var greeting)
                && greeting.ValueKind == JsonValueKind.String && greeting.GetString() is { Length: > 0 } text
                ? (true, $"{name} answers: “{text.Trim()}”")
                : (false, $"{name} answered, but not as asked.");
        }
        catch (JsonException)
        {
            return (false, $"{name} answered, but not in JSON.");
        }
    }
}
