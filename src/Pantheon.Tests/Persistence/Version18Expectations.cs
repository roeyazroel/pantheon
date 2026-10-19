using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 17 to 18 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version18Expectations
{
    /// <summary>Applies the expected version 18 changes to a document at version 17 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 18;
        var world = root["World"]!.AsObject();
        world["Diplomacy"]!.AsObject()["Wars"] = new JsonArray();
        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            var society = settlement!["Society"]!.AsObject();
            society["HolyWar"] = null;
            society["HolyWarEndedTick"] = 0;
        }
    }
}
