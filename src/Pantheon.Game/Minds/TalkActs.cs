using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Minds;

/// <summary>What a god may do in a conversation (ADR-066).</summary>
public enum TalkActKind
{
    /// <summary>Agree to the alliance the player asks for.</summary>
    Alliance,

    /// <summary>Agree to the peace the player asks for.</summary>
    Peace,

    /// <summary>Agree to a treaty the player asks for.</summary>
    Treaty,

    /// <summary>Accept what the player proposed before.</summary>
    Accept,

    /// <summary>Decline what the player proposed before.</summary>
    Decline,

    /// <summary>Offer the player an alliance, for the player to answer.</summary>
    ProposeAlliance,

    /// <summary>Ask the player for peace, for the player to answer.</summary>
    ProposePeace,

    /// <summary>Declare war on the player.</summary>
    DeclareWar,

    /// <summary>Break the alliance with the player.</summary>
    BreakAlliance,
}

/// <summary>How a god stands towards an act (ADR-066).</summary>
public enum TalkWillingness
{
    /// <summary>It would do it now.</summary>
    Willing,

    /// <summary>It would do it if the conversation moved it a little further.</summary>
    Reluctant,

    /// <summary>It will not, whatever is said.</summary>
    Unwilling,
}

/// <summary>One thing a god may do in a conversation.</summary>
/// <param name="Id">Its id for the model, for example <c>alliance</c> or <c>treaty2</c>.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Words">What it is, in words for the model: "agree to become their ally".</param>
/// <param name="Utility">The god's own judgement of it: it acts when this, leaning with the conversation's goodwill, is above 0.</param>
/// <param name="Deal">For a treaty, the player's proposal the god agrees to.</param>
public sealed record TalkAct(string Id, TalkActKind Kind, string Words, float Utility, PoliticsAction? Deal = null)
{
    /// <summary>Whether the act is against the player, so goodwill holds it back rather than drawing it on.</summary>
    public bool Hostile => Kind is TalkActKind.DeclareWar or TalkActKind.BreakAlliance;

    /// <summary>How the god stands towards the act with the conversation's goodwill.</summary>
    /// <param name="goodwill">The goodwill, from -1 to 1.</param>
    /// <param name="reach">How far full goodwill moves the god's judgement.</param>
    /// <returns>Willing, reluctant or unwilling.</returns>
    public TalkWillingness Willingness(float goodwill, float reach)
    {
        var leaning = Utility + ((Hostile ? -goodwill : goodwill) * reach);
        return leaning > 0f ? TalkWillingness.Willing : leaning > -reach ? TalkWillingness.Reluctant : TalkWillingness.Unwilling;
    }
}

/// <summary>
/// The acts a god may take in a conversation with the player (ADR-066), each judged by the god's own
/// utility AI: the alliance, peace and treaties the player may ask for, the player's proposal to
/// answer, and the god's own offers and hostile acts. Every act runs through the diplomacy authority,
/// as the player's or the god's command.
/// </summary>
public static class TalkActs
{
    // The god diplomat acts unprompted on a move above this utility (GodDiplomat.Threshold).
    private const float ActThreshold = 0.25f;

    // Accepting is easier than proposing: the god diplomat adds these to an alliance's and a peace's utility.
    private const float AllianceAccept = 0.2f;
    private const float PeaceAccept = 0.3f;

    /// <summary>Every act the authority would allow between the god and the player now, with the god's judgement of it.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="me">The player's god.</param>
    /// <returns>The acts, the player's proposal's answers first.</returns>
    public static IReadOnlyList<TalkAct> For(GameSession session, GodState god, int me)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        var world = session.World;
        var candidates = session.PreviewGodDiplomacy(god).Where(c => c.OtherGodId == me).ToList();
        float? Utility(DiplomaticCommandKind kind) => candidates.Where(c => c.Kind == kind).Select(c => (float?)c.Utility).FirstOrDefault();
        bool Allowed(int issuer, int other, DiplomaticCommandKind kind) => session.Inspect(a => a.Diplomacy.Check(world, new DiplomaticCommand(issuer, other, kind), issuer)) == DiplomacyRejection.None;

        var acts = new List<TalkAct>();
        if (DiplomacyAuthority.FindProposal(world, me, god.Id) is { } proposal && Allowed(god.Id, me, DiplomaticCommandKind.Accept))
        {
            var accept = Utility(DiplomaticCommandKind.Accept) ?? -(Utility(DiplomaticCommandKind.Decline) ?? 1f);
            acts.Add(new TalkAct("accept", TalkActKind.Accept, $"accept what they proposed ({Proposed(proposal, session)})", accept));
            acts.Add(new TalkAct("decline", TalkActKind.Decline, "decline what they proposed", 1f));
        }

