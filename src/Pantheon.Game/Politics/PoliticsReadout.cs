using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Politics;

/// <summary>How the player will sign a treaty: for how long, and whether in secret. The panel keeps this; it is not game state.</summary>
/// <param name="DurationSeconds">The term; 0 until broken.</param>
/// <param name="Secret">Whether to sign in secret.</param>
public sealed record TreatyChoice(float DurationSeconds, bool Secret)
{
    /// <summary>The terms the panel offers, in order.</summary>
    public static IReadOnlyList<float> Durations { get; } = [120f, 300f, 600f, 1200f, 0f];

    /// <summary>Five minutes, in public.</summary>
    public static TreatyChoice Default { get; } = new(300f, false);
}

/// <summary>A treaty in force, with the action that breaks it.</summary>
/// <param name="Text">How it reads.</param>
/// <param name="Break">Breaking it.</param>
public sealed record TreatyLine(string Text, PoliticsAction Break);

/// <summary>The politics of one other god in the diplomacy panel (ADR-025).</summary>
/// <param name="Treaties">The player's treaties with it, each with the action that breaks it.</param>
/// <param name="Others">Its other treaties the player knows of, in one line; empty when none.</param>
/// <param name="Favours">The favours either owes the other; empty when none.</param>
/// <param name="Proposal">Its treaty or request waiting for the player's answer; empty when none.</param>
/// <param name="Offers">The treaties the player could propose to it now.</param>
/// <param name="Requests">What the player could ask of it now.</param>
/// <param name="Aims">The wars the player could declare on it, by goal (ADR-026).</param>
public sealed record PoliticsEntry(IReadOnlyList<TreatyLine> Treaties, string Others, string Favours, string Proposal, IReadOnlyList<PoliticsAction> Offers, IReadOnlyList<PoliticsAction> Requests, IReadOnlyList<PoliticsAction> Aims);

/// <summary>Builds the diplomacy panel's politics for each god (spec §38–41, §54, ADR-025). Pure.</summary>
public static class PoliticsReadout
{
    // The tribute the panel offers, in Divine Power a minute, and the power it asks of a debtor.
    private const float Tribute = 20f;
    private const float PowerAsk = 100f;

