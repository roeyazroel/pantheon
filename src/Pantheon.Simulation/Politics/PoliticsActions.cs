using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Politics;

/// <summary>Declares war for the authority: the status, the memories, the betrayal of an ally and, unless invoked by a treaty, the partners' defence.</summary>
/// <param name="world">World state.</param>
/// <param name="aggressor">The god that declares war.</param>
/// <param name="victim">The god it declares war on.</param>
/// <param name="events">Receives the events.</param>
/// <param name="invokePacts">Whether the victim's defensive pacts are called.</param>
internal delegate void WarDeclarer(WorldState world, int aggressor, int victim, ICollection<WorldEvent> events, bool invokePacts);

/// <summary>
/// What the diplomacy authority does for treaties, requests and absorption (spec §38–41, §54,
/// ADR-025), once a command has passed its checks.
/// </summary>
internal sealed class PoliticsActions
{
    private readonly DiplomacyRules _rules;
    private readonly IActRecorder _ledger;
    private readonly TreatyBook _book;
    private readonly WarDeclarer _war;

    /// <summary>Creates the actions.</summary>
    public PoliticsActions(DiplomacyRules rules, IActRecorder ledger, WarDeclarer war)
    {
        _rules = rules;
        _ledger = ledger;
        _book = new TreatyBook(rules, ledger);
        _war = war;
    }

    /// <summary>The treaty book.</summary>
    public TreatyBook Book => _book;

    /// <summary>
    /// Signs a treaty. A joint war sends both to war with its target; a non-aggression pact or a
    /// tribute signed in war brings peace.
    /// </summary>
    public void Sign(WorldState world, int proposer, int accepter, TreatyTerms terms, ICollection<WorldEvent> events, Action<int, int, DiplomaticStatus> setStatus)
    {
        if ((terms.Kind is TreatyKind.NonAggression or TreatyKind.Tribute) && DiplomacyModel.AreAtWar(world, proposer, accepter))
        {
            setStatus(proposer, accepter, DiplomaticStatus.Neutral);
            _ledger.Record(world, proposer, accepter, DiplomaticAct.MadePeace, null);
            _ledger.Record(world, accepter, proposer, DiplomaticAct.MadePeace, null);
        }

        var treaty = _book.Add(world, proposer, accepter, terms, events);
        if (treaty.Kind == TreatyKind.JointWar && treaty.TargetGodId is { } target)
        {
            foreach (var party in new[] { proposer, accepter })
            {
                if (!DiplomacyModel.AreAtWar(world, party, target))
                {
                    _war(world, party, target, events, true);
                }
            }
        }
    }

    /// <summary>A partner honours its defensive pact: the treaty comes to light, and it goes to war with the aggressor.</summary>
    public void Invoke(WorldState world, Treaty treaty, int defender, int victim, int aggressor, ICollection<WorldEvent> events)
    {
        _book.Reveal(world, treaty, events);
        var betrayal = DiplomacyModel.AreAllied(world, defender, aggressor);
        _war(world, defender, aggressor, events, false);
        events.Add(new TreatyInvoked { Tick = world.Tick, TreatyId = treaty.Id, DefenderGodId = defender, ProtectedGodId = victim, AggressorGodId = aggressor, Betrayal = betrayal });
    }

    /// <summary>A god grants a request, repaying a favour it owed.</summary>
    public void Grant(WorldState world, int asker, int asked, RequestAsk ask, ICollection<WorldEvent> events)
    {
        var politics = _rules.Politics;
        switch (ask.Kind)
        {
            case AskKind.DivinePower:
                var giver = GodStanding.Find(world, asked)!;
                var given = Math.Min(giver.DivinePower, ask.Amount);
                giver.DivinePower -= given;
                GodStanding.Find(world, asker)!.DivinePower += given;
                break;
            case AskKind.WarSupport:
                if (ask.TargetGodId is { } target && !DiplomacyModel.AreAtWar(world, asked, target))
                {
                    _war(world, asked, target, events, true);
                }

                break;
            case AskKind.Backing:
                Promise(world, asked, asker, ObligationKind.Back, politics.CouncilIntervalSeconds + politics.VoteSeconds);
                break;
            case AskKind.Territory:
                _book.Add(world, asker, asked, new TreatyTerms { Kind = TreatyKind.TerritorialAgreement, DurationSeconds = politics.DecreeSeconds, Secret = false }, events);
                break;
            case AskKind.Assistance:
                Promise(world, asked, asker, ObligationKind.Assist, politics.AssistSeconds);
                break;
        }

        var favour = FavourBook.Find(world, asker, asked);
        if (favour is not null)
        {
            FavourBook.Settle(world, favour);
        }

        _ledger.Record(world, asker, asked, DiplomaticAct.KeptWord, null);
        events.Add(new RequestAnswered { Tick = world.Tick, AskerGodId = asker, AskedGodId = asked, Ask = ask.Kind, TargetGodId = ask.TargetGodId, Granted = true, Owed = favour is not null });
    }

