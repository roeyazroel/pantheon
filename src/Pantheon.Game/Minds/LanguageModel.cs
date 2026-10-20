using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pantheon.Game.Minds;

/// <summary>
/// A language model that gives the rival gods their minds (ADR-065): Claude, a model running on the
/// player's own computer, or none at all. The game asks it questions off the simulation's thread and
/// takes its answers in between ticks, as it takes a remote player's; it never waits for one to play.
/// </summary>
public interface ILanguageModel
{
    /// <summary>The model's name, for the player, for example "Claude Opus 5.5".</summary>
    string Name { get; }

    /// <summary>Asks a question; the answer is JSON that follows the question's schema, or why there is none.</summary>
    /// <param name="question">The question.</param>
    /// <param name="cancel">Cancels the question.</param>
    /// <returns>The answer; never throws for the model's or the network's failures.</returns>
    Task<ModelAnswer> Ask(ModelQuestion question, CancellationToken cancel);
}

/// <summary>How hard a model thinks before it answers: harder is better and slower.</summary>
public enum ModelEffort
{
    /// <summary>Quick answers, for talk.</summary>
    Low,

    /// <summary>Considered answers, for the pantheon's making.</summary>
    Medium,

    /// <summary>The model's best.</summary>
    High,
}

/// <summary>One turn of a conversation: the player's words, or the model's.</summary>
/// <param name="FromModel">Whether the model said it.</param>
/// <param name="Text">What was said.</param>
public sealed record ModelTurn(bool FromModel, string Text);

/// <summary>A question for a language model.</summary>
public sealed record ModelQuestion
{
    /// <summary>The standing instructions: who the model plays and the rules it keeps.</summary>
    public required string Instructions { get; init; }

    /// <summary>The conversation, oldest first; the last turn is the player's.</summary>
    public required IReadOnlyList<ModelTurn> Turns { get; init; }

    /// <summary>The JSON schema the answer follows, as JSON text: an object with every property required and no others.</summary>
    public required string Schema { get; init; }

    /// <summary>How hard the model thinks.</summary>
    public ModelEffort Effort { get; init; } = ModelEffort.Low;

    /// <summary>The longest answer, in tokens, thinking included.</summary>
    public int MaxTokens { get; init; } = 4000;
}

/// <summary>How a question went.</summary>
public enum ModelOutcome
{
    /// <summary>The model answered.</summary>
    Answered,

    /// <summary>The model declined to answer.</summary>
    Refused,

    /// <summary>The answer ran past its length and is incomplete.</summary>
    CutShort,

    /// <summary>The model could not be reached, or the answer could not be read.</summary>
    Failed,
}

/// <summary>A model's answer.</summary>
/// <param name="Outcome">How the question went.</param>
/// <param name="Json">The answer's JSON when answered; empty otherwise.</param>
/// <param name="Problem">Why there is no answer, in words; <c>null</c> when answered.</param>
/// <param name="InputTokens">Tokens read, when the model tells.</param>
/// <param name="OutputTokens">Tokens written, when the model tells.</param>
public sealed record ModelAnswer(ModelOutcome Outcome, string Json, string? Problem, long InputTokens = 0, long OutputTokens = 0)
{
    /// <summary>An answer.</summary>
    /// <param name="json">Its JSON.</param>
    /// <param name="inputTokens">Tokens read.</param>
    /// <param name="outputTokens">Tokens written.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Of(string json, long inputTokens = 0, long outputTokens = 0) => new(ModelOutcome.Answered, json, null, inputTokens, outputTokens);

    /// <summary>No answer.</summary>
    /// <param name="outcome">Why: refused, cut short or failed.</param>
    /// <param name="problem">Why, in words.</param>
    /// <returns>The answer.</returns>
    public static ModelAnswer Without(ModelOutcome outcome, string problem) => new(outcome, string.Empty, problem);
}
