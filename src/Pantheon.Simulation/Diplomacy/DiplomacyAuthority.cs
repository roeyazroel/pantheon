using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>Why the authority refuses a diplomatic command, or <see cref="None"/> when it is carried out.</summary>
public enum DiplomacyRejection
{
    /// <summary>The command was carried out.</summary>
    None,

    /// <summary>A god does not exist, or a god addressed itself.</summary>
    UnknownGod,

    /// <summary>The issuer does not control the acting god.</summary>
    NotAuthorized,

    /// <summary>The command does not fit the gods' status (for example, breaking an alliance they do not have).</summary>
    WrongStatus,

    /// <summary>A proposal is already waiting between them.</summary>
    AlreadyProposed,

    /// <summary>There is no proposal to answer.</summary>
    NoProposal,

    /// <summary>Their status changed too recently.</summary>
    TooSoon,

    /// <summary>A treaty forbids it until it is broken (ADR-025).</summary>
    TreatyForbids,

    /// <summary>The terms of a treaty or request are not valid.</summary>
    InvalidTerms,

    /// <summary>There is no such treaty between them.</summary>
    NoTreaty,

    /// <summary>The god cannot grant what is asked now.</summary>
    CannotGrant,
}

/// <summary>
/// The authority for diplomacy (spec §73, §113, ADR-012, ADR-025): it checks a god's command
/// against the two gods' status and treaties and carries it out. Breaking an alliance, declaring
/// war on an ally, breaking a treaty or absorbing an ally is a betrayal: the betrayed god's trust
/// collapses and every other god trusts the betrayer less (spec §40). A declaration of war calls
/// the victim's defensive pacts.
/// </summary>
public sealed class DiplomacyAuthority
{
    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;
    private readonly Politics.PoliticsActions _politics;

    /// <summary>Creates the authority.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="ledger">Where acts are recorded.</param>
    public DiplomacyAuthority(DiplomacyRules rules, IActRecorder ledger)
    {
        DiplomacyRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        _rules = rules;
        _ledger = ledger;
        _politics = new Politics.PoliticsActions(rules, ledger, War);
    }

    /// <summary>The treaty book, for the politics step.</summary>
    public Politics.TreatyBook Treaties => _politics.Book;

    /// <summary>Declares war for a god without the checks, as a council's pantheon-wide war does; the victim's pacts are not called.</summary>
    /// <param name="world">World state.</param>
    /// <param name="aggressor">The god that goes to war.</param>
    /// <param name="victim">Its enemy.</param>
    /// <param name="events">Receives the events.</param>
    public void DeclareWarFor(WorldState world, int aggressor, int victim, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        War(world, aggressor, victim, events, invokePacts: false);
    }

    /// <summary>Whether a command would be accepted, without changing anything.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <returns>Why it would be refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection Check(WorldState world, DiplomaticCommand command, int? issuerGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (command.GodId == command.OtherGodId || GodStanding.Find(world, command.GodId) is null || GodStanding.Find(world, command.OtherGodId) is null)
        {
            return DiplomacyRejection.UnknownGod;
        }

        if (issuerGodId is { } issuer && issuer != command.GodId)
        {
            return DiplomacyRejection.NotAuthorized;
        }

