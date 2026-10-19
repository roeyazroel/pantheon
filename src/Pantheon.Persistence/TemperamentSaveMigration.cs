using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 17 (ADR-037), run by <see cref="SaveMigrations"/>.</summary>
internal static class TemperamentSaveMigration
{
    /// <summary>
    /// Version 27 to 28 (ADR-037): a Creature's personality gains courage, protectiveness, jealousy
    /// and playfulness. Version 27 knew none, so each Creature starts them at the middle, 0.5.
    /// </summary>
    internal static void UpgradeFrom27(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Creatures"] is not JsonArray creatures)
        {
            return;
        }

        foreach (var node in creatures)
        {
            if (node is JsonObject creature && creature["Personality"] is JsonObject personality)
            {
                AddMember(personality, "Courage", 0.5f, fromVersion: 27);
                AddMember(personality, "Protectiveness", 0.5f, fromVersion: 27);
                AddMember(personality, "Jealousy", 0.5f, fromVersion: 27);
                AddMember(personality, "Playfulness", 0.5f, fromVersion: 27);
            }
        }
    }
}
