using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Navigation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>Strategic navigation over the region graph (spec §85).</summary>
public sealed class RegionPathfinderTests
{
    [Fact]
    public void OpenLand_GivesAStraightPath()
    {
        var world = DivineWorlds.Create(0f);

        var path = RegionPathfinder.FindPath(world.Regions, new GroundPosition(100f, 100f), new GroundPosition(900f, 700f));

        Assert.Equal([new GroundPosition(900f, 700f)], path);
    }

    [Fact]
    public void Path_GoesAroundTheSea()
    {
        var world = DivineWorlds.Create(0f);
        // A sea wall across columns 8 at rows 0-12, open at the top rows 13-15.
        for (var row = 0; row <= 12; row++)
        {
            world.Regions.Cells[(row * DivineWorlds.RegionsPerSide) + 8].Biome = Biome.Sea;
        }

        var from = new GroundPosition(300f, 100f);
        var to = new GroundPosition(800f, 100f);
        var path = RegionPathfinder.FindPath(world.Regions, from, to);

        Assert.NotNull(path);
        Assert.Equal(to, path![^1]);
        var previous = from;
        foreach (var point in path)
        {
            Assert.True(RegionPathfinder.IsLineWalkable(world.Regions, previous, point));
            previous = point;
        }

        Assert.Contains(path, point => point.Z > 12 * DivineWorlds.RegionSize);
    }

    [Fact]
    public void UnreachableIsland_HasNoPath()
    {
        var world = DivineWorlds.Create(0f);
        foreach (var cell in world.Regions.Cells.Where(cell => world.Regions.GetCoordinates(cell.Id).Column == 8))
        {
            cell.Biome = Biome.Sea;
        }

        Assert.Null(RegionPathfinder.FindPath(world.Regions, new GroundPosition(300f, 100f), new GroundPosition(800f, 100f)));
        Assert.Null(RegionPathfinder.FindPath(world.Regions, new GroundPosition(300f, 100f), new GroundPosition(-10f, 100f)));
    }
}
