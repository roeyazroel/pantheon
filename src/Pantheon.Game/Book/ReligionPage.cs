using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Book;

/// <summary>The book's Religion page (spec §57): the player's faith village by village, every god's faith, the Old Faiths and the godless. Pure.</summary>
public static class ReligionPage
{
    // Doubt from which a village's faith is shown as failing.
    private const float Failing = 0.4f;

    /// <summary>Builds the page.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var lines = new List<BookLine>();
        if (session.LocalGod is { } god)
        {
            var feeling = Feelings(world, god.Id);
            lines.Add(new("Your faith", BookTone.Heading));
            lines.Add(new(Format(
                "{0} follow you in {1} villages, and you lead {2}. Among them: faith {3:0.00}, love {4:0.00}, fear {5:0.00}, doubt {6:0.00}, dependency {7:0.00}, resentment {8:0.00}.",
                GodStanding.CountFollowers(world, god.Id),
                world.Settlements.Count(s => BeliefModel.Find(s, god.Id) is { Adherence: > 0.01f }),
                world.Settlements.Count(s => MigrationModel.MainGod(s.Beliefs) == god.Id),
                feeling.Faith,
                feeling.Love,
                feeling.Fear,
                feeling.Doubt,
                feeling.Dependency,
                feeling.Resentment)));
            lines.Add(new("Your villages", BookTone.Heading));
            foreach (var settlement in world.Settlements.Where(s => s.Population > 0 && BeliefModel.Find(s, god.Id) is { Adherence: > 0.01f })
                .OrderByDescending(s => GodStanding.CountFollowers(s, god.Id)).ThenBy(s => s.Id))
            {
                var belief = BeliefModel.Find(settlement, god.Id)!;
                var leader = MigrationModel.MainGod(settlement.Beliefs) is { } main && main != god.Id ? "; led by " + GodName(world, main) : string.Empty;
                lines.Add(new(
                    Format("{0}: {1:0%} of {2}; faith {3:0.00}, love {4:0.00}, fear {5:0.00}, doubt {6:0.00}{7}", settlement.Name, belief.Adherence, settlement.Population, belief.Faith, belief.Love, belief.Fear, belief.Doubt, leader),
                    belief.Doubt >= Failing ? BookTone.Warning : BookTone.Plain));
            }

            lines.Add(new("Your prophets and elders", BookTone.Heading));
            lines.AddRange(NotableSection.Yours(world, god.Id));
        }

        lines.Add(new("Every god's faith", BookTone.Heading));
        foreach (var other in world.Gods.OrderBy(g => g.Id))
        {
            var feeling = Feelings(world, other.Id);
            lines.Add(new(
                Format("{0}: {1}, leads {2}; faith {3:0.00}, love {4:0.00}, fear {5:0.00}, doubt {6:0.00}", other.Name, Count(GodStanding.CountFollowers(world, other.Id), "follower"), Count(world.Settlements.Count(s => MigrationModel.MainGod(s.Beliefs) == other.Id), "village"), feeling.Faith, feeling.Love, feeling.Fear, feeling.Doubt),
                other.Id == session.LocalGodId ? BookTone.Good : BookTone.Plain));
        }

        lines.Add(new("Old Faiths and the godless", BookTone.Heading));
        var quiet = true;
        foreach (var settlement in world.Settlements.Where(s => s.Population > 0).OrderBy(s => s.Id))
        {
            var godless = Math.Max(0f, 1f - BeliefModel.TotalAdherence(settlement.Beliefs));
            if (settlement.OldFaith is { } old)
            {
                quiet = false;
                lines.Add(new(Format("{0}: {1:0%} keep the Old Faith of {2}", settlement.Name, old.Share, GodName(world, old.GodId)), BookTone.Warning));
            }

            if (godless >= 0.5f)
            {
                quiet = false;
                lines.Add(new(Format("{0}: {1:0%} follow no god", settlement.Name, godless), BookTone.Muted));
            }
        }

        if (quiet)
        {
            lines.Add(new("No village keeps an Old Faith, and none has turned from the gods."));
        }

        return new BookPage(BookTab.Religion, "Religion", lines);
    }

    /// <summary>A god's followers' mean feelings, weighted by how many follow it in each village.</summary>
    private static (float Faith, float Love, float Fear, float Doubt, float Dependency, float Resentment) Feelings(WorldState world, int godId)
    {
        var (weight, faith, love, fear, doubt, dependency, resentment) = (0f, 0f, 0f, 0f, 0f, 0f, 0f);
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is not GodBelief belief)
            {
                continue;
            }

            var w = Math.Max(0, settlement.Population) * belief.Adherence;
            (weight, faith, love, fear, doubt) = (weight + w, faith + (w * belief.Faith), love + (w * belief.Love), fear + (w * belief.Fear), doubt + (w * belief.Doubt));
            (dependency, resentment) = (dependency + (w * belief.Dependency), resentment + (w * belief.Resentment));
        }

        return weight <= 0f ? default : (faith / weight, love / weight, fear / weight, doubt / weight, dependency / weight, resentment / weight);
    }

    /// <summary>A count with its noun, singular for one.</summary>
    private static string Count(int count, string noun) => Format("{0} {1}{2}", count, noun, count == 1 ? string.Empty : "s");

    /// <summary>A god's name.</summary>
    private static string GodName(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
