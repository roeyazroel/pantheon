using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Divine;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Terrain;

namespace Pantheon.SiteExport;

/// <summary>
/// An island as the world generator makes it for a seed, at the first size, with the gods the
/// scribes write for it: what the website's island view draws. The heights, fresh water and biomes go
/// in a binary file of samples; the villages, gods, Creatures, holy sites and rivers in JSON.
/// </summary>
internal static class IslandExport
{
    // Samples a side in the site's grid: about 8 m apart on the small island.
    private const int Grid = 257;

    /// <summary>What the island index lists of an island.</summary>
    /// <param name="Seed">The seed as typed.</param>
    /// <param name="File">Its JSON file.</param>
    /// <param name="Home">The player's home village.</param>
    /// <param name="Settlements">How many villages.</param>
    /// <param name="Gods">How many gods.</param>
    public sealed record Summary(string Seed, string File, string Home, int Settlements, int Gods);

    /// <summary>Writes an island.</summary>
    /// <param name="content">The game's content.</param>
    /// <param name="minds">The minds' content, for the scribes.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="seedText">The seed as typed.</param>
    /// <param name="directory">Where to write.</param>
    /// <returns>Its summary.</returns>
    public static Summary Write(GameContent content, MindContent minds, WorldSeed seed, string seedText, string directory)
    {
        var options = new NewWorldOptions();
        options = options with { Born = PantheonBirth.Write(content, minds, seed, options) };
        var world = WorldMaker.Generate(content, seed, options);
        var state = world.State;
        var terrain = world.Terrain;
        var slug = Slug(seedText);

        File.WriteAllBytes(Path.Combine(directory, $"island-{slug}.bin"), Samples(terrain));

        using var stream = File.Create(Path.Combine(directory, $"island-{slug}.json"));
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartObject();
        json.WriteString("seed", seedText);
        json.WriteString("seedValue", seed.ToString());
        json.WriteNumber("worldSize", terrain.MaxCoordinate);
        json.WriteNumber("seaLevel", state.Generation.Shape.SeaLevel);
        json.WriteNumber("maxElevation", state.Generation.Shape.MaxElevation);
        json.WriteNumber("grid", Grid);
        json.WriteString("samples", $"island-{slug}.bin");
        json.WriteStartArray("biomes");
        foreach (var biome in Enum.GetNames<Biome>())
        {
            json.WriteStringValue(biome);
        }

        json.WriteEndArray();
        Gods(json, state);
        Settlements(json, state);
        json.WriteStartArray("holySites");
        foreach (var site in state.HolySites)
        {
            json.WriteStartObject();
            json.WriteString("name", site.Name);
            json.WriteString("kind", site.Kind.ToString());
            Position(json, site.Position.X, site.Position.Z);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("creatures");
        foreach (var creature in state.Creatures)
        {
            json.WriteStartObject();
            json.WriteString("name", creature.Name);
            json.WriteString("species", creature.Species);
            json.WriteNumber("god", creature.GodId);
            Position(json, creature.Position.X, creature.Position.Z);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        Rivers(json, terrain.Hydrology);
        json.WriteEndObject();
        json.Flush();

        var home = GodPlanner.Home(state, 1)?.Name ?? string.Empty;
        return new Summary(seedText, $"island-{slug}.json", home, state.Settlements.Count, state.Gods.Count);
    }

    /// <summary>Writes the list of islands.</summary>
    /// <param name="islands">The islands written.</param>
    /// <param name="directory">Where to write.</param>
    public static void WriteIndex(IReadOnlyList<Summary> islands, string directory)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(directory, "index.json"), JsonSerializer.Serialize(islands, options));
    }

