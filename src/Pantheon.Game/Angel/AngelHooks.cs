using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Culture;
using Pantheon.Game.Disasters;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Angel;

/// <summary>
/// Reads what moves the angel from the world's events (ADR-074): its god's own deeds, kind and harsh,
/// and what happens to its god: its villages struck, hungry, won and lost, rivals' Creatures in its
/// lands, wars, offers and meetings, its Creature's fights, its traits and its rank. Pure.
/// </summary>
public static class AngelHooks
{
    /// <summary>The moments in a frame's events that move a god's angel.</summary>
    /// <param name="events">The events, in order.</param>
    /// <param name="world">The world, for names.</param>
    /// <param name="content">The content, for the miracles.</param>
    /// <param name="godId">The angel's god.</param>
    /// <returns>The hooks.</returns>
    public static List<AngelHook> Read(IReadOnlyList<WorldEvent> events, WorldState world, GameContent content, int godId)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        var hooks = new List<AngelHook>();
        foreach (var worldEvent in events)
        {
            switch (worldEvent)
            {
                case MiracleCast cast:
                    Cast(hooks, cast, world, content, godId);
                    break;
                case VillagersSetDown down when down.GodId == godId && (down.Rescued || down.Drowned):
                    hooks.Add(new AngelHook(down.Rescued ? AngelMoment.DeedKind : AngelMoment.DeedHarsh, down.Tick,
                        down.Rescued ? Format("You lifted {0} people from danger and set them down safe.", down.People) : Format("You dropped {0} people into the water, and they drowned.", down.People),
                        down.FromSettlementId));
                    break;
                case HandStruck struck when struck.GodId == godId && (struck.Deaths > 0 || struck.HousesLost > 0):
                    hooks.Add(new AngelHook(AngelMoment.DeedHarsh, struck.Tick, Format("You threw a {0} into {1}: {2} houses lost, {3} dead.", struck.Kind.ToString().ToLowerInvariant(), Village(world, struck.SettlementId), struck.HousesLost, struck.Deaths), struck.SettlementId));
                    break;
                case OfferingGiven offering when offering.GodId == godId:
                    hooks.Add(new AngelHook(AngelMoment.DeedKind, offering.Tick, Format("You gave an offering of {0} to {1}.", Hand.HandText.Goods(offering.Food, offering.Wood, offering.Stone), offering.ToSettlementId is { } to ? Village(world, to) : "your Creature"), offering.ToSettlementId));
                    break;
                case DiplomacyChanged change:
                    Diplomacy(hooks, change, world, godId);
                    break;
                case FamineStarted famine when Led(world, famine.SettlementId, godId) is { } hungry:
                    hooks.Add(new AngelHook(AngelMoment.VillageHungry, famine.Tick, Format("{0}'s stores ran out: its people go hungry.", hungry.Name), hungry.Id));
                    break;
                case ThirstStarted thirst when Led(world, thirst.SettlementId, godId) is { } dry:
                    hooks.Add(new AngelHook(AngelMoment.VillageHungry, thirst.Tick, Format("{0}'s wells ran dry: its people go thirsty.", dry.Name), dry.Id));
                    break;
                case SettlementDamaged damage when Led(world, damage.SettlementId, godId) is { } village:
                    hooks.Add(new AngelHook(AngelMoment.VillageStruck, damage.Tick, Format("{0} struck {1}: {2} houses lost, {3} dead.", Capital(DisasterText.Kind(damage.Cause)), village.Name, damage.HousesLost, damage.Deaths), village.Id, Detail: DisasterText.Kind(damage.Cause)));
                    break;
                case FollowingChanged following when following.GodId == godId:
                    hooks.Add(new AngelHook(following.Gained ? AngelMoment.VillageGained : AngelMoment.VillageLost, following.Tick,
                        Format(following.Gained ? "{0} now follows you." : "{0} no longer follows you.", Village(world, following.SettlementId)), following.SettlementId));
                    break;
                case Rebellion rising when rising.GodId == godId:
                    hooks.Add(new AngelHook(AngelMoment.VillageLost, rising.Tick, Format("{0} rose against you: {1} people renounced you.", Village(world, rising.SettlementId), rising.People), rising.SettlementId));
                    break;
                case MigrationStarted leaving when leaving.AbandonedGodId == godId:
                    hooks.Add(new AngelHook(AngelMoment.VillageLost, leaving.Tick, Format("{0} people left {1}, giving you up.", leaving.People, Village(world, leaving.OriginSettlementId)), leaving.OriginSettlementId));
                    break;
                case CreatureWatched watched when watched.GodId == godId && watched.VisitorGodId != godId:
                    hooks.Add(new AngelHook(AngelMoment.Visitor, watched.Tick, Format("{0}, {1}'s Creature, came into {2}, your village.", CreatureName(world, watched.CreatureId), GodName(world, watched.VisitorGodId), Village(world, watched.SettlementId)),
                        watched.SettlementId, watched.VisitorGodId, watched.CreatureId));
                    break;
                case CreatureFightEnded fight:
                    Fought(hooks, fight, world, godId);
                    break;
                case GodsMet met when met.GodId == godId || met.OtherGodId == godId:
                    var other = met.GodId == godId ? met.OtherGodId : met.GodId;
                    hooks.Add(new AngelHook(AngelMoment.Met, met.Tick, Format("You met {0}.", GodName(world, other)), met.SettlementId, other));
                    break;
                case TraitEarned trait when trait.GodId == godId:
                    hooks.Add(new AngelHook(AngelMoment.Trait, trait.Tick, Format("The world now sees you as {0}.", CultureText.Name(trait.Trait).ToLowerInvariant()), Detail: CultureText.Name(trait.Trait).ToLowerInvariant()));
                    break;
                case RankAchieved rank when rank.GodId == godId:
                    hooks.Add(new AngelHook(AngelMoment.Rank, rank.Tick, Format("You rose to {0}.", DivineText.Rank(rank.Rank)), Detail: DivineText.Rank(rank.Rank)));
                    break;
            }
        }

