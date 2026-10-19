using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 10 to 11 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version11Expectations
{
    /// <summary>Applies the expected version 11 changes to a document at version 10 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 11;
        foreach (var node in root["World"]!["Creatures"]!.AsArray())
        {
            var creature = node!.AsObject();
            creature["CombatSkill"] = 0f;
            creature["Scars"] = 0;
            creature["Relations"] = new JsonArray();
            creature["Activity"]!.AsObject()["TargetCreatureId"] = null;
            foreach (var memory in creature["Memories"]!.AsArray())
            {
                memory!.AsObject()["OtherCreatureId"] = null;
            }
        }
    }
}
