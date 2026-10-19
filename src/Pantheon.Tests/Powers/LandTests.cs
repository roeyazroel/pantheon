using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Land;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.History;
using Pantheon.Game.Land;
using Pantheon.Game.Launch;
using Pantheon.Simulation.Land;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>The Divine Acts that reshape the land (spec §14, ADR-045): an island, a split mountain and a river's new course.</summary>
public sealed class LandTests
{
    private const int Side = 65;
    private const float Spacing = 4f;

    [Fact]
    public void TheReshaper_RaisesAnIslandFromTheSea_CutsAGorgeToItsFloor_AndCarvesAFallingChannelBehindADam()
    {
        var sea = Flat(-20f);
        Reshape(sea, Shape(ReshapeKind.Island, 128f, 128f, 128f, 128f, radius: 40f, peak: 10f));
        var mountain = Flat(50f);
        Reshape(mountain, Shape(ReshapeKind.Gorge, 20f, 128f, 236f, 128f, radius: 20f, from: 10f, to: 5f));
        var plain = Flat(20f);
        Reshape(plain, Shape(ReshapeKind.Channel, 20f, 128f, 236f, 128f, radius: 10f, peak: 3f, from: 20f, to: 8f, dam: (20f, 172f, 26f)));

        Assert.Equal(10f, At(sea, 128f, 128f), 3);
        Assert.True(At(sea, 128f, 148f) > 0f);
        Assert.Equal(-20f, At(sea, 128f, 252f));
        Assert.Equal(7.5f, At(mountain, 128f, 128f), 2);
        Assert.Equal(50f, At(mountain, 128f, 160f));
        Assert.True(mountain.Max() <= 50f);
        Assert.Equal((17f, 7f), (At(plain, 20f, 128f), At(plain, 236f, 128f)));
        Assert.True(At(plain, 128f, 128f) < 13f);
        Assert.Equal(26f, At(plain, 20f, 172f), 3);
    }

    [Fact]
    public void TheReshapedTerrain_IsTheSameAsMakingItAgainFromTheSeed_WithEveryReshaping()
    {
        var (session, terrain) = Start();
        var world = session.World;

        terrain = LandShaper.Apply(world, terrain, Lands.Find(world, terrain, ReshapeKind.Island, world.Settlements[0].Position)!);
        terrain = LandShaper.Apply(world, terrain, Lands.Find(world, terrain, ReshapeKind.Gorge, world.Settlements[0].Position)!);
        var again = TerrainGenerator.Generate(world.Seed, world.Generation, world.Land.Reshapes);

        Assert.Equal(2, world.Land.Reshapes.Count);
        Assert.Equal(3, world.Land.NextReshapeId);
        Assert.True(again.Heights.SequenceEqual(terrain.Heights));
        Assert.True(again.Biomes.SequenceEqual(terrain.Biomes));
        Assert.True(again.Ground.SequenceEqual(TerrainGenerator.Generate(world.Seed, world.Generation).Ground));
    }

    [Fact]
    public void AnIsland_TurnsTheSeaIntoNewLand_WithSoilPlantsAndRivers_KeepingTheirLevels()
    {
        var (session, terrain) = Start();
        var world = session.World;
        var island = Lands.Find(world, terrain, ReshapeKind.Island, world.Settlements[0].Position)!;
        world.Regions.TryGetRegionIdAt(island.X, island.Z, out var centre);
        world.Water.Rivers[0].Level = 0.9f;
        var name = world.Water.Rivers[0].Name;
        var land = world.Regions.Cells.Count(r => r.Biome != Biome.Sea);

        LandShaper.Apply(world, terrain, island);

        var risen = world.Regions.Cells[centre];
        Assert.NotEqual(Biome.Sea, risen.Biome);
        Assert.True(risen.Elevation > 0f && risen.Fertility > 0f && risen.SoilMoisture > 0f);
        Assert.True(world.Regions.Cells.Count(r => r.Biome != Biome.Sea) > land);
        Assert.Equal(0.9f, world.Water.Rivers.Single(r => r.Name == name).Level);
    }

