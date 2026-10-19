using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 15 (ADR-035), run by <see cref="SaveMigrations"/>.</summary>
internal static class BeastSaveMigration
{
    /// <summary>
    /// Version 25 to 26 (ADR-035): the great events gain the ancient beast, the next anomaly and a
    /// place for each event. Version 25 knew none: the beast's lair is found when it is first needed,
    /// the first anomaly is set on the first step, and no event under way has a place.
    /// </summary>
    internal static void UpgradeFrom25(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["GreatEvents"] is not JsonObject great)
        {
            return;
        }

        foreach (var node in great["Events"] as JsonArray ?? [])
        {
            if (node is JsonObject @event)
            {
                AddNullMember(@event, "Position", fromVersion: 25);
            }
        }

        AddNullMember(great, "Beast", fromVersion: 25);
        AddNode(great, "NextAnomalyTick", JsonValue.Create(0), fromVersion: 25);
    }
}
