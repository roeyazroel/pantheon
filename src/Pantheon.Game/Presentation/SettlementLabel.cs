using System;
using System.Globalization;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Presentation;

/// <summary>Text of the floating label above a settlement.</summary>
public static class SettlementLabel
{
    /// <summary>Formats the settlement name with its population below it.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <returns>Two-line label text.</returns>
    public static string Format(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var people = settlement.Population == 1 ? "person" : "people";
        return string.Create(CultureInfo.InvariantCulture, $"{settlement.Name}\n{settlement.Population} {people}");
    }

    /// <summary>
    /// Formats the settlement name, its population, the share of it that follows its main god and,
    /// when the land threatens it, a third line of marks: drought (ADR-013), fire, ruins and a
    /// shield (ADR-014).
    /// </summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="world">World, for god names, the soil and fires.</param>
    /// <param name="rules">Settlement rules, for the drought.</param>
    /// <returns>Label text, for example "Elar\n38 people · Ilyra 42%\ndrought · fire nearby".</returns>
    public static string Format(SettlementState settlement, WorldState world, SettlementRules rules)
    {
        var text = Format(settlement, world);
        var marks = Disasters.DisasterText.Marks(settlement, world, rules);
        return marks.Count > 0 ? text + "\n" + string.Join(" · ", marks) : text;
    }

    /// <summary>Formats the settlement name, its population and the share of it that follows its main god.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="world">World, for god names.</param>
    /// <returns>Two-line label text, for example "Elar\n38 people · Ilyra 42%".</returns>
    public static string Format(SettlementState settlement, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var text = Format(settlement);
        if (MainBelief(settlement) is not { } main)
        {
            return text;
        }

        var name = GodStanding.Find(world, main.GodId)?.Name ?? "?";
        return string.Create(CultureInfo.InvariantCulture, $"{text} · {name} {main.Adherence:0%}");
    }

    /// <summary>The belief with the most followers, if anyone follows a god.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <returns>The main belief, or <c>null</c> when nobody follows a god.</returns>
    public static GodBelief? MainBelief(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        GodBelief? main = null;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence > 0.005f && (main is null || belief.Adherence > main.Adherence))
            {
                main = belief;
            }
        }

        return main;
    }
}
