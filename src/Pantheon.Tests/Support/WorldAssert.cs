using System;
using System.Text.Json;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Xunit;

namespace Pantheon.Tests.Support;

/// <summary>Exact, field-by-field comparisons of worlds and terrain.</summary>
internal static class WorldAssert
{
    /// <summary>Asserts that two worlds are identical, including every float.</summary>
    /// <param name="expected">Expected world.</param>
    /// <param name="actual">Actual world.</param>
    public static void Equal(WorldState expected, WorldState actual)
    {
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Random, actual.Random);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Generation, PantheonJson.SaveOptions),
            JsonSerializer.Serialize(actual.Generation, PantheonJson.SaveOptions));

        Assert.Equal(expected.Regions.Columns, actual.Regions.Columns);
        Assert.Equal(expected.Regions.Rows, actual.Regions.Rows);
        Assert.Equal(expected.Regions.RegionSize, actual.Regions.RegionSize);
        Assert.Equal(expected.Regions.Cells.Count, actual.Regions.Cells.Count);
        for (var i = 0; i < expected.Regions.Cells.Count; i++)
        {
            var e = expected.Regions.Cells[i];
            var a = actual.Regions.Cells[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Biome, a.Biome);
            Assert.Equal(e.Elevation, a.Elevation);
            Assert.Equal(e.Temperature, a.Temperature);
            Assert.Equal(e.Moisture, a.Moisture);
            Assert.Equal(e.Fertility, a.Fertility);
            Assert.Equal(e.Vegetation, a.Vegetation);
            Assert.Equal(e.Blessing, a.Blessing);
            Assert.Equal(e.SoilMoisture, a.SoilMoisture);
        }

        Assert.Equal(expected.Settlements.Count, actual.Settlements.Count);
        for (var i = 0; i < expected.Settlements.Count; i++)
        {
            var e = expected.Settlements[i];
            var a = actual.Settlements[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.RegionId, a.RegionId);
            Assert.Equal(e.Position, a.Position);
            Assert.Equal(e.Population, a.Population);
            Assert.Equal(e.Food, a.Food);
            Assert.Equal(e.Housing, a.Housing);
            Assert.Equal(e.Health, a.Health);
            Assert.Equal(e.ConstructionProgress, a.ConstructionProgress);
            Assert.Equal(
                JsonSerializer.Serialize(e.Beliefs, PantheonJson.SaveOptions),
                JsonSerializer.Serialize(a.Beliefs, PantheonJson.SaveOptions));
        }

        Assert.Equal(
            JsonSerializer.Serialize(expected.Gods, PantheonJson.SaveOptions),
            JsonSerializer.Serialize(actual.Gods, PantheonJson.SaveOptions));

        // Everything else, including state added after these field checks were written.
        Assert.Equal(
            JsonSerializer.Serialize(expected, PantheonJson.SaveOptions),
            JsonSerializer.Serialize(actual, PantheonJson.SaveOptions));
    }

    /// <summary>Asserts that two terrain fields hold identical samples.</summary>
    /// <param name="expected">Expected terrain.</param>
    /// <param name="actual">Actual terrain.</param>
    public static void Equal(TerrainField expected, TerrainField actual)
    {
        Assert.Equal(expected.Resolution, actual.Resolution);
        Assert.Equal(expected.Spacing, actual.Spacing);
        Assert.True(expected.Heights.SequenceEqual(actual.Heights), "Heights differ.");
        Assert.True(expected.Moisture.SequenceEqual(actual.Moisture), "Moisture differs.");
        Assert.True(expected.Temperature.SequenceEqual(actual.Temperature), "Temperature differs.");
        Assert.True(expected.Biomes.SequenceEqual(actual.Biomes), "Biomes differ.");
    }
}
