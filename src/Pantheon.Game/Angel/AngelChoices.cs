using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Angel;

/// <summary>What a suggestion of the angel's would have its god do (ADR-074).</summary>
public enum AngelDeed
{
    /// <summary>Cast a miracle on a village.</summary>
    Cast,

    /// <summary>Send its Creature to work in a village.</summary>
    CreatureWork,

    /// <summary>Set its Creature on a rival's.</summary>
    CreatureFight,

    /// <summary>Answer a god: an offer, a war.</summary>
    Answer,

    /// <summary>Look at a place.</summary>
    Look,
}

/// <summary>A deed the angel may suggest (ADR-074): only what its god can do now.</summary>
/// <param name="Id">Its id in the angel's question, for example <c>d1</c>.</param>
/// <param name="Deed">What it is.</param>
/// <param name="Text">It in a few words, for the player: "Rain on Mossgate".</param>
/// <param name="Kindness">1 when it helps people, −1 when it harms them, 0 otherwise.</param>
/// <param name="Target">Where it is done, for Show me.</param>
/// <param name="Why">Why it might be done, for the angel's model.</param>
/// <param name="MiracleId">The miracle, for a cast.</param>
/// <param name="SettlementId">The village, if any.</param>
/// <param name="GodId">The god, for an answer.</param>
/// <param name="CreatureId">The rival Creature, for a fight.</param>
public sealed record AngelSuggestion(string Id, AngelDeed Deed, string Text, int Kindness, GroundPosition Target, string Why, string? MiracleId = null, int? SettlementId = null, int? GodId = null, int? CreatureId = null);

/// <summary>
/// The deeds the angel may suggest (ADR-074): miracles its god can cast now as a kind heart would weigh
/// them and as a harsh one would, help for the villages in its hooks, its Creature at work or set on a
/// visitor, and the gods waiting for an answer. Reads the session; changes nothing.
/// </summary>
public static class AngelChoices
{
    /// <summary>A suggestion's id by its place.</summary>
    /// <param name="index">Its place, from 0.</param>
    /// <returns>The id.</returns>
    public static string IdOf(int index) => string.Create(CultureInfo.InvariantCulture, $"d{index + 1}");

    /// <summary>The deeds the angel may suggest now, the most pressing first.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The angel's god.</param>
    /// <param name="hooks">What moved the angel: their villages, visitors and gods come first.</param>
    /// <param name="max">The most deeds.</param>
    /// <returns>The deeds, with their ids.</returns>
    public static List<AngelSuggestion> Of(GameSession session, GodState god, IReadOnlyList<AngelHook> hooks, int max)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(hooks);
        var world = session.World;
        var kind = Casts(session, god, GodPersonality.Guardian, harms: false);
        var harsh = Casts(session, god, GodPersonality.Tyrant, harms: true);
        var found = new List<AngelSuggestion>();
        foreach (var hook in hooks.OrderByDescending(h => h.Weight))
        {
            ForHook(found, world, god, hook, kind);
        }

        found.AddRange(kind.Take(3));
        found.AddRange(harsh.Take(3));
        if (Work(world, god, Home(world, god)) is { } work)
        {
            found.Add(work);
        }

        if (found.Count == 0 && Home(world, god) is { } home)
        {
            found.Add(Look(home.Name, home.Position, home.Id, "your home"));
        }

