using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Disasters;

/// <summary>Words for disasters (ADR-014), shared by alerts, labels, the HUD and the debug panel.</summary>
public static class DisasterText
{
    /// <summary>Blight from which fields are shown as blighted (ADR-029).</summary>
    public const float BlightShown = 0.2f;

    /// <summary>What lit a fire.</summary>
    /// <param name="cause">The cause.</param>
    /// <returns>For example "lightning".</returns>
    public static string Cause(FireCause cause) => cause switch
    {
        FireCause.Lightning => "lightning",
        FireCause.Hearth => "a spark from the hearths",
        FireCause.Earthquake => "the earthquake",
        FireCause.Miracle => "a god",
    };

    /// <summary>The settlement nearest a point, if any.</summary>
    /// <param name="world">World state.</param>
    /// <param name="point">The point.</param>
    /// <returns>The settlement.</returns>
    public static SettlementState? Nearest(WorldState world, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(world);
        SettlementState? nearest = null;
        var best = float.MaxValue;
        foreach (var settlement in world.Settlements)
        {
            var dx = settlement.Position.X - point.X;
            var dz = settlement.Position.Z - point.Z;
            if ((dx * dx) + (dz * dz) < best)
            {
                (nearest, best) = (settlement, (dx * dx) + (dz * dz));
            }
        }

        return nearest;
    }

    /// <summary>The volcano's name, after the village nearest it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="regionId">The crater's region.</param>
    /// <returns>For example "Mount Elar".</returns>
    public static string VolcanoName(WorldState world, int regionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return "Mount " + (Nearest(world, world.Regions.GetRegionCenter(regionId))?.Name ?? "Fire");
    }

    /// <summary>What damaged a settlement.</summary>
    /// <param name="kind">The disaster.</param>
    /// <returns>For example "the fire".</returns>
    public static string Kind(DisasterKind kind) => kind switch
    {
        DisasterKind.Earthquake => "the earthquake",
        DisasterKind.Fire => "the fire",
        DisasterKind.Flood => "the flood",
        DisasterKind.Hurricane => "the hurricane",
        DisasterKind.Tsunami => "the wave",
        DisasterKind.Landslide => "the landslide",
        DisasterKind.Eruption => "the eruption",
        DisasterKind.Tornado => "the tornado",
        DisasterKind.Meteor => "the falling star",
    };

    /// <summary>
    /// The short marks a settlement's label carries: in drought, burning, flooded, wells dry, ruins,
    /// blight, cut off by a landslide, shielded. Empty when none applies.
    /// </summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="world">World state.</param>
    /// <param name="rules">Settlement rules, for the drought.</param>
    /// <returns>The marks, in that order.</returns>
    public static IReadOnlyList<string> Marks(SettlementState settlement, WorldState world, SettlementRules rules)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        var marks = new List<string>();
        if (SettlementEconomy.DroughtOf(rules, world.Regions, settlement) >= rules.DroughtThreshold)
        {
            marks.Add("drought");
        }

        if (DisasterModel.FireThreat(rules, world.Regions, settlement) > 0f)
        {
            marks.Add(world.Regions.Cells[settlement.RegionId].Fire > 0f ? "burning" : "fire nearby");
        }

        if (world.Regions.Cells[settlement.RegionId].Flood > 0f)
        {
            marks.Add("flooded");
        }

        if (Simulation.Belief.Hazards.ThirstOf(rules, settlement) >= 1f)
        {
            marks.Add("wells dry");
        }

        if (settlement.Ruins > 0)
        {
            marks.Add(string.Create(CultureInfo.InvariantCulture, $"{settlement.Ruins} in ruins"));
        }

        if (SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, rules.FarmlandRadius).MeanBlight >= BlightShown)
        {
            marks.Add("blight");
        }

        if (world.Regions.Cells[settlement.RegionId].Rubble >= Simulation.Navigation.RegionPathfinder.BlockingRubble)
        {
            marks.Add("cut off");
        }

        if (DisasterModel.IsShielded(settlement, world.Tick))
        {
            marks.Add("shielded");
        }

        return marks;
    }
}
