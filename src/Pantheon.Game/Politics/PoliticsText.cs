using System;
using System.Globalization;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Politics;

/// <summary>Words for treaties, requests, favours and the Divine Council (spec §38–42, ADR-025). Pure and culture-invariant.</summary>
public static class PoliticsText
{
    /// <summary>A treaty's name.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example "non-aggression pact".</returns>
    public static string Name(TreatyKind kind) => kind switch
    {
        TreatyKind.NonAggression => "non-aggression pact",
        TreatyKind.DefensivePact => "defensive pact",
        TreatyKind.JointWar => "joint war",
        TreatyKind.SharedInfluence => "pact of shared influence",
        TreatyKind.ReligiousAccess => "pact of religious access",
        TreatyKind.CreatureNonAggression => "Creature truce",
        TreatyKind.DisasterAssistance => "pact of disaster assistance",
        TreatyKind.MutualDefense => "mutual defence pact",
        TreatyKind.TerritorialAgreement => "territorial agreement",
        TreatyKind.Tribute => "tribute",
        TreatyKind.MigrationAgreement => "migration agreement",
    };

    /// <summary>What a treaty does, in a sentence's end.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example "neither may declare war on the other".</returns>
    public static string Effect(TreatyKind kind) => kind switch
    {
        TreatyKind.NonAggression => "neither may declare war on the other",
        TreatyKind.DefensivePact => "the first joins the second's wars against an aggressor",
        TreatyKind.JointWar => "both make war on a god, and neither makes a separate peace",
        TreatyKind.SharedInfluence => "each casts in the other's influence as an ally would",
        TreatyKind.ReligiousAccess => "each may preach where the other leads",
        TreatyKind.CreatureNonAggression => "their Creatures never fight",
        TreatyKind.DisasterAssistance => "each answers disasters among the other's people",
        TreatyKind.MutualDefense => "each joins the other's wars against any aggressor",
        TreatyKind.TerritorialAgreement => "neither casts in the villages the other leads",
        TreatyKind.Tribute => "one pays the other Divine Power every minute",
        TreatyKind.MigrationAgreement => "each welcomes the other's migrants",
    };

    /// <summary>A treaty in force, as one god sees it.</summary>
    /// <param name="treaty">The treaty.</param>
    /// <param name="world">World state, for names and time.</param>
    /// <param name="viewerGodId">The god looking; <c>null</c> for the host's view.</param>
    /// <returns>For example "Secret defensive pact: you defend Vael against Kaio (4:10 left)".</returns>
    public static string Describe(Treaty treaty, WorldState world, int? viewerGodId)
    {
        ArgumentNullException.ThrowIfNull(treaty);
        ArgumentNullException.ThrowIfNull(world);
        string Who(int godId) => godId == viewerGodId ? "you" : GodName(world, godId);
        var head = (treaty.Secret ? "Secret " : string.Empty) + Name(treaty.Kind);
        var detail = treaty.Kind switch
        {
            TreatyKind.DefensivePact => Format("{0} defend{1} {2}{3}", Who(treaty.FirstGodId), treaty.FirstGodId == viewerGodId ? string.Empty : "s", Who(treaty.SecondGodId),
                treaty.TargetGodId is { } aimed ? " against " + Who(aimed) : " against any aggressor"),
            TreatyKind.JointWar => Format("{0} and {1} against {2}", Who(treaty.FirstGodId), Who(treaty.SecondGodId), treaty.TargetGodId is { } enemy ? Who(enemy) : "a god"),
            TreatyKind.Tribute => treaty.PayerGodId is { } payer
                ? Format("{0} pay{1} {2} {3:0} a minute", Who(payer), payer == viewerGodId ? string.Empty : "s", Who(treaty.PartnerOf(payer)), treaty.Amount)
                : string.Empty,
            TreatyKind.NonAggression or TreatyKind.SharedInfluence or TreatyKind.ReligiousAccess or TreatyKind.CreatureNonAggression or TreatyKind.DisasterAssistance
                or TreatyKind.MutualDefense or TreatyKind.TerritorialAgreement or TreatyKind.MigrationAgreement => Format("{0} and {1}", Who(treaty.FirstGodId), Who(treaty.SecondGodId)),
        };
        return Format("{0}{1}: {2} ({3})", char.ToUpperInvariant(head[0]), head[1..], detail, Remaining(treaty, world));
    }

