using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 7 (ADR-027), run by <see cref="SaveMigrations"/>.</summary>
internal static class VictorySaveMigration
{
    /// <summary>
    /// Version 18 to 19 (ADR-027): the world records how it may be won, and each god how long it
    /// has conquered or led the world's culture. Version 18 knew only Divine Ascendancy, so its
    /// worlds are won that way alone, by pantheons as large as the divine rules allow, and no
    /// god's other clocks have started.
    /// </summary>
    internal static void UpgradeFrom18(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Victory", new JsonObject { ["Ascendancy"] = true, ["Conquest"] = false, ["Cultural"] = false, ["PantheonMaxGods"] = null }, fromVersion: 18);
        if (world["Gods"] is JsonArray gods)
        {
            foreach (var node in gods)
            {
                if (node is JsonObject god)
                {
                    AddNullMember(god, "ConquestSinceTick", fromVersion: 18);
                    AddNullMember(god, "CulturalSinceTick", fromVersion: 18);
                }
            }
        }
    }
}