    /// <summary>The gods, each with its title, personality and Creature.</summary>
    private static void Gods(Utf8JsonWriter json, WorldState state)
    {
        json.WriteStartArray("gods");
        foreach (var god in state.Gods)
        {
            var creature = state.Creatures.Find(c => c.GodId == god.Id);
            json.WriteStartObject();
            json.WriteNumber("id", god.Id);
            json.WriteString("name", god.Name);
            json.WriteString("title", PersonaText.Title(god));
            json.WriteString("personality", DivineText.Personality(god.Personality));
            json.WriteBoolean("player", god.Controller == Domain.Gods.GodController.Human);
            json.WriteString("creature", creature?.Name ?? string.Empty);
            json.WriteString("species", creature?.Species ?? string.Empty);
            json.WriteString("home", GodPlanner.Home(state, god.Id)?.Name ?? string.Empty);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    /// <summary>The villages, each with the god most of it follows and that god's share.</summary>
    private static void Settlements(Utf8JsonWriter json, WorldState state)
    {
        json.WriteStartArray("settlements");
        foreach (var settlement in state.Settlements)
        {
            var leader = settlement.Beliefs.OrderByDescending(b => b.Adherence).FirstOrDefault();
            json.WriteStartObject();
            json.WriteString("name", settlement.Name);
            json.WriteNumber("population", settlement.Population);
            json.WriteNumber("god", leader is { Adherence: > 0f } ? leader.GodId : 0);
            json.WriteNumber("share", MathF.Round(leader?.Adherence ?? 0f, 2));
            Position(json, settlement.Position.X, settlement.Position.Z);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    /// <summary>The rivers: their names and courses, every point's water surface and width.</summary>
    private static void Rivers(Utf8JsonWriter json, HydrologyMap hydrology)
    {
        json.WriteStartArray("rivers");
        foreach (var river in hydrology.Rivers)
        {
            json.WriteStartObject();
            json.WriteString("name", river.Name);
            json.WriteStartArray("points");
            foreach (var point in river.Points)
            {
                json.WriteStartArray();
                json.WriteNumberValue(MathF.Round(point.X, 1));
                json.WriteNumberValue(MathF.Round(point.Z, 1));
                json.WriteNumberValue(MathF.Round(point.Surface, 2));
                json.WriteNumberValue(MathF.Round(point.Width, 1));
                json.WriteEndArray();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("lakes");
        foreach (var lake in hydrology.Lakes)
        {
            json.WriteStartObject();
            json.WriteString("name", lake.Name);
            json.WriteNumber("surface", MathF.Round(lake.Surface, 2));
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    /// <summary>A position in metres.</summary>
    private static void Position(Utf8JsonWriter json, float x, float z)
    {
        json.WriteNumber("x", MathF.Round(x, 1));
        json.WriteNumber("z", MathF.Round(z, 1));
    }

    /// <summary>
    /// The grid's samples, little-endian: every height (float32), then every fresh water surface
    /// (float32, NaN where none stands), then every biome (a byte, the <see cref="Biome"/> value). A
    /// grid sample shows water when any terrain sample it stands for does, so narrow rivers still show.
    /// </summary>
    private static byte[] Samples(TerrainField terrain)
    {
        var count = Grid * Grid;
        var bytes = new byte[(count * 4 * 2) + count];
        var step = (terrain.Resolution - 1) / (float)(Grid - 1);
        var reach = (int)MathF.Ceiling(step / 2f);
        for (var gz = 0; gz < Grid; gz++)
        {
            for (var gx = 0; gx < Grid; gx++)
            {
                var index = (gz * Grid) + gx;
                var x = (int)MathF.Round(gx * step);
                var z = (int)MathF.Round(gz * step);
                BitConverter.TryWriteBytes(bytes.AsSpan(index * 4, 4), terrain.GetHeight(x, z));
                BitConverter.TryWriteBytes(bytes.AsSpan((count + index) * 4, 4), Water(terrain, x, z, reach));
                bytes[(count * 8) + index] = (byte)terrain.GetBiome(x, z);
            }
        }

        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("The island samples are written little-endian.");
        }

        return bytes;
    }

    /// <summary>The highest fresh water surface over the terrain samples around a grid sample, or NaN.</summary>
    private static float Water(TerrainField terrain, int x, int z, int reach)
    {
        var surface = float.NaN;
        for (var dz = -reach; dz <= reach; dz++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var sx = Math.Clamp(x + dx, 0, terrain.Resolution - 1);
                var sz = Math.Clamp(z + dz, 0, terrain.Resolution - 1);
                if (terrain.IsUnderFreshWater(sx, sz))
                {
                    var here = terrain.Hydrology.WaterSurface[(sz * terrain.Resolution) + sx];
                    surface = float.IsNaN(surface) ? here : MathF.Max(surface, here);
                }
            }
        }

        return surface;
    }

    /// <summary>A seed as a file name: letters and digits, words joined by dashes.</summary>
    private static string Slug(string seed)
    {
        var builder = new StringBuilder();
        foreach (var c in seed.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '-');
        }

        return builder.ToString().Trim('-') is { Length: > 0 } slug ? slug : "seed";
    }
}
