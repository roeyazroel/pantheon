using System;
using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Hand;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Migration;

namespace Pantheon.Simulation.Hand;

/// <summary>
/// Something a god's hand threw strikes the land (spec §55, ADR-030): a rock wrecks houses and kills
/// within its reach and leaves a crater; a tree or a body does less. Thrown villagers die. The
/// village struck, and those who see it, fear the god; striking its own followers is cruel.
/// </summary>
internal sealed class HandThrow(HandRules rules, SettlementRules settlements)
{
    /// <summary>The thrown thing strikes the point.</summary>
    public void Strike(WorldState world, int godId, HeldObject held, GroundPosition target, ICollection<WorldEvent> events)
    {
        var force = held.Kind == HeldKind.Rock ? 1f : rules.TreeThrowFactor;
        var (houses, deaths) = (0, held.Kind == HeldKind.Villagers ? held.Amount : 0);
        SettlementState? struck = null;
        foreach (var settlement in world.Settlements)
        {
            var reach = HazardModel.Falloff(settlement.Position, target, rules.ThrowRadius + Reach(settlement)) * force;
            if (reach <= 0f || settlement.Population <= 0)
            {
                continue;
            }

            struck ??= settlement;
            houses += Ruin(settlement, rules.ThrowHousingLoss * reach);
            var killed = Math.Min(settlement.Population, (int)MathF.Round(settlement.Population * rules.ThrowDeaths * reach));
            settlement.Population -= killed;
            deaths += killed;
            HandModel.Impress(settlement.Beliefs, godId, rules.Smiting, reach);
            var follows = MigrationModel.MainGod(settlement.Beliefs) == godId;
            ConductModel.Note(world, godId, follows ? ConductModel.Act.Cruelty : ConductModel.Act.Harm, reach);
        }

        if (world.Regions.TryGetRegionIdAt(target.X, target.Z, out var regionId) && world.Regions.Cells[regionId] is { Biome: not Biome.Sea } region)
        {
            region.Damage = SimMath.Clamp01(region.Damage + (rules.ThrowLandHarm * force));
            HazardModel.AddMark(world, TerrainMarkKind.Crater, target, rules.ThrowRadius * 0.6f * force);
        }

        HandModel.Witness(world, godId, target, rules.WitnessRadius, rules.Smiting with { Fear = rules.Smiting.Fear * 0.5f, Love = rules.Smiting.Love * 0.5f });
        events.Add(new HandStruck { Tick = world.Tick, GodId = godId, Kind = held.Kind, Position = target, SettlementId = struck?.Id, HousesLost = houses, Deaths = deaths });
    }

    /// <summary>Houses a share of a settlement's homes falling to ruin; returns how many fell.</summary>
    private int Ruin(SettlementState settlement, float share)
    {
        var standing = settlement.Housing / settlements.HousingPerHouse;
        var fallen = Math.Min(standing, (int)MathF.Round(standing * SimMath.Clamp01(share)));
        settlement.Housing -= fallen * settlements.HousingPerHouse;
        settlement.Ruins += fallen;
        return fallen;
    }

    /// <summary>How far a settlement's houses spread around its centre, in metres.</summary>
    private static float Reach(SettlementState settlement) => 20f + (2f * MathF.Sqrt(Math.Max(0, settlement.Housing)));
}
