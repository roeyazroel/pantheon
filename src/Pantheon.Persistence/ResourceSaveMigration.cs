using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 13 (ADR-033), run by <see cref="SaveMigrations"/>.</summary>
internal static class ResourceSaveMigration
{
    /// <summary>Wood and stone a village of version 23 starts with, as a new world's do.</summary>
    internal const float StartingWood = 30f;

    /// <summary>Stone a village of version 23 starts with.</summary>
    internal const float StartingStone = 15f;

    /// <summary>
    /// Version 23 to 24 (ADR-033): the land gains its forests and stone, villages their stores of wood
    /// and stone, and the world its roads. Version 23 knew none: each region's forest is its biome's
    /// factor on its vegetation, whole, and its stone its biome's (without a new world's patches);
    /// each village starts with a new world's stores; and no road has been built.
    /// </summary>
    internal static void UpgradeFrom23(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        foreach (var node in (world["Regions"] as JsonObject)?["Cells"] as JsonArray ?? [])
        {
            if (node is JsonObject region)
            {
                var (factor, stone) = Biome(region["Biome"]) switch
                {
                    "Grassland" => (0.6, 0.15),
                    "Forest" => (1.1, 0.2),
                    "Highland" => (1.6, 0.75),
                    _ => (0.0, 0.0),
                };
                var vegetation = ReadNumber(region["Vegetation"]);
                var forest = double.IsFinite(vegetation) ? Math.Clamp(factor * vegetation, 0.0, 1.0) : 0.0;
                AddMember(region, "Forest", (float)forest, fromVersion: 23);
                AddMember(region, "Timber", (float)forest, fromVersion: 23);
                AddMember(region, "Stone", (float)stone, fromVersion: 23);
            }
        }

        foreach (var node in world["Settlements"] as JsonArray ?? [])
        {
            if (node is JsonObject settlement)
            {
                AddMember(settlement, "Wood", StartingWood, fromVersion: 23);
                AddMember(settlement, "Stone", StartingStone, fromVersion: 23);
            }
        }

        AddNode(world, "Roads", new JsonArray(), fromVersion: 23);
        AddNode(world, "NextRoadId", JsonValue.Create(1), fromVersion: 23);
    }

    /// <summary>A region's biome by name, whether the save wrote it as a name or a number.</summary>
    private static string Biome(JsonNode? node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String
        ? value.GetValue<string>()
        : ReadNumber(node) switch
        {
            1.0 => "Grassland",
            2.0 => "Forest",
            3.0 => "Highland",
            _ => "Sea",
        };
}
