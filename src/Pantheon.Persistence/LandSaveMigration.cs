using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 24 (ADR-045), run by <see cref="SaveMigrations"/>.</summary>
internal static class LandSaveMigration
{
    /// <summary>
    /// Version 31 to 32 (ADR-045): the world gains the land's reshapings. Version 31 had no Divine
    /// Act that reshaped the land, so the land is as the seed made it.
    /// </summary>
    internal static void UpgradeFrom31(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Land", new JsonObject { ["Reshapes"] = new JsonArray(), ["NextReshapeId"] = 1 }, fromVersion: 31);
    }
}
