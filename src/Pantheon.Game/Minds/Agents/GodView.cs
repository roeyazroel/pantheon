using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Divine;
using Pantheon.Game.Gods;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// The world as a god sees it, in words for its mind (ADR-072): itself and its Creature, the gods it
/// knows and how it stands with each, the villages that concern it, the holy sites, the proposals
/// waiting for its answer, its aims, the miracles it may cast, and what its instincts would do.
/// </summary>
public static class GodView
{
    /// <summary>How things stand, from the god's side.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <param name="rules">The agents' rules: how many villages to tell of.</param>
    /// <returns>The text.</returns>
    public static string Describe(GameSession session, GodState god, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(rules);
        var world = session.World;
        var text = new StringBuilder();
        Self(text, world, god);
        Gods(text, session, god);
        Villages(text, session, god, rules.VillagesShown);
        Sites(text, world);
        Waiting(text, world, god);
        Aims(text, world, god);
        Miracles(text, session, god);
        Instincts(text, session, god);
        return text.ToString();
    }

    /// <summary>The god's standing and its Creature.</summary>
    private static void Self(StringBuilder text, WorldState world, GodState god)
    {
        var people = Math.Max(1, world.Settlements.Sum(s => s.Population));
        var followers = GodStanding.CountFollowers(world, god.Id);
        var domains = god.Domains.Count > 0 ? "; your domains: " + string.Join(", ", god.Domains) : string.Empty;
        text.Append(Format("You: {0}, {1}, with {2:0} Divine Power and {3} followers ({4:0%} of the island's people){5}.\n", god.Name, DivineText.Rank(god.Rank), god.DivinePower, followers, followers / (float)people, domains));
        if (world.Creatures.Find(c => c.Id == god.CreatureId) is not { } creature)
        {
            text.Append("You have no Creature.\n");
            return;
        }

        var needs = (creature.Needs.Hunger >= 0.6f ? ", hungry" : string.Empty) + (creature.Needs.Fatigue >= 0.6f ? ", tired" : string.Empty);
        text.Append(Format("Your Creature: {0}, a {1} of level {2}: {3}, near {4}; health {5:0%}{6}.\n", creature.Name, creature.Species.ToLowerInvariant(), creature.Level, CreatureText.DescribeActivity(creature, world).ToLowerInvariant(), Nearest(world, creature.Position), creature.Health, needs));
    }

    /// <summary>The gods it knows, and how many it does not.</summary>
    private static void Gods(StringBuilder text, GameSession session, GodState god)
    {
        var world = session.World;
        var people = Math.Max(1, world.Settlements.Sum(s => s.Population));
        var unknown = 0;
        text.Append("\nThe gods you know:\n");
        foreach (var other in world.Gods.Where(g => g.Id != god.Id))
        {
            if (!Acquaintance.Knows(session.Content.Diplomacy.Acquaintance, world, god.Id, other.Id))
            {
                unknown++;
                continue;
            }

            var nature = DivineText.Personality(other.Personality) is { Length: > 0 } personality ? Format(" (the {0})", personality) : string.Empty;
            var creature = world.Creatures.Find(c => c.Id == other.CreatureId) is { } its && its.Activity.Phase != CreaturePhase.Banished
                ? Format("; its Creature {0} is near {1}", its.Name, Nearest(world, its.Position))
                : string.Empty;
            var ties = DiplomacyText.Ties(world, other.Id, god.Id) is { Length: > 0 } t ? "; " + t.TrimEnd('.') : string.Empty;
            text.Append(Format("- {0}{1}: {2} with you; {3}; it holds {4:0%} of the people{5}{6}.\n", other.Name, nature,
                DiplomacyText.Status(DiplomacyModel.StatusOf(world, god.Id, other.Id)).ToLowerInvariant(), MindWords.Stance(world, god.Id, other.Id, other.Name),
                GodStanding.CountFollowers(world, other.Id) / (float)people, ties, creature));
        }

        if (unknown > 0)
        {
            text.Append(Format("- and {0} god{1} you have not met.\n", unknown, unknown == 1 ? string.Empty : "s"));
        }
    }

    /// <summary>The villages that concern it most: its own, those it reaches, then the nearest to its home.</summary>
    private static void Villages(StringBuilder text, GameSession session, GodState god, int shown)
    {
        var world = session.World;
        var field = new InfluenceCache(session.Content.DivineRules, world).For(god.Id);
        var home = GodPlanner.Home(world, god.Id)?.Position ?? world.Creatures.Find(c => c.Id == god.CreatureId)?.Position ?? default;
        var rows = world.Settlements.Where(s => s.Population > 0).Select(s =>
        {
            var mine = BeliefModel.Find(s, god.Id)?.Adherence ?? 0f;
            var reach = field.At(s.Position);
            var leads = TreatyModel.Leader(s) == god.Id;
            var concern = (leads ? 3f : 0f) + (2f * mine) + (reach >= 0.1f ? 1f : 0f) - (Distance(home, s.Position) / 2000f);
            return (Village: s, Mine: mine, Reach: reach, Concern: concern);
        }).OrderByDescending(r => r.Concern).Take(shown).ToList();
        text.Append("\nVillages (those that concern you most):\n");
        foreach (var (village, mine, reach, _) in rows)
        {
            var leader = TreatyModel.Leader(village) is { } id ? world.Gods.Find(g => g.Id == id)?.Name ?? "a god" : "no god";
            var trouble = (village.Plague > 0.1f ? "; plague" : string.Empty) + (village.Integrity < 0.7f ? "; damaged" : string.Empty);
            text.Append(Format("- {0}: {1} people, following {2}; {3:0%} believe in you; {4}{5}.\n", village.Name, village.Population, leader == god.Name ? "you" : leader, mine, reach >= 0.1f ? "within your reach" : "beyond your reach", trouble));
        }
    }

