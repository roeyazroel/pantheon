using System;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Water;

/// <summary>Rivers and lakes found and cut into the land when a world is generated (spec §5, §93, ADR-022).</summary>
public sealed class HydrologyTests
{
    private static readonly Lazy<GeneratedWorld> River = new(() => WorldGenerator.Generate(new WorldSeed(42), TestContent.RiverWorldGen));

    [Fact]
    public void AWorldWithHydrology_HasRivers_AndLakes_RecordedInItsState()
    {
        var world = River.Value;

        Assert.NotEmpty(world.Terrain.Hydrology.Rivers);
        Assert.Equal(world.Terrain.Hydrology.Rivers.Count, world.State.Water.Rivers.Count);
        Assert.Equal(world.Terrain.Hydrology.Lakes.Count, world.State.Water.Lakes.Count);
        Assert.Equal(Enumerable.Range(1, world.State.Water.Rivers.Count), world.State.Water.Rivers.Select(r => r.Id));
        Assert.All(world.State.Water.Rivers, river => Assert.Equal(river.Regions.Count, river.Regions.Distinct().Count()));
        Assert.Contains(world.State.Regions.Cells, region => region.FreshWater > 0.5f);
    }

    [Fact]
    public void Generation_IsDeterministic()
    {
        var again = WorldGenerator.Generate(new WorldSeed(42), TestContent.RiverWorldGen);
        var first = River.Value;

        Assert.True(first.Terrain.Hydrology.WaterSurface.SequenceEqual(again.Terrain.Hydrology.WaterSurface));
        Assert.True(first.Terrain.Heights.SequenceEqual(again.Terrain.Heights));
        Assert.Equal(first.State.Water.Rivers.Select(r => (r.Name, string.Join(",", r.Regions))), again.State.Water.Rivers.Select(r => (r.Name, string.Join(",", r.Regions))));
    }

    [Fact]
    public void ARiver_NeverRisesTowardsItsMouth_AndRunsInAChannelBelowTheLand()
    {
        foreach (var river in River.Value.Terrain.Hydrology.Rivers)
        {
            for (var i = 1; i < river.Points.Count; i++)
            {
                Assert.True(river.Points[i].Surface <= river.Points[i - 1].Surface + 1e-4f, $"{river.Name} rises at point {i}");
            }

            var middle = river.Points[river.Points.Count / 2];
            Assert.True(River.Value.Terrain.SampleHeight(middle.X, middle.Z) < middle.Surface, $"{river.Name}'s channel is not below its water");
        }
    }

    [Fact]
    public void AMainRiver_ReachesTheSea_AndATributaryJoinsARiver()
    {
        var rivers = River.Value.Terrain.Hydrology.Rivers;
        var main = rivers.Where(r => r.JoinsRiver is null).ToList();

        Assert.NotEmpty(main);
        Assert.All(main, river => Assert.True(River.Value.Terrain.SampleHeight(river.Points[^1].X, river.Points[^1].Z) < 0.5f, $"{river.Name} ends above the sea"));
        Assert.All(rivers.Where(r => r.JoinsRiver is not null), river => Assert.InRange(river.JoinsRiver!.Value, 0, rivers.Count - 1));
    }

    [Fact]
    public void ALake_StandsOverGroundBelowItsSurface()
    {
        var terrain = River.Value.Terrain;
        Assert.NotEmpty(terrain.Hydrology.Lakes);
        foreach (var lake in terrain.Hydrology.Lakes)
        {
            var under = 0;
            for (var i = 0; i < terrain.Heights.Length; i++)
            {
                if (terrain.Hydrology.WaterSurface[i] == lake.Surface)
                {
                    Assert.True(terrain.Heights[i] < lake.Surface);
                    under++;
                }
            }

            Assert.True(under > 0, lake.Name + " covers nothing");
        }
    }

    [Fact]
    public void Settlements_AreNeverFoundedInTheWater_AndPreferItsBanks()
    {
        var world = River.Value;
        foreach (var settlement in world.State.Settlements)
        {
            Assert.False(world.Terrain.IsUnderFreshWater(settlement.Position.X, settlement.Position.Z));
        }

        var dry = WorldGenerator.Generate(new WorldSeed(42), TestContent.RiverWorldGen with
        {
            StartingSettlement = TestContent.RiverWorldGen.StartingSettlement with { WaterPreference = 0f },
        });
        Assert.True(Watered(world) >= Watered(dry));
    }

    [Fact]
    public void AWorldWithoutHydrology_HasNoFreshWater()
    {
        var world = WorldGenerator.Generate(new WorldSeed(42), TestContent.SmallWorldGen);

        Assert.Empty(world.State.Water.Rivers);
        Assert.Empty(world.State.Water.Lakes);
        Assert.All(world.State.Regions.Cells, region => Assert.Equal(0f, region.FreshWater));
        Assert.All(world.Terrain.Hydrology.WaterSurface.ToArray(), surface => Assert.True(float.IsNaN(surface)));
    }

    [Fact]
    public void Validation_RejectsHeadwatersWiderThanRivers_AndCellsThatSplitSamples()
    {
        var hydrology = TestContent.RiverWorldGen.Hydrology!;

        var exception = Assert.Throws<ArgumentException>(() => WorldGenSettingsValidator.Validate(TestContent.RiverWorldGen with
        {
            Hydrology = hydrology with { HeadwaterCatchment = hydrology.RiverCatchment * 2f, CellSize = 2000f },
        }));

        Assert.Contains("HeadwaterCatchment", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CellSize", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARiverWorld_SavesAndLoads_AndKeepsPlayingTheSameWay()
    {
        var content = TestContent.Small with { WorldGen = TestContent.RiverWorldGen };
        var engine = content.CreateEngine();
        var straight = global::Pantheon.Game.GameSession.CreateNew(content, new WorldSeed(42)).World;
        var resumed = global::Pantheon.Game.GameSession.CreateNew(content, new WorldSeed(42)).World;
        straight.Water.Rivers[0].Level = 1.4f;
        resumed.Water.Rivers[0].Level = 1.4f;

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = global::Pantheon.Persistence.WorldSaveSerializer.Deserialize(global::Pantheon.Persistence.WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(straight.Water.Rivers.Select(r => r.Level), resumed.Water.Rivers.Select(r => r.Level));
        Assert.Equal(straight.Regions.Cells.Select(r => r.Flood), resumed.Regions.Cells.Select(r => r.Flood));
    }

    /// <summary>How many settlements have a river or lake in or beside their region.</summary>
    private static int Watered(GeneratedWorld world) => world.State.Settlements.Count(settlement =>
    {
        var (column, row) = world.State.Regions.GetCoordinates(settlement.RegionId);
        return world.State.Regions.Cells.Any(region =>
        {
            var (c, r) = world.State.Regions.GetCoordinates(region.Id);
            return Math.Abs(c - column) <= 1 && Math.Abs(r - row) <= 1 && region.FreshWater > 0.3f;
        });
    });
}
