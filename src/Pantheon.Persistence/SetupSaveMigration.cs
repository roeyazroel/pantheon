using System.Text.Json.Nodes;

namespace Pantheon.Persistence;

/// <summary>The save migration of the New World page's choices: Milestone 15, part 59 (ADR-082), run by <see cref="SaveMigrations"/>.</summary>
internal static class SetupSaveMigration
{
    /// <summary>
    /// Version 36 to 37 (ADR-082): the world gains its setup, its victory how long it must be held, and
    /// every god the colour it chose. A world made before these choices had normal calamities, even
    /// rivals of any temper and its size's villages; its victory is held for the divine rules' time and
    /// its gods wear their ids' colours, so it plays and looks as it did.
    /// </summary>
    internal static void UpgradeFrom36(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        world["Setup"] = new JsonObject
        {
            ["Calamities"] = "Normal",
            ["Rivals"] = "Even",
            ["Temper"] = "Mixed",
            ["Villages"] = "Normal",
        };
        if (world["Victory"] is JsonObject victory)
        {
            victory["HoldSeconds"] = null;
        }

        if (world["Gods"] is JsonArray gods)
        {
            foreach (var node in gods)
            {
                if (node is JsonObject god)
                {
                    god["Color"] = null;
                }
            }
        }
    }
}