    /// <summary>A god refuses a request: a favour it owed is forfeit, and the asker remembers the ingratitude.</summary>
    public void Refuse(WorldState world, int asker, int asked, RequestAsk ask, ICollection<WorldEvent> events)
    {
        var favour = FavourBook.Find(world, asker, asked);
        if (favour is not null)
        {
            FavourBook.Settle(world, favour);
            _ledger.Record(world, asker, asked, DiplomaticAct.BrokePromise, null);
        }

        events.Add(new RequestAnswered { Tick = world.Tick, AskerGodId = asker, AskedGodId = asked, Ask = ask.Kind, TargetGodId = ask.TargetGodId, Granted = false, Owed = favour is not null });
    }

    /// <summary>
    /// A god absorbs its ally (spec §54): in every settlement where it is followed more than its
    /// ally, a share of the ally's followers turn to it.
    /// </summary>
    /// <returns>The people who turned.</returns>
    public int Absorb(WorldState world, int me, int ally)
    {
        var people = 0f;
        foreach (var settlement in world.Settlements)
        {
            var mine = BeliefModel.Find(settlement, me);
            var theirs = BeliefModel.Find(settlement, ally);
            if (mine is null || theirs is null || mine.Adherence <= theirs.Adherence || theirs.Adherence <= 0f)
            {
                continue;
            }

            var moved = theirs.Adherence * _rules.Politics.AbsorbShare;
            theirs.Adherence -= moved;
            mine.Adherence = SimMath.Clamp01(mine.Adherence + moved);
            people += moved * settlement.Population;
        }

        return (int)MathF.Round(people);
    }

    /// <summary>
    /// A loser accepts the victor's terms (spec §44): by the war's goal, a village's believers turn
    /// to the victor, a holy site changes keeper, a tribute is paid, or the loser keeps out of the
    /// victor's lands. A defender that repelled the attack exacts a tribute. Peace follows.
    /// </summary>
    public void Dictate(WorldState world, int winner, int loser, ICollection<WorldEvent> events, Action<int, int, DiplomaticStatus> setStatus)
    {
        var conflict = _rules.Conflict;
        var war = Conflict.WarBook.Find(world, winner, loser);
        var goal = war is not null && war.AggressorGodId == winner ? war.Goal : Domain.Conflict.WarGoal.Tribute;
        int? settlementId = null, siteId = null;
        var people = 0;
        switch (goal)
        {
            case Domain.Conflict.WarGoal.Conquest when Ceded(world, winner, loser) is { } village:
                settlementId = village.Id;
                var theirs = BeliefModel.Find(village, loser)!;
                var moved = theirs.Adherence * conflict.CededShare;
                theirs.Adherence -= moved;
                if (BeliefModel.Find(village, winner) is { } mine)
                {
                    mine.Adherence = SimMath.Clamp01(mine.Adherence + moved);
                }
                else
                {
                    village.Beliefs.Add(new Domain.Belief.GodBelief { GodId = winner, Adherence = moved, Faith = theirs.Faith, Love = 0f, Fear = 0f, Doubt = 0f });
                }

                people = (int)MathF.Round(moved * village.Population);
                break;
            case Domain.Conflict.WarGoal.HolySite when world.HolySites.Find(s => s.Id == war!.SiteId) is { } site:
                siteId = site.Id;
                (site.KeeperGodId, site.ClaimantGodId, site.ClaimTicks) = (winner, null, 0);
                break;
            case Domain.Conflict.WarGoal.Humiliation:
                _book.Add(world, winner, loser, new TreatyTerms { Kind = TreatyKind.TerritorialAgreement, DurationSeconds = conflict.DictatedSeconds, Secret = false }, events);
                break;
            default:
                _book.Add(world, winner, loser, new TreatyTerms { Kind = TreatyKind.Tribute, PayerGodId = loser, Amount = conflict.DictatedTribute, DurationSeconds = conflict.DictatedSeconds, Secret = false }, events);
                break;
        }

        setStatus(winner, loser, DiplomaticStatus.Neutral);
        _ledger.Record(world, winner, loser, DiplomaticAct.MadePeace, null);
        _ledger.Record(world, loser, winner, DiplomaticAct.Dictated, null);
        if (war is not null)
        {
            world.Diplomacy.Wars.Remove(war);
        }

        events.Add(new PeaceDictated { Tick = world.Tick, WinnerGodId = winner, LoserGodId = loser, Goal = goal, SettlementId = settlementId, SiteId = siteId, People = people });
    }

    /// <summary>The village a conquered god cedes: the one it leads where the victor is followed most.</summary>
    private static Domain.Settlements.SettlementState? Ceded(WorldState world, int winner, int loser)
    {
        Domain.Settlements.SettlementState? best = null;
        var most = -1f;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && TreatyModel.Leader(settlement) == loser)
            {
                var share = BeliefModel.Find(settlement, winner)?.Adherence ?? 0f;
                if (share > most)
                {
                    (best, most) = (settlement, share);
                }
            }
        }

        return best;
    }

    /// <summary>Adds a promise due in a number of seconds.</summary>
    private static void Promise(WorldState world, int debtor, int creditor, ObligationKind kind, float seconds) =>
        world.Diplomacy.Obligations.Add(new Obligation
        {
            DebtorGodId = debtor,
            CreditorGodId = creditor,
            Kind = kind,
            SinceTick = world.Tick,
            DueTick = world.Tick + (long)MathF.Round(seconds * SimulationTime.TicksPerSecond),
        });
}
