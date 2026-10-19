using System;
using System.Collections.Generic;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Conflict;

/// <summary>
/// Covert acts (spec §43, §45, ADR-026): a veiled miracle or a proxy act may be traced to the god
/// behind it. The chance grows with what the wronged god knows of it; a draw from the seed and the
/// tick decides, so it is the same every time.
/// </summary>
public static class Covert
{
    /// <summary>Whether a covert act against a god is traced to its author.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="actorGodId">The author.</param>
    /// <param name="victimGodId">The god whose people it worked on.</param>
    /// <param name="salt">What the act was, to keep draws apart.</param>
    /// <returns><c>true</c> when exposed.</returns>
    public static bool IsExposed(ConflictRules rules, WorldState world, int actorGodId, int victimGodId, string salt)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var intel = IntelModel.Level(rules, world, victimGodId, actorGodId);
        var draw = new SimulationRandom(SeedMixer.Derive(world.Seed, "covert." + salt, world.Tick)).NextFloat();
        return draw < rules.ExposureChance * (0.25f + (0.75f * intel));
    }

    /// <summary>The wronged god remembers the covert act, the world sees deceit, and the exposure is reported.</summary>
    /// <param name="ledger">Where acts are recorded; <c>null</c> for none.</param>
    /// <param name="world">World state.</param>
    /// <param name="actorGodId">The author.</param>
    /// <param name="victimGodId">The wronged god.</param>
    /// <param name="act">What it did.</param>
    /// <param name="settlementId">Where.</param>
    /// <param name="events">Receives <see cref="CovertActExposed"/>.</param>
    public static void Expose(IActRecorder? ledger, WorldState world, int actorGodId, int victimGodId, string act, int? settlementId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ledger?.Record(world, victimGodId, actorGodId, DiplomaticAct.CovertAct, settlementId);
        Culture.ConductModel.Note(world, actorGodId, Culture.ConductModel.Act.Deceit, 1f);
        events.Add(new CovertActExposed { Tick = world.Tick, GodId = actorGodId, VictimGodId = victimGodId, Act = act, SettlementId = settlementId });
    }
}
