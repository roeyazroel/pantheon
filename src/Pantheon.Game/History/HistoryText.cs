using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;

namespace Pantheon.Game.History;

/// <summary>The world's history in words (spec §50: "Year 44 — Pact of Three", ADR-020). Pure and culture-invariant.</summary>
public static class HistoryText
{
    /// <summary>The year of the world's history a tick falls in, from year 0.</summary>
    /// <param name="tick">The tick.</param>
    /// <param name="rules">History rules.</param>
    /// <returns>The year.</returns>
    public static int Year(long tick, HistoryRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return (int)(tick / Math.Max(1L, (long)MathF.Round(rules.SecondsPerYear * SimulationTime.TicksPerSecond)));
    }

    /// <summary>A record as a title, the way chronicles name events.</summary>
    /// <param name="record">The record.</param>
    /// <param name="world">The world, for names.</param>
    /// <returns>For example "The Pact of Vael and Seren".</returns>
    public static string Title(HistoryRecord record, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(world);
        var god = GodName(world, record.GodId);
        var other = GodName(world, record.OtherGodId);
        var both = record.OtherGodId is null ? god : god + " and " + other;
        var place = world.Settlements.Find(s => s.Id == record.SettlementId)?.Name ?? "a lost village";
        var creature = CreatureName(world, record.CreatureId);
        return record.Kind switch
        {
            HistoryKind.Alliance => Format("The Pact of {0} and {1}", god, other),
            HistoryKind.War => Format("{0} goes to war with {1}", god, other),
            HistoryKind.Peace => Format("The Peace of {0} and {1}", god, other),
            HistoryKind.Betrayal => Format("The Betrayal of {0}", other),
            HistoryKind.Dominion => Format("The Dominion of {0} begins", both),
            HistoryKind.Victory => Flow.VictoryText.Title(Enum.IsDefined((VictoryKind)record.Count) ? (VictoryKind)record.Count : VictoryKind.Ascendancy, both),
            HistoryKind.Rank => Format("{0} becomes a {1}", god, DivineText.Rank((DivineRank)record.Count)),
            HistoryKind.Conversion => Format("{0} turns to {1}", place, god),
            HistoryKind.Earthquake => Format("The Shaking of {0}", place),
            HistoryKind.Fire => Format("The Burning of {0}", place),
            HistoryKind.Famine => Format("The Famine of {0}", place),
            HistoryKind.Exodus => Format("The Exodus from {0}", place),
            HistoryKind.Fall => Format("The Fall of {0}", place),
            HistoryKind.Resettled => Format("{0} is settled again", place),
            HistoryKind.Battle => Format("The Battle of {0} and {1}", creature, CreatureName(world, record.OtherCreatureId)),
            HistoryKind.Legend => Format(record.Count >= 10 ? "{0} becomes a legend" : "{0} grows mighty", creature),
            HistoryKind.Domain => Format("{0} takes up {1}", god, Domains.DomainText.InSentence((GodDomain)record.Count)),
            HistoryKind.Claimed => Format("{0} claims {1}", god, Domains.DomainText.SiteName(world, record.Count)),
            HistoryKind.Schism => Format("The Schism of {0}", place),
            HistoryKind.Reunion => Format("The Old Faith of {0} returns to {1}", place, god),
            HistoryKind.Flood => Format("The Flood of {0}", place),
            HistoryKind.Thirst => Format("The Thirst of {0}", place),
            HistoryKind.Founding => Format("The Founding of {0}", place),
            HistoryKind.Temple => Format("{0} raises a temple to {1}", place, god),
            HistoryKind.Revolt => Format("The Revolt of {0} against {1}", place, god),
            HistoryKind.Renunciation => Format("{0} renounces the gods", place),
            HistoryKind.Doctrine => Format("{0} embraces {1}", place, Culture.CultureText.Name((Domain.Culture.Doctrine)record.Count)),
            HistoryKind.Treaty => Format("{0} and {1} sign a {2}", god, other, Politics.PoliticsText.Name((Domain.Politics.TreatyKind)record.Count)),
            HistoryKind.Revelation => Format("The secret {0} of {1} and {2} comes to light", Politics.PoliticsText.Name((Domain.Politics.TreatyKind)record.Count), god, other),
            HistoryKind.Decree => DecreeTitle(record, world, god, place),
            HistoryKind.Absorption => Format("{0} absorbs the followers of {1}", god, other),
            HistoryKind.HolyWar => Format("The Holy War against {0}, for {1}", place, god),
            HistoryKind.DictatedPeace => Format("{0} dictates peace to {1}", god, other),
            HistoryKind.Exposure => Format("The Plot of {0} against {1} is uncovered", god, other),
            HistoryKind.Hurricane => Format("The Great Storm of {0}", place),
            HistoryKind.Scar => Format("The Scarring of {0}", place),
            HistoryKind.Tsunami => Format("The Great Wave of {0}", place),
            HistoryKind.Landslide => Format("The Landslide at {0}", place),
            HistoryKind.Eruption => Format("The Eruption of {0}", record.SettlementId is { } id && world.Settlements.Find(s => s.Id == id) is { } village ? Disasters.DisasterText.VolcanoName(world, world.Disasters.Volcano?.RegionId ?? village.RegionId) : place),
            HistoryKind.Tornado => Format("The Whirlwind of {0}", place),
            HistoryKind.Meteor => Format("The Falling Star of {0}", place),
            HistoryKind.Blight => Format("The Blight of {0}", place),
            HistoryKind.Locusts => Format("The Locusts of {0}", place),
            HistoryKind.Calling => Format("{0} becomes a {1}", creature, Creatures.CreatureText.Path((Domain.Creatures.CreatureSpecialisation)record.Count)),
            HistoryKind.Rescue => Format("The Rescue of {0} by {1}", place, god),
            HistoryKind.Stoning => Format("The Stoning of {0}", place),
            HistoryKind.Hallowing => Format("{0} hallows the land of {1}", god, place),
            HistoryKind.Awakening => Format("{0} wakes {1}", god, world.Disasters.Volcano is { } volcano ? Disasters.DisasterText.VolcanoName(world, volcano.RegionId) : "the mountain"),
            HistoryKind.Stillness => Format("The Great Stillness of {0}", god),
        };
    }

