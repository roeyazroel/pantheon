using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>
/// A god that casts against a decree of the Divine Council defies it (spec §42, ADR-025): every
/// other god remembers it and trusts it less, and the world sees deceit. Decrees bind by reputation,
/// not by force.
/// </summary>
public static class Defiance
{
    /// <summary>Notes a cast that defies a decree.</summary>
    /// <param name="ledger">Where acts are recorded; <c>null</c> for none.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The caster.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="target">Where it was cast.</param>
    /// <param name="reach">Its reach in metres.</param>
    /// <param name="affected">The settlements it reached.</param>
    /// <param name="events">Receives <see cref="CouncilDefied"/>.</param>
    public static void Note(IActRecorder? ledger, WorldState world, int godId, MiracleDefinition miracle, GroundPosition target, float reach, IReadOnlyList<int> affected, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        if (ledger is null || CouncilModel.Defied(world, miracle, target, reach, affected) is not { } motion)
        {
            return;
        }

        foreach (var god in world.Gods)
        {
            if (god.Id != godId)
            {
                ledger.Record(world, god.Id, godId, DiplomaticAct.DefiedCouncil, null);
            }
        }

        Culture.ConductModel.Note(world, godId, Culture.ConductModel.Act.Deceit, 1f);
        events.Add(new CouncilDefied { Tick = world.Tick, GodId = godId, Motion = motion });
    }
}
