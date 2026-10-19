using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 12 (ADR-032), run by <see cref="SaveMigrations"/>.</summary>
internal static class NotableSaveMigration
{
    /// <summary>
    /// Version 22 to 23 (ADR-032): the world gains its prophets and elders, and history records the name
    /// of the individual they concern. Version 22 knew no notables: none has risen yet (elders are chosen
    /// on the first step), and no record names one.
    /// </summary>
    internal static void UpgradeFrom22(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Notables", new JsonArray(), fromVersion: 22);
        AddNode(world, "NextNotableId", JsonValue.Create(1), fromVersion: 22);
        foreach (var node in world["History"] as JsonArray ?? [])
        {
            if (node is JsonObject record)
            {
                AddNullMember(record, "Name", fromVersion: 22);
            }
        }
    }
}