        return hooks;
    }

    /// <summary>A miracle: the god's own deed, kind or harsh, or another god's that struck the god's village.</summary>
    private static void Cast(List<AngelHook> hooks, MiracleCast cast, WorldState world, GameContent content, int godId)
    {
        if (content.Miracles.Find(cast.MiracleId) is not { } miracle)
        {
            return;
        }

        var harms = Simulation.Culture.CultureModel.Harms(miracle);
        var first = cast.AffectedSettlementIds.Count > 0 ? cast.AffectedSettlementIds[0] : (int?)null;
        if (cast.GodId == godId)
        {
            if (first is null && cast.Deaths == 0)
            {
                return;
            }

            var dead = cast.Deaths > 0 ? Format(": {0} dead", cast.Deaths) : string.Empty;
            hooks.Add(new AngelHook(harms ? AngelMoment.DeedHarsh : AngelMoment.DeedKind, cast.Tick, Format("You cast {0} on {1}{2}.", miracle.Name, Village(world, first), dead), first, Detail: miracle.Name));
            return;
        }

        if (!harms)
        {
            return;
        }

        foreach (var id in cast.AffectedSettlementIds)
        {
            if (Led(world, id, godId) is { } village)
            {
                var caster = cast.Veiled ? "An unseen god" : GodName(world, cast.GodId);
                hooks.Add(new AngelHook(AngelMoment.VillageStruck, cast.Tick, Format("{0} struck {1}, your village, with {2}.", caster, village.Name, miracle.Name), village.Id, cast.Veiled ? null : cast.GodId, Detail: miracle.Name));
                return;
            }
        }
    }

    /// <summary>A diplomatic move: the god's own, kind or harsh, or another god's war, betrayal or offer.</summary>
    private static void Diplomacy(List<AngelHook> hooks, DiplomacyChanged change, WorldState world, int godId)
    {
        if (change.ActorGodId == godId)
        {
            var other = GodName(world, change.OtherGodId);
            var (moment, text) = change.Kind switch
            {
                DiplomaticCommandKind.DeclareWar => (AngelMoment.DeedHarsh, Format("You declared war on {0}.", other)),
                DiplomaticCommandKind.BreakAlliance => (AngelMoment.DeedHarsh, Format("You broke your alliance with {0}.", other)),
                DiplomaticCommandKind.BreakTreaty => (AngelMoment.DeedHarsh, Format("You broke a treaty with {0}.", other)),
                DiplomaticCommandKind.RequestPeace => (AngelMoment.DeedKind, Format("You asked {0} for peace.", other)),
                DiplomaticCommandKind.OfferAlliance => (AngelMoment.DeedKind, Format("You offered {0} an alliance.", other)),
                DiplomaticCommandKind.Accept when change.Answered is ProposalKind.Peace or ProposalKind.Alliance => (AngelMoment.DeedKind, Format("You accepted {0}'s {1}.", other, change.Answered == ProposalKind.Peace ? "peace" : "alliance")),
                _ => ((AngelMoment?)null, string.Empty),
            };
            if (moment is { } deed)
            {
                hooks.Add(new AngelHook(deed, change.Tick, text, GodId: change.OtherGodId));
            }

            return;
        }

        if (change.OtherGodId != godId)
        {
            return;
        }

        var actor = GodName(world, change.ActorGodId);
        var (heard, words) = change.Kind switch
        {
            DiplomaticCommandKind.DeclareWar => (AngelMoment.War, Format("{0} declared war on you.", actor)),
            DiplomaticCommandKind.BreakAlliance => (AngelMoment.War, Format("{0} broke its alliance with you.", actor)),
            DiplomaticCommandKind.BreakTreaty => (AngelMoment.War, Format("{0} broke a treaty with you.", actor)),
            DiplomaticCommandKind.OfferAlliance => (AngelMoment.Offer, Format("{0} offers you an alliance.", actor)),
            DiplomaticCommandKind.RequestPeace => (AngelMoment.Offer, Format("{0} asks you for peace.", actor)),
            DiplomaticCommandKind.ProposeTreaty => (AngelMoment.Offer, Format("{0} proposes a treaty to you.", actor)),
            DiplomaticCommandKind.Request => (AngelMoment.Offer, Format("{0} asks a favour of you.", actor)),
            DiplomaticCommandKind.DemandSurrender => (AngelMoment.War, Format("{0} demands your surrender.", actor)),
            _ => ((AngelMoment?)null, string.Empty),
        };
        if (heard is { } moment2)
        {
            hooks.Add(new AngelHook(moment2, change.Tick, words, GodId: change.ActorGodId));
        }
    }

    /// <summary>A fight's end, when the god's Creature fought.</summary>
    private static void Fought(List<AngelHook> hooks, CreatureFightEnded fight, WorldState world, int godId)
    {
        if (fight.Outcome == FightOutcome.Drew || world.Creatures.Find(c => c.Id == fight.CreatureId) is not { } winner || world.Creatures.Find(c => c.Id == fight.OtherCreatureId) is not { } loser)
        {
            return;
        }

        if (winner.GodId == godId)
        {
            hooks.Add(new AngelHook(AngelMoment.CreatureWon, fight.Tick, Format("Your Creature {0} beat {1}, {2}'s Creature.", winner.Name, loser.Name, GodName(world, loser.GodId)), GodId: loser.GodId, CreatureId: loser.Id));
        }
        else if (loser.GodId == godId)
        {
            var how = fight.Outcome == FightOutcome.BrokeOff ? "ran from" : "was beaten by";
            hooks.Add(new AngelHook(AngelMoment.CreatureLost, fight.Tick, Format("Your Creature {0} {1} {2}, {3}'s Creature.", loser.Name, how, winner.Name, GodName(world, winner.GodId)), GodId: winner.GodId, CreatureId: winner.Id));
        }
    }

    /// <summary>A village the god leads, by id.</summary>
    private static SettlementState? Led(WorldState world, int settlementId, int godId) =>
        world.Settlements.Find(s => s.Id == settlementId) is { } village && TreatyModel.Leader(village) == godId ? village : null;

    /// <summary>A village's name.</summary>
    private static string Village(WorldState world, int? settlementId) => world.Settlements.Find(s => s.Id == settlementId)?.Name ?? "the land";

    /// <summary>A god's name.</summary>
    private static string GodName(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a god";

    /// <summary>A Creature's name.</summary>
    private static string CreatureName(WorldState world, int creatureId) => world.Creatures.Find(c => c.Id == creatureId)?.Name ?? "a Creature";

    /// <summary>A text with its first letter capital.</summary>
    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
