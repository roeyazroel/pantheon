using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How villages build (spec §61, ADR-052): houses, temples and walls made in code, and each village's look.</summary>
public sealed class ArchitectureTests
{
    private static readonly ArchitectureSettings Settings = TestContent.SettlementView.Architecture;

    [Theory]
    [InlineData(HouseStyle.RoundHut)]
    [InlineData(HouseStyle.Longhouse)]
    [InlineData(HouseStyle.StoneCottage)]
    [InlineData(HouseStyle.Adobe)]
    [InlineData(HouseStyle.StiltHouse)]
    public void EveryHouse_StandsInTheGround_FitsItsPlot_FacesItsDoorOut_AndAnUpperStoreyRaisesIt(HouseStyle style)
    {
        var low = HouseMesh.Build(style, twoStoreys: false, detailed: true);
        var high = HouseMesh.Build(style, twoStoreys: true, detailed: true);
        IEnumerable<Vector3> Slot(TaggedMesh mesh, BuildingSlot slot) => mesh.Positions.Where((_, i) => (int)mesh.Tags[i].Y == (int)slot);

        Assert.Equal(-HouseMesh.Sink, low.Positions.Min(p => p.Y), 2);
        Assert.InRange(low.Positions.Max(p => p.Y), 2.5f, HouseMesh.Top);
        Assert.All(Slot(low, BuildingSlot.Wall), p => Assert.InRange(new Vector2(p.X, p.Z).Length(), 0f, 2.8f));
        Assert.All(Slot(low, BuildingSlot.Door), p => Assert.True(p.Z > 1f, "the door faces +Z, towards the shrine"));
        Assert.NotEmpty(Slot(low, BuildingSlot.Roof));
        Assert.True(!HouseMesh.HasUpperStorey(style) ? high.Positions.Max(p => p.Y) == low.Positions.Max(p => p.Y) : high.Positions.Max(p => p.Y) > low.Positions.Max(p => p.Y) + 1f);
        Assert.All(high.Positions, p => Assert.True(p.Y <= HouseMesh.Top));
        for (var t = 0; t < low.Indices.Length; t += 3)
        {
            var (a, b, c) = (low.Positions[low.Indices[t]], low.Positions[low.Indices[t + 1]], low.Positions[low.Indices[t + 2]]);
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), low.Normals[low.Indices[t]]) <= 1e-5f, "Godot's front faces wind clockwise");
        }
    }

    [Fact]
    public void TheLand_PicksTheStyle_AndVillagesGrowIntoLonghousesAndUpperStoreys()
    {
        Assert.All(Enum.GetValues<Biome>(), biome => Assert.True(Enum.IsDefined(VillageArchitecture.StyleOf(biome))));
        var world = SettlementWorlds.Create(population: 100, housing: 120, food: 400f, fertility: 0.6f);
        var village = SettlementWorlds.Settlement(world);
        void On(Biome biome) => world.Regions.Cells.ForEach(r => r.Biome = biome);

        On(Biome.Desert);
        var desert = VillageArchitecture.Of(village, world, 10, Settings, null);
        On(Biome.Swamp);
        var swamp = VillageArchitecture.Of(village, world, 10, Settings, null);
        On(Biome.Grassland);
        var hamlet = VillageArchitecture.Of(village, world, Settings.LonghouseHouses - 1, Settings, null);
        var village18 = VillageArchitecture.Of(village, world, Settings.LonghouseHouses, Settings, null);
        var town = VillageArchitecture.Of(village, world, Settings.TwoStoreyHouses, Settings, null);

        Assert.Equal((HouseStyle.Adobe, HouseStyle.StiltHouse), (desert.Style, swamp.Style));
        Assert.Equal((HouseStyle.RoundHut, false), (hamlet.Style, hamlet.TwoStoreys));
        Assert.Equal((HouseStyle.Longhouse, false), (village18.Style, village18.TwoStoreys));
        Assert.True(town.TwoStoreys);
        Assert.Contains(desert.Wall, Settings.Styles[HouseStyle.Adobe].Walls);
        On(Biome.Desert);
        Assert.Equal(desert, VillageArchitecture.Of(village, world, 10, Settings, null));
        On(Biome.Swamp);
        Assert.False(VillageArchitecture.Of(village, world, Settings.TwoStoreyHouses, Settings, null).TwoStoreys);
    }

    [Fact]
    public void AStronglyFeltGod_ColoursTheHouses_TurfForNature_DarkForWar_WhitewashForLearning()
    {
        var world = SettlementWorlds.Create(population: 100, housing: 120, food: 400f, fertility: 0.6f);
        var village = SettlementWorlds.Settlement(world);
        VillageLook With(StyleMotif motif, float strength) => VillageArchitecture.Of(village, world, 10, Settings, new ReligiousStyle(motif, strength, 1, "test"));

        var garden = With(StyleMotif.Garden, 0.8f);
        var monument = With(StyleMotif.Monument, 0.8f);
        var lore = With(StyleMotif.Lore, 0.8f);
        var faint = With(StyleMotif.Monument, Settings.MotifStrength / 2f);

        Assert.Equal((StyleMotif.Garden, Settings.GardenRoof), (garden.Motif!.Value, garden.Roof));
        Assert.Equal((Settings.MonumentWall, Settings.MonumentRoof), (monument.Wall, monument.Roof));
        Assert.Equal((Settings.LoreWall, Settings.LoreRoof), (lore.Wall, lore.Roof));
        Assert.Null(faint.Motif);
        var shades = Enumerable.Range(0, 60).Select(i => VillageArchitecture.ShadeOf(Settings, 3, i)).ToList();
        Assert.All(shades, shade => Assert.InRange(shade, 1f - Settings.HouseVariation, 1f + Settings.HouseVariation));
        Assert.True(shades.Distinct().Count() > 50);
        Assert.Equal(shades[7], VillageArchitecture.ShadeOf(Settings, 3, 7));
    }

    [Fact]
    public void Temples_TakeTheirGodsStyle_AndWallsHaveStakesStoneAndTowersByTheGates()
    {
        var temples = new StyleMotif?[] { null, StyleMotif.Garden, StyleMotif.Monument, StyleMotif.Lore }.Select(m => CivicMesh.Temple(m, detailed: true)).ToList();
        Assert.Equal(4, temples.Select(t => t.Positions.Length).Distinct().Count());
        Assert.All(temples, t => Assert.Contains(t.Tags, tag => (int)tag.Y == (int)BuildingSlot.Roof));
        Assert.Equal(3.3f, CivicMesh.Stake(detailed: true).Positions.Max(p => p.Y), 3);
        Assert.True(CivicMesh.Tower(stone: false, detailed: true).Positions.Max(p => p.Y) > CivicMesh.WallSegment(detailed: true).Positions.Max(p => p.Y) + 3f);

        var terrain = Flat();
        var site = new SettlementSite(new Vector3(128f, 5f, 128f), [], null, [], 30f, []);
        var towers = CivicLayout.Towers(site, terrain);
        var stakes = CivicLayout.Walls(site, terrain, 0.36f);
        var posts = CivicLayout.Walls(site, terrain);

        Assert.Equal(8, towers.Count);
        Assert.All(towers, t => Assert.Equal(30f + CivicLayout.WallMargin, Vector2.Distance(new Vector2(t.X, t.Z), new Vector2(128f, 128f)), 2));
        Assert.True(stakes.Count > 6 * posts.Count);
        var post = posts[3];
        var yaw = CivicLayout.AlongRing(site, post);
        var alongX = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
        Assert.Equal(0f, Vector2.Dot(alongX, Vector2.Normalize(new Vector2(post.X - 128f, post.Z - 128f))), 3);
    }

    [Fact]
    public void Architecture_NeedsColoursForEveryStyle()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"StiltHouse\": {", "\"Stilts\": {", StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
    }

    /// <summary>Flat dry ground 5 m above the sea, 256 m across.</summary>
    private static TerrainField Flat()
    {
        const int side = 129;
        var count = side * side;
        return new TerrainField(side, 2f, Enumerable.Repeat(5f, count).ToArray(), new float[count], new float[count], Enumerable.Repeat(Biome.Grassland, count).ToArray());
    }
}
