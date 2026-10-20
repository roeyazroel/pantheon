using System.Text.Json.Nodes;

namespace Pantheon.Persistence;

/// <summary>The save migrations of the gods' minds: Milestone 15, parts 44 (ADR-065) and 51 (ADR-072), run by <see cref="SaveMigrations"/>.</summary>
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

    /// <summary>
    /// Version 35 to 36 (ADR-072): every god gains a journal, what its own mind keeps of the world. A world
    /// saved before the gods' agents has none: its gods have not yet thought for themselves.
    /// </summary>
    internal static void UpgradeFrom35(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Gods"] is not JsonArray gods)
        {
            return;
        }

        foreach (var node in gods)
        {
            if (node is JsonObject god)
            {
                god["Journal"] = null;
            }
        }
    }
}
