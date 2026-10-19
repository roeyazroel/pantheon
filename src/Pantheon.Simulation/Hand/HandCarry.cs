using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Hand;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Navigation;

namespace Pantheon.Simulation.Hand;

/// <summary>
/// Villagers set down by a god's hand (spec §55, ADR-030): into a settlement they join at once, on
/// open land from which they walk to the nearest one, or into the sea, where they drown. Carried out
/// of danger to safety, they and those who see it love the god for it.
/// </summary>
internal sealed class HandCarry(HandRules rules, SettlementRules settlements)
{
    /// <summary>Sets the villagers down at a point.</summary>
    public void Land(WorldState world, int godId, HeldObject held, GroundPosition target, bool sea, ICollection<WorldEvent> events)
    {
        var from = held.SettlementId ?? 0;
        if (sea)
        {
            HandModel.Witness(world, godId, target, rules.WitnessRadius, rules.Smiting);
            ConductModel.Note(world, godId, ConductModel.Act.Harm, held.Amount / 10f);
            events.Add(new VillagersSetDown { Tick = world.Tick, GodId = godId, FromSettlementId = from, ToSettlementId = null, People = held.Amount, Rescued = false, Drowned = true });
            return;
        }

        var near = HandAuthority.Nearest(world, target, rules.Reach);
        var destination = near ?? HandAuthority.Nearest(world, target, float.MaxValue);
        if (destination is null)
        {
            return;
        }

        var rescued = held.FromDanger && Disasters.DisasterModel.DangerTo(settlements, world, destination) < HandModel.Danger;
        if (rescued)
        {
            HandModel.Impress(held.Beliefs, godId, rules.Rescue, 1f);
            HandModel.Witness(world, godId, target, rules.WitnessRadius, rules.Rescue);
            ConductModel.Note(world, godId, ConductModel.Act.Help, held.Amount / 10f);
        }

        var group = new MigrationGroup
        {
            Id = world.Migration.NextGroupId++,
            OriginSettlementId = from,
            DestinationSettlementId = destination.Id,
            Cause = MigrationCause.Carried,
            Population = held.Amount,
            Food = 0f,
            Health = held.Health,
            Position = target,
            Path = near is null ? Way(world, target, destination.Position) : [],
            Beliefs = held.Beliefs,
            TurnedAwayFrom = [],
            DepartedTick = world.Tick,
        };
        if (near is not null)
        {
            // Set down in the village itself: they join it at once.
            MigrationModel.Merge(near, group);
            world.Migration.NextGroupId--;
        }
        else
        {
            world.Migration.Groups.Add(group);
        }

        events.Add(new VillagersSetDown { Tick = world.Tick, GodId = godId, FromSettlementId = from, ToSettlementId = destination.Id, People = held.Amount, Rescued = rescued, Drowned = false });
    }

    /// <summary>The way from the open land to the settlement: around the sea when it can be found, else straight.</summary>
    private static List<GroundPosition> Way(WorldState world, GroundPosition from, GroundPosition to) =>
        RegionPathfinder.FindPath(world.Regions, from, to) is { } path ? [.. path] : [to];
}