        return found.DistinctBy(s => (s.Deed, s.MiracleId, s.SettlementId, s.GodId, s.CreatureId)).Take(max).Select((s, i) => s with { Id = IdOf(i) }).ToList();
    }

    /// <summary>The deeds that answer a hook: help for a struck or hungry village, the Creature set on a visitor, an answer to a god.</summary>
    private static void ForHook(List<AngelSuggestion> found, WorldState world, GodState god, AngelHook hook, List<AngelSuggestion> kind)
    {
        switch (hook.Moment)
        {
            case AngelMoment.VillageHungry or AngelMoment.VillageStruck or AngelMoment.VillageLost when world.Settlements.Find(s => s.Id == hook.SettlementId) is { } village:
                var help = kind.FirstOrDefault(s => s.SettlementId == village.Id);
                found.Add(help ?? Look(village.Name, village.Position, village.Id, "it is in trouble"));
                if (hook.Moment == AngelMoment.VillageHungry && Work(world, god, village) is { } work)
                {
                    found.Add(work);
                }

                break;
            case AngelMoment.Visitor when world.Creatures.Find(c => c.Id == hook.CreatureId) is { } visitor && Mine(world, god) is { } mine:
                found.Add(new AngelSuggestion(string.Empty, AngelDeed.CreatureFight, Format("Set {0} on {1}", mine.Name, visitor.Name), 0, visitor.Position, "a rival's Creature walks in your lands", CreatureId: visitor.Id, GodId: visitor.GodId));
                break;
            case AngelMoment.Offer or AngelMoment.War when world.Gods.Find(g => g.Id == hook.GodId) is { } other:
                var place = Home(world, other)?.Position ?? world.Creatures.Find(c => c.Id == other.CreatureId)?.Position ?? default;
                found.Add(new AngelSuggestion(string.Empty, AngelDeed.Answer, Format("Answer {0}", other.Name), 0, place, hook.Text, GodId: other.Id));
                break;
        }
    }

    /// <summary>The miracles a heart would cast now, best first: those that help, or those that harm.</summary>
    private static List<AngelSuggestion> Casts(GameSession session, GodState god, GodPersonality heart, bool harms)
    {
        var world = session.World;
        var found = new List<AngelSuggestion>();
        foreach (var candidate in session.PreviewGodAiAs(god, heart))
        {
            if (candidate.Kind != AiActionKind.CastMiracle || candidate.Utility <= 0f || session.Content.Miracles.Find(candidate.MiracleId!) is not { } miracle
                || Simulation.Culture.CultureModel.Harms(miracle) != harms || world.Settlements.Find(s => s.Id == candidate.SettlementId) is not { } village)
            {
                continue;
            }

            found.Add(new AngelSuggestion(string.Empty, AngelDeed.Cast, Format("{0} on {1}", miracle.Name, village.Name), harms ? -1 : 1, village.Position,
                Format("a miracle that {0}; {1:0} power; {2}", harms ? "harms" : "helps", candidate.Cost, candidate.Reason), miracle.Id, village.Id));
        }

        return found.DistinctBy(s => s.SettlementId).ToList();
    }

    /// <summary>The god's Creature sent to work in a village, when it is free to go.</summary>
    private static AngelSuggestion? Work(WorldState world, GodState god, Domain.Settlements.SettlementState? village)
    {
        if (village is null || Mine(world, god) is not { } creature || creature.Activity.Action is CreatureAction.Farm or CreatureAction.GatherWood or CreatureAction.HelpBuild or CreatureAction.Fight)
        {
            return null;
        }

        return new AngelSuggestion(string.Empty, AngelDeed.CreatureWork, Format("{0} to work in {1}", creature.Name, village.Name), 1, village.Position, "its fields, wood and buildings want hands", SettlementId: village.Id);
    }

    /// <summary>A look at a village.</summary>
    private static AngelSuggestion Look(string name, GroundPosition place, int settlementId, string why) =>
        new(string.Empty, AngelDeed.Look, Format("Look at {0}", name), 0, place, why, SettlementId: settlementId);

    /// <summary>The god's Creature, when it is in the world.</summary>
    private static CreatureState? Mine(WorldState world, GodState god) =>
        world.Creatures.Find(c => c.Id == god.CreatureId) is { } creature && creature.Activity.Phase != CreaturePhase.Banished ? creature : null;

    /// <summary>The god's home village.</summary>
    private static Domain.Settlements.SettlementState? Home(WorldState world, GodState god) =>
        GodPlanner.Home(world, god.Id) ?? world.Settlements.Find(s => s.Id == world.Creatures.Find(c => c.Id == god.CreatureId)?.HomeSettlementId);

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
