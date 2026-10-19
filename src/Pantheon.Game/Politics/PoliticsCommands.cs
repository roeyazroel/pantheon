using System;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Politics;
using Pantheon.Game.Diplomacy;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Politics;

/// <summary>A button in the diplomacy, council or settlements panel: a label and the command it gives.</summary>
/// <param name="Label">What the button says.</param>
/// <param name="Diplomacy">The diplomatic command, if it is one.</param>
/// <param name="Council">The council command, if it is one.</param>
/// <param name="Proxy">The proxy act, if it is one (ADR-026).</param>
/// <param name="HolyWar">The word to a holy war, if it is one (ADR-026).</param>
public sealed record PoliticsAction(string Label, DiplomaticCommand? Diplomacy = null, CouncilCommand? Council = null, Domain.Conflict.ProxyCommand? Proxy = null, Simulation.Conflict.HolyWarCommand? HolyWar = null);

/// <summary>The player's treaties, requests and council business (spec §38–42, ADR-025). Each command returns a status line.</summary>
public static class PoliticsCommands
{
    /// <summary>Carries out a panel's action for the local player's god; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="action">The action.</param>
    /// <returns>Status line.</returns>
    public static string Run(GameSession session, PoliticsAction action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        if (action.Diplomacy is { } move)
        {
            return session.Client is { } client
                ? client.Request(id => new Networking.Protocol.DiplomacyIntent(id, move.OtherGodId, move.Kind, move.Terms, move.TreatyId, move.Ask))
                : DiplomacyAs(session, session.LocalGodId ?? 0, move).Text;
        }

        if (action.Proxy is not null || action.HolyWar is not null)
        {
            return Conflict.ConflictCommands.Run(session, action);
        }

        if (action.Council is { } council)
        {
            return session.Client is { } client
                ? client.Request(id => new Networking.Protocol.CouncilIntent(id, council.Motion, council.Choice))
                : CouncilAs(session, session.LocalGodId ?? 0, council.Motion, council.Choice).Text;
        }

        return "Nothing to do";
    }