    /// <summary>What a record's title leaves out, if anything worth saying.</summary>
    /// <param name="record">The record.</param>
    /// <param name="world">The world, for names.</param>
    /// <returns>For example "12 dead"; empty when the title says it all.</returns>
    public static string Detail(HistoryRecord record, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(world);
        return record.Kind switch
        {
            HistoryKind.Earthquake => record.Count > 0 ? Format("{0} dead", record.Count) : "houses fell",
            HistoryKind.Fire => Format("{0} houses lost", record.Count),
            HistoryKind.Flood => record.Count > 0 ? Format("{0} houses lost", record.Count) : "people drowned",
            HistoryKind.Hurricane or HistoryKind.Tsunami or HistoryKind.Tornado => Format("{0} houses lost", record.Count),
            HistoryKind.Landslide => record.Count > 0 ? Format("{0} dead", record.Count) : "houses buried",
            HistoryKind.Rescue => Format("{0} carried to safety", record.Count),
            HistoryKind.Stoning => record.Count > 0 ? Format("{0} dead", record.Count) : "houses fell",
            HistoryKind.Founding => Format("{0} settlers", record.Count),
            HistoryKind.Revolt => Format("{0} renounced their god", record.Count),
            HistoryKind.Absorption => Format("{0} turned", record.Count),
            HistoryKind.HolyWar => Format("{0} killed", record.Count),
            HistoryKind.Exodus => Format("{0} people left", record.Count),
            HistoryKind.Battle => Format("{0} prevailed", CreatureName(world, record.CreatureId)),
            HistoryKind.Betrayal => Format("broken by {0}", GodName(world, record.GodId)),
            HistoryKind.Legend => Format("level {0}", record.Count),
            HistoryKind.Claimed => record.OtherGodId is null ? string.Empty : Format("taken from {0}", GodName(world, record.OtherGodId)),
            HistoryKind.Schism => Format("{0} keep the Old Faith of {1}", record.Count, GodName(world, record.GodId)),
            HistoryKind.Alliance or HistoryKind.War or HistoryKind.Peace or HistoryKind.Dominion or HistoryKind.Victory or HistoryKind.Rank or HistoryKind.Conversion
                or HistoryKind.Famine or HistoryKind.Fall or HistoryKind.Resettled or HistoryKind.Domain or HistoryKind.Reunion or HistoryKind.Thirst or HistoryKind.Temple or HistoryKind.Renunciation or HistoryKind.Doctrine
                or HistoryKind.Treaty or HistoryKind.Revelation or HistoryKind.Decree or HistoryKind.DictatedPeace or HistoryKind.Exposure or HistoryKind.Scar
                or HistoryKind.Eruption or HistoryKind.Meteor or HistoryKind.Blight or HistoryKind.Locusts
                or HistoryKind.Calling or HistoryKind.Hallowing or HistoryKind.Awakening or HistoryKind.Stillness => string.Empty,
        };
    }

