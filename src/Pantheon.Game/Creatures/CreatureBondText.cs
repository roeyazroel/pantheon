using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>Player-facing wording for how Creatures regard each other (spec §27, ADR-017). Pure and culture-invariant.</summary>
public static class CreatureBondText
{
    // A view this strong is worth mentioning.
    private const float Noticeable = 0.3f;

    // Trust this low is distrust.
    private const float Distrust = 0.15f;

    // Trust this high is worth mentioning.
    private const float HighTrust = 0.6f;

    // Encounters named in a summary, newest first.
    private const int EncountersShown = 2;

    /// <summary>What a view amounts to, strongest feelings first.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="view">The view.</param>
    /// <returns>For example ["rival", "fears it"]; ["met"] when nothing stands out.</returns>
    public static IReadOnlyList<string> Labels(CreatureBondRules rules, CreatureRelation view)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(view);
        var labels = new List<string>();
        if (CreatureBonds.IsFriend(rules, view))
        {
            labels.Add("friend");
        }

        if (CreatureBonds.IsRival(rules, view))
        {
            labels.Add("rival");
        }

        if (view.Fear >= Noticeable)
        {
            labels.Add("fears it");
        }

        if (view.Respect >= Noticeable)
        {
            labels.Add("respects it");
        }

        if (view.Trust < Distrust)
        {
            labels.Add("distrusts it");
        }
        else if (view.Trust >= HighTrust)
        {
            labels.Add("trusts it");
        }

        if (labels.Count == 0)
        {
            labels.Add("met");
        }

        return labels;
    }

    /// <summary>An encounter as a few words, from the side of the Creature that remembers it.</summary>
    /// <param name="kind">The encounter.</param>
    /// <returns>For example "lost to it".</returns>
    public static string Encounter(EncounterKind kind) => kind switch
    {
        EncounterKind.Met => "met",
        EncounterKind.Greeted => "greeted",
        EncounterKind.Played => "played together",
        EncounterKind.Won => "beat it",
        EncounterKind.Lost => "lost to it",
        EncounterKind.Spared => "spared it",
        EncounterKind.WasSpared => "was spared by it",
        EncounterKind.Drew => "fought to a draw",
        EncounterKind.Fled => "fled from it",
        EncounterKind.ChasedOff => "chased it off",
        EncounterKind.Betrayed => "was betrayed by it",
        EncounterKind.AttackedOurPeople => "saw it attack our people",
    };

    /// <summary>One Creature's view of another, with its latest memories; with no strong feeling, just the memories.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="world">World state, for names.</param>
    /// <param name="view">The view.</param>
    /// <returns>For example "Gristle (Vael): rival, fears it (lost to it, fled from it)", or "Quill (Kaio): greeted, met".</returns>
    public static string Describe(CreatureBondRules rules, WorldState world, CreatureRelation view)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(view);
        var other = world.Creatures.Find(c => c.Id == view.OtherCreatureId);
        var name = other is null ? "a lost Creature" : Format("{0} ({1})", other.Name, GodStanding.Find(world, other.GodId)?.Name ?? "no god");
        var memories = view.Encounters.AsEnumerable().Reverse().Take(EncountersShown).Select(e => Encounter(e.Kind)).Distinct(StringComparer.Ordinal).ToList();
        var labels = Labels(rules, view);
        if (labels is ["met"] && memories.Count > 0)
        {
            return Format("{0}: {1}", name, string.Join(", ", memories));
        }

        var recalled = memories.Count == 0 ? string.Empty : " (" + string.Join(", ", memories) + ")";
        return Format("{0}: {1}{2}", name, string.Join(", ", labels), recalled);
    }

    /// <summary>Every view a Creature holds of the Creatures it has met, for its card.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The views joined by " · ", or a line saying it has met no one.</returns>
    public static string Summary(CreatureBondRules rules, WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var met = creature.Relations.Where(view => view.LastMetTick >= 0 || view.Encounters.Count > 0).ToList();
        return met.Count == 0
            ? "Has not met another Creature yet"
            : string.Join(" · ", met.Select(view => Describe(rules, world, view)));
    }

    /// <summary>How two Creatures regard each other, for a god's card in the diplomacy panel.</summary>
    /// <param name="rules">Bond rules.</param>
    /// <param name="mine">The player's Creature.</param>
    /// <param name="theirs">The other god's Creature.</param>
    /// <returns>For example "Bramble: rival, fears it. Gristle: rival." or that they have not met.</returns>
    public static string Between(CreatureBondRules rules, CreatureState mine, CreatureState theirs)
    {
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);
        var (a, b) = (CreatureBonds.Find(mine, theirs.Id), CreatureBonds.Find(theirs, mine.Id));
        if (a is null && b is null)
        {
            return Format("{0} and {1} have not met.", mine.Name, theirs.Name);
        }

        string Side(CreatureState who, CreatureRelation? view) => Format("{0}: {1}.", who.Name, view is null ? "met" : Feeling(rules, view));
        return Side(mine, a) + " " + Side(theirs, b);
    }

    /// <summary>What stands out in a view, or, when nothing does, what happened last.</summary>
    private static string Feeling(CreatureBondRules rules, CreatureRelation view)
    {
        var labels = Labels(rules, view);
        return labels is ["met"] && view.Encounters.Count > 0 ? Encounter(view.Encounters[^1].Kind) : string.Join(", ", labels);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
