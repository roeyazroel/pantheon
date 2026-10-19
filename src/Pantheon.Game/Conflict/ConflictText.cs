using System;
using System.Globalization;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Conflict;

namespace Pantheon.Game.Conflict;

/// <summary>Words for wars and their goals, intelligence, proxy acts and holy wars (spec §43–46, ADR-026). Pure and culture-invariant.</summary>
public static class ConflictText
{
    /// <summary>A war goal, as what the war is for.</summary>
    /// <param name="goal">The goal.</param>
    /// <returns>For example "conquest".</returns>
    public static string Goal(WarGoal goal) => goal switch
    {
        WarGoal.Conquest => "conquest",
        WarGoal.HolySite => "a holy site",
        WarGoal.Tribute => "tribute",
        WarGoal.Humiliation => "to humble its Creature",
    };

    /// <summary>A war as one side sees it.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="war">The war.</param>
    /// <param name="viewerGodId">The side looking.</param>
    /// <returns>For example "Your war on Kaio for conquest: 6 to 3 (10 to dictate peace)".</returns>
    public static string War(ConflictRules rules, WorldState world, WarRecord war, int viewerGodId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(war);
        var enemy = war.AggressorGodId == viewerGodId ? war.DefenderGodId : war.AggressorGodId;
        var mine = war.ScoreOf(viewerGodId);
        var theirs = war.ScoreOf(enemy);
        var head = war.AggressorGodId == viewerGodId
            ? Format("Your war on {0} for {1}", GodName(world, enemy), Goal(war.Goal) + Site(world, war))
            : Format("{0}'s war on you for {1}", GodName(world, enemy), Goal(war.Goal) + Site(world, war));
        var won = WarBook.Achieved(rules, world, war, viewerGodId) ? "; you may demand its surrender" : Format(" ({0:0} to dictate peace)", rules.GoalScore);
        return Format("{0}: {1:0} to {2:0}{3}", head, mine, theirs, won);
    }

    /// <summary>The terms a victor demands, as the loser would read them.</summary>
    /// <param name="world">World state.</param>
    /// <param name="victorId">The victor.</param>
    /// <param name="loserId">The loser.</param>
    /// <returns>For example "cede believers in Elar".</returns>
    public static string SurrenderTerms(WorldState world, int victorId, int loserId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var war = WarBook.Find(world, victorId, loserId);
        var goal = war is not null && war.AggressorGodId == victorId ? war.Goal : WarGoal.Tribute;
        return goal switch
        {
            WarGoal.Conquest => "cede believers in one of your villages",
            WarGoal.HolySite => "give up " + (world.HolySites.Find(s => s.Id == war!.SiteId)?.Name ?? "a holy site"),
            WarGoal.Tribute => "pay tribute for ten minutes",
            WarGoal.Humiliation => "keep out of its villages for ten minutes",
        };
    }

    /// <summary>What a god knows of another, in words.</summary>
    /// <param name="intel">Intelligence (0..1).</param>
    /// <returns>For example "well informed".</returns>
    public static string Intel(float intel) => intel >= 0.6f ? "well informed" : intel >= 0.3f ? "partly informed" : "in the dark";

    /// <summary>A rival's following as far as the player knows it: exact, rounded, or unknown.</summary>
    /// <param name="intel">What the player knows of the rival (0..1).</param>
    /// <param name="followers">Its followers.</param>
    /// <param name="share">Its share of all belief.</param>
    /// <returns>For example "about 350 followers (~30%)".</returns>
    public static string Following(float intel, int followers, float share)
    {
        if (intel >= 0.6f)
        {
            return Format("{0} followers ({1:0%})", followers, share);
        }

        return intel >= 0.3f
            ? Format("about {0} followers (~{1:0%})", (int)(MathF.Round(followers / 50f) * 50f), MathF.Round(share * 20f) / 20f)
            : "following unknown";
    }

    /// <summary>A proxy act's name.</summary>
    /// <param name="kind">The act.</param>
    /// <returns>For example "incite rebels".</returns>
    public static string Proxy(ProxyKind kind) => kind switch
    {
        ProxyKind.Incite => "incite rebels",
        ProxyKind.Propaganda => "spread propaganda",
    };

    /// <summary>A holy war stance, as a button.</summary>
    /// <param name="stance">The stance.</param>
    /// <returns>For example "Encourage".</returns>
    public static string Stance(HolyWarStance stance) => stance switch
    {
        HolyWarStance.Ignore => "Ignore",
        HolyWarStance.Encourage => "Encourage",
        HolyWarStance.Discourage => "Discourage",
    };

    /// <summary>Why the conflict authority refused.</summary>
    /// <param name="rejection">The verdict.</param>
    /// <returns>One line.</returns>
    public static string Describe(ConflictRejection rejection) => rejection switch
    {
        ConflictRejection.None => "Done",
        ConflictRejection.UnknownGod => "There is no such god",
        ConflictRejection.NotAuthorized => "You cannot act for that god",
        ConflictRejection.UnknownSettlement => "No one lives there",
        ConflictRejection.NoFollowers => "That god has no following there",
        ConflictRejection.OutsideInfluence => "Your influence there is too weak",
        ConflictRejection.NotEnoughPower => "Not enough Divine Power",
        ConflictRejection.TooSoon => "Your agents need time before they act again",
        ConflictRejection.NotItsWar => "Your people wage no holy war there",
    };

    /// <summary>" (the Sacred Peak)" for a holy site war, or nothing.</summary>
    private static string Site(WorldState world, WarRecord war) =>
        war.Goal == WarGoal.HolySite && world.HolySites.Find(s => s.Id == war.SiteId) is { } site ? " (" + site.Name + ")" : string.Empty;

    /// <summary>A god's name.</summary>
    internal static string GodName(WorldState world, int godId) => GodStanding.Find(world, godId)?.Name ?? "a forgotten god";

    /// <summary>Invariant-culture string formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
