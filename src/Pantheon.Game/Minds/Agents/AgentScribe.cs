using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// What the scribes say for the gods and Creatures when no model does (ADR-072): a line for the moment from
/// <c>minds.json</c>'s voices, chosen from the seed, the speaker and the tick, its names filled in. A god
/// speaks only to the god the moment concerns.
/// </summary>
public static class AgentScribe
{
    /// <summary>What a god says to another of a moment between them.</summary>
    /// <param name="voices">The scribes' lines.</param>
    /// <param name="world">The world, for names and the seed.</param>
    /// <param name="god">The speaker.</param>
    /// <param name="hook">The moment.</param>
    /// <returns>The words, or <c>null</c> when the scribes say nothing of it.</returns>
    public static string? GodLine(VoiceLines voices, WorldState world, GodState god, Hook hook)
    {
        ArgumentNullException.ThrowIfNull(voices);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(hook);
        if (hook.Voice.Length == 0 || hook.OtherGodId is not { } to || !voices.Gods.TryGetValue(hook.Voice, out var lines))
        {
            return null;
        }

        var creature = world.Creatures.Find(c => c.Id == hook.OtherCreatureId)?.Name ?? world.Creatures.Find(c => c.Id == god.CreatureId)?.Name ?? "Creature";
        return Fill(Pick(lines, world, god.Id, hook.Tick), god.Name, god.Name, Name(world, to), Village(world, hook.SettlementId), creature);
    }

    /// <summary>What a Creature says aloud at a moment.</summary>
    /// <param name="voices">The scribes' lines.</param>
    /// <param name="world">The world, for names and the seed.</param>
    /// <param name="creature">The speaker.</param>
    /// <param name="hook">The moment.</param>
    /// <returns>The words, or <c>null</c> when the scribes say nothing of it.</returns>
    public static string? CreatureLine(VoiceLines voices, WorldState world, CreatureState creature, Hook hook)
    {
        ArgumentNullException.ThrowIfNull(voices);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(hook);
        if (hook.Voice.Length == 0 || !voices.Creatures.TryGetValue(hook.Voice, out var lines))
        {
            return null;
        }

        var other = world.Creatures.Find(c => c.Id == hook.OtherCreatureId)?.Name ?? (hook.OtherGodId is { } god ? Name(world, god) : "it");
        return Fill(Pick(lines, world, 1000 + creature.Id, hook.Tick), creature.Name, Name(world, creature.GodId), other, Village(world, hook.SettlementId), creature.Name);
    }

    /// <summary>One of the lines, from the seed, the speaker and the tick.</summary>
    private static string Pick(IReadOnlyList<string> lines, WorldState world, int speaker, long tick) =>
        lines[(int)(SeedMixer.Derive(world.Seed, "voice", (tick * 4099) + speaker) % (ulong)lines.Count)];

    /// <summary>A line with its names filled in.</summary>
    private static string Fill(string line, string name, string god, string other, string village, string creature) => line
        .Replace("{name}", name, StringComparison.Ordinal)
        .Replace("{god}", god, StringComparison.Ordinal)
        .Replace("{other}", other, StringComparison.Ordinal)
        .Replace("{village}", village, StringComparison.Ordinal)
        .Replace("{creature}", creature, StringComparison.Ordinal);

    /// <summary>A god's name.</summary>
    private static string Name(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a god";

    /// <summary>A village's name.</summary>
    private static string Village(WorldState world, int? settlementId) => world.Settlements.Find(s => s.Id == settlementId)?.Name ?? "the village";
}
