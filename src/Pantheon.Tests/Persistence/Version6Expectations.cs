using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 5 to 6 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version6Expectations
{
    /// <summary>Applies the expected version 6 additions to a document at version 5 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 6;
        var world = root["World"]!.AsObject();
        world["Diplomacy"] = new JsonObject { ["Relations"] = new JsonArray(), ["Stances"] = new JsonArray(), ["Proposals"] = new JsonArray() };
    }
}
