using System;
using System.Collections.Generic;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// The treaty-making half of an AI god's diplomacy (spec §38–41, §47–49, ADR-025): it weighs every
/// treaty it could propose to every god, the treaties it might break, and the favours it might call
/// in, and answers treaties and requests put to it. Every weight comes from how it sees the other
/// god, the balance of power and its personality; what it does goes through the diplomacy authority.
/// </summary>
public sealed class TreatyAdvisor
{
    // A move must be worth this much to be made.
    private const float Threshold = 0.2f;

    // A god this short of power asks a debtor for some.
    private const float PoorPower = 150f;

    private readonly DiplomacyRules _rules;
    private readonly DiplomacyAuthority _authority;
    private readonly Func<GodState, GodProfile?>? _profiles;

    /// <summary>Creates the advisor.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="authority">The diplomacy authority it acts through.</param>
    /// <param name="profiles">Each god's own profile, to judge what another AI god would accept; <c>null</c> to assume it might.</param>
    public TreatyAdvisor(DiplomacyRules rules, DiplomacyAuthority authority, Func<GodState, GodProfile?>? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _authority = authority;
        _profiles = profiles;
    }

    /// <summary>Breaks the treaty it most wants rid of, else calls in a favour, else proposes the treaty it most wants.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="events">Receives the events of what it does.</param>
    public void Act(WorldState world, GodState god, GodProfile profile, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        foreach (var move in Moves(world, god, profile))
        {
            if (move.Utility < Threshold)
            {
                return;
            }

            if (_authority.Execute(world, move.Command, god.Id, events) == DiplomacyRejection.None)
            {
                return;
            }
        }
    }

    /// <summary>Every treaty, break and request the god weighs now, best first.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>The moves with their utility.</returns>
    public List<(DiplomaticCommand Command, float Utility)> Moves(WorldState world, GodState god, GodProfile profile)
    {
        var moves = new List<(DiplomaticCommand, float)>();
        var followings = new Followings(world);
        foreach (var other in world.Gods)
        {
            if (other.Id == god.Id)
            {
                continue;
            }

            var view = DiplomacyModel.FindRelation(world, god.Id, other.Id) ?? DiplomacyModel.NewRelation(_rules, god.Id, other.Id);
            foreach (var kind in Enum.GetValues<TreatyKind>())
            {
                if (Wish(world, god, other, profile, view, kind, followings) is { } wish && Plausible(world, god, other, wish.Terms, followings))
                {
                    moves.Add((new DiplomaticCommand(god.Id, other.Id, DiplomaticCommandKind.ProposeTreaty, Terms: wish.Terms), wish.Utility));
                }
            }

            foreach (var treaty in TreatyModel.KnownBetween(world, god.Id, god.Id, other.Id))
            {
                moves.Add((new DiplomaticCommand(god.Id, other.Id, DiplomaticCommandKind.BreakTreaty, TreatyId: treaty.Id), BreakWish(god, treaty, profile, view, followings)));
            }

            if (FavourBook.Find(world, god.Id, other.Id) is not null && Ask(world, god, other) is { } ask)
            {
                moves.Add((new DiplomaticCommand(god.Id, other.Id, DiplomaticCommandKind.Request, Ask: ask), 0.6f + (0.4f * profile.Opportunism)));
            }
        }

        moves.Sort(static (a, b) => b.Item2.CompareTo(a.Item2));
        return moves;
    }

    /// <summary>
    /// How much a god wants to accept a treaty or request put to it: positive to accept. Trust,
    /// goodwill and what the other's reputation says of its word count for it; suspicion and grudges
    /// against it; each kind adds what it is worth to this god.
    /// </summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="god">The god answering.</param>
    /// <param name="proposal">The proposal.</param>
    /// <param name="profile">Its profile.</param>
    /// <param name="view">How it sees the proposer.</param>
    /// <returns>The utility of accepting.</returns>
    public static float AnswerUtility(DiplomacyRules rules, WorldState world, GodState god, DiplomaticProposal proposal, GodProfile profile, GodRelation view) =>
        AnswerUtility(world, god, proposal, profile, view, new Followings(world ?? throw new ArgumentNullException(nameof(world))));

