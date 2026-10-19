using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 9 to 10 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version10Expectations
{
    /// <summary>Applies the expected version 10 changes to a document at version 9 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 10;
        var world = root["World"]!.AsObject();
        world["Migration"] = new JsonObject { ["Groups"] = new JsonArray(), ["NextGroupId"] = 1 };
        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            settlement!.AsObject()["Emigration"] = 0f;
        }

        foreach (var god in world["Gods"]!.AsArray())
        {
            god!.AsObject()["RefugeePolicy"] = "Welcome";
        }
    }
}