        if (Utility(DiplomaticCommandKind.OfferAlliance) is { } alliance)
        {
            if (Allowed(me, god.Id, DiplomaticCommandKind.OfferAlliance))
            {
                acts.Add(new TalkAct("alliance", TalkActKind.Alliance, "agree to become their ally, if they ask", alliance + AllianceAccept));
            }

            if (Allowed(god.Id, me, DiplomaticCommandKind.OfferAlliance))
            {
                acts.Add(new TalkAct("propose_alliance", TalkActKind.ProposeAlliance, "offer them an alliance yourself", alliance));
            }
        }

        if (Utility(DiplomaticCommandKind.RequestPeace) is { } peace)
        {
            if (Allowed(me, god.Id, DiplomaticCommandKind.RequestPeace))
            {
                acts.Add(new TalkAct("peace", TalkActKind.Peace, "agree to make peace, if they ask", peace + PeaceAccept));
            }

            if (Allowed(god.Id, me, DiplomaticCommandKind.RequestPeace))
            {
                acts.Add(new TalkAct("propose_peace", TalkActKind.ProposePeace, "ask them for peace yourself", peace));
            }
        }

        acts.AddRange(Treaties(session, god, me));
        if (Utility(DiplomaticCommandKind.DeclareWar) is { } war && Allowed(god.Id, me, DiplomaticCommandKind.DeclareWar))
        {
            acts.Add(new TalkAct("declare_war", TalkActKind.DeclareWar, "declare war on them", war - ActThreshold));
        }

        if (Utility(DiplomaticCommandKind.BreakAlliance) is { } betray && Allowed(god.Id, me, DiplomaticCommandKind.BreakAlliance))
        {
            acts.Add(new TalkAct("break_alliance", TalkActKind.BreakAlliance, "break your alliance with them", betray - ActThreshold));
        }