    /// <summary><see cref="AnswerUtility(DiplomacyRules, WorldState, GodState, DiplomaticProposal, GodProfile, GodRelation)"/> with the followers already counted.</summary>
    private static float AnswerUtility(WorldState world, GodState god, DiplomaticProposal proposal, GodProfile profile, GodRelation view, Followings followings)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(view);
        var proposer = GodStanding.Find(world, proposal.FromGodId);
        var word = proposer is null ? 0f : (proposer.Traits.Contains(GodTrait.Honest) ? 0.2f : 0f) - (proposer.Traits.Contains(GodTrait.Deceptive) ? 0.4f : 0f);
        var goodwill = DiplomacyModel.Goodwill(view);
        var grudge = DiplomacyModel.Grudge(view) * profile.Grudge;
        var basis = (profile.Cooperation * (view.Trust + (0.5f * goodwill) + 0.3f)) - view.Suspicion - (0.5f * grudge) + word - 0.3f;
        if (proposal.Kind == ProposalKind.Request && proposal.Ask is { } ask)
        {
            var owed = FavourBook.Find(world, proposal.FromGodId, god.Id) is not null ? 0.6f + view.Gratitude : 0f;
            return basis + owed + view.Gratitude + RequestFit(world, god, ask, profile);
        }

        if (proposal.Terms is not { } terms)
        {
            return -1f;
        }

        var secret = terms.Secret ? 0.2f * profile.Treachery : 0f;
        if (terms.Kind is TreatyKind.NonAggression or TreatyKind.TerritorialAgreement or TreatyKind.Tribute)
        {
            // Pacts that keep the peace are weighed as peace is: by weariness, fear and the balance, not by trust.
            var weariness = DiplomacyModel.FindStance(world, god.Id, proposal.FromGodId) is { Status: DiplomaticStatus.War } war
                ? Math.Min(1f, (world.Tick - war.SinceTick) / (float)SimulationTime.TicksPerSecond / 300f) : 0f;
            var peace = (profile.Cooperation * (0.4f + Math.Max(0f, -followings.Advantage(god.Id, proposal.FromGodId)) + Threat(world, god.Id, followings) + weariness))
                + view.Fear - (0.3f * grudge) - (0.3f * profile.Aggression) + word - 0.4f;
            return peace + secret + TreatyFit(world, god, proposal.FromGodId, terms, profile, view, followings);
        }

