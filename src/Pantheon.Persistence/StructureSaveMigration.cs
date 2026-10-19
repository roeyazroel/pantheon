using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 27 (ADR-048), run by <see cref="SaveMigrations"/>.</summary>
internal static class StructureSaveMigration
{
    /// <summary>
    /// Version 32 to 33 (ADR-048): every settlement's houses gain a structural integrity. Version 32
    /// never cracked a house, so every village's houses are whole.
    /// </summary>
    internal static void UpgradeFrom32(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Settlements"] is not JsonArray settlements)
        {
            return;
        }

        foreach (var node in settlements)
        {
            if (node is JsonObject settlement)
            {
                AddMember(settlement, "Integrity", 1f, fromVersion: 32);
            }
        }
    }
}
