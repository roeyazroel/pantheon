using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 23 (Milestone 15, part 12, ADR-032) adds.</summary>
internal static class Version23Expectations
{
    /// <summary>Applies the expected version 23 changes to a document at version 22 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 23;
        var world = root["World"]!.AsObject();
        world["Notables"] = new JsonArray();
        world["NextNotableId"] = 1;
        foreach (var record in world["History"]!.AsArray())
        {
            record!["Name"] = null;
        }
    }
}