    /// <summary>The terms of a proposed treaty, as the god asked sees them.</summary>
    /// <param name="terms">The terms.</param>
    /// <param name="world">World state, for names.</param>
    /// <param name="viewerGodId">The god asked.</param>
    /// <returns>For example "a secret defensive pact against Kaio for 15 minutes".</returns>
    public static string Terms(TreatyTerms terms, WorldState world, int? viewerGodId)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(world);
        var what = (terms.Secret ? "a secret " : "a ") + Name(terms.Kind);
        var detail = terms.Kind switch
        {
            TreatyKind.DefensivePact => terms.TargetGodId is { } aimed ? " to defend you against " + GodName(world, aimed) : " to defend you against any aggressor",
            TreatyKind.JointWar => " against " + (terms.TargetGodId is { } enemy ? GodName(world, enemy) : "a god"),
            TreatyKind.Tribute => terms.PayerGodId == viewerGodId ? Format(": you pay {0:0} a minute", terms.Amount) : Format(": it pays you {0:0} a minute", terms.Amount),
            TreatyKind.NonAggression or TreatyKind.SharedInfluence or TreatyKind.ReligiousAccess or TreatyKind.CreatureNonAggression or TreatyKind.DisasterAssistance
                or TreatyKind.MutualDefense or TreatyKind.TerritorialAgreement or TreatyKind.MigrationAgreement => string.Empty,
        };
        return what + detail + (terms.DurationSeconds > 0f ? Format(" for {0}", Duration(terms.DurationSeconds)) : " until broken");
    }

    /// <summary>What a request asks.</summary>
    /// <param name="ask">The request.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>For example "war on Kaio".</returns>
    public static string Ask(RequestAsk ask, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(world);
        return ask.Kind switch
        {
            AskKind.DivinePower => Format("{0:0} Divine Power", ask.Amount),
            AskKind.WarSupport => "war on " + (ask.TargetGodId is { } target ? GodName(world, target) : "its enemy"),
            AskKind.Backing => "your vote in the council",
            AskKind.Territory => "that you keep out of its villages",
            AskKind.Assistance => "help for its people in need",
        };
    }

    /// <summary>A motion, as a title.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>For example "Condemn Kaio".</returns>
    public static string Motion(CouncilMotion motion, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(world);
        var target = motion.TargetGodId is { } god ? GodName(world, god) : "a god";
        return motion.Kind switch
        {
            CouncilMotionKind.Condemn => "Condemn " + target,
            CouncilMotionKind.ProtectSacredLand => "Protect " + (world.HolySites.Find(s => s.Id == motion.SiteId)?.Name ?? "a holy site"),
            CouncilMotionKind.ProhibitPractice => "Prohibit " + (motion.Practice is { } practice ? Practice(practice) : "a practice"),
            CouncilMotionKind.CoordinateDisasterResponse => "Coordinate the response to disasters",
            CouncilMotionKind.PantheonWar => "Pantheon-wide war on " + target,
            CouncilMotionKind.NeutralTerritory => "Make " + (world.Settlements.Find(s => s.Id == motion.SettlementId)?.Name ?? "a village") + " neutral territory",
        };
    }

    /// <summary>What a decree in force does.</summary>
    /// <param name="motion">The decree's motion.</param>
    /// <param name="world">World state, for names.</param>
    /// <returns>One clause.</returns>
    public static string Decree(CouncilMotion motion, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(motion);
        var target = motion.TargetGodId is { } god ? GodName(world, god) : "a god";
        return motion.Kind switch
        {
            CouncilMotionKind.Condemn => target + " stands condemned",
            CouncilMotionKind.ProtectSacredLand => "no harm may fall on " + (world.HolySites.Find(s => s.Id == motion.SiteId)?.Name ?? "a holy site"),
            CouncilMotionKind.ProhibitPractice => (motion.Practice is { } practice ? Capitalize(Practice(practice)) : "A practice") + " are prohibited",
            CouncilMotionKind.CoordinateDisasterResponse => "help where disaster struck costs half",
            CouncilMotionKind.PantheonWar => "the pantheon wars on " + target,
            CouncilMotionKind.NeutralTerritory => (world.Settlements.Find(s => s.Id == motion.SettlementId)?.Name ?? "a village") + " is neutral: no god converts or strikes there",
        };
    }

    /// <summary>A practice's name.</summary>
    /// <param name="practice">The practice.</param>
    /// <returns>For example "cataclysms".</returns>
    public static string Practice(Practice practice) => practice switch
    {
        Domain.Politics.Practice.Smiting => "smiting mortals",
        Domain.Politics.Practice.Cataclysms => "cataclysms",
        Domain.Politics.Practice.Terror => "terror",
    };

    /// <summary>Why the council was summoned.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <returns>For example "after a betrayal".</returns>
    public static string Trigger(CouncilTrigger trigger) => trigger switch
    {
        CouncilTrigger.Betrayal => "after a betrayal",
        CouncilTrigger.Dominance => "as a god rises to dominate",
        CouncilTrigger.Disaster => "after a disaster",
        CouncilTrigger.Tabled => "in regular session",
    };

    /// <summary>A vote's word.</summary>
    /// <param name="choice">The vote.</param>
    /// <returns>"for", "against" or "abstains".</returns>
    public static string Vote(VoteChoice choice) => choice switch
    {
        VoteChoice.Yes => "for",
        VoteChoice.No => "against",
        VoteChoice.Abstain => "abstains",
    };

    /// <summary>Why the council refused a command.</summary>
    /// <param name="rejection">The verdict.</param>
    /// <returns>One line.</returns>
    public static string Describe(CouncilRejection rejection) => rejection switch
    {
        CouncilRejection.None => "Done",
        CouncilRejection.UnknownGod => "You have no god in the council",
        CouncilRejection.NotAuthorized => "You cannot speak for that god",
        CouncilRejection.NoSession => "The council is not in session",
        CouncilRejection.InvalidMotion => "That motion names nothing the council can decide",
        CouncilRejection.AlreadyTabled => "You already have a motion waiting",
    };

    /// <summary>Time left in a treaty, or "until broken".</summary>
    /// <param name="treaty">The treaty.</param>
    /// <param name="world">World state.</param>
    /// <returns>For example "4:10 left".</returns>
    public static string Remaining(Treaty treaty, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(treaty);
        ArgumentNullException.ThrowIfNull(world);
        return treaty.EndsTick is { } end ? Clock(Math.Max(0, end - world.Tick) / (float)SimulationTime.TicksPerSecond) + " left" : "until broken";
    }

    /// <summary>Seconds as minutes, for example "15 minutes".</summary>
    /// <param name="seconds">Seconds.</param>
    /// <returns>The duration.</returns>
    public static string Duration(float seconds) => seconds >= 120f ? Format("{0:0} minutes", seconds / 60f) : Format("{0:0} seconds", seconds);

    /// <summary>Seconds as a clock, for example "4:10".</summary>
    /// <param name="seconds">Seconds.</param>
    /// <returns>The clock.</returns>
    public static string Clock(float seconds) => Format("{0}:{1:00}", (int)(seconds / 60f), (int)(seconds % 60f));

    /// <summary>A god's name.</summary>
    internal static string GodName(WorldState world, int godId) => GodStanding.Find(world, godId)?.Name ?? "a forgotten god";

    /// <summary>The first letter in upper case.</summary>
    internal static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Invariant-culture string formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
