using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Navigation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The hazards as pure functions and state changes (spec §19–20, ADR-029).</summary>
public sealed class HazardModelTests
{
    private static readonly HazardRules Hazards = HazardWorlds.Hazards;

    [Fact]
    public void TheVolcano_IsTheHighestLand_WhenItRisesHighEnough()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 100f);

        var crater = HazardModel.FindVolcano(Hazards.Volcano, world.Regions);

        Assert.NotNull(crater);
        Assert.Equal(world.Regions.Columns - 1, world.Regions.GetCoordinates(crater.Value).Column);
        HazardWorlds.Mountain(world, 50f);
        Assert.Null(HazardModel.FindVolcano(Hazards.Volcano, world.Regions));
    }

    [Fact]
    public void Lava_RunsDownTheSteepestSlope_ForItsLength_AndStopsAtTheSea()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 100f);
        foreach (var region in world.Regions.Cells.Where(r => world.Regions.GetCoordinates(r.Id).Column < 2))
        {
            region.Biome = Biome.Sea;
        }

        var crater = HazardModel.FindVolcano(Hazards.Volcano, world.Regions)!.Value;
        var path = HazardModel.LavaPath(world.Regions, crater, 40);

        Assert.Equal(crater, path[0]);
        Assert.Equal(world.Regions.Columns - 2, path.Count);
        Assert.All(path.Skip(1).Zip(path), pair => Assert.True(world.Regions.Cells[pair.First].Elevation < world.Regions.Cells[pair.Second].Elevation));
        Assert.DoesNotContain(path, id => world.Regions.Cells[id].Biome == Biome.Sea);
        Assert.Equal(6, HazardModel.LavaPath(world.Regions, crater, 6).Count);
    }

    [Fact]
    public void AWave_FloodsTheLowCoast_OnlyAfterAStrongQuakeNearIt()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Coast(world, 2);
        var coast = new GroundPosition(200f, 512f);

        Assert.Null(HazardModel.RaiseTsunami(Hazards.Tsunami, world, coast, 5.5f));
        Assert.Null(HazardModel.RaiseTsunami(Hazards.Tsunami, world, new GroundPosition(900f, 512f), 7.5f));
        Assert.DoesNotContain(world.Regions.Cells, r => r.Flood > 0f);

        var wave = HazardModel.RaiseTsunami(Hazards.Tsunami, world, coast, 7f);

        Assert.NotNull(wave);
        Assert.Equal(Hazards.Tsunami.RadiusPerMagnitude * 2f, wave.Value.Radius);
        Assert.True(world.Regions.Cells[wave.Value.RegionId].Flood > 0.5f);
        Assert.Equal(2, world.Regions.GetCoordinates(wave.Value.RegionId).Column);
        Assert.All(world.Regions.Cells.Where(r => r.Elevation >= Hazards.Tsunami.HeightPerMagnitude * 2f), r => Assert.Equal(0f, r.Flood));
    }

    [Fact]
    public void ASlide_BuriesTheLandBelowInRubble_HarmsIt_BlocksTheWay_AndLeavesItsMark()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 300f);
        var slope = world.Regions.Cells[(8 * world.Regions.Columns) + 10];
        var below = HazardModel.Downhill(world.Regions, slope.Id)!.Value;

        HazardModel.Slide(Hazards.Landslide, world, slope.Id, below.RegionId);

        var buried = world.Regions.Cells[below.RegionId];
        Assert.True(below.Drop >= Hazards.Landslide.MinDrop);
        Assert.Equal((1f, 0.5f), (buried.Rubble, slope.Rubble));
        Assert.Equal((0.5f, 0.5f), (buried.Damage, slope.Damage));
        Assert.False(RegionPathfinder.IsWalkable(buried));
        var mark = Assert.Single(world.Disasters.Marks);
        Assert.Equal(TerrainMarkKind.Slide, mark.Kind);
    }

    [Fact]
    public void AMeteor_HarmsAndScarsTheLand_SetsItAlight_LeavesACrater_AndShakesIt()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(world);
        var point = world.Regions.GetRegionCenter(100);

        var quake = HazardModel.Impact(HazardWorlds.Rules, world, point);

        var struck = world.Regions.Cells[100];
        Assert.Equal(1f, struck.Damage);
        Assert.Equal(Hazards.Meteor.Scar, struck.Scar, 4);
        Assert.True(struck.Fire > 0f);
        Assert.Equal(0f, world.Regions.Cells[0].Damage);
        Assert.Equal(Hazards.Meteor.Magnitude, quake.Magnitude);
        Assert.Contains(quake, world.Disasters.Earthquakes);
        Assert.Equal(TerrainMarkKind.Crater, Assert.Single(world.Disasters.Marks).Kind);
    }

    [Fact]
    public void TornadoesAndSwarms_TakeTheNextHazardId_AndTheVolcanoWakesOnlyWhereThereIsOne()
    {
        var world = DivineWorlds.Create(0f, 300f);

        var tornado = HazardModel.SpawnTornado(Hazards.Tornado, world, new GroundPosition(100f, 100f), 0f, 10f);
        var swarm = HazardModel.SpawnSwarm(world, new GroundPosition(200f, 200f), 20f);

        Assert.Equal((1, 2, 3), (tornado.Id, swarm.Id, world.Disasters.NextHazardId));
        Assert.Equal((Hazards.Tornado.Speed, 0f), (tornado.VelocityX, tornado.VelocityZ));
        Assert.Equal((100L, 200L), (tornado.EndsAtTick, swarm.EndsAtTick));
        Assert.Null(HazardModel.Wake(Hazards.Volcano, world));

        HazardWorlds.Mountain(world, 100f);
        var volcano = HazardModel.Wake(Hazards.Volcano, world);

        Assert.NotNull(volcano);
        Assert.Equal(VolcanoPhase.Rumbling, volcano.Phase);
        Assert.Equal(400L, volcano.PhaseEndsTick);
    }

    [Fact]
    public void Falloff_IsWholeAtTheCentre_AndNothingAtTheRadius()
    {
        var centre = new GroundPosition(0f, 0f);

        Assert.Equal(1f, HazardModel.Falloff(centre, centre, 100f));
        Assert.Equal(0.5f, HazardModel.Falloff(new GroundPosition(50f, 0f), centre, 100f), 4);
        Assert.Equal(0f, HazardModel.Falloff(new GroundPosition(150f, 0f), centre, 100f));
        Assert.Equal(0f, HazardModel.Falloff(centre, centre, 0f));
    }
}