        return acts;
    }

    /// <summary>Does an act: the player's proposal and the god's acceptance, or the god's own command.</summary>
    /// <param name="session">The session.</param>
    /// <param name="act">The act.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="me">The player's god.</param>
    /// <returns>Whether it was done, and what happened in words.</returns>
    public static (bool Done, string Text) Do(GameSession session, TalkAct act, GodState god, int me)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(god);
        var name = god.Name;
        CommandOutcome Mine(DiplomaticCommand command) => PoliticsCommands.DiplomacyAs(session, me, command);
        CommandOutcome Its(DiplomaticCommandKind kind, Domain.Conflict.WarAim? aim = null) => PoliticsCommands.DiplomacyAs(session, god.Id, new DiplomaticCommand(god.Id, me, kind, Aim: aim));
        (bool, string) Agreed(CommandOutcome asked, string done) =>
            !asked.Accepted ? (false, asked.Text) : Its(DiplomaticCommandKind.Accept) is { Accepted: true } ? (true, done) : (false, $"{name} did not, in the end, agree.");
        return act.Kind switch
        {
            TalkActKind.Alliance => Agreed(Mine(new DiplomaticCommand(me, god.Id, DiplomaticCommandKind.OfferAlliance)), $"You and {name} are allies."),
            TalkActKind.Peace => Agreed(Mine(new DiplomaticCommand(me, god.Id, DiplomaticCommandKind.RequestPeace)), $"You and {name} are at peace."),
            TalkActKind.Treaty => act.Deal?.Diplomacy is { Terms: { } terms } deal ? Agreed(Mine(deal), $"{name} agrees to {Treaty(terms, session.World, me)}.") : (false, string.Empty),
            TalkActKind.Accept => Done(Its(DiplomaticCommandKind.Accept), $"{name} accepts your proposal."),
            TalkActKind.Decline => Done(Its(DiplomaticCommandKind.Decline), $"{name} declines your proposal."),
            TalkActKind.ProposeAlliance => Done(Its(DiplomaticCommandKind.OfferAlliance), $"{name} offers you an alliance."),
            TalkActKind.ProposePeace => Done(Its(DiplomaticCommandKind.RequestPeace), $"{name} asks you for peace."),
            TalkActKind.DeclareWar => Done(Its(DiplomaticCommandKind.DeclareWar, War(session, god, me)), $"{name} declares war on you!"),
            TalkActKind.BreakAlliance => Done(Its(DiplomaticCommandKind.BreakAlliance), $"{name} breaks your alliance!"),
        };
    }

    /// <summary>A command's outcome as what happened.</summary>
    private static (bool, string) Done(CommandOutcome outcome, string done) => outcome.Accepted ? (true, done) : (false, outcome.Text);

    /// <summary>The treaties the player may ask for, each judged as the god would judge the player's proposal of it.</summary>
    private static IEnumerable<TalkAct> Treaties(GameSession session, GodState god, int me)
    {
        var world = session.World;
        if (session.Engine.Gods?.Ai.ProfileOf(god) is not { } profile)
        {
            yield break;
        }

        var rules = session.Content.Diplomacy;
        var view = DiplomacyModel.FindRelation(world, god.Id, me) ?? DiplomacyModel.NewRelation(rules, god.Id, me);
        var offers = PoliticsReadout.ForGod(session, god.Id, TreatyChoice.Default).Offers;
        for (var i = 0; i < offers.Count; i++)
        {
            if (offers[i].Diplomacy is not { Terms: { } terms })
            {
                continue;
            }

            var proposal = new DiplomaticProposal { FromGodId = me, ToGodId = god.Id, Kind = ProposalKind.Treaty, ExpiresAtTick = world.Tick, Terms = terms };
            yield return new TalkAct(
                string.Create(CultureInfo.InvariantCulture, $"treaty{i + 1}"),
                TalkActKind.Treaty,
                $"agree to {Treaty(terms, world, me)}, if they ask",
                TreatyAdvisor.AnswerUtility(rules, world, god, proposal, profile, view),
                offers[i]);
        }
    }

    /// <summary>A treaty between the player and the god in words, from the god's side.</summary>
    /// <param name="terms">Its terms.</param>
    /// <param name="world">The world, for names.</param>
    /// <param name="me">The player's god.</param>
    /// <returns>For example "a non-aggression pact for 5 minutes" or "tribute of 20 power a minute, paid by them to you, for 5 minutes".</returns>
    public static string Treaty(Domain.Politics.TreatyTerms terms, Domain.World.WorldState world, int me)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(world);
        var target = terms.TargetGodId is { } id ? world.Gods.Find(g => g.Id == id)?.Name : null;
        var lasting = terms.DurationSeconds > 0f ? string.Create(CultureInfo.InvariantCulture, $" for {terms.DurationSeconds / 60f:0} minutes") : " for good";
        var what = terms.Kind switch
        {
            Domain.Politics.TreatyKind.Tribute => string.Create(CultureInfo.InvariantCulture, $"tribute of {terms.Amount:0} power a minute, paid by {(terms.PayerGodId == me ? "them to you" : "you to them")},"),
            Domain.Politics.TreatyKind.JointWar => $"a joint war on {target ?? "a third god"}",
            Domain.Politics.TreatyKind.DefensivePact => target is null ? "a defensive pact" : $"a defensive pact against {target}",
            Domain.Politics.TreatyKind.NonAggression or Domain.Politics.TreatyKind.SharedInfluence or Domain.Politics.TreatyKind.ReligiousAccess
                or Domain.Politics.TreatyKind.CreatureNonAggression or Domain.Politics.TreatyKind.DisasterAssistance or Domain.Politics.TreatyKind.MutualDefense
                or Domain.Politics.TreatyKind.TerritorialAgreement or Domain.Politics.TreatyKind.MigrationAgreement => "a " + PoliticsText.Name(terms.Kind),
        };
        return what + lasting;
    }

    /// <summary>The player's proposal to the god, in a few words.</summary>
    private static string Proposed(DiplomaticProposal proposal, GameSession session) => proposal.Kind switch
    {
        ProposalKind.Alliance => "an alliance",
        ProposalKind.Peace => "peace",
        ProposalKind.Treaty => proposal.Terms is { } terms ? PoliticsText.Name(terms.Kind) : "a treaty",
        ProposalKind.Request => proposal.Ask is { } ask ? PoliticsText.Ask(ask, session.World) : "a favour",
        ProposalKind.Surrender => "your surrender terms",
    };

    /// <summary>What the god would fight for, as its own diplomat chooses.</summary>
    private static Domain.Conflict.WarAim? War(GameSession session, GodState god, int me) =>
        session.Engine.Gods?.Ai.ProfileOf(god) is { } profile ? Simulation.Conflict.WarAims.For(session.World, god, me, profile) : null;
}