    /// <summary>A diplomatic command from a god, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="command">The command; its god is replaced by the issuer.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome DiplomacyAs(GameSession session, int godId, DiplomaticCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        var mine = command with { GodId = godId };
        var other = PoliticsText.GodName(session.World, mine.OtherGodId);
        var rejection = session.Command((authorities, events) => authorities.Diplomacy.Execute(session.World, mine, godId, events));
        return rejection == DiplomacyRejection.None
            ? new CommandOutcome(true, Done(session, mine, other))
            : new CommandOutcome(false, DiplomacyText.DescribeRejection(rejection, mine.Kind, other));
    }

    /// <summary>A council command from a god, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="motion">The motion to table; <c>null</c> to vote.</param>
    /// <param name="choice">The vote; <c>null</c> to table.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome CouncilAs(GameSession session, int godId, CouncilMotion? motion, VoteChoice? choice)
    {
        ArgumentNullException.ThrowIfNull(session);
        var rejection = session.Command((authorities, _) => authorities.Council?.Execute(session.World, new CouncilCommand(godId, motion, choice), godId) ?? CouncilRejection.NoSession);
        if (rejection != CouncilRejection.None)
        {
            return new CommandOutcome(false, PoliticsText.Describe(rejection));
        }

        return new CommandOutcome(true, motion is not null
            ? "Tabled for the next session: " + PoliticsText.Motion(motion, session.World)
            : "You vote " + PoliticsText.Vote(choice!.Value));
    }

    /// <summary>Debug: the council meets now on the first tabled motion, or to condemn the local player's strongest rival.</summary>
    /// <param name="session">The session; only the host may summon it.</param>
    /// <returns>Status line.</returns>
    public static string Summon(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        if (world.Diplomacy.Council.Session is not null || session.Politics is not { } politics)
        {
            return "The council is already in session";
        }

        var council = world.Diplomacy.Council;
        CouncilMotion motion;
        if (council.Tabled.Count > 0)
        {
            motion = council.Tabled[0];
            council.Tabled.RemoveAt(0);
        }
        else if (Rival(session) is { } rival)
        {
            motion = new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = rival, Secret = false };
        }
        else
        {
            motion = new CouncilMotion { Kind = CouncilMotionKind.CoordinateDisasterResponse, Secret = false };
        }

        session.Command((_, events) =>
        {
            politics.Chamber.Open(world, motion, CouncilTrigger.Tabled, events);
            return true;
        });
        return "The council meets: " + PoliticsText.Motion(motion, world);
    }

    /// <summary>
    /// Debug: the local player's god proposes a treaty of a kind to its strongest rival, which
    /// accepts at once: a joint war is against the third god, a defensive pact guards the rival
    /// against anyone, and a tribute is paid by the rival.
    /// </summary>
    /// <param name="session">The session; only the host may do this.</param>
    /// <param name="kind">The treaty.</param>
    /// <returns>Status line.</returns>
    public static string SignTreaty(GameSession session, TreatyKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        if (session.LocalGodId is not { } me || Rival(session) is not { } rival)
        {
            return "There is no rival to sign with";
        }

        var third = session.World.Gods.Find(g => g.Id != me && g.Id != rival)?.Id;
        var terms = new TreatyTerms
        {
            Kind = kind,
            DurationSeconds = kind == TreatyKind.Tribute ? 600f : 0f,
            TargetGodId = kind == TreatyKind.JointWar ? third : null,
            PayerGodId = kind == TreatyKind.Tribute ? rival : null,
            Amount = kind == TreatyKind.Tribute ? 20f : 0f,
            Secret = false,
        };
        var world = session.World;
        var verdict = session.Command((authorities, events) =>
        {
            var proposed = authorities.Diplomacy.Execute(world, new DiplomaticCommand(me, rival, DiplomaticCommandKind.ProposeTreaty, Terms: terms), null, events);
            return proposed != DiplomacyRejection.None ? proposed : authorities.Diplomacy.Execute(world, new DiplomaticCommand(rival, me, DiplomaticCommandKind.Accept), null, events);
        });
        var name = PoliticsText.GodName(world, rival);
        return verdict == DiplomacyRejection.None
            ? PoliticsText.Format("You and {0} signed a {1}", name, PoliticsText.Name(kind))
            : DiplomacyText.DescribeRejection(verdict, DiplomaticCommandKind.ProposeTreaty, name);
    }

    /// <summary>The status line of a command carried out.</summary>
    private static string Done(GameSession session, DiplomaticCommand command, string other)
    {
        if (command.Kind == DiplomaticCommandKind.ProposeTreaty && command.Terms is { } terms)
        {
            return PoliticsText.Format("You propose a {0}{1} to {2}", terms.Secret ? "secret " : string.Empty, PoliticsText.Name(terms.Kind), other);
        }

        if (command.Kind == DiplomaticCommandKind.Request && command.Ask is { } ask)
        {
            return PoliticsText.Format("You ask {0} for {1}", other, PoliticsText.Ask(ask, session.World));
        }

        return command.Kind switch
        {
            DiplomaticCommandKind.BreakTreaty => PoliticsText.Format("You broke your treaty with {0}", other),
            DiplomaticCommandKind.Absorb => PoliticsText.Format("You absorbed {0}'s followers where you lead", other),
            DiplomaticCommandKind.DemandSurrender => PoliticsText.Format("You demand {0}'s surrender", other),
            DiplomaticCommandKind.OfferAlliance or DiplomaticCommandKind.BreakAlliance or DiplomaticCommandKind.DeclareWar or DiplomaticCommandKind.RequestPeace
                or DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline or DiplomaticCommandKind.ProposeTreaty or DiplomaticCommandKind.Request
                => PoliticsText.Format("{0}: {1}", DiplomacyText.Command(command.Kind), other),
        };
    }

    /// <summary>The god with the most followers other than the local player's.</summary>
    private static int? Rival(GameSession session)
    {
        int? rival = null;
        var best = -1;
        foreach (var god in session.World.Gods)
        {
            var followers = Simulation.Belief.GodStanding.CountFollowers(session.World, god.Id);
            if (god.Id != session.LocalGodId && followers > best)
            {
                (rival, best) = (god.Id, followers);
            }
        }

        return rival;
    }
}