        return basis + secret + TreatyFit(world, god, proposal.FromGodId, terms, profile, view, followings);
    }

    /// <summary>
    /// Whether another AI god might accept the terms: a god does not keep offering what its
    /// counterpart plainly refuses. A player's god is always worth asking.
    /// </summary>
    private bool Plausible(WorldState world, GodState god, GodState other, TreatyTerms terms, Followings followings)
    {
        if (other.Controller != GodController.Ai || _profiles?.Invoke(other) is not { } theirs)
        {
            return true;
        }

        var view = DiplomacyModel.FindRelation(world, other.Id, god.Id) ?? DiplomacyModel.NewRelation(_rules, other.Id, god.Id);
        var proposal = new DiplomaticProposal { FromGodId = god.Id, ToGodId = other.Id, Kind = ProposalKind.Treaty, ExpiresAtTick = world.Tick, Terms = terms };
        return AnswerUtility(world, other, proposal, theirs, view, followings) > 0f;
    }

    /// <summary>What a treaty's kind is worth to the god accepting it.</summary>
    private static float TreatyFit(WorldState world, GodState god, int proposer, TreatyTerms terms, GodProfile profile, GodRelation view, Followings followings)
    {
        var atWar = DiplomacyModel.AreAtWar(world, god.Id, proposer);
        var allyOfTarget = terms.TargetGodId is { } target && DiplomacyModel.AreAllied(world, god.Id, target);
        return terms.Kind switch
        {
            TreatyKind.NonAggression => atWar ? 0.3f : 0.1f,
            TreatyKind.DefensivePact or TreatyKind.MutualDefense => (0.5f * profile.Cooperation) + Threat(world, god.Id, followings) - (allyOfTarget ? 1f : 0f),
            TreatyKind.JointWar => terms.TargetGodId is { } enemy ? (profile.Aggression * Enmity(world, god.Id, enemy)) - (allyOfTarget ? 1.5f : 0f) - 0.2f : -1f,
            TreatyKind.SharedInfluence or TreatyKind.MigrationAgreement or TreatyKind.CreatureNonAggression => (0.5f * profile.Cooperation) - view.Rivalry + 0.2f,
            TreatyKind.ReligiousAccess => (0.5f * profile.Cooperation) - view.Rivalry - (0.3f * profile.Convert),
            TreatyKind.DisasterAssistance => (0.5f * profile.Protect) + 0.2f,
            TreatyKind.TerritorialAgreement => (0.3f * view.Rivalry) - (0.3f * profile.Convert),
            TreatyKind.Tribute => terms.PayerGodId != god.Id ? 1f
                : god.DivinePower < terms.Amount * terms.DurationSeconds / 60f * 0.5f ? -2f
                : (atWar ? 0.2f : -0.3f) - (terms.Amount / 100f),
        };
    }

    /// <summary>What granting a request costs or brings the god.</summary>
    private static float RequestFit(WorldState world, GodState god, RequestAsk ask, GodProfile profile) => ask.Kind switch
    {
        AskKind.DivinePower => -profile.Thrift * ask.Amount / Math.Max(1f, god.DivinePower),
        AskKind.WarSupport => ask.TargetGodId is { } enemy
            ? (profile.Aggression * Enmity(world, god.Id, enemy)) - (DiplomacyModel.AreAllied(world, god.Id, enemy) ? 1.5f : 0f) - 0.3f
            : -1f,
        AskKind.Backing => 0.3f,
        AskKind.Territory => -0.2f - (0.3f * profile.Convert),
        AskKind.Assistance => 0.1f - (0.2f * profile.Thrift),
    };

    /// <summary>The treaty a god would propose to another, of a kind, and how much it wants it; <c>null</c> when the kind does not fit.</summary>
    private static (TreatyTerms Terms, float Utility)? Wish(WorldState world, GodState god, GodState other, GodProfile profile, GodRelation view, TreatyKind kind, Followings followings)
    {
        var status = DiplomacyModel.StatusOf(world, god.Id, other.Id);
        var friendly = status is DiplomaticStatus.Friendly or DiplomaticStatus.Alliance;
        var cold = status is DiplomaticStatus.Hostile or DiplomaticStatus.War;
        var goodwill = DiplomacyModel.Goodwill(view);
        var grudge = DiplomacyModel.Grudge(view) * profile.Grudge;
        var advantage = followings.Advantage(god.Id, other.Id);
        var coop = profile.Cooperation;
        TreatyTerms Terms(float seconds, int? target = null, bool secret = false) => new() { Kind = kind, DurationSeconds = seconds, TargetGodId = target, Secret = secret };
        switch (kind)
        {
            case TreatyKind.NonAggression when cold || status == DiplomaticStatus.Neutral:
                return (Terms(600f), (coop * (0.6f + view.Fear + Math.Max(0f, -advantage))) + (cold ? 0.2f : 0f) - grudge - (0.4f * profile.Aggression) - 0.4f);
            case TreatyKind.MutualDefense when friendly:
                return (Terms(900f), (coop * (view.Trust + goodwill + Threat(world, god.Id, followings))) - view.Rivalry - 0.6f);
            case TreatyKind.DefensivePact when !cold && Leader(world, god.Id, other.Id, followings) is { } leader:
                var secret = DiplomacyModel.AreAllied(world, god.Id, leader) || profile.Treachery > 0.5f;
                return (Terms(900f, leader, secret), (coop * goodwill) + (profile.Aggression * Enmity(world, god.Id, leader)) + Threat(world, god.Id, followings) - 0.8f);
            case TreatyKind.JointWar when !cold && Enemy(world, god.Id, other.Id) is { } enemy:
                return (Terms(600f, enemy), (profile.Aggression * Enmity(world, god.Id, enemy)) + (0.5f * goodwill) - 1.1f);
            case TreatyKind.SharedInfluence when !cold:
                return (Terms(900f), (coop * (view.Trust + goodwill)) - view.Rivalry + (friendly ? 0.2f : 0f) - 0.4f);
            case TreatyKind.ReligiousAccess when friendly:
                return (Terms(600f), (profile.Convert * (0.5f + goodwill)) + (0.3f * view.Trust) - 0.5f);
            case TreatyKind.CreatureNonAggression when !cold && god.CreatureId is not null && other.CreatureId is not null:
                return (Terms(900f), (coop * (0.3f + goodwill + (0.5f * view.Fear))) - 0.4f);
            case TreatyKind.DisasterAssistance when friendly:
                return (Terms(900f), (profile.Protect * coop * (goodwill + view.Trust)) + (0.2f * profile.Protect) - 0.4f);
            case TreatyKind.TerritorialAgreement when status is DiplomaticStatus.Hostile or DiplomaticStatus.Neutral:
                return (Terms(600f), (coop * view.Rivalry) + view.Fear + (cold ? 0.2f : 0f) - grudge - 0.5f);
            case TreatyKind.Tribute when cold && advantage > 0.3f:
                return (new TreatyTerms { Kind = kind, DurationSeconds = 600f, PayerGodId = other.Id, Amount = 20f, Secret = false }, (2f * profile.Aggression * advantage) + (status == DiplomaticStatus.War ? 0.2f : 0f) - 0.5f);
            case TreatyKind.MigrationAgreement when !cold:
                return (Terms(900f), (coop * (0.3f + goodwill)) + (friendly ? 0.1f : 0f) - 0.4f);
            default:
                return null;
        }
    }

    /// <summary>How much a god wants to break a treaty: a pact that keeps it from a war it wants, a tribute it no longer fears to stop, a joint war it is tired of.</summary>
    private static float BreakWish(GodState god, Treaty treaty, GodProfile profile, GodRelation view, Followings followings)
    {
        var grudge = DiplomacyModel.Grudge(view) * profile.Grudge;
        var advantage = followings.Advantage(god.Id, treaty.PartnerOf(god.Id));
        return treaty.Kind switch
        {
            TreatyKind.NonAggression => (profile.Treachery * (view.Rivalry + grudge + Math.Max(0f, advantage))) + (profile.Aggression * 0.3f) - view.Trust - 0.8f,
            TreatyKind.Tribute => treaty.PayerGodId == god.Id ? (profile.Treachery * Math.Max(0f, advantage)) - view.Fear - 0.3f : -1f,
            TreatyKind.JointWar => (profile.Cooperation * 0.3f) - (profile.Aggression * 0.5f) - view.Trust,
            TreatyKind.DefensivePact or TreatyKind.SharedInfluence or TreatyKind.ReligiousAccess or TreatyKind.CreatureNonAggression or TreatyKind.DisasterAssistance
                or TreatyKind.MutualDefense or TreatyKind.TerritorialAgreement or TreatyKind.MigrationAgreement
                => (profile.Treachery * (view.Rivalry + grudge)) - view.Trust - DiplomacyModel.Goodwill(view) - 0.8f,
        };
    }

    /// <summary>What a creditor asks of its debtor: war on a common enemy, Divine Power when short, or its vote while the council sits.</summary>
    private RequestAsk? Ask(WorldState world, GodState god, GodState debtor)
    {
        foreach (var enemy in world.Gods)
        {
            if (enemy.Id != debtor.Id && enemy.Id != god.Id && DiplomacyModel.AreAtWar(world, god.Id, enemy.Id)
                && !DiplomacyModel.AreAtWar(world, debtor.Id, enemy.Id) && !DiplomacyModel.AreAllied(world, debtor.Id, enemy.Id) && !TreatyModel.ForbidsWar(world, debtor.Id, enemy.Id))
            {
                return new RequestAsk(AskKind.WarSupport, enemy.Id);
            }
        }

        if (god.DivinePower < PoorPower && debtor.DivinePower > 2f * PoorPower)
        {
            return new RequestAsk(AskKind.DivinePower, Amount: Math.Min(_rules.Politics.MaxPowerAsk, PoorPower));
        }

        return world.Diplomacy.Council.Session is not null ? new RequestAsk(AskKind.Backing) : null;
    }

    /// <summary>The god leading the world that is neither of the two, if it leads past a third of all followers.</summary>
    private static int? Leader(WorldState world, int godId, int otherId, Followings followings)
    {
        int? leader = null;
        var best = 0.33f;
        foreach (var third in world.Gods)
        {
            var share = followings.ShareOf(third.Id);
            if (third.Id != godId && third.Id != otherId && share > best)
            {
                (leader, best) = (third.Id, share);
            }
        }

        return leader;
    }

    /// <summary>A god both the god and the other are hostile to or at war with, the one the god hates most.</summary>
    private static int? Enemy(WorldState world, int godId, int otherId)
    {
        int? enemy = null;
        var worst = 0.5f;
        foreach (var third in world.Gods)
        {
            if (third.Id == godId || third.Id == otherId || DiplomacyModel.AreAllied(world, otherId, third.Id)
                || DiplomacyModel.StatusOf(world, otherId, third.Id) is not (DiplomaticStatus.Hostile or DiplomaticStatus.War))
            {
                continue;
            }

            var enmity = Enmity(world, godId, third.Id);
            if (enmity > worst)
            {
                (enemy, worst) = (third.Id, enmity);
            }
        }

        return enemy;
    }

    /// <summary>How much a god hates another: rivalry, grudge, distrust and war.</summary>
    private static float Enmity(WorldState world, int godId, int otherId)
    {
        var view = DiplomacyModel.FindRelation(world, godId, otherId);
        var war = DiplomacyModel.AreAtWar(world, godId, otherId) ? 0.5f : 0f;
        return view is null ? war : view.Rivalry + DiplomacyModel.Grudge(view) + (0.5f - view.Trust) + war;
    }

    /// <summary>How far any god but this one leads past a third of all followers (0..1).</summary>
    private static float Threat(WorldState world, int godId, Followings followings)
    {
        var threat = 0f;
        foreach (var other in world.Gods)
        {
            if (other.Id != godId && !DiplomacyModel.AreAllied(world, godId, other.Id))
            {
                threat = Math.Max(threat, SimMath.Clamp01((followings.ShareOf(other.Id) - 0.33f) * 3f));
            }
        }

        return threat;
    }

    /// <summary>
    /// Every god's followers, counted once for a weighing, as <see cref="GodStanding.CountFollowers(WorldState, int)"/>
    /// and <see cref="VictorySystem.ShareOf"/> count them: counting again for each treaty kind was most
    /// of the AI's time in a large world (ADR-027).
    /// </summary>
    private sealed class Followings
    {
        private readonly Dictionary<int, int> _counts = [];
        private readonly int _total;

        /// <summary>Counts every god's followers.</summary>
        public Followings(WorldState world)
        {
            foreach (var god in world.Gods)
            {
                var followers = GodStanding.CountFollowers(world, god.Id);
                _counts[god.Id] = followers;
                _total += followers;
            }
        }

        /// <summary>A god's share of all followers.</summary>
        public float ShareOf(int godId) => _total > 0 ? (float)_counts.GetValueOrDefault(godId) / _total : 0f;

        /// <summary>The god's lead in followers over the other, from -1 to 1.</summary>
        public float Advantage(int godId, int otherId)
        {
            var mine = _counts.GetValueOrDefault(godId);
            var theirs = _counts.GetValueOrDefault(otherId);
            return (mine - theirs) / (float)Math.Max(1, mine + theirs);
        }
    }
}