    [Fact]
    public void TheActs_AreRefused_WhereTheyCannotBeDone()
    {
        var (session, terrain) = Start();
        var world = session.World;
        var village = world.Settlements[0].Position;
        var peak = Peak(terrain);

        Assert.Null(LandShaper.Plan(Lands.Rules, world, terrain, ReshapeKind.Island, village, 1));
        Assert.Null(LandShaper.Plan(Lands.Rules, world, terrain, ReshapeKind.Gorge, Lowland(terrain), 1));
        Assert.Null(LandShaper.Plan(Lands.Rules, world, terrain, ReshapeKind.Channel, peak, 1));
        Assert.Null(LandShaper.Plan(Lands.Rules, world, terrain, ReshapeKind.Channel, Lands.Find(world, terrain, ReshapeKind.Island, village)!.Position(), 1));
        Assert.Null(LandShaper.Plan(LandRules.None, world, terrain, ReshapeKind.Island, Lands.Find(world, terrain, ReshapeKind.Island, village)!.Position(), 1));
    }

    [Fact]
    public void ATurnedRiver_RunsDownToWhereTheGodPointed_AndTheRegionThereGainsFreshWater()
    {
        var (session, terrain) = Start();
        var world = session.World;
        var channel = Enumerable.Range(0, world.Settlements.Count).Select(i => Lands.Find(world, terrain, ReshapeKind.Channel, world.Settlements[i].Position)).First(c => c is not null)!;
        var mouth = new GroundPosition(channel.ToX, channel.ToZ);

        var turned = LandShaper.Apply(world, terrain, channel);

        var nearest = turned.Hydrology.Rivers.SelectMany(r => r.Points).Min(p => MathF.Sqrt(((p.X - mouth.X) * (p.X - mouth.X)) + ((p.Z - mouth.Z) * (p.Z - mouth.Z))));
        Assert.True(nearest < 3f * Lands.Rules.ChannelWidth, $"the nearest river point is {nearest:0} m from where the god pointed");
        Assert.True(channel.DamTop > channel.FromHeight);
        Assert.True(turned.SampleHeight(channel.DamX, channel.DamZ) > terrain.SampleHeight(channel.DamX, channel.DamZ));
    }

