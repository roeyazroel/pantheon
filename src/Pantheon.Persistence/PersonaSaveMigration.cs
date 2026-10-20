using System.Text.Json.Nodes;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 44 (ADR-065), run by <see cref="SaveMigrations"/>.</summary>
internal static class PersonaSaveMigration
{
    /// <summary>
    /// Version 34 to 35 (ADR-065): every god gains a persona, its epithet, history, voice and temper.
    /// A world made before personas has none: its gods are told by their personalities alone and
    /// lean nowhere, so they play as they did.
    /// </summary>
    internal static void UpgradeFrom34(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Gods"] is not JsonArray gods)
        {
            return;
        }

        foreach (var node in gods)
        {
            if (node is JsonObject god)
            {
                god["Persona"] = null;
            }
        }
    }
}
