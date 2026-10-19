using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>
/// The Divine Council's sessions (spec §42, ADR-025). A betrayal, a new dominance or a disaster
/// that entered the history summons the council, no sooner than a cooldown after the last; a
/// motion a god tabled waits for the regular session. The vote stays open for a while; a motion
/// passes with more votes for than against and at least two for (one in a world of two gods).
/// Public ballots are remembered; promises of backing are kept or broken at the count.
/// </summary>
public sealed class CouncilChamber
{
    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;
    private readonly DiplomacyAuthority _authority;

    /// <summary>Creates the chamber.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="ledger">Where acts are recorded.</param>
    /// <param name="authority">The diplomacy authority, for pantheon-wide wars.</param>
    public CouncilChamber(DiplomacyRules rules, IActRecorder ledger, DiplomacyAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _ledger = ledger;
        _authority = authority;
    }

    /// <summary>One step: decrees lapse, an open vote closes when due, or a session opens.</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives the council's events.</param>
    public void Step(WorldState world, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        var council = world.Diplomacy.Council;
        council.Decrees.RemoveAll(d => d.EndsTick <= world.Tick);
        if (council.NextSessionTick < 0)
        {
            council.NextSessionTick = world.Tick + Ticks(_rules.Politics.FirstCouncilSeconds);
        }

        if (council.Session is { } session)
        {
            if (world.Tick >= session.ClosesTick)
            {
                Close(world, session, events);
            }

            return;
        }

        if (Summons(world) is { } summons)
        {
            Open(world, summons.Motion, summons.Trigger, events);
        }
    }

    /// <summary>Opens a session on a motion now; its proposer votes for it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="motion">The motion.</param>
    /// <param name="trigger">Why.</param>
    /// <param name="events">Receives <see cref="CouncilSummoned"/>.</param>
    public void Open(WorldState world, CouncilMotion motion, CouncilTrigger trigger, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(events);
        var session = new CouncilSession { Motion = motion, Trigger = trigger, OpenedTick = world.Tick, ClosesTick = world.Tick + Ticks(_rules.Politics.VoteSeconds), Votes = [] };
        if (motion.ProposerGodId is { } proposer)
        {
            session.Votes.Add(new CouncilVote { GodId = proposer, Choice = VoteChoice.Yes });
        }

        world.Diplomacy.Council.Session = session;
        events.Add(new CouncilSummoned { Tick = world.Tick, Motion = motion, Trigger = trigger });
    }

    /// <summary>What summons the council now, if anything: an event since the last session once the cooldown passed, else a tabled motion at the regular session.</summary>
    private (CouncilMotion Motion, CouncilTrigger Trigger)? Summons(WorldState world)
    {
        var council = world.Diplomacy.Council;
        var since = council.LastClosedTick;
        if (world.Tick - since >= Ticks(_rules.Politics.CouncilCooldownSeconds) && world.Tick > Ticks(_rules.Politics.FirstCouncilSeconds) / 2)
        {
            if (LatestBetrayal(world, since) is { } betrayal)
            {
                return (new CouncilMotion { Kind = CouncilMotionKind.Condemn, ProposerGodId = betrayal.Victim, TargetGodId = betrayal.Betrayer, Secret = false }, CouncilTrigger.Betrayal);
            }

            if (Dominant(world, since) is { } dominant)
            {
                return (new CouncilMotion { Kind = CouncilMotionKind.PantheonWar, TargetGodId = dominant, Secret = true }, CouncilTrigger.Dominance);
            }

            if (world.History.Exists(r => r.Tick > since && r.Kind is HistoryKind.Earthquake or HistoryKind.Flood or HistoryKind.Fire or HistoryKind.Plague or HistoryKind.GreatDrought)
                && !council.Decrees.Exists(d => d.Motion.Kind == CouncilMotionKind.CoordinateDisasterResponse))
            {
                return (new CouncilMotion { Kind = CouncilMotionKind.CoordinateDisasterResponse, Secret = false }, CouncilTrigger.Disaster);
            }
        }

        if (world.Tick < council.NextSessionTick)
        {
            return null;
        }

        council.NextSessionTick = world.Tick + Ticks(_rules.Politics.CouncilIntervalSeconds);
        while (council.Tabled.Count > 0)
        {
            var motion = council.Tabled[0];
            council.Tabled.RemoveAt(0);
            if (CouncilAuthority.IsValid(world, motion))
            {
                return (motion, CouncilTrigger.Tabled);
            }
        }

        return null;
    }

    /// <summary>The god with the most followers among those that began to dominate since a tick, alone or with an ally.</summary>
    private static int? Dominant(WorldState world, long since)
    {
        int? leader = null;
        var most = -1;
        foreach (var god in world.Gods)
        {
            var followers = GodStanding.CountFollowers(world, god.Id);
            if (god.DominantSinceTick is { } start && start > since && followers > most)
            {
                (leader, most) = (god.Id, followers);
            }
        }

        return leader;
    }