    [Fact]
    public void CastingAnAct_ReshapesTheLand_MakesTheTerrainAgain_AndEntersTheHistory_ButNotOnLand()
    {
        var (session, terrain) = Start();
        var god = session.LocalGod!;
        (god.Rank, god.DivinePower) = (DivineRank.GreaterGod, 10_000f);
        var target = Lands.Find(session.World, terrain, ReshapeKind.Island, session.World.Settlements[0].Position)!.Position();
        var changed = 0;
        session.TerrainChanged += () => changed++;

        var refused = SessionCommands.CastMiracle(session, "raise_island", session.World.Settlements[0].Position);
        var cast = SessionCommands.CastMiracle(session, "raise_island", target);

        Assert.Contains("an island rises only from the open sea", refused, StringComparison.Ordinal);
        Assert.StartsWith("Raise an Island", cast, StringComparison.Ordinal);
        Assert.Equal(1, changed);
        Assert.NotSame(terrain, session.Terrain);
        Assert.Contains(session.World.History, h => h.Kind == HistoryKind.Raising && h.GodId == god.Id);
        var act = Assert.Single(session.DrainEvents().OfType<DivineActDone>());
        Assert.StartsWith("You raise an island from the sea off ", new AlertFeed().Push([act], session.World, god.Id, 0).Single().Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AJoinedPlayer_MakesItsTerrainAgain_WhenTheHostReshapesTheLand()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        var (where, said) = LandCommands.Reshape(shared.Host, ReshapeKind.Island, shared.Host.World.Settlements[0].Position);
        shared.Pass(3.0, ana);

        Assert.NotNull(where);
        Assert.StartsWith("An island made at", said, StringComparison.Ordinal);
        Assert.Single(ana.Session!.World.Land.Reshapes);
        Assert.True(ana.Session.Terrain.Heights.SequenceEqual(shared.Host.Terrain.Heights));
    }

    [Fact]
    public void TheOption_F1_AndTheHistory_TellOfTheReshapedLand()
    {
        var (session, _) = Start();
        var (where, _) = LandCommands.Reshape(session, ReshapeKind.Gorge, session.World.Settlements[0].Position);
        var lines = new List<string>();

        LandReadout.Append(lines, new DebugSnapshot
        {
            FramesPerSecond = 60, World = session.World, SettlementRules = session.Content.SettlementRules, DivineRules = session.Content.DivineRules,
            IsPaused = false, TimeScale = 1, TicksPerSecond = 10, TerrainBackend = "Terrain3D", TerrainIsFallback = false, ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 300f, RegionBoundariesVisible = false,
        });

        Assert.NotNull(where);
        Assert.StartsWith($"Land: 1 reshaped: a gorge through a mountain by {session.LocalGod!.Name} at (", Assert.Single(lines), StringComparison.Ordinal);
        Assert.StartsWith($"{session.LocalGod.Name} splits the mountain above ", HistoryText.Title(session.World.History.Last(h => h.Kind == HistoryKind.Splitting), session.World), StringComparison.Ordinal);
        Assert.Equal(ReshapeKind.Channel, LaunchOptions.Parse(["--reshape=river"]).Reshape);
        Assert.Single(LaunchOptions.Parse(["--reshape=volcano"]).Warnings);
    }

    /// <summary>A small world with rivers, and its terrain.</summary>
    private static (GameSession Session, TerrainField Terrain) Start()
    {
        var session = GameSession.CreateNew(TestContent.Small with { WorldGen = TestContent.RiverWorldGen }, new WorldSeed(42));
        return (session, session.Terrain);
    }

    /// <summary>Flat heights.</summary>
    private static float[] Flat(float height) => Enumerable.Repeat(height, Side * Side).ToArray();

    /// <summary>Applies one reshaping to a test grid.</summary>
    private static void Reshape(float[] heights, TerrainReshape reshape) =>
        typeof(TerrainField).Assembly.GetType("Pantheon.Simulation.Terrain.TerrainReshaper")!
            .GetMethod("Apply")!.Invoke(null, [new List<TerrainReshape> { reshape }, Side, Spacing, heights]);

    /// <summary>A test grid's height at a point.</summary>
    private static float At(float[] heights, float x, float z) => heights[((int)(z / Spacing) * Side) + (int)(x / Spacing)];

    /// <summary>A reshaping for the test grid.</summary>
    private static TerrainReshape Shape(ReshapeKind kind, float x, float z, float toX, float toZ, float radius, float peak = 0f, float from = 0f, float to = 0f, (float X, float Z, float Top)? dam = null) => new()
    {
        Id = 1, Kind = kind, GodId = 1, Tick = 0, X = x, Z = z, ToX = toX, ToZ = toZ, Radius = radius, Peak = peak, FromHeight = from, ToHeight = to,
        DamX = dam?.X ?? 0f, DamZ = dam?.Z ?? 0f, DamTop = dam?.Top ?? 0f,
    };

    /// <summary>The highest point of the ground.</summary>
    private static GroundPosition Peak(TerrainField terrain)
    {
        var ground = terrain.Ground;
        var best = 0;
        for (var i = 1; i < ground.Length; i++)
        {
            best = ground[i] > ground[best] ? i : best;
        }

        return new GroundPosition(best % terrain.Resolution * terrain.Spacing, best / terrain.Resolution * terrain.Spacing);
    }

    /// <summary>A point of low dry ground.</summary>
    private static GroundPosition Lowland(TerrainField terrain)
    {
        var ground = terrain.Ground;
        for (var i = 0; i < ground.Length; i++)
        {
            if (ground[i] is > 2f and < 6f)
            {
                return new GroundPosition(i % terrain.Resolution * terrain.Spacing, i / terrain.Resolution * terrain.Spacing);
            }
        }

        throw new InvalidOperationException("No low ground.");
    }
}

/// <summary>Where a reshaping was aimed.</summary>
internal static class ReshapeExtensions
{
    /// <summary>The point the god pointed at: an island's centre, or a gorge's or channel's end.</summary>
    /// <param name="reshape">The reshaping.</param>
    /// <returns>The point.</returns>
    public static GroundPosition Position(this TerrainReshape reshape) =>
        reshape.Kind == ReshapeKind.Island ? new GroundPosition(reshape.X, reshape.Z) : new GroundPosition((reshape.X + reshape.ToX) / 2f, (reshape.Z + reshape.ToZ) / 2f);
}
