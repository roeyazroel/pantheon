using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Migration;

/// <summary>Words for migration (ADR-016), shared by alerts, labels, the panel and the debug view.</summary>
public static class MigrationText
{
    /// <summary>Why people leave.</summary>
    /// <param name="cause">The cause.</param>
    /// <returns>For example "famine".</returns>
    public static string Cause(MigrationCause cause) => cause switch
    {
        MigrationCause.Famine => "famine",
        MigrationCause.Homelessness => "no roof",
        MigrationCause.Danger => "danger",
        MigrationCause.War => "war",
        MigrationCause.Fear => "fear of their god",
        MigrationCause.Doubt => "doubt in their god",
        MigrationCause.Unhappiness => "unhappiness",
        MigrationCause.Unrest => "unrest",
    };

    /// <summary>A refugee policy, as a button says it.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>For example "Welcome".</returns>
    public static string Policy(RefugeePolicy policy) => policy switch
    {
        RefugeePolicy.Welcome => "Welcome",
        RefugeePolicy.DemandConversion => "Demand conversion",
        RefugeePolicy.TurnAway => "Turn away",
    };

    /// <summary>What a policy does, as a sentence about a god.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>For example "demands that migrants convert".</returns>
    public static string Practice(RefugeePolicy policy) => policy switch
    {
        RefugeePolicy.Welcome => "welcomes migrants",
        RefugeePolicy.DemandConversion => "demands that migrants convert",
        RefugeePolicy.TurnAway => "turns migrants away",
    };

    /// <summary>How a journey ended, as a short phrase.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>For example "were welcomed".</returns>
    public static string Outcome(MigrationOutcome outcome) => outcome switch
    {
        MigrationOutcome.Welcomed => "were welcomed",
        MigrationOutcome.Converted => "were made to convert",
        MigrationOutcome.Returned => "came home",
        MigrationOutcome.Perished => "died on the road",
    };

    /// <summary>A label mark for a settlement whose people are leaving or that migrants are heading for; empty for none.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="world">World state.</param>
    /// <returns>The marks: "people leaving", "migrants coming".</returns>
    public static IReadOnlyList<string> Marks(SettlementState settlement, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(world);
        var marks = new List<string>();
        if (settlement.Emigration >= 1f)
        {
            marks.Add("people leaving");
        }

        if (world.Migration.Groups.Exists(g => g.DestinationSettlementId == settlement.Id && g.OriginSettlementId != settlement.Id))
        {
            marks.Add("migrants coming");
        }

        return marks;
    }

    /// <summary>A group as one line: how many, from where to where, why and how far along.</summary>
    /// <param name="group">The group.</param>
    /// <param name="world">World state, for the names.</param>
    /// <returns>For example "23 from Elar to Valen (war)".</returns>
    public static string Group(MigrationGroup group, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(world);
        return string.Create(CultureInfo.InvariantCulture, $"{group.Population} from {Name(world, group.OriginSettlementId)} to {Name(world, group.DestinationSettlementId)} ({Cause(group.Cause)})");
    }

    /// <summary>A settlement's name.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <returns>Its name, or "a village".</returns>
    public static string Name(WorldState world, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Settlements.Find(s => s.Id == settlementId)?.Name ?? "a village";
    }

    /// <summary>A pressure as words, for the debug view.</summary>
    /// <param name="pressure">The pressure.</param>
    /// <returns>For example "0.42 (war)".</returns>
    public static string Pressure(MigrationPressure pressure) =>
        string.Create(CultureInfo.InvariantCulture, $"{pressure.Value:0.00} ({Cause(pressure.Cause)})");
}
