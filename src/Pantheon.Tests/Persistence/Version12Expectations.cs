using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 11 to 12 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version12Expectations
{
    /// <summary>Applies the expected version 12 changes to a document at version 11 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 12;
        root["World"]!.AsObject()["History"] = new JsonArray();
    }
}
