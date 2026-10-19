using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 22 (ADR-043), run by <see cref="SaveMigrations"/>.</summary>
internal static class VillagerSaveMigration
{
    /// <summary>
    /// Version 30 to 31 (ADR-043): the world gains the villagers its gods follow. Version 30 could
    /// follow no villager, so no god follows one.
    /// </summary>
    internal static void UpgradeFrom30(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Villagers", new JsonArray(), fromVersion: 30);
        AddNode(world, "NextVillagerId", JsonValue.Create(1), fromVersion: 30);
    }
}
