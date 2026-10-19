using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Culture;

namespace Pantheon.Game.Culture;

/// <summary>Lines about a settlement's culture and its people's deeper feelings, for the Settlements panel and the debug panel (ADR-024).</summary>
public static class CultureReadout
{
    // A doctrine is named from this hold.
    private const float Named = 0.15f;

    /// <summary>The doctrines a settlement holds, strongest first, and how far it has turned from the gods.</summary>
    /// <param name="rules">Culture rules, for the secular stages.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>For example "Holds: charity (strongly), pacifism (somewhat). Towards the gods: devout (4% turned away)."</returns>
    public static string Doctrines(CultureRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var culture = settlement.Culture;
        var held = Enum.GetValues<Doctrine>().Where(d => culture.Get(d) >= Named).OrderByDescending(culture.Get)
            .Select(d => Format("{0} ({1})", CultureText.Name(d), Strength(culture.Get(d)))).ToList();
        var stage = CultureModel.Stage(rules, culture.Secularism);
        return Format("Holds: {0}. Towards the gods: {1} ({2:0%} turned away).", held.Count == 0 ? "no doctrine yet" : string.Join(", ", held), CultureText.Name(stage), culture.Secularism);
    }

    /// <summary>How the people feel about the local player's god beyond faith and love: their dependency and resentment.</summary>
    /// <param name="session">The session.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line.</returns>
    public static string Feelings(GameSession session, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settlement);
        var parts = settlement.Beliefs.Where(b => b.Adherence >= 0.05f).OrderByDescending(b => b.Adherence)
            .Select(b => Format("{0}: love {1:0%}, fear {2:0%}, need {3:0%}, resentment {4:0%}", GodStanding.Find(session.World, b.GodId)?.Name ?? "?", b.Love, b.Fear, b.Dependency, b.Resentment));
        return "Feelings: " + string.Join("   ", parts);
    }

    /// <summary>The debug panel's line for a settlement's culture.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line.</returns>
    public static string Debug(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var culture = settlement.Culture;
        var doctrines = string.Join(" ", Enum.GetValues<Doctrine>().Select(d => Format("{0} {1:0.00}", d, culture.Get(d))));
        var feelings = string.Join(" ", settlement.Beliefs.Select(b => Format("god{0} dep {1:0.00} res {2:0.00}", b.GodId, b.Dependency, b.Resentment)));
        return Format("  culture: secular {0:0.00}   {1}   {2}", culture.Secularism, doctrines, feelings);
    }

    /// <summary>A hold as a word.</summary>
    private static string Strength(float hold) => hold >= 0.7f ? "strongly" : hold >= 0.4f ? "firmly" : "somewhat";

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