        var status = DiplomacyModel.StatusOf(world, command.GodId, command.OtherGodId);
        var stance = DiplomacyModel.FindStance(world, command.GodId, command.OtherGodId);
        var settled = stance is null || world.Tick - stance.SinceTick >= CooldownTicks();
        var proposedLately = stance?.LastProposalTick is { } last && world.Tick - last < Ticks(_rules.ProposalCooldownSeconds);
        return command.Kind switch
        {
            DiplomaticCommandKind.OfferAlliance => status is DiplomaticStatus.Alliance or DiplomaticStatus.War ? DiplomacyRejection.WrongStatus
                : PendingBetween(world, command.GodId, command.OtherGodId) is not null || proposedLately ? DiplomacyRejection.AlreadyProposed
                : DiplomacyRejection.None,
            DiplomaticCommandKind.RequestPeace => status != DiplomaticStatus.War ? DiplomacyRejection.WrongStatus
                : Politics.TreatyModel.ForbidsPeace(world, command.GodId, command.OtherGodId) ? DiplomacyRejection.TreatyForbids
                : PendingBetween(world, command.GodId, command.OtherGodId) is not null || proposedLately ? DiplomacyRejection.AlreadyProposed
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.BreakAlliance => status != DiplomaticStatus.Alliance ? DiplomacyRejection.WrongStatus
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.DeclareWar => status == DiplomaticStatus.War ? DiplomacyRejection.WrongStatus
                : Politics.TreatyModel.ForbidsWar(world, command.GodId, command.OtherGodId) ? DiplomacyRejection.TreatyForbids
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.Accept => FindProposal(world, command.OtherGodId, command.GodId) is not { } proposal ? DiplomacyRejection.NoProposal
                : Politics.PoliticsChecks.Acceptance(_rules, world, proposal, command.GodId, (a, b) => Settled(world, a, b)),
            DiplomaticCommandKind.Decline => FindProposal(world, command.OtherGodId, command.GodId) is null
                ? DiplomacyRejection.NoProposal
                : DiplomacyRejection.None,
            DiplomaticCommandKind.ProposeTreaty => Politics.PoliticsChecks.Treaty(_rules.Politics, world, command.GodId, command.OtherGodId, command.Terms) is var terms && terms != DiplomacyRejection.None ? terms
                : PendingBetween(world, command.GodId, command.OtherGodId) is not null || proposedLately ? DiplomacyRejection.AlreadyProposed
                : DiplomacyRejection.None,
            DiplomaticCommandKind.BreakTreaty => FindTreaty(world, command) is null ? DiplomacyRejection.NoTreaty : DiplomacyRejection.None,
            DiplomaticCommandKind.Request => Politics.PoliticsChecks.Ask(_rules.Politics, world, command.GodId, command.OtherGodId, command.Ask) is var ask && ask != DiplomacyRejection.None ? ask
                : PendingBetween(world, command.GodId, command.OtherGodId) is not null || proposedLately ? DiplomacyRejection.AlreadyProposed
                : DiplomacyRejection.None,
            DiplomaticCommandKind.Absorb => status != DiplomaticStatus.Alliance ? DiplomacyRejection.WrongStatus
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
        };
    }

    /// <summary>Checks a command and carries it out when it is allowed.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The command.</param>
    /// <param name="issuerGodId">The god the issuer controls; <c>null</c> for the host itself.</param>
    /// <param name="events">Receives a <see cref="DiplomacyChanged"/> event when accepted.</param>
    /// <returns>Why it was refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection Execute(WorldState world, DiplomaticCommand command, int? issuerGodId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var rejection = Check(world, command, issuerGodId);
        if (rejection != DiplomacyRejection.None)
        {
            return rejection;
        }

        var (me, other) = (command.GodId, command.OtherGodId);
        var betrayal = false;
        var answered = command.Kind is DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline ? FindProposal(world, other, me)?.Kind : null;
        switch (command.Kind)
        {
            case DiplomaticCommandKind.DeclareWar:
                War(world, me, other, events, invokePacts: true);
                return DiplomacyRejection.None;
            case DiplomaticCommandKind.ProposeTreaty:
                Propose(world, me, other, ProposalKind.Treaty, command.Terms, null);
                break;
            case DiplomaticCommandKind.Request:
                Propose(world, me, other, ProposalKind.Request, null, command.Ask);
                break;
            case DiplomaticCommandKind.BreakTreaty:
                _politics.Book.End(world, FindTreaty(world, command)!, me, events);
                break;
            case DiplomaticCommandKind.Absorb:
                var people = _politics.Absorb(world, me, other);
                SetStatus(world, me, other, DiplomaticStatus.Neutral);
                Betray(world, me, other);
                betrayal = true;
                events.Add(new AllyAbsorbed { Tick = world.Tick, GodId = me, AllyGodId = other, People = people });
                break;
            case DiplomaticCommandKind.OfferAlliance:
                Propose(world, me, other, ProposalKind.Alliance, null, null);
                break;
            case DiplomaticCommandKind.RequestPeace:
                Propose(world, me, other, ProposalKind.Peace, null, null);
                break;
            case DiplomaticCommandKind.BreakAlliance:
                SetStatus(world, me, other, DiplomaticStatus.Neutral);
                Betray(world, me, other);
                betrayal = true;
                break;
            case DiplomaticCommandKind.Accept:
                Answer(world, me, other, accepted: true, events);
                break;
            case DiplomaticCommandKind.Decline:
                Answer(world, me, other, accepted: false, events);
                break;
        }

        events.Add(new DiplomacyChanged
        {
            Tick = world.Tick,
            ActorGodId = me,
            OtherGodId = other,
            Kind = command.Kind,
            Status = DiplomacyModel.StatusOf(world, me, other),
            Betrayal = betrayal,
            Answered = answered,
        });
        return DiplomacyRejection.None;
    }

    /// <summary>The proposal from one god to another, if any is waiting.</summary>
    /// <param name="world">World state.</param>
    /// <param name="fromGodId">The proposing god.</param>
    /// <param name="toGodId">The god asked.</param>
    /// <returns>The proposal, or <c>null</c>.</returns>
    public static DiplomaticProposal? FindProposal(WorldState world, int fromGodId, int toGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Diplomacy.Proposals.Find(p => p.FromGodId == fromGodId && p.ToGodId == toGodId);
    }

    /// <summary>The treaty a command names, if it binds both gods.</summary>
    private static Domain.Politics.Treaty? FindTreaty(WorldState world, DiplomaticCommand command) =>
        command.TreatyId is { } id ? world.Diplomacy.Treaties.Find(t => t.Id == id && t.Binds(command.GodId) && t.Binds(command.OtherGodId)) : null;

    /// <summary>Whether two gods' status has held long enough to change.</summary>
    private bool Settled(WorldState world, int a, int b) =>
        DiplomacyModel.FindStance(world, a, b) is not { } stance || world.Tick - stance.SinceTick >= CooldownTicks();

    /// <summary>A proposal between two gods in either direction, if any.</summary>
    private static DiplomaticProposal? PendingBetween(WorldState world, int a, int b) => FindProposal(world, a, b) ?? FindProposal(world, b, a);

    /// <summary>Ticks a pair must keep a status before changing it again.</summary>
    private long CooldownTicks() => Ticks(_rules.StatusCooldownSeconds);

    /// <summary>Simulated seconds in ticks.</summary>
    private static long Ticks(float seconds) => (long)MathF.Round(seconds * SimulationTime.TicksPerSecond);

    /// <summary>Adds a proposal that lapses after <c>ProposalSeconds</c>, and notes when it was made.</summary>
    private void Propose(WorldState world, int from, int to, ProposalKind kind, Domain.Politics.TreatyTerms? terms, Domain.Politics.RequestAsk? ask)
    {
        world.Diplomacy.Proposals.Add(new DiplomaticProposal
        {
            FromGodId = from,
            ToGodId = to,
            Kind = kind,
            ExpiresAtTick = world.Tick + Ticks(_rules.ProposalSeconds),
            Terms = terms,
            Ask = ask,
        });
        DiplomacyModel.EnsureStance(world, from, to).LastProposalTick = world.Tick;
    }

    /// <summary>Accepts or declines the proposal from <paramref name="proposer"/>.</summary>
    private void Answer(WorldState world, int me, int proposer, bool accepted, ICollection<WorldEvent> events)
    {
        var proposal = FindProposal(world, proposer, me)!;
        world.Diplomacy.Proposals.Remove(proposal);
        if (proposal.Kind == ProposalKind.Request && proposal.Ask is { } ask)
        {
            if (accepted)
            {
                _politics.Grant(world, proposer, me, ask, events);
            }
            else
            {
                _politics.Refuse(world, proposer, me, ask, events);
            }

            return;
        }

        if (proposal.Kind == ProposalKind.Treaty && proposal.Terms is { } terms && accepted)
        {
            _politics.Sign(world, proposer, me, terms, events, (a, b, status) => SetStatus(world, a, b, status));
            return;
        }

        if (!accepted)
        {
            var relation = DiplomacyModel.EnsureRelation(_rules, world, proposer, me);
            relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.HarmRivalry / 2f));
            return;
        }

        var (status, act) = proposal.Kind switch
        {
            ProposalKind.Alliance => (DiplomaticStatus.Alliance, DiplomaticAct.Allied),
            ProposalKind.Peace or ProposalKind.Treaty or ProposalKind.Request => (DiplomaticStatus.Neutral, DiplomaticAct.MadePeace),
        };
        SetStatus(world, me, proposer, status);
        _ledger.Record(world, me, proposer, act, null);
        _ledger.Record(world, proposer, me, act, null);
        var faith = act == DiplomaticAct.MadePeace ? 1f : 0.5f;
        Culture.ConductModel.Note(world, me, Culture.ConductModel.Act.Faithfulness, faith);
        Culture.ConductModel.Note(world, proposer, Culture.ConductModel.Act.Faithfulness, faith);
    }

    /// <summary>
    /// Declares war: the status, the victim's memory, the world's note of aggression, the betrayal
    /// of an ally, the treaties between them broken and, unless a treaty invoked this war, every
    /// partner bound to defend the victim (spec §38–39).
    /// </summary>
    private void War(WorldState world, int me, int other, ICollection<WorldEvent> events, bool invokePacts)
    {
        var betrayal = DiplomacyModel.AreAllied(world, me, other);
        SetStatus(world, me, other, DiplomaticStatus.War);
        _ledger.Record(world, other, me, DiplomaticAct.DeclaredWar, null);
        Culture.ConductModel.Note(world, me, Culture.ConductModel.Act.Aggression, 2f);
        if (betrayal)
        {
            Betray(world, me, other);
        }

        _politics.Book.EndByWar(world, me, other, events);
        events.Add(new DiplomacyChanged { Tick = world.Tick, ActorGodId = me, OtherGodId = other, Kind = DiplomaticCommandKind.DeclareWar, Status = DiplomaticStatus.War, Betrayal = betrayal });
        if (!invokePacts)
        {
            return;
        }

        foreach (var (treaty, defender) in Politics.TreatyModel.Defenders(world, other, me))
        {
            if (world.Diplomacy.Treaties.Contains(treaty) && !DiplomacyModel.AreAtWar(world, defender, me))
            {
                _politics.Invoke(world, treaty, defender, other, me, events);
            }
        }
    }

    /// <summary>The betrayed god remembers, and every other god trusts the betrayer less (spec §40).</summary>
    private void Betray(WorldState world, int betrayer, int betrayed)
    {
        _ledger.Record(world, betrayed, betrayer, DiplomaticAct.Betrayed, null);
        Culture.ConductModel.Note(world, betrayer, Culture.ConductModel.Act.Deceit, 2f);
        foreach (var god in world.Gods)
        {
            if (god.Id != betrayer && god.Id != betrayed)
            {
                var relation = DiplomacyModel.EnsureRelation(_rules, world, god.Id, betrayer);
                relation.Trust = SimMath.Clamp01(relation.Trust - _rules.BetrayalReputationLoss);
            }
        }
    }

    /// <summary>Sets a pair's status from now.</summary>
    private static void SetStatus(WorldState world, int a, int b, DiplomaticStatus status)
    {
        var stance = DiplomacyModel.EnsureStance(world, a, b);
        stance.Status = status;
        stance.SinceTick = world.Tick;
    }
}
