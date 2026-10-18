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
}

/// <summary>
/// The authority for diplomacy (spec §73, §113, ADR-012): it checks a god's command against the
/// two gods' status and carries it out. Breaking an alliance, or declaring war on an ally, is a
/// betrayal: the betrayed god's trust collapses and every other god trusts the betrayer less
/// (spec §40).
/// </summary>
public sealed class DiplomacyAuthority
{
    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;

    /// <summary>Creates the authority.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="ledger">Where acts are recorded.</param>
    public DiplomacyAuthority(DiplomacyRules rules, IActRecorder ledger)
    {
        DiplomacyRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        _rules = rules;
        _ledger = ledger;
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
                : PendingBetween(world, command.GodId, command.OtherGodId) is not null || proposedLately ? DiplomacyRejection.AlreadyProposed
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.BreakAlliance => status != DiplomaticStatus.Alliance ? DiplomacyRejection.WrongStatus
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.DeclareWar => status == DiplomaticStatus.War ? DiplomacyRejection.WrongStatus
                : !settled ? DiplomacyRejection.TooSoon
                : DiplomacyRejection.None,
            DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline => FindProposal(world, command.OtherGodId, command.GodId) is null
                ? DiplomacyRejection.NoProposal
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
        switch (command.Kind)
        {
            case DiplomaticCommandKind.OfferAlliance:
                Propose(world, me, other, ProposalKind.Alliance);
                break;
            case DiplomaticCommandKind.RequestPeace:
                Propose(world, me, other, ProposalKind.Peace);
                break;
            case DiplomaticCommandKind.BreakAlliance:
                SetStatus(world, me, other, DiplomaticStatus.Neutral);
                Betray(world, me, other);
                betrayal = true;
                break;
            case DiplomaticCommandKind.DeclareWar:
                betrayal = DiplomacyModel.AreAllied(world, me, other);
                SetStatus(world, me, other, DiplomaticStatus.War);
                _ledger.Record(world, other, me, DiplomaticAct.DeclaredWar, null);
                if (betrayal)
                {
                    Betray(world, me, other);
                }

                break;
            case DiplomaticCommandKind.Accept:
                Answer(world, me, other, accepted: true);
                break;
            case DiplomaticCommandKind.Decline:
                Answer(world, me, other, accepted: false);
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

    /// <summary>A proposal between two gods in either direction, if any.</summary>
    private static DiplomaticProposal? PendingBetween(WorldState world, int a, int b) => FindProposal(world, a, b) ?? FindProposal(world, b, a);

    /// <summary>Ticks a pair must keep a status before changing it again.</summary>
    private long CooldownTicks() => Ticks(_rules.StatusCooldownSeconds);

    /// <summary>Simulated seconds in ticks.</summary>
    private static long Ticks(float seconds) => (long)MathF.Round(seconds * SimulationTime.TicksPerSecond);

    /// <summary>Adds a proposal that lapses after <c>ProposalSeconds</c>, and notes when it was made.</summary>
    private void Propose(WorldState world, int from, int to, ProposalKind kind)
    {
        world.Diplomacy.Proposals.Add(new DiplomaticProposal
        {
            FromGodId = from,
            ToGodId = to,
            Kind = kind,
            ExpiresAtTick = world.Tick + Ticks(_rules.ProposalSeconds),
        });
        DiplomacyModel.EnsureStance(world, from, to).LastProposalTick = world.Tick;
    }

    /// <summary>Accepts or declines the proposal from <paramref name="proposer"/>.</summary>
    private void Answer(WorldState world, int me, int proposer, bool accepted)
    {
        var proposal = FindProposal(world, proposer, me)!;
        world.Diplomacy.Proposals.Remove(proposal);
        if (!accepted)
        {
            var relation = DiplomacyModel.EnsureRelation(_rules, world, proposer, me);
            relation.Suspicion = SimMath.Clamp01(relation.Suspicion + (_rules.HarmRivalry / 2f));
            return;
        }

        var (status, act) = proposal.Kind switch
        {
            ProposalKind.Alliance => (DiplomaticStatus.Alliance, DiplomaticAct.Allied),
            ProposalKind.Peace => (DiplomaticStatus.Neutral, DiplomaticAct.MadePeace),
        };
        SetStatus(world, me, proposer, status);
        _ledger.Record(world, me, proposer, act, null);
        _ledger.Record(world, proposer, me, act, null);
    }

    /// <summary>The betrayed god remembers, and every other god trusts the betrayer less (spec §40).</summary>
    private void Betray(WorldState world, int betrayer, int betrayed)
    {
        _ledger.Record(world, betrayed, betrayer, DiplomaticAct.Betrayed, null);
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
