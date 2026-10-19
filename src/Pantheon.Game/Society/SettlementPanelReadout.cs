using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Society;

namespace Pantheon.Game.Society;

/// <summary>What the Settlements panel shows of one settlement (spec §57, ADR-023).</summary>
/// <param name="SettlementId">The settlement.</param>
/// <param name="Title">Its name and people.</param>
/// <param name="Lines">Its faiths, stores, society, technology, buildings and work, one line each.</param>
/// <param name="Priority">What it puts first, and who asked.</param>
/// <param name="Options">The priorities the player may choose from.</param>
/// <param name="Note">Why the player may not set its course, or how to.</param>
/// <param name="Actions">The player's word to its holy war and its proxy acts here (ADR-026).</param>
public sealed record SettlementPanelModel(int SettlementId, string Title, IReadOnlyList<string> Lines, string Priority, IReadOnlyList<PriorityOption> Options, string Note, IReadOnlyList<Politics.PoliticsAction> Actions);

/// <summary>One priority the player may set.</summary>
/// <param name="Priority">The priority.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">What it does.</param>
/// <param name="Current">Whether the settlement puts it first now.</param>
/// <param name="Enabled">Whether the player may choose it now.</param>
public sealed record PriorityOption(SettlementPriority Priority, string Name, string Description, bool Current, bool Enabled);

/// <summary>Builds the Settlements panel's model from the session (ADR-023). Pure and culture-invariant.</summary>
public static class SettlementPanelReadout
{
    /// <summary>The panel's model for a settlement.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <returns>The model, or <c>null</c> when there is no such settlement.</returns>
    public static SettlementPanelModel? Build(GameSession session, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        if (world.Settlements.Find(s => s.Id == settlementId) is not { } settlement)
        {
            return null;
        }

        var content = session.Content;
        var society = settlement.Society;
        var outlook = SettlementEconomy.Assess(content.SettlementRules, settlement, world.Regions);
        var lines = new List<string>
        {
            Faiths(session, settlement),
            SocietyText.Format(
                "Food {0:0} of {1:0}   water {2:0} of {3:0}   health {4:0%}   homes {5}",
                settlement.Food, outlook.Storage, settlement.Water, outlook.WaterStorage, settlement.Health, settlement.Housing),
            Resources.ResourceText.Stores(content.SettlementRules, settlement, outlook.Labour.Gatherers),
            Resources.ResourceText.Roads(content.SettlementRules.Roads, world, settlement),
            SocietyText.Format(
                "Safety {0}   happiness {1}   order {2}   wealth {3:0}   learning {4:0%}",
                SocietyText.Level(society.Safety), SocietyText.Level(society.Happiness), SocietyText.Level(society.Stability), society.Wealth, society.Education),
            "Knows: " + string.Join("   ", Enum.GetValues<TechnologyField>().Select(f => SocietyText.Format("{0} {1:0.0}", SocietyText.Name(f), society.Technology.Get(f)))),
            "Has built: " + (society.Buildings.Count == 0 ? "nothing yet" : string.Join(", ", society.Buildings.Select(b => Building(session, b)))),
            Work(content.Society, world, settlement),
            Culture.CultureReadout.Doctrines(content.DivineRules.Culture, settlement),
            Culture.CultureReadout.Feelings(session, settlement),
        };
        lines.AddRange(Conflict.SettlementConflict.Lines(session, settlement));

        var setter = society.PriorityGodId is { } god ? GodStanding.Find(world, god)?.Name : null;
        var priority = society.Priority == SettlementPriority.None
            ? "Puts nothing first: the people run their own lives."
            : SocietyText.Format("Puts first: {0}{1}.", SocietyText.Name(society.Priority), setter is null ? string.Empty : " (asked by " + setter + ")");
        var me = session.LocalGodId ?? 0;
        var check = new SocietyAuthority(content.Society).Check(world, new PriorityCommand(me, settlement.Id, SettlementPriority.Food), me);
        var allowed = check is PriorityRejection.None or PriorityRejection.Unchanged;
        var options = Enum.GetValues<SettlementPriority>()
            .Select(p => new PriorityOption(p, SocietyText.Name(p), SocietyText.Describe(p), p == society.Priority, allowed && p != society.Priority))
            .ToList();
        var note = allowed
            ? "Choose what its people put first. A course holds at least a minute."
            : SocietyText.Describe(check, SettlementPriority.Food, settlement.Name) + ".";
        return new SettlementPanelModel(settlement.Id, SocietyText.Format("{0}: {1} people", settlement.Name, settlement.Population), lines, priority, options, note, Conflict.SettlementConflict.Actions(session, settlement));
    }

    /// <summary>Who the people follow, largest first.</summary>
    private static string Faiths(GameSession session, SettlementState settlement)
    {
        var parts = settlement.Beliefs.Where(b => b.Adherence >= 0.01f).OrderByDescending(b => b.Adherence)
            .Select(b => SocietyText.Format("{0} {1:0%}", GodStanding.Find(session.World, b.GodId)?.Name ?? "?", b.Adherence)).ToList();
        var none = 1f - settlement.Beliefs.Sum(b => b.Adherence);
        if (none >= 0.01f)
        {
            parts.Add(SocietyText.Format("no god {0:0%}", none));
        }

        return "Follows: " + string.Join(", ", parts);
    }

    /// <summary>A building, with its god for a temple.</summary>
    private static string Building(GameSession session, Building building) =>
        building.GodId is { } god ? SocietyText.Format("temple to {0}", GodStanding.Find(session.World, god)?.Name ?? "?") : SocietyText.Name(building.Kind);

    /// <summary>The building under way, or what the priority waits for.</summary>
    private static string Work(SocietyRules rules, Domain.World.WorldState world, SettlementState settlement)
    {
        var society = settlement.Society;
        if (society.Project is { } project && SocietyModel.Find(rules, project) is { } building)
        {
            return SocietyText.Format("Building {0}: {1:0%}", SocietyText.Name(project), society.ProjectProgress / building.Work);
        }

        if (SocietyModel.NextProject(rules, world, settlement) is { } next)
        {
            return SocietyText.Format("Saving for {0}: {1:0} of {2:0} wealth", SocietyText.Name(next.Kind), society.Wealth, next.Wealth);
        }

        return society.Priority == SettlementPriority.Expansion
            ? SocietyText.Format("Gathering settlers: {0} of {1} people needed", settlement.Population, rules.Expansion.MinPopulation)
            : "Nothing under way.";
    }
}
