using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 19 (ADR-039), run by <see cref="SaveMigrations"/>.</summary>
internal static class OfferingSaveMigration
{
    /// <summary>
    /// Version 29 to 30 (ADR-039): what a hand holds gains the food, wood and stone of an offering.
    /// Version 29 knew no offerings, so whatever a hand holds carries none.
    /// </summary>
    internal static void UpgradeFrom29(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Gods"] is not JsonArray gods)
        {
            return;
        }

        foreach (var node in gods)
        {
            if (node is JsonObject god && god["Held"] is JsonObject held)
            {
                AddMember(held, "Food", 0f, fromVersion: 29);
                AddMember(held, "Wood", 0f, fromVersion: 29);
                AddMember(held, "Stone", 0f, fromVersion: 29);
            }
        }
    }
}
