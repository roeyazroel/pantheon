using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 18 (ADR-038), run by <see cref="SaveMigrations"/>.</summary>
internal static class LandformSaveMigration
{
    /// <summary>
    /// Version 28 to 29 (ADR-038): the generation settings gain the landforms. A version 28 world had
    /// none, so its terrain is generated again as it was, with its own biome rules.
    /// </summary>
    internal static void UpgradeFrom28(JsonObject root)
    {
        if (root["World"] is JsonObject world && world["Generation"] is JsonObject generation)
        {
            AddNullMember(generation, "Landforms", fromVersion: 28);
        }
    }
}
