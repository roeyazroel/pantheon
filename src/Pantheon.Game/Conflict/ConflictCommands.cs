using System;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Conflict;

namespace Pantheon.Game.Conflict;

/// <summary>The player's proxy acts and words to holy wars (spec §45–46, ADR-026). Each returns a status line.</summary>
public static class ConflictCommands
{
    /// <summary>Carries out a proxy act or a word to a holy war for the local player's god; a joined player asks the host.</summary>
    /// <param name="session">The session.</param>
    /// <param name="action">The action.</param>
    /// <returns>Status line.</returns>
    public static string Run(GameSession session, PoliticsAction action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        var me = session.LocalGodId ?? 0;
        if (action.Proxy is { } proxy)
        {
            return session.Client is { } client
                ? client.Request(id => new Networking.Protocol.ProxyIntent(id, proxy.SettlementId, proxy.TargetGodId, proxy.Kind))
                : ProxyAs(session, me, proxy).Text;
        }

        if (action.HolyWar is { } word)
        {
            return session.Client is { } client
                ? client.Request(id => new Networking.Protocol.HolyWarIntent(id, word.SettlementId, word.Stance))
                : HolyWarAs(session, me, word).Text;
        }

        return "Nothing to do";
    }

    /// <summary>A proxy act by a god, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="command">The act; its god is replaced by the issuer.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome ProxyAs(GameSession session, int godId, ProxyCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        var mine = command with { GodId = godId };
        var rejection = session.Command((authorities, events) => authorities.Conflict?.Execute(session.World, mine, godId, events) ?? ConflictRejection.UnknownGod);
        var place = session.World.Settlements.Find(s => s.Id == mine.SettlementId)?.Name ?? "the village";
        return rejection == ConflictRejection.None
            ? new CommandOutcome(true, ConflictText.Format("Your agents {0} against {1} in {2}", ConflictText.Proxy(mine.Kind), ConflictText.GodName(session.World, mine.TargetGodId), place))
            : new CommandOutcome(false, ConflictText.Describe(rejection));
    }

    /// <summary>A god's word to its people's holy war, as its player's intent.</summary>
    /// <param name="session">The session.</param>
    /// <param name="godId">The issuing god.</param>
    /// <param name="command">The word; its god is replaced by the issuer.</param>
    /// <returns>Whether it was carried out, and the status line.</returns>
    public static CommandOutcome HolyWarAs(GameSession session, int godId, HolyWarCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        var mine = command with { GodId = godId };
        var rejection = session.Command((authorities, _) => authorities.Conflict?.Execute(session.World, mine, godId) ?? ConflictRejection.UnknownGod);
        return rejection == ConflictRejection.None
            ? new CommandOutcome(true, mine.Stance switch
            {
                HolyWarStance.Encourage => "You urge your people on",
                HolyWarStance.Discourage => "You ask your people to stop: they love you a little less for it",
                HolyWarStance.Ignore => "You leave your people to their war",
            })
            : new CommandOutcome(false, ConflictText.Describe(rejection));
    }

    /// <summary>Debug: the focused settlement goes to holy war on its neighbour of another faith now, if it has one.</summary>
    /// <param name="session">The session; only the host may do this.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <returns>Status line.</returns>
    public static string StartHolyWar(GameSession session, int settlementId)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        if (world.Settlements.Find(s => s.Id == settlementId) is not { } settlement || Simulation.Politics.TreatyModel.Leader(settlement) is not { } god)
        {
            return "No village to wage holy war";
        }

        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        Domain.Settlements.SettlementState? target = null;
        foreach (var other in world.Settlements)
        {
            var (c, r) = world.Regions.GetCoordinates(other.RegionId);
            var distance = Math.Max(Math.Abs(c - column), Math.Abs(r - row));
            if (other.Id != settlement.Id && other.Population > 0 && Simulation.Politics.TreatyModel.Leader(other) is { } leader && leader != god
                && (target is null || distance < Distance(world, settlement, target)))
            {
                target = other;
            }
        }

        if (target is null)
        {
            return "No neighbour of another faith";
        }

        settlement.Culture.Militarism = Math.Max(settlement.Culture.Militarism, 0.9f);
        settlement.Society.HolyWar = new HolyWarState
        {
            DefenderSettlementId = target.Id, GodId = god, EnemyGodId = Simulation.Politics.TreatyModel.Leader(target)!.Value, SinceTick = world.Tick,
            Stance = HolyWarStance.Ignore, Killed = 0, Converted = 0,
        };
        return ConflictText.Format("{0} goes to holy war on {1}", settlement.Name, target.Name);
    }

    /// <summary>Debug: the local player's god declares war on its strongest rival for a goal.</summary>
    /// <param name="session">The session; only the host may do this.</param>
    /// <param name="goal">The goal.</param>
    /// <returns>Status line.</returns>
    public static string DeclareWar(GameSession session, WarGoal goal)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var me = session.LocalGodId ?? 0;
        var rival = world.Gods.Where(g => g.Id != me).OrderByDescending(g => Simulation.Belief.GodStanding.CountFollowers(world, g.Id)).FirstOrDefault();
        if (rival is null)
        {
            return "There is no rival";
        }

        var aim = new WarAim(goal, goal == WarGoal.HolySite ? world.HolySites.Find(s => s.KeeperGodId == rival.Id)?.Id : null);
        var verdict = session.Command((authorities, events) =>
        {
            // A debug driver: the gods meet first if they had not (ADR-036).
            Simulation.Diplomacy.Acquaintance.Introduce(world, me, rival.Id, Domain.Events.MeetingWay.Diplomacy, null, events);
            return authorities.Diplomacy.Execute(world, new Domain.Diplomacy.DiplomaticCommand(me, rival.Id, Domain.Diplomacy.DiplomaticCommandKind.DeclareWar, Aim: aim), null, events);
        });
        return verdict == Simulation.Diplomacy.DiplomacyRejection.None
            ? ConflictText.Format("You declare war on {0} for {1}", rival.Name, ConflictText.Goal(Simulation.Conflict.WarBook.Find(world, me, rival.Id)?.Goal ?? goal))
            : Diplomacy.DiplomacyText.DescribeRejection(verdict, Domain.Diplomacy.DiplomaticCommandKind.DeclareWar, rival.Name);
    }

    /// <summary>Regions between two settlements.</summary>
    private static int Distance(Domain.World.WorldState world, Domain.Settlements.SettlementState a, Domain.Settlements.SettlementState b)
    {
        var (ac, ar) = world.Regions.GetCoordinates(a.RegionId);
        var (bc, br) = world.Regions.GetCoordinates(b.RegionId);
        return Math.Max(Math.Abs(ac - bc), Math.Abs(ar - br));
    }
}
