using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 3 (ADR-023), run by <see cref="SaveMigrations"/>.</summary>
internal static class SocietySaveMigration
{
    /// <summary>
    /// Version 14 to 15: every settlement gains a society. Version 14 had none, so each starts as a
    /// newly founded settlement does: safe, fairly content and settled, poor and unlearned, knowing
    /// nothing yet, with no buildings beyond its houses and no priority.
    /// </summary>
    internal static void UpgradeFrom14(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Settlements"] is not JsonArray settlements)
        {
            return;
        }

        foreach (var node in settlements)
        {
            if (node is JsonObject settlement)
            {
                AddNode(settlement, "Society", Founded(), fromVersion: 14);
            }
        }
    }

    /// <summary>A newly founded settlement's society, as JSON.</summary>
    private static JsonObject Founded() => new()
    {
        ["Safety"] = 1f,
        ["Happiness"] = 0.6f,
        ["Wealth"] = 0f,
        ["Education"] = 0.1f,
        ["Stability"] = 0.7f,
        ["Technology"] = new JsonObject
        {
            ["Agriculture"] = 0f, ["Architecture"] = 0f, ["Medicine"] = 0f, ["Warfare"] = 0f,
            ["Navigation"] = 0f, ["Engineering"] = 0f, ["Science"] = 0f, ["Religion"] = 0f,
        },
        ["Buildings"] = new JsonArray(),
        ["Priority"] = "None",
        ["PriorityGodId"] = null,
        ["PrioritySinceTick"] = 0,
        ["Project"] = null,
        ["ProjectProgress"] = 0f,
        ["LastFoundingTick"] = 0,
    };
}
