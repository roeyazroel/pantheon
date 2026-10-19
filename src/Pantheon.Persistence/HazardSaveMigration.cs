using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 9 (ADR-029), run by <see cref="SaveMigrations"/>.</summary>
internal static class HazardSaveMigration
{
    /// <summary>
    /// Version 20 to 21 (ADR-029): regions gain blight and landslide rubble, and the disasters their
    /// tornadoes, locust swarms, volcano, the marks left on the land and the villages told of blight.
    /// Version 20 knew none of these: no field is blighted, no way blocked, nothing is on the move, the
    /// volcano is found when it is first needed, and the land bears no marks.
    /// </summary>
    internal static void UpgradeFrom20(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (world["Regions"] is JsonObject regions && regions["Cells"] is JsonArray cells)
        {
            foreach (var node in cells)
            {
                if (node is JsonObject region)
                {
                    AddMember(region, "Blight", 0f, fromVersion: 20);
                    AddMember(region, "Rubble", 0f, fromVersion: 20);
                }
            }
        }

        if (world["Disasters"] is JsonObject disasters)
        {
            AddNode(disasters, "Tornadoes", new JsonArray(), fromVersion: 20);
            AddNode(disasters, "Swarms", new JsonArray(), fromVersion: 20);
            AddNode(disasters, "NextHazardId", JsonValue.Create(1), fromVersion: 20);
            AddNullMember(disasters, "Volcano", fromVersion: 20);
            AddNode(disasters, "Marks", new JsonArray(), fromVersion: 20);
            AddNode(disasters, "BlightedSettlementIds", new JsonArray(), fromVersion: 20);
        }
    }
}
