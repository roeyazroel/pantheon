using System.Linq;
using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 16 (ADR-036), run by <see cref="SaveMigrations"/>.</summary>
internal static class PlanSaveMigration
{
    /// <summary>
    /// Version 26 to 27 (ADR-036): gods gain the gods they know, their goals and when they plan next.
    /// In version 26 every god dealt with every other, so each knows them all; none holds a goal yet,
    /// and each plans on its next turn.
    /// </summary>
    internal static void UpgradeFrom26(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Gods"] is not JsonArray gods)
        {
            return;
        }

        var ids = gods.OfType<JsonObject>().Select(g => g["Id"]?.GetValue<int>()).OfType<int>().Order().ToList();
        foreach (var god in gods.OfType<JsonObject>())
        {
            var id = god["Id"]?.GetValue<int>();
            AddNode(god, "KnownGods", new JsonArray([.. ids.Where(other => other != id).Select(other => (JsonNode)JsonValue.Create(other))]), fromVersion: 26);
            AddNode(god, "Goals", new JsonArray(), fromVersion: 26);
            AddNode(god, "NextPlanTick", JsonValue.Create(0L), fromVersion: 26);
        }
    }
}
