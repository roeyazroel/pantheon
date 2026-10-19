using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Culture;
using Pantheon.Game.Disasters;
using Pantheon.Game.Divine;
using Pantheon.Game.Domains;
using Pantheon.Game.Flow;
using Pantheon.Game.Weather;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Book;

/// <summary>The book's World page (spec §57): the world, its gods, the race for victory, its dangers and its holy sites. Pure.</summary>
public static class WorldPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var content = session.Content;
        var people = world.Settlements.Sum(s => Math.Max(0, s.Population));
        var lines = new List<BookLine>
        {
            new("The world", BookTone.Heading),
            new(Format("Seed {0}, an island {1:0} m across, won by {2}.", world.Seed.Value, world.Regions.Columns * world.Regions.RegionSize, VictoryText.Settings(world.Victory).ToLowerInvariant())),
            new("The land: " + Presentation.BiomeText.Census(world.Regions) + ".", BookTone.Muted),
            new(Format("{0} played. {1}{2}", DivineText.Clock(world.Tick / (double)SimulationTime.TicksPerSecond), SeasonText.Describe(content.Weather, world) is { } season ? season + ". " : string.Empty, WeatherText.Describe(world.Weather, world.Tick))),
            new(Format(
                "{0} people in {1} villages; {2} follow no god; {3} on the road.",
                people,
                world.Settlements.Count(s => s.Population > 0),
                world.Settlements.Sum(s => (int)MathF.Round(Math.Max(0, s.Population) * Math.Max(0f, 1f - BeliefModel.TotalAdherence(s.Beliefs)))),
                world.Migration.Groups.Sum(g => g.Population))),
            new(Resources.ResourceText.World(world)),
            new("The gods", BookTone.Heading),
        };
        foreach (var god in world.Gods.OrderByDescending(g => GodStanding.CountFollowers(world, g.Id)).ThenBy(g => g.Id))
        {
            lines.AddRange(Describe(session, god, people));
        }

        lines.Add(new("The race", BookTone.Heading));
        lines.AddRange(Race(session));
        lines.Add(new("Great events", BookTone.Heading));
        lines.AddRange(world.GreatEvents.Events.Count == 0
            ? [new BookLine("No great event is under way.", BookTone.Muted)]
            : world.GreatEvents.Events.Select(e => new BookLine(GreatEvents.GreatEventText.Describe(e, world), BookTone.Warning)));
        lines.Add(new("Prophets and elders", BookTone.Heading));
        lines.AddRange(NotableSection.World(world, session.LocalGodId));
        lines.Add(new("Dangers", BookTone.Heading));
        lines.Add(new(Dangers(world)));
        lines.Add(new("Holy sites", BookTone.Heading));
        lines.AddRange(world.HolySites.Select(site => new BookLine(Format(
            "{0}: {1}",
            DomainText.SiteName(site),
            site.KeeperGodId is { } keeper ? "kept by " + GodName(world, keeper) : "unclaimed"))));
        return new BookPage(BookTab.World, "The world", lines);
    }

    /// <summary>A god's lines: standing, power, domains, how it is seen, its ties and its Creature.</summary>
    private static IEnumerable<BookLine> Describe(GameSession session, GodState god, int people)
    {
        var world = session.World;
        var mine = god.Id == session.LocalGodId;
        var followers = GodStanding.CountFollowers(world, god.Id);
        var domains = god.Domains.Count == 0 ? "no domain" : string.Join(", ", god.Domains.Select(DomainText.Name));
        var seen = god.Traits.Count == 0 ? string.Empty : "; seen as " + string.Join(", ", god.Traits.Select(CultureText.Name));
        var name = DivineText.Personality(god.Personality) is { Length: > 0 } personality ? god.Name + " the " + personality : god.Name;
        yield return new(
            Format("{0}{1}: {2}, {3} follower{4} ({5:0%} of all), {6:0} power; {7}{8}", name, mine ? " (you)" : string.Empty, DivineText.Rank(god.Rank), followers, followers == 1 ? string.Empty : "s", people > 0 ? followers / (float)people : 0f, god.DivinePower, domains, seen),
            mine ? BookTone.Good : BookTone.Plain);
        var allies = world.Gods.Where(o => DiplomacyModel.AreAllied(world, god.Id, o.Id)).Select(o => o.Name).ToList();
        var wars = world.Gods.Where(o => DiplomacyModel.AreAtWar(world, god.Id, o.Id)).Select(o => o.Name).ToList();
        if (allies.Count + wars.Count > 0)
        {
            yield return new("   " + string.Join("; ", new[] { allies.Count > 0 ? "allied with " + string.Join(", ", allies) : null, wars.Count > 0 ? "at war with " + string.Join(", ", wars) : null }.Where(t => t is not null)));
        }

        if (world.Creatures.Find(c => c.GodId == god.Id) is { } creature)
        {
            var paths = creature.Specialisations.Count == 0 ? string.Empty : ", " + string.Join(", ", creature.Specialisations.Select(CreatureText.Path));
            yield return new(Format("   its Creature {0}, level {1}{2}", creature.Name, creature.Level, paths), BookTone.Muted);
        }
    }

    /// <summary>Who has won, or how far each god has held a lead towards a victory.</summary>
    private static IEnumerable<BookLine> Race(GameSession session)
    {
        var world = session.World;
        if (world.WinnerGodIds.Count > 0)
        {
            yield return new("Won by " + DivineText.Names(world, world.WinnerGodIds, session.LocalGodId) + ".", BookTone.Good);
            yield break;
        }

        var needed = session.Content.DivineRules.VictorySeconds;
        var any = false;
        foreach (var god in world.Gods)
        {
            foreach (var (since, kind) in new[] { (god.DominantSinceTick, "dominance"), (god.ConquestSinceTick, "conquest"), (god.CulturalSinceTick, "cultural lead") })
            {
                if (since is { } tick)
                {
                    any = true;
                    yield return new(Format("{0} has held its {1} for {2} of {3}.", god.Name, kind, DivineText.Clock((world.Tick - tick) / (double)SimulationTime.TicksPerSecond), DivineText.Clock(needed)), god.Id == session.LocalGodId ? BookTone.Good : BookTone.Warning);
                }
            }
        }

        if (!any)
        {
            yield return new("No god holds a lead towards victory yet.");
        }
    }

    /// <summary>The disasters and hazards under way.</summary>
    private static string Dangers(WorldState world)
    {
        var d = world.Disasters;
        var volcano = d.Volcano is { Phase: not VolcanoPhase.Dormant } awake
            ? Format("; {0} {1}", DisasterText.VolcanoName(world, awake.RegionId), awake.Phase == VolcanoPhase.Erupting ? "erupts" : "rumbles")
            : string.Empty;
        return Format(
            "{0} earthquakes, {1} regions burning, {2} flooded, {3} blighted; {4} tornadoes and {5} locust swarms{6}.",
            d.Earthquakes.Count,
            world.Regions.Cells.Count(r => r.Fire > 0f),
            world.Regions.Cells.Count(r => r.Flood > 0.02f),
            world.Regions.Cells.Count(r => r.Blight >= DisasterText.BlightShown),
            d.Tornadoes.Count,
            d.Swarms.Count,
            volcano);
    }

    /// <summary>A god's name.</summary>
    private static string GodName(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a forgotten god";

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
