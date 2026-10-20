using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Game.Minds;

/// <summary>Who said a line of a conversation.</summary>
public enum TalkSpeaker
{
    /// <summary>The god.</summary>
    God,

    /// <summary>The player.</summary>
    Player,

    /// <summary>What happened, told by the game.</summary>
    Note,
}

/// <summary>A line of a conversation, as the panel shows it.</summary>
/// <param name="Speaker">Who said it.</param>
/// <param name="Text">What was said.</param>
public sealed record TalkLine(TalkSpeaker Speaker, string Text);

/// <summary>Something the player might say next.</summary>
/// <param name="Text">The words.</param>
/// <param name="ActId">The act it asks of the god, for the scribes to answer without a model; <c>ask</c> asks what it wants; <c>null</c> for free words.</param>
public sealed record TalkSuggestion(string Text, string? ActId = null);

/// <summary>
/// A conversation with one god (ADR-066): its lines, the turns the model was given (kept as they
/// were sent, so the conversation only ever grows), the suggestions shown, and the goodwill the
/// player's words have won, fading with the world's time. Kept for the session, not in the world:
/// what talk changes is the treaties and wars it leads to.
/// </summary>
public sealed class GodTalk
{
    private float _goodwill;
    private long _goodwillTick;

    /// <summary>Creates a conversation.</summary>
    /// <param name="godId">The god spoken with.</param>
    public GodTalk(int godId) => GodId = godId;

    /// <summary>The god spoken with.</summary>
    public int GodId { get; }

    /// <summary>The lines, oldest first.</summary>
    public List<TalkLine> Lines { get; } = [];

    /// <summary>The turns the model was given and gave, oldest first.</summary>
    public List<ModelTurn> Turns { get; } = [];

    /// <summary>What the player might say next.</summary>
    public IReadOnlyList<TalkSuggestion> Suggestions { get; set; } = [];

    /// <summary>Whether the god is answering now.</summary>
    public bool Answering { get; set; }

    /// <summary>Words the god sent the player unasked since the player last spoke (ADR-072), for its model to know it said them.</summary>
    public List<string> Sent { get; } = [];

    /// <summary>The conversation's goodwill at a tick: what the player's words have won, from -1 to 1, halving every half-life.</summary>
    /// <param name="tick">The world's tick now.</param>
    /// <param name="rules">The talk's rules.</param>
    /// <returns>The goodwill.</returns>
    public float Goodwill(long tick, TalkRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var seconds = Math.Max(0L, tick - _goodwillTick) / (float)SimulationTime.TicksPerSecond;
        return _goodwill * MathF.Pow(0.5f, seconds / rules.GoodwillHalfLifeSeconds);
    }

    /// <summary>Moves the goodwill by a reply's regard.</summary>
    /// <param name="tick">The world's tick now.</param>
    /// <param name="regard">How the player's words moved the god, from -3 to 3.</param>
    /// <param name="rules">The talk's rules.</param>
    public void Move(long tick, int regard, TalkRules rules)
    {
        _goodwill = Math.Clamp(Goodwill(tick, rules) + (Math.Clamp(regard, -3, 3) / 3f * rules.RegardStep), -1f, 1f);
        _goodwillTick = tick;
    }
}
