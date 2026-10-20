using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Persistence;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// How the gods' and Creatures' agents think (ADR-072), from <c>minds.json</c>'s <c>Agents</c>: how often a
/// mind may be asked, how long hooks gather into one turn, how often a god looks about it unprompted, how
/// many questions an hour, what a turn may do, and the quick models asked when the player prefers them.
/// </summary>
public sealed record AgentRules
{
    /// <summary>The least real time between one god's turns, in seconds.</summary>
    public required float GodTurnSeconds { get; init; }

    /// <summary>The least real time between one Creature's turns, in seconds.</summary>
    public required float CreatureTurnSeconds { get; init; }

    /// <summary>How long a mind waits after a hook for others to come with it, in real seconds.</summary>
    public required float GatherSeconds { get; init; }

    /// <summary>The world's seconds between a god's turns to look about it when nothing wakes it.</summary>
    public required float ReflectSeconds { get; init; }

    /// <summary>The most turns an hour unless the player chooses otherwise.</summary>
    public required int TurnsPerHour { get; init; }

    /// <summary>The most questions waiting on the model at once.</summary>
    public required int MaxAsking { get; init; }

    /// <summary>The most acts a god takes in a turn.</summary>
    public required int MaxActions { get; init; }

    /// <summary>The most words a god sends in a turn.</summary>
    public required int MaxMessages { get; init; }

    /// <summary>The longest a god's journal may be, in characters.</summary>
    public required int JournalLength { get; init; }

    /// <summary>The world's seconds the planner leaves a god's own aims be.</summary>
    public required float AimHoldSeconds { get; init; }

    /// <summary>The least weight a god's own aims carry (0..1).</summary>
    public required float AimWeight { get; init; }

    /// <summary>How many villages a god is told of, the nearest to its concerns first.</summary>
    public required int VillagesShown { get; init; }

    /// <summary>How hard the model thinks about a turn.</summary>
    public required ModelEffort Effort { get; init; }

    /// <summary>The longest answer to a turn, in tokens.</summary>
    public required int MaxTokens { get; init; }

    /// <summary>How long the agents rest after three failed questions in a row, in real seconds, before they are asked again.</summary>
    public required float RestSeconds { get; init; }

    /// <summary>Without a model, the world's seconds between one god's words.</summary>
    public required float ScribeGodSeconds { get; init; }

    /// <summary>Without a model, the world's seconds between one Creature's words.</summary>
    public required float ScribeCreatureSeconds { get; init; }

    /// <summary>How long a Creature's words stay above it, in real seconds.</summary>
    public required float SpeechSeconds { get; init; }

    /// <summary>The Claude model the agents ask when the player prefers the quick one.</summary>
    public required string QuickClaudeModel { get; init; }

    /// <summary>The GPT model the agents ask when the player prefers the quick one.</summary>
    public required string QuickOpenAIModel { get; init; }

    /// <summary>The Gemini model the agents ask when the player prefers the quick one.</summary>
    public required string QuickGeminiModel { get; init; }

