using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 14 (ADR-034), run by <see cref="SaveMigrations"/>.</summary>
internal static class GreatEventSaveMigration
{
    /// <summary>
    /// Version 24 to 25 (ADR-034): villages gain their plague and immunity, and the world its great
    /// events. Version 24 knew none: nobody is sick or immune, no great event is under way, the first
    /// eclipse is set on the first step, and no faith has reformed.
    /// </summary>
    internal static void UpgradeFrom24(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        foreach (var node in world["Settlements"] as JsonArray ?? [])
        {
            if (node is JsonObject settlement)
            {
                AddMember(settlement, "Plague", 0f, fromVersion: 24);
                AddMember(settlement, "Immunity", 0f, fromVersion: 24);
            }
        }

        AddNode(world, "GreatEvents", new JsonObject
        {
            ["Events"] = new JsonArray(),
            ["NextId"] = 1,
            ["NextEclipseTick"] = 0,
            ["LastReformations"] = new JsonObject(),
            ["DrySinceTick"] = null,
        }, fromVersion: 24);
    }
}
