using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 18 to 19 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version19Expectations
{
    /// <summary>Applies the expected version 19 changes to a document at version 18 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 19;
        var world = root["World"]!.AsObject();
        world["Victory"] = new JsonObject { ["Ascendancy"] = true, ["Conquest"] = false, ["Cultural"] = false, ["PantheonMaxGods"] = null };
        foreach (var god in world["Gods"]!.AsArray())
        {
            god!["ConquestSinceTick"] = null;
            god["CulturalSinceTick"] = null;
        }
    }
}