    /// <summary>Throws when a rule is out of range or a quick model is not offered.</summary>
    /// <param name="content">The minds' content, for the models offered.</param>
    internal void Validate(MindContent content)
    {
        if (!(GodTurnSeconds is >= 5f and <= 3600f) || !(CreatureTurnSeconds is >= 5f and <= 3600f) || !(GatherSeconds is >= 0f and <= 60f)
            || !(ReflectSeconds is >= 30f and <= 7200f) || TurnsPerHour is < 10 or > 5000 || MaxAsking is < 1 or > 8
            || MaxActions is < 1 or > 6 || MaxMessages is < 0 or > 4 || JournalLength is < 200 or > 4000)
        {
            throw new ContentFormatException($"{MindContent.FileName}: Agents needs GodTurnSeconds and CreatureTurnSeconds from 5 to 3600, GatherSeconds from 0 to 60, ReflectSeconds from 30 to 7200, TurnsPerHour from 10 to 5000, MaxAsking from 1 to 8, MaxActions from 1 to 6, MaxMessages from 0 to 4 and JournalLength from 200 to 4000.");
        }

        if (!(AimHoldSeconds is >= 10f and <= 3600f) || !(AimWeight is >= 0f and <= 1f) || VillagesShown is < 4 or > 60 || !Enum.IsDefined(Effort)
            || MaxTokens is < 500 or > 32000 || !(RestSeconds is >= 10f and <= 3600f) || !(ScribeGodSeconds is >= 5f and <= 3600f)
            || !(ScribeCreatureSeconds is >= 5f and <= 3600f) || !(SpeechSeconds is >= 1f and <= 30f))
        {
            throw new ContentFormatException($"{MindContent.FileName}: Agents needs AimHoldSeconds from 10 to 3600, AimWeight from 0 to 1, VillagesShown from 4 to 60, an Effort, MaxTokens from 500 to 32000, RestSeconds from 10 to 3600, ScribeGodSeconds and ScribeCreatureSeconds from 5 to 3600 and SpeechSeconds from 1 to 30.");
        }

        foreach (var (id, models) in new[] { (QuickClaudeModel, content.ClaudeModels), (QuickOpenAIModel, content.OpenAIModels), (QuickGeminiModel, content.GeminiModels) })
        {
            if (!models.Any(m => string.Equals(m.Id, id, StringComparison.Ordinal)))
            {
                throw new ContentFormatException($"{MindContent.FileName}: the agents' quick model '{id}' is not among the models offered.");
            }
        }
    }
}

/// <summary>
/// What the scribes say for the gods and Creatures when no model does (ADR-072), from <c>minds.json</c>'s
/// <c>Voices</c>: lines by the moment, one chosen from the seed. <c>{name}</c> is the speaker, <c>{god}</c> a
/// Creature's god, <c>{other}</c> the god or Creature it speaks of, <c>{village}</c> the village and
/// <c>{creature}</c> the Creature concerned.
/// </summary>
public sealed record VoiceLines
{
    /// <summary>The moments a god speaks of to another god, each with its lines.</summary>
    public static IReadOnlyList<string> GodMoments { get; } =
    [
        "Visitor.Welcome", "Visitor.Watch", "Visitor.Repel", "War.Declared", "War.Answered", "Alliance.Offered", "Alliance.Formed",
        "Peace.Asked", "Peace.Made", "Betrayed", "Betraying", "Struck", "Fight.Won", "Fight.Lost", "Met",
    ];

    /// <summary>The moments a Creature speaks of, each with its lines.</summary>
    public static IReadOnlyList<string> CreatureMoments { get; } =
    [
        "Meeting.Friend", "Meeting.Foe", "Meeting.Stranger", "Fight.Won", "Fight.Lost", "Fight.Drew", "Fight.Fled", "Judged.Rewarded",
        "Judged.Punished", "Grew", "Need.Hungry", "Need.Tired", "Home.Struck", "Noticed.Watched", "Noticed.Guard",
    ];

    /// <summary>The gods' lines by moment.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Gods { get; init; }

    /// <summary>The Creatures' lines by moment.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Creatures { get; init; }

    /// <summary>Throws when a moment has no lines or an unknown moment is given.</summary>
    internal void Validate()
    {
        foreach (var (name, lines, moments) in new[] { ("Gods", Gods, GodMoments), ("Creatures", Creatures, CreatureMoments) })
        {
            if (moments.FirstOrDefault(m => !lines.TryGetValue(m, out var said) || said.Count == 0 || said.Any(string.IsNullOrWhiteSpace)) is { } missing)
            {
                throw new ContentFormatException($"{MindContent.FileName}: Voices.{name} needs lines for '{missing}'.");
            }

            if (lines.Keys.FirstOrDefault(k => !moments.Contains(k, StringComparer.Ordinal)) is { } unknown)
            {
                throw new ContentFormatException($"{MindContent.FileName}: Voices.{name} has no moment '{unknown}'.");
            }
        }
    }
}
