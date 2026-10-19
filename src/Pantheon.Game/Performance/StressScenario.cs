using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Disasters;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Performance;

/// <summary>
/// The repeatable stress scenario of spec §104 (ADR-044): the vast island with 50 villages of 2,000
/// people (100,000 in all), its 8 gods and their Creatures, 20 weather cells and three disasters at
/// once (an earthquake, a fire and a flood), and the crowd drawn in thousands near the camera.
/// Deterministic: the same seed makes the same stress world.
/// </summary>
public static class StressScenario
{
    /// <summary>The world size the scenario adds to the content.</summary>
    public const string SizeName = "Stress";

    /// <summary>The villages.</summary>
    public const int Settlements = 50;

    /// <summary>The people in all.</summary>
    public const int People = 100_000;

    /// <summary>The weather cells.</summary>
    public const int WeatherCells = 20;

    /// <summary>How close, in metres, the camera looks at the busiest villages.</summary>
    public const float CameraDistance = 700f;

    // The most figures a village shows near the camera, and how near, in metres, it must be.
    private const int FiguresPerVillage = 1000;
    private const float CrowdDistance = 1300f;

    // A weather cell's radius and pressure: wet, but short of a storm.
    private const float CellRadius = 260f;
    private const float CellPressure = 0.45f;

    /// <summary>The content with the stress world size: the vast island with 50 villages.</summary>
    /// <param name="content">The shipped content.</param>
    /// <returns>The content to make the stress world from.</returns>
    public static GameContent Content(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var largest = content.Sizes.Sizes.OrderByDescending(s => s.RegionsPerSide * s.VertexSpacing).ThenByDescending(s => s.Settlements).First();
        var stress = largest with { Name = SizeName, Settlements = Settlements, Gods = Math.Max(largest.Gods, 8) };
        return content with { Sizes = content.Sizes with { Sizes = [.. content.Sizes.Sizes, stress] } };
    }

    /// <summary>The presentation with the crowd drawn in thousands: more figures per village, and from farther away.</summary>
    /// <param name="presentation">The presentation settings.</param>
    /// <returns>The stress run's presentation settings.</returns>
    public static PresentationContent Presentation(PresentationContent presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var citizens = presentation.SettlementView.Citizens with
        {
            MaxRepresentatives = FiguresPerVillage,
            RegionalTierEnterDistance = CrowdDistance,
            RegionalTierExitDistance = CrowdDistance + 200f,
        };
        return presentation with { SettlementView = presentation.SettlementView with { Citizens = citizens } };
    }

    /// <summary>Fills a new stress world: the people, the weather cells and the disasters.</summary>
    /// <param name="session">A session made from <see cref="Content"/> at the stress size.</param>
    /// <returns>What the world holds, as a status line.</returns>
    public static string Apply(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        var each = People / Math.Max(1, world.Settlements.Count);
        foreach (var settlement in world.Settlements)
        {
            (settlement.Population, settlement.Housing) = (each, each + (each / 10));
            settlement.Food = Math.Max(settlement.Food, each * 2f);
            settlement.Water = Math.Max(settlement.Water, each);
        }

        AddCells(session);
        var places = world.Settlements.OrderBy(s => s.Id).Select(s => s.Position).ToList();
        if (places.Count >= 3)
        {
            DisasterCommands.StartQuake(session, places[0]);
            DisasterCommands.StartFire(session, places[places.Count / 2]);
            DisasterCommands.StartFlood(session, places[^1]);
        }

        return Describe(world);
    }

    /// <summary>The village with the most others near it, where the crowd is thickest.</summary>
    /// <param name="world">World state.</param>
    /// <returns>Where to look.</returns>
    public static GroundPosition Busiest(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Settlements
            .OrderByDescending(s => world.Settlements.Count(o => Distance(o.Position, s.Position) <= CrowdDistance))
            .ThenBy(s => s.Id)
            .Select(s => s.Position)
            .FirstOrDefault();
    }

    /// <summary>What the stress world holds.</summary>
    /// <param name="world">World state.</param>
    /// <returns>For example "Stress world: 50 settlements, 100000 people, ...".</returns>
    public static string Describe(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Stress world: {0} settlements, {1} people, {2} gods, {3} Creatures, {4} weather cells, {5} earthquakes, {6} regions burning, {7} flooded",
            world.Settlements.Count,
            world.Settlements.Sum(s => s.Population),
            world.Gods.Count,
            world.Creatures.Count,
            world.Weather.Cells.Count,
            world.Disasters.Earthquakes.Count,
            world.Regions.Cells.Count(r => r.Fire > 0f),
            world.Regions.Cells.Count(r => r.Flood > 0f));
    }

    /// <summary>Adds wet cells over the villages until there are <see cref="WeatherCells"/>.</summary>
    private static void AddCells(GameSession session)
    {
        var world = session.World;
        var rules = session.Content.Weather;
        var weather = world.Weather;
        var (windX, windZ) = WeatherModel.WindVector(weather);
        for (var i = 0; weather.Cells.Count < WeatherCells && world.Settlements.Count > 0; i++)
        {
            var at = world.Settlements[(i * 7) % world.Settlements.Count].Position;
            var cell = new WeatherCell
            {
                Id = weather.NextCellId++,
                X = at.X,
                Z = at.Z,
                VelocityX = windX * rules.CellSpeed,
                VelocityZ = windZ * rules.CellSpeed,
                Radius = CellRadius,
                Pressure = CellPressure,
                Temperature = WeatherModel.GroundTemperature(world.Regions, at.X, at.Z, 0.6f),
                Humidity = 0.9f,
                Precipitation = 0f,
                Wind = 0f,
                ElectricalActivity = 0f,
                SummonedByGodId = null,
            };
            WeatherModel.Refresh(rules, cell);
            weather.Cells.Add(cell);
        }
    }

    /// <summary>Metres between two points on the ground.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
