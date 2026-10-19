using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 10 (ADR-030), run by <see cref="SaveMigrations"/>.</summary>
internal static class CallingSaveMigration
{
    /// <summary>
    /// Version 21 to 22 (ADR-030): Creatures gain the paths they took up and their callings, gods what
    /// their hands hold, and regions the blessing a god hallowed. Version 21 knew none of these: no
    /// Creature has walked a path yet, every hand is empty and no land is hallowed.
    /// </summary>
    internal static void UpgradeFrom21(JsonObject root)
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
                    AddMember(region, "Hallowed", 0f, fromVersion: 21);
                }
            }
        }

        foreach (var node in world["Creatures"] as JsonArray ?? [])
        {
            if (node is JsonObject creature)
            {
                AddNode(creature, "Specialisations", new JsonArray(), fromVersion: 21);
                var callings = new JsonObject();
                foreach (var path in new[] { "Guardian", "Warrior", "Destroyer", "Healer", "NatureKeeper", "Missionary", "Trickster", "Commander" })
                {
                    callings[path] = 0f;
                }

                AddNode(creature, "Callings", callings, fromVersion: 21);
            }
        }

        foreach (var node in world["Gods"] as JsonArray ?? [])
        {
            if (node is JsonObject god)
            {
                AddNullMember(god, "Held", fromVersion: 21);
            }
        }
    }
}