    /// <summary>A record as a chronicle line.</summary>
    /// <param name="record">The record.</param>
    /// <param name="world">The world, for names.</param>
    /// <param name="rules">History rules, for the year.</param>
    /// <returns>For example "Year 12 — The Shaking of Elar (12 dead)".</returns>
    public static string Line(HistoryRecord record, WorldState world, HistoryRules rules)
    {
        var detail = Detail(record, world);
        return Format("Year {0} — {1}{2}", Year(record.Tick, rules), Title(record, world), detail.Length > 0 ? " (" + detail + ")" : string.Empty);
    }

    /// <summary>Whether a record concerns a god: as an actor, a party, through its Creature or through a settlement that follows it most.</summary>
    /// <param name="record">The record.</param>
    /// <param name="world">The world.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> when it concerns the god.</returns>
    public static bool Concerns(HistoryRecord record, WorldState world, int? godId)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(world);
        if (godId is not { } me)
        {
            return false;
        }

        return record.GodId == me || record.OtherGodId == me
            || world.Creatures.Exists(c => c.GodId == me && (c.Id == record.CreatureId || c.Id == record.OtherCreatureId))
            || (world.Settlements.Find(s => s.Id == record.SettlementId) is { } settlement && settlement.Beliefs.Count > 0
                && settlement.Beliefs.MaxBy(b => b.Adherence)!.GodId == me);
    }

    /// <summary>A god's name, or "a forgotten god".</summary>
    private static string GodName(WorldState world, int? godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>A decree's title: what the council passed, from the record's packed motion.</summary>
    private static string DecreeTitle(HistoryRecord record, WorldState world, string god, string place)
    {
        var detail = record.Count % 1000;
        return (Domain.Politics.CouncilMotionKind)(record.Count / 1000) switch
        {
            Domain.Politics.CouncilMotionKind.Condemn => Format("The Council condemns {0}", god),
            Domain.Politics.CouncilMotionKind.ProtectSacredLand => Format("The Council protects {0}", world.HolySites.Find(site => site.Id == detail)?.Name ?? "a holy site"),
            Domain.Politics.CouncilMotionKind.ProhibitPractice => Format("The Council prohibits {0}", Politics.PoliticsText.Practice((Domain.Politics.Practice)detail)),
            Domain.Politics.CouncilMotionKind.CoordinateDisasterResponse => "The Council unites against disaster",
            Domain.Politics.CouncilMotionKind.PantheonWar => Format("The Pantheon makes war on {0}", god),
            Domain.Politics.CouncilMotionKind.NeutralTerritory => Format("The Council makes {0} neutral ground", place),
        };
    }

    /// <summary>A Creature's name, or "a forgotten Creature".</summary>
    private static string CreatureName(WorldState world, int? creatureId) => world.Creatures.Find(c => c.Id == creatureId)?.Name ?? "a forgotten Creature";

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}

/// <summary>One line of the history panel.</summary>
/// <param name="Year">The year.</param>
/// <param name="Text">The chronicle line.</param>
/// <param name="ConcernsYou">Whether it concerns the player's god.</param>
public sealed record HistoryEntry(int Year, string Text, bool ConcernsYou);

/// <summary>Builds the history panel (spec §57's History panel) from the world. Pure.</summary>
public static class HistoryReadout
{
    /// <summary>The history, newest first.</summary>
    /// <param name="world">The world.</param>
    /// <param name="rules">History rules.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<HistoryEntry> Build(WorldState world, HistoryRules rules, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return [.. world.History.AsEnumerable().Reverse().Select(r => new HistoryEntry(HistoryText.Year(r.Tick, rules), HistoryText.Line(r, world, rules), HistoryText.Concerns(r, world, localGodId)))];
    }
}