    /// <summary>The latest betrayal of an ally since a tick: who betrayed, and the god it wronged.</summary>
    private static (int Betrayer, int Victim)? LatestBetrayal(WorldState world, long since)
    {
        (int, int)? latest = null;
        var at = since;
        foreach (var relation in world.Diplomacy.Relations)
        {
            foreach (var memory in relation.Memories)
            {
                if (memory.Tick > at && memory.Act == DiplomaticAct.Betrayed)
                {
                    (latest, at) = ((relation.SubjectGodId, relation.ObserverGodId), memory.Tick);
                }
            }
        }

        return latest;
    }

    /// <summary>Counts the votes, settles promises of backing, remembers a public ballot and enacts a passed motion.</summary>
    private void Close(WorldState world, CouncilSession session, ICollection<WorldEvent> events)
    {
        var council = world.Diplomacy.Council;
        int yes = 0, no = 0;
        var voters = new List<int>();
        foreach (var vote in session.Votes)
        {
            yes += vote.Choice == VoteChoice.Yes ? 1 : 0;
            no += vote.Choice == VoteChoice.No ? 1 : 0;
            if (vote.Choice == VoteChoice.Yes)
            {
                voters.Add(vote.GodId);
            }
        }

        var passed = yes > no && yes >= Math.Min(2, world.Gods.Count - 1);
        SettleBacking(world, session, events);
        if (!session.Motion.Secret)
        {
            Remember(world, session);
        }

        if (passed)
        {
            Enact(world, session.Motion, voters, events);
        }

        council.Session = null;
        council.LastClosedTick = world.Tick;
        events.Add(new CouncilClosed { Tick = world.Tick, Motion = session.Motion, Passed = passed, Yes = yes, No = no });
    }

    /// <summary>The target of a public ballot remembers who voted against it; the proposer remembers who voted it down.</summary>
    private void Remember(WorldState world, CouncilSession session)
    {
        var motion = session.Motion;
        foreach (var vote in session.Votes)
        {
            if (motion.TargetGodId is { } target && vote.Choice == VoteChoice.Yes && vote.GodId != target)
            {
                _ledger.Record(world, target, vote.GodId, DiplomaticAct.VotedAgainst, null);
            }

            if (motion.ProposerGodId is { } proposer && vote.Choice == VoteChoice.No && vote.GodId != proposer)
            {
                _ledger.Record(world, proposer, vote.GodId, DiplomaticAct.VotedAgainst, null);
            }
        }
    }

    /// <summary>A god that promised its backing kept its word if it voted as its creditor did.</summary>
    private void SettleBacking(WorldState world, CouncilSession session, ICollection<WorldEvent> events)
    {
        foreach (var promise in world.Diplomacy.Obligations.FindAll(o => o.Kind == ObligationKind.Back && o.SinceTick <= session.ClosesTick))
        {
            var theirs = session.Votes.Find(v => v.GodId == promise.CreditorGodId)?.Choice;
            if (theirs is null or VoteChoice.Abstain)
            {
                continue;
            }

            var kept = session.Votes.Find(v => v.GodId == promise.DebtorGodId)?.Choice == theirs;
            world.Diplomacy.Obligations.Remove(promise);
            _ledger.Record(world, promise.CreditorGodId, promise.DebtorGodId, kept ? DiplomaticAct.KeptWord : DiplomaticAct.BrokePromise, null);
            events.Add(new ObligationEnded { Tick = world.Tick, DebtorGodId = promise.DebtorGodId, CreditorGodId = promise.CreditorGodId, Kind = promise.Kind, Kept = kept });
        }
    }

    /// <summary>A passed motion becomes a decree; condemnation and pantheon-wide war act at once.</summary>
    private void Enact(WorldState world, CouncilMotion motion, List<int> voters, ICollection<WorldEvent> events)
    {
        var politics = _rules.Politics;
        switch (motion.Kind)
        {
            case CouncilMotionKind.Condemn when motion.TargetGodId is { } condemned:
                foreach (var god in world.Gods)
                {
                    if (god.Id != condemned)
                    {
                        var view = DiplomacyModel.EnsureRelation(_rules, world, god.Id, condemned);
                        view.Trust = SimMath.Clamp01(view.Trust - politics.CondemnTrustLoss);
                    }
                }

                foreach (var settlement in world.Settlements)
                {
                    if (BeliefModel.Find(settlement, condemned) is { } belief)
                    {
                        belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, politics.CondemnDoubt);
                    }
                }

                break;
            case CouncilMotionKind.PantheonWar when motion.TargetGodId is { } enemy:
                foreach (var voter in voters)
                {
                    if (voter != enemy && !DiplomacyModel.AreAtWar(world, voter, enemy))
                    {
                        _authority.DeclareWarFor(world, voter, enemy, events);
                    }
                }

                break;
        }

        world.Diplomacy.Council.Decrees.Add(new CouncilDecree { Motion = motion, PassedTick = world.Tick, EndsTick = world.Tick + Ticks(politics.DecreeSeconds) });
    }

    /// <summary>Simulated seconds in ticks.</summary>
    private static long Ticks(float seconds) => (long)MathF.Round(seconds * SimulationTime.TicksPerSecond);
}
