using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Minds.Agents;

/// <summary>A Creature's mind's answer to a turn.</summary>
/// <param name="Say">What it says aloud; empty to say nothing.</param>
/// <param name="Choice">The id of the choice it makes, or <c>none</c>.</param>
public sealed record CreatureTurn(string Say, string Choice);

/// <summary>
/// What a Creature's mind is told at a moment that matters to it (ADR-072), and how its answer is read:
/// who it is, what happened, how it is and where, who is near, what its god has taught it, what it
/// remembers, and what it could do now, its instincts' likeliest first.
/// </summary>
public static class CreatureAgentPrompt
{
    /// <summary>The choice that is no choice: it goes on as it was.</summary>
    public const string NoChoice = "none";

    // The longest a Creature's line may be, in characters.
    private const int MaxSay = 120;

    // A learned weight above this is a liking, below its mirror a dislike (preferences start at 0.5).
    private const float Liking = 0.65f;

    /// <summary>The answer's JSON schema.</summary>
    public const string Schema = """{"type":"object","properties":{"say":{"type":"string","description":"What you say aloud: one short line, or empty."},"choice":{"type":"string","description":"The id of one choice from the list, or none."}},"required":["say","choice"],"additionalProperties":false}""";

    /// <summary>The question for the Creature's model.</summary>
    /// <param name="session">The session.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="hooks">What woke it.</param>
    /// <param name="choices">What it could do now.</param>
    /// <param name="rules">The agents' rules.</param>
    /// <returns>The question.</returns>
    public static ModelQuestion Question(GameSession session, CreatureState creature, IReadOnlyList<Hook> hooks, IReadOnlyList<CreatureOption> choices, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rules);
        return new ModelQuestion
        {
            Instructions = Instructions(session.World, creature),
            Turns = [new ModelTurn(false, Turn(session.World, creature, hooks, choices))],
            Schema = Schema,
            Effort = rules.Effort,
            MaxTokens = Math.Min(rules.MaxTokens, 1500),
        };
    }

    /// <summary>The standing instructions: who the Creature is and how it answers.</summary>
    /// <param name="world">The world, for its god's name.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The instructions.</returns>
    public static string Instructions(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        var god = world.Gods.Find(g => g.Id == creature.GodId)?.Name ?? "a god";
        return string.Create(CultureInfo.InvariantCulture, $"""
            You are {creature.Name}, the {creature.Species.ToLowerInvariant()} Creature of the god {god}, on an island in PANTHEON, a god game in the spirit of Black & White. You are a great beast who has learned a few words of speech. You are {TemperamentText.Describe(creature)}.
            You are woken at the moments that matter to you. Then you may say something aloud, and choose what to do next.
            - say: one short line, under twelve words, as a beast who has learned some words would say it: simple, vivid, often speaking of yourself by name. Or nothing ("").
            - choice: the id of one choice from the list, or none to go on as you are. Your instincts, what your god has taught you and your nature, rank the choices, the strongest first; follow them unless the moment moves you otherwise.
            Answer only with the JSON asked for.
            """);
    }

    /// <summary>A turn: what happened, how it is, and its choices.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="hooks">What woke it.</param>
    /// <param name="choices">What it could do now.</param>
    /// <returns>The turn's text.</returns>
    public static string Turn(WorldState world, CreatureState creature, IReadOnlyList<Hook> hooks, IReadOnlyList<CreatureOption> choices)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(choices);
        var text = new StringBuilder("What just happened:\n");
        foreach (var hook in hooks)
        {
            text.Append("- ").Append(hook.Text).Append('\n');
        }

        var needs = creature.Needs;
        text.Append(Format("\nHow you are: health {0:0%}; {1}; {2}; {3}; level {4}; your loyalty to your god {5:0.0}.\n", creature.Health, needs.Hunger >= 0.6f ? "hungry" : "fed", needs.Fatigue >= 0.6f ? "tired" : "rested", needs.Boredom >= 0.6f ? "bored" : "content", creature.Level, creature.Loyalty));
        text.Append(Format("Where you are: near {0}, {1}.\n", GodView.Nearest(world, creature.Position), CreatureText.DescribeActivity(creature, world).ToLowerInvariant()));
        Near(text, world, creature);
        Taught(text, creature);
        Remembered(text, world, creature);
        text.Append(choices.Count == 0 ? "\nYou cannot choose anything else now; you may only speak.\n" : "\nYour choices now, the strongest instinct first:\n");
        for (var i = 0; i < choices.Count; i++)
        {
            text.Append(Format("- {0}: {1}\n", Id(i), CreatureText.Doing(choices[i].Action, CreatureText.Concerning(world, choices[i].SettlementId, choices[i].CreatureId))));
        }

        text.Append(Format("- {0}: go on as you are\n", NoChoice));
        return text.ToString();
    }

    /// <summary>A choice's id by its place in the list.</summary>
    /// <param name="index">Its place, from 0.</param>
    /// <returns>For example <c>c1</c>.</returns>
    public static string Id(int index) => string.Create(CultureInfo.InvariantCulture, $"c{index + 1}");

    /// <summary>Reads an answer; <c>null</c> when it is not the JSON asked for.</summary>
    /// <param name="json">The answer.</param>
    /// <returns>The turn, its line trimmed.</returns>
    public static CreatureTurn? Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var say = root.TryGetProperty("say", out var line) && line.ValueKind == JsonValueKind.String ? line.GetString()?.Trim().Trim('"', '“', '”') ?? string.Empty : string.Empty;
            var choice = root.TryGetProperty("choice", out var chosen) && chosen.ValueKind == JsonValueKind.String ? chosen.GetString()?.Trim() ?? NoChoice : NoChoice;
            return new CreatureTurn(say.Length <= MaxSay ? say : say[..(MaxSay - 1)].TrimEnd() + "…", choice);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The Creatures in sight, and how it stands with each.</summary>
    private static void Near(StringBuilder text, WorldState world, CreatureState creature)
    {
        var near = world.Creatures.Where(c => c.Id != creature.Id && c.Activity.Phase != CreaturePhase.Banished && Distance(c.Position, creature.Position) <= 400f).ToList();
        if (near.Count == 0)
        {
            return;
        }

        text.Append("Creatures near you: ");
        text.AppendJoin("; ", near.Select(other =>
        {
            var god = world.Gods.Find(g => g.Id == other.GodId)?.Name ?? "a god";
            var stance = DiplomacyModel.AreAtWar(world, creature.GodId, other.GodId) ? ", whose god is at war with yours"
                : DiplomacyModel.AreAllied(world, creature.GodId, other.GodId) ? ", whose god is your god's ally" : string.Empty;
            return Format("{0}, {1}'s {2}{3}, health {4:0%}", other.Name, god, other.Species.ToLowerInvariant(), stance, other.Health);
        }));
        text.Append(".\n");
    }

    /// <summary>What its god's rewards and punishments taught it to like and dislike.</summary>
    private static void Taught(StringBuilder text, CreatureState creature)
    {
        var likes = creature.Preferences.Where(p => p.Weight >= Liking).OrderByDescending(p => p.Weight).Select(p => CreatureText.Doing(p.Action, null)).Distinct(StringComparer.Ordinal).Take(3).ToList();
        var dislikes = creature.Preferences.Where(p => p.Weight <= 1f - Liking).OrderBy(p => p.Weight).Select(p => CreatureText.Doing(p.Action, null)).Distinct(StringComparer.Ordinal).Take(3).ToList();
        if (likes.Count + dislikes.Count == 0)
        {
            return;
        }

        text.Append("What your god has taught you:");
        if (likes.Count > 0)
        {
            text.Append(" you like ").AppendJoin(", ", likes).Append('.');
        }

        if (dislikes.Count > 0)
        {
            text.Append(" You were punished for ").AppendJoin(", ", dislikes).Append('.');
        }

        text.Append('\n');
    }

    /// <summary>What it did last, and how its god judged it.</summary>
    private static void Remembered(StringBuilder text, WorldState world, CreatureState creature)
    {
        var memories = creature.Memories.TakeLast(4).Select(m =>
        {
            var judged = m.Feedback switch
            {
                CreatureFeedback.Rewarded => " (rewarded)",
                CreatureFeedback.Punished => " (punished)",
                CreatureFeedback.None => string.Empty,
            };
            return CreatureText.Doing(m.Action, CreatureText.Concerning(world, m.SettlementId, m.OtherCreatureId)) + judged;
        }).ToList();
        if (memories.Count > 0)
        {
            text.Append("What you did last: ").AppendJoin("; ", memories).Append(".\n");
        }
    }

    /// <summary>The distance between two places, in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