    /// <summary>The politics between the player and another god.</summary>
    /// <param name="session">The session.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="choice">How the player will sign.</param>
    /// <returns>The entry.</returns>
    public static PoliticsEntry ForGod(GameSession session, int otherGodId, TreatyChoice choice)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(choice);
        var world = session.World;
        var me = session.LocalGodId ?? 0;
        var known = TreatyModel.KnownBy(world, me).Where(t => t.Binds(otherGodId)).ToList();
        var treaties = known.Where(t => t.Binds(me))
            .Select(t => new TreatyLine(PoliticsText.Describe(t, world, me), new PoliticsAction("Break", new DiplomaticCommand(me, otherGodId, DiplomaticCommandKind.BreakTreaty, TreatyId: t.Id))))
            .ToList();
        var others = known.Where(t => !t.Binds(me)).Select(t => Other(t, world, otherGodId)).ToList();
        var othersLine = others.Count == 0 ? string.Empty : "Its other treaties: " + string.Join("; ", others) + ".";
        return new PoliticsEntry(treaties, othersLine, Favours(session, me, otherGodId), Proposal(session, me, otherGodId), Offers(session, me, otherGodId, choice), Requests(session, me, otherGodId), Aims(session, me, otherGodId));
    }

    /// <summary>The wars the player could declare on the god now, one per goal (spec §44).</summary>
    private static List<PoliticsAction> Aims(GameSession session, int me, int other)
    {
        var world = session.World;
        var war = new DiplomaticCommand(me, other, DiplomaticCommandKind.DeclareWar);
        if (session.Inspect(a => a.Diplomacy.Check(world, war, me)) != DiplomacyRejection.None)
        {
            return [];
        }

        var aims = new List<PoliticsAction>
        {
            new("War for conquest", war with { Aim = new Domain.Conflict.WarAim(Domain.Conflict.WarGoal.Conquest) }),
            new("War for tribute", war with { Aim = new Domain.Conflict.WarAim(Domain.Conflict.WarGoal.Tribute) }),
        };
        if (world.Creatures.Exists(c => c.GodId == me) && world.Creatures.Exists(c => c.GodId == other))
        {
            aims.Add(new("War to humble its Creature", war with { Aim = new Domain.Conflict.WarAim(Domain.Conflict.WarGoal.Humiliation) }));
        }

        foreach (var site in world.HolySites.Where(s => s.KeeperGodId == other))
        {
            aims.Add(new("War for " + site.Name, war with { Aim = new Domain.Conflict.WarAim(Domain.Conflict.WarGoal.HolySite, site.Id) }));
        }

        return aims;
    }

    /// <summary>A treaty between the god and a third, in a few words: "joint war with Seren against Kaio".</summary>
    private static string Other(Treaty treaty, Domain.World.WorldState world, int godId)
    {
        var partner = PoliticsText.GodName(world, treaty.PartnerOf(godId));
        var against = treaty.TargetGodId is { } target ? " against " + PoliticsText.GodName(world, target) : string.Empty;
        return PoliticsText.Name(treaty.Kind) + " with " + partner + against;
    }

    /// <summary>The favours owed between the player and the god.</summary>
    private static string Favours(GameSession session, int me, int other)
    {
        var world = session.World;
        var name = PoliticsText.GodName(world, other);
        var parts = new List<string>();
        if (FavourBook.Find(world, me, other) is { } owedToMe)
        {
            parts.Add(PoliticsText.Format("{0} owes you {1}", name, Count(owedToMe.Weight)));
        }

        if (FavourBook.Find(world, other, me) is { } owedByMe)
        {
            parts.Add(PoliticsText.Format("you owe {0} {1}", name, Count(owedByMe.Weight)));
        }

        foreach (var promise in world.Diplomacy.Obligations.Where(o => (o.DebtorGodId == me && o.CreditorGodId == other) || (o.DebtorGodId == other && o.CreditorGodId == me)))
        {
            var due = PoliticsText.Clock(Math.Max(0, promise.DueTick - world.Tick) / (float)Domain.World.SimulationTime.TicksPerSecond);
            parts.Add(promise.DebtorGodId == me
                ? PoliticsText.Format("you promised {0} {1} ({2} left)", name, Promise(promise.Kind), due)
                : PoliticsText.Format("{0} promised you {1} ({2} left)", name, Promise(promise.Kind), due));
        }

        return parts.Count == 0 ? string.Empty : PoliticsText.Capitalize(string.Join("; ", parts)) + ".";
    }

    /// <summary>The god's treaty or request waiting for the player.</summary>
    private static string Proposal(GameSession session, int me, int other)
    {
        var world = session.World;
        if (DiplomacyAuthority.FindProposal(world, other, me) is not { } proposal)
        {
            return string.Empty;
        }

        return proposal switch
        {
            { Kind: ProposalKind.Treaty, Terms: { } terms } => "It proposes " + PoliticsText.Terms(terms, world, me) + ".",
            { Kind: ProposalKind.Request, Ask: { } ask } => (FavourBook.Find(world, other, me) is not null ? "It calls in the favour you owe it: " : "It asks you for ") + PoliticsText.Ask(ask, world) + ".",
            { Kind: ProposalKind.Alliance } => "It proposes an alliance.",
            { Kind: ProposalKind.Surrender } => "It demands your surrender: " + Conflict.ConflictText.SurrenderTerms(world, other, me) + ".",
            _ => "It asks for peace.",
        };
    }

    /// <summary>Every treaty the player could propose to the god now, on its chosen terms.</summary>
    private static List<PoliticsAction> Offers(GameSession session, int me, int other, TreatyChoice choice)
    {
        var world = session.World;
        var offers = new List<PoliticsAction>();
        void Offer(string label, TreatyTerms terms)
        {
            var command = new DiplomaticCommand(me, other, DiplomaticCommandKind.ProposeTreaty, Terms: terms);
            if (session.Inspect(a => a.Diplomacy.Check(world, command, me)) == DiplomacyRejection.None)
            {
                offers.Add(new PoliticsAction(label, command));
            }
        }

        var lasting = choice.DurationSeconds;
        foreach (var kind in Enum.GetValues<TreatyKind>())
        {
            var terms = new TreatyTerms { Kind = kind, DurationSeconds = lasting, Secret = choice.Secret };
            switch (kind)
            {
                case TreatyKind.DefensivePact:
                    Offer(PoliticsText.Format("Defend {0}", PoliticsText.GodName(world, other)), terms);
                    foreach (var third in Thirds(session, me, other))
                    {
                        Offer(PoliticsText.Format("Defend {0} against {1}", PoliticsText.GodName(world, other), PoliticsText.GodName(world, third)), terms with { TargetGodId = third });
                    }

                    break;
                case TreatyKind.JointWar:
                    foreach (var third in Thirds(session, me, other))
                    {
                        Offer(PoliticsText.Format("Joint war on {0}", PoliticsText.GodName(world, third)), terms with { TargetGodId = third });
                    }

                    break;
                case TreatyKind.Tribute:
                    var term = lasting > 0f ? lasting : 600f;
                    Offer(PoliticsText.Format("Pay {0:0}/min", Tribute), terms with { PayerGodId = me, Amount = Tribute, DurationSeconds = term });
                    Offer(PoliticsText.Format("Demand {0:0}/min", Tribute), terms with { PayerGodId = other, Amount = Tribute, DurationSeconds = term });
                    break;
                case TreatyKind.NonAggression:
                case TreatyKind.SharedInfluence:
                case TreatyKind.ReligiousAccess:
                case TreatyKind.CreatureNonAggression:
                case TreatyKind.DisasterAssistance:
                case TreatyKind.MutualDefense:
                case TreatyKind.TerritorialAgreement:
                case TreatyKind.MigrationAgreement:
                    Offer(PoliticsText.Capitalize(PoliticsText.Name(kind)), terms);
                    break;
            }
        }

        return offers;
    }

    /// <summary>What the player could ask of the god now: all a favour may call for when it owes one, its vote while the council sits, and absorbing it when allied.</summary>
    private static List<PoliticsAction> Requests(GameSession session, int me, int other)
    {
        var world = session.World;
        var asks = new List<PoliticsAction>();
        void Ask(string label, DiplomaticCommand command)
        {
            if (session.Inspect(a => a.Diplomacy.Check(world, command, me)) == DiplomacyRejection.None)
            {
                asks.Add(new PoliticsAction(label, command));
            }
        }

        if (FavourBook.Find(world, me, other) is not null)
        {
            Ask(PoliticsText.Format("Ask {0:0} power", PowerAsk), Request(me, other, new RequestAsk(AskKind.DivinePower, Amount: PowerAsk)));
            foreach (var enemy in Thirds(session, me, other).Where(t => DiplomacyModel.AreAtWar(world, me, t)))
            {
                Ask("Ask war on " + PoliticsText.GodName(world, enemy), Request(me, other, new RequestAsk(AskKind.WarSupport, enemy)));
            }

            Ask("Ask it to keep out", Request(me, other, new RequestAsk(AskKind.Territory)));
            Ask("Ask for help", Request(me, other, new RequestAsk(AskKind.Assistance)));
        }

        if (world.Diplomacy.Council.Session is not null)
        {
            Ask("Ask for its vote", Request(me, other, new RequestAsk(AskKind.Backing)));
        }

        Ask("Absorb its followers", new DiplomaticCommand(me, other, DiplomaticCommandKind.Absorb));
        return asks;
    }

    /// <summary>A request command.</summary>
    private static DiplomaticCommand Request(int me, int other, RequestAsk ask) => new(me, other, DiplomaticCommandKind.Request, Ask: ask);

    /// <summary>The gods other than the two.</summary>
    private static IEnumerable<int> Thirds(GameSession session, int me, int other) => session.World.Gods.Select(g => g.Id).Where(id => id != me && id != other);

    /// <summary>"a favour" or "2 favours".</summary>
    private static string Count(float weight) => weight < 1.5f ? "a favour" : PoliticsText.Format("{0:0} favours", weight);

    /// <summary>What a promise was.</summary>
    private static string Promise(ObligationKind kind) => kind switch
    {
        ObligationKind.Assist => "help in need",
        ObligationKind.Back => "your vote",
    };
}
