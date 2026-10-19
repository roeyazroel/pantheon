using System;
using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 8 (ADR-028), run by <see cref="SaveMigrations"/>.</summary>
internal static class NatureSaveMigration
{
    /// <summary>
    /// Version 19 to 20 (ADR-028): regions gain snow, wildlife, harm, scars and pollution, and the
    /// world its seasons. Version 19 knew none of these: no snow lies, the land holds as many animals
    /// as its cover (as a new world does), nothing is harmed or polluted, and the world keeps its
    /// even climate, without seasons.
    /// </summary>
    internal static void UpgradeFrom19(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (world["Regions"] is JsonObject regions && regions["Cells"] is JsonArray cells)
        {
            foreach (var node in cells)
            {
                if (node is not JsonObject region)
                {
                    continue;
                }

                var sea = region["Biome"] is JsonValue biome && biome.TryGetValue<string>(out var name) && string.Equals(name, "Sea", StringComparison.Ordinal);
                var vegetation = region["Vegetation"] is JsonValue cover && cover.TryGetValue<float>(out var value) ? Math.Clamp(value, 0f, 1f) : 0f;
                AddMember(region, "Snow", 0f, fromVersion: 19);
                AddMember(region, "Wildlife", sea ? 0f : vegetation, fromVersion: 19);
                AddMember(region, "Damage", 0f, fromVersion: 19);
                AddMember(region, "Scar", 0f, fromVersion: 19);
                AddMember(region, "Pollution", 0f, fromVersion: 19);
            }
        }

        AddNullMember(world, "Seasons", fromVersion: 19);
    }
}