    /// <summary>The holy sites and their keepers.</summary>
    private static void Sites(StringBuilder text, WorldState world)
    {
        if (world.HolySites.Count == 0)
        {
            return;
        }

        text.Append("\nHoly sites:\n");
        foreach (var site in world.HolySites)
        {
            var keeper = site.KeeperGodId is { } id ? world.Gods.Find(g => g.Id == id)?.Name ?? "a god" : "no one";
            text.Append(Format("- {0}: kept by {1}.\n", site.Name, keeper));
        }
    }

    /// <summary>The proposals waiting for its answer.</summary>
    private static void Waiting(StringBuilder text, WorldState world, GodState god)
    {
        var waiting = world.Diplomacy.Proposals.Where(p => p.ToGodId == god.Id).ToList();
        if (waiting.Count == 0)
        {
            return;
        }

        text.Append("\nProposals waiting for your answer (accept or decline them by the god's name):\n");
        foreach (var proposal in waiting)
        {
            var what = proposal.Kind switch
            {
                Domain.Diplomacy.ProposalKind.Alliance => "an alliance",
                Domain.Diplomacy.ProposalKind.Peace => "peace",
                Domain.Diplomacy.ProposalKind.Treaty => proposal.Terms is { } terms ? "a " + Politics.PoliticsText.Name(terms.Kind) : "a treaty",
                Domain.Diplomacy.ProposalKind.Request => proposal.Ask is { } ask ? Politics.PoliticsText.Ask(ask, world) : "a favour",
                Domain.Diplomacy.ProposalKind.Surrender => "that you surrender",
            };
            text.Append(Format("- {0} proposes {1}.\n", world.Gods.Find(g => g.Id == proposal.FromGodId)?.Name ?? "a god", what));
        }
    }

    /// <summary>Its aims now.</summary>
    private static void Aims(StringBuilder text, WorldState world, GodState god)
    {
        text.Append(god.Goals.Count == 0 ? "\nYour aims now: none.\n" : "\nYour aims now: " + string.Join("; ", god.Goals.Select(g => GoalText.Goal(g, world, null))) + ".\n");
    }

    /// <summary>The miracles it may afford now, of its rank.</summary>
    private static void Miracles(StringBuilder text, GameSession session, GodState god)
    {
        var castable = session.Content.Miracles.Miracles
            .Where(m => m.Scale != Domain.Miracles.MiracleScale.DivineAct && m.MinimumRank <= god.Rank && m.PowerCost <= god.DivinePower)
            .Select(m => Format("{0} ({1}, {2:0} power, {3})", m.Id, m.Name, m.PowerCost, Simulation.Culture.CultureModel.Harms(m) ? "harms" : "helps"))
            .ToList();
        text.Append(castable.Count == 0 ? "\nYou cannot afford a miracle now.\n" : "\nMiracles you can afford (id, name, cost; on a village within your reach): " + string.Join("; ", castable) + ".\n");
    }

    /// <summary>What its instincts would do: the utility AI's best few moves and aims.</summary>
    private static void Instincts(StringBuilder text, GameSession session, GodState god)
    {
        if (session.Engine.Gods?.Ai is not { } ai || ai.ProfileOf(god) is not { } profile)
        {
            return;
        }

        var world = session.World;
        var lines = new List<string>();
        foreach (var candidate in ai.Evaluate(world, god).Where(c => c.Kind != AiActionKind.Wait).Take(3))
        {
            var place = world.Settlements.Find(s => s.Id == candidate.SettlementId)?.Name ?? Nearest(world, candidate.Target);
            lines.Add(candidate.Kind == AiActionKind.CastMiracle ? Format("cast {0} on {1} ({2})", candidate.MiracleId, place, candidate.Reason) : Format("{0} ({1})", candidate.Kind == AiActionKind.SetCreatureOn ? "set your Creature on a rival" : "send your Creature to " + place, candidate.Reason));
        }

        foreach (var move in ai.EvaluateDiplomacy(world, god).Where(m => m.Utility > 0f).Take(2))
        {
            lines.Add(Format("{0} {1} ({2})", DiplomacyText.Command(move.Kind).ToLowerInvariant(), world.Gods.Find(g => g.Id == move.OtherGodId)?.Name ?? "a god", move.Reason));
        }

        foreach (var option in ai.Planner.Options(world, god, profile).Take(2))
        {
            lines.Add("aim to " + option.Reason);
        }

        if (lines.Count > 0)
        {
            text.Append("\nYour instincts would: " + string.Join("; ", lines) + ".\n");
        }
    }

    /// <summary>The name of the village nearest a place.</summary>
    internal static string Nearest(WorldState world, GroundPosition place) =>
        world.Settlements.Where(s => s.Population > 0).MinBy(s => Distance(s.Position, place))?.Name ?? "the wilds";

    /// <summary>The distance between two places, in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
