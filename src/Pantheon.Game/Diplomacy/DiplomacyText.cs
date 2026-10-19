using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Diplomacy;

/// <summary>Player-facing wording for diplomacy (spec §49: explainable attitudes). Pure and culture-invariant.</summary>
public static class DiplomacyText
{
    /// <summary>A status as the player reads it.</summary>
    /// <param name="status">Status.</param>
    /// <returns>For example "at war".</returns>
    public static string Status(DiplomaticStatus status) => status switch
    {
        DiplomaticStatus.Alliance => "allied",
        DiplomaticStatus.Friendly => "friendly",
        DiplomaticStatus.Neutral => "neutral",
        DiplomaticStatus.Hostile => "hostile",
        DiplomaticStatus.War => "at war",
    };

    /// <summary>A command as a button label.</summary>
    /// <param name="kind">Command.</param>
    /// <returns>For example "Declare war".</returns>
    public static string Command(DiplomaticCommandKind kind) => kind switch
    {
        DiplomaticCommandKind.OfferAlliance => "Offer alliance",
        DiplomaticCommandKind.BreakAlliance => "Break alliance",
        DiplomaticCommandKind.DeclareWar => "Declare war",
        DiplomaticCommandKind.RequestPeace => "Request peace",
        DiplomaticCommandKind.Accept => "Accept",
        DiplomaticCommandKind.Decline => "Decline",
        DiplomaticCommandKind.ProposeTreaty => "Propose a treaty",
        DiplomaticCommandKind.BreakTreaty => "Break the treaty",
        DiplomaticCommandKind.Request => "Ask",
        DiplomaticCommandKind.Absorb => "Absorb its followers",
        DiplomaticCommandKind.DemandSurrender => "Demand surrender",
    };

    /// <summary>How one god feels about another, in a few words.</summary>
    /// <param name="relation">The relation, or <c>null</c> when they have none yet.</param>
    /// <returns>For example "distrusts you and sees you as a rival".</returns>
    public static string Attitude(GodRelation? relation)
    {
        if (relation is null)
        {
            return "has formed no view of you yet";
        }

        var parts = new List<string>();
        parts.Add(relation.Trust >= 0.65f ? "trusts you" : relation.Trust <= 0.35f ? "distrusts you" : "is unsure of you");
        if (relation.Rivalry >= 0.5f)
        {
            parts.Add("sees you as a rival");
        }

        if (relation.Fear >= 0.4f)
        {
            parts.Add("fears your power");
        }

        if (relation.Gratitude >= 0.3f)
        {
            parts.Add("is grateful to you");
        }

        if (relation.Suspicion >= 0.5f)
        {
            parts.Add("suspects your motives");
        }

        return string.Join(", ", parts.Take(parts.Count - 1)) + (parts.Count > 1 ? " and " : string.Empty) + parts[^1];
    }

    /// <summary>
    /// Why a god feels as it does (spec §49): its weightiest memories of the other, grouped by act,
    /// with the settlements they concerned.
    /// </summary>
    /// <param name="relation">The relation, or <c>null</c>.</param>
    /// <param name="world">World, for settlement names.</param>
    /// <returns>For example "remembers that you betrayed it, converted Elar and Brightwater"; empty when it remembers nothing.</returns>
    public static string Explain(GodRelation? relation, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (relation is null || relation.Memories.Count == 0)
        {
            return string.Empty;
        }

        var reasons = relation.Memories
            .GroupBy(m => m.Act)
            .Select(group => (Act: group.Key, Weight: group.Sum(m => m.Weight), Places: group.Select(m => m.SettlementId).OfType<int>().Distinct().ToList()))
            .OrderByDescending(entry => entry.Weight)
            .Take(3)
            .Select(entry => Describe(entry.Act, entry.Places.Select(id => world.Settlements.Find(s => s.Id == id)?.Name).OfType<string>().ToList()));
        return "remembers that you " + string.Join("; ", reasons);
    }

    /// <summary>The player's own grievances against another god: the hostile acts the player's god remembers of it.</summary>
    /// <param name="relation">How the player's god sees the other, or <c>null</c>.</param>
    /// <param name="world">World, for settlement names.</param>
    /// <param name="other">The other god's name.</param>
    /// <returns>For example "Vael struck your followers in Elar"; empty when there are none.</returns>
    public static string Grievances(GodRelation? relation, WorldState world, string other)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (relation is null)
        {
            return string.Empty;
        }

        var grievances = relation.Memories
            .Where(m => DiplomacyModel.IsHostile(m.Act))
            .GroupBy(m => m.Act)
            .OrderByDescending(group => group.Sum(m => m.Weight))
            .Take(3)
            .Select(group => Describe(group.Key, group.Select(m => m.SettlementId).OfType<int>().Distinct().Select(id => world.Settlements.Find(s => s.Id == id)?.Name).OfType<string>().ToList())
                .Replace("its followers", "your followers", StringComparison.Ordinal)
                .Replace("betrayed it", "betrayed you", StringComparison.Ordinal)
                .Replace("declared war on it", "declared war on you", StringComparison.Ordinal)
                .Replace("let your Creature beat its Creature", "let its Creature beat yours", StringComparison.Ordinal)
                .Replace("let your Creature", "let its Creature", StringComparison.Ordinal)
                .Replace("wronged its ally", "wronged your ally", StringComparison.Ordinal))
            .ToList();
        return grievances.Count == 0 ? string.Empty : other + " " + string.Join("; ", grievances);
    }

    /// <summary>A god's alliances and wars with gods other than the player (ADR-015).</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="localGodId">The player's god, left out.</param>
    /// <returns>For example "Allied with Seren; at war with Kaio and Vael"; empty when it has neither.</returns>
    public static string Ties(WorldState world, int godId, int localGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var allies = new List<string>();
        var enemies = new List<string>();
        foreach (var other in world.Gods)
        {
            if (other.Id == godId || other.Id == localGodId)
            {
                continue;
            }

            var status = DiplomacyModel.StatusOf(world, godId, other.Id);
            if (status == DiplomaticStatus.Alliance)
            {
                allies.Add(other.Name);
            }
            else if (status == DiplomaticStatus.War)
            {
                enemies.Add(other.Name);
            }
        }

        var ties = new List<string>();
        if (allies.Count > 0)
        {
            ties.Add("Allied with " + JoinNames(allies));
        }

        if (enemies.Count > 0)
        {
            ties.Add((ties.Count > 0 ? "at war with " : "At war with ") + JoinNames(enemies));
        }

        return string.Join("; ", ties);
    }

    /// <summary>What the panel says of a god the player has not met (ADR-036).</summary>
    public const string Unmet = "You have not met this god. Your gods meet when your villages are neighbours, a road or believers join them, your Creatures meet, or you sit in the Divine Council.";

    /// <summary>Why a diplomatic command was refused.</summary>
    /// <param name="rejection">The authority's verdict.</param>
    /// <param name="kind">The command.</param>
    /// <param name="other">The other god's name.</param>
    /// <returns>One line.</returns>
    public static string DescribeRejection(DiplomacyRejection rejection, DiplomaticCommandKind kind, string other) => rejection switch
    {
        DiplomacyRejection.None => Format("{0}: done", Command(kind)),
        DiplomacyRejection.UnknownGod => "There is no such god",
        DiplomacyRejection.NotAuthorized => "You cannot speak for that god",
        DiplomacyRejection.WrongStatus => Format("You cannot {0} {1} now", Command(kind).ToLowerInvariant(), StatusPhrase(kind, other)),
        DiplomacyRejection.AlreadyProposed => Format("A proposal between you and {0} is already waiting", other),
        DiplomacyRejection.NoProposal => Format("{0} has proposed nothing", other),
        DiplomacyRejection.TooSoon => Format("Your standing with {0} changed too recently", other),
        DiplomacyRejection.TreatyForbids => Format("A treaty with {0} forbids it: break the treaty first", other),
        DiplomacyRejection.InvalidTerms => "Those terms are not valid",
        DiplomacyRejection.NoTreaty => Format("There is no such treaty with {0}", other),
        DiplomacyRejection.CannotGrant => "You cannot grant that now",
        DiplomacyRejection.NotMet => Format("You have not met {0} yet", other),
    };

    /// <summary>One act, with the places it concerned.</summary>
    private static string Describe(DiplomaticAct act, List<string> places)
    {
        var where = places.Count == 0 ? string.Empty : " " + string.Join(" and ", places.Take(3));
        return act switch
        {
            DiplomaticAct.Helped => "helped its followers" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Courted => "courted its followers" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Harmed => "struck its followers" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Converted => "converted" + (where.Length > 0 ? where : " its followers"),
            DiplomaticAct.CreatureAttacked => "let your Creature attack its followers" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Betrayed => "betrayed it",
            DiplomaticAct.DeclaredWar => "declared war on it",
            DiplomaticAct.Allied => "allied with it",
            DiplomaticAct.MadePeace => "made peace with it",
            DiplomaticAct.WrongedAlly => "wronged its ally" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Sheltered => "took in its followers" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.TurnedAway => "turned its followers away" + (where.Length > 0 ? " from" + where : string.Empty),
            DiplomaticAct.CreatureDefeated => "let your Creature beat its Creature",
            DiplomaticAct.TookHolySite => "took its holy site",
            DiplomaticAct.Rescued => "saved its followers in great need" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.BrokeTreaty => "broke a treaty with it",
            DiplomaticAct.BrokePromise => "broke a promise to it",
            DiplomaticAct.VotedAgainst => "voted against it in the council",
            DiplomaticAct.DefiedCouncil => "defied the council",
            DiplomaticAct.KeptWord => "kept your word to it",
            DiplomaticAct.SignedTreaty => "signed a treaty with it",
            DiplomaticAct.PlottedAgainst => "plotted against it in secret",
            DiplomaticAct.HolyWar => "let your people wage holy war on its people" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.CovertAct => "worked against its people in secret" + (where.Length > 0 ? " in" + where : string.Empty),
            DiplomaticAct.Dictated => "dictated peace to it",
        };
    }

    /// <summary>Words for whom the command was aimed at.</summary>
    private static string StatusPhrase(DiplomaticCommandKind kind, string other) => kind switch
    {
        DiplomaticCommandKind.OfferAlliance or DiplomaticCommandKind.DeclareWar => "with " + other,
        _ => "towards " + other,
    };

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>Names joined as "A", "A and B" or "A, B and C".</summary>
    private static string JoinNames(List<string> names) =>
        names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[^1];
}
