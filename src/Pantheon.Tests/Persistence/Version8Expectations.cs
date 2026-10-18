using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 7 to 8 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version8Expectations
{
    /// <summary>Applies the expected version 8 additions to a document at version 7 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 8;
        var world = root["World"]!.AsObject();
        world["Disasters"] = new JsonObject { ["Earthquakes"] = new JsonArray(), ["NextEarthquakeId"] = 1 };
        foreach (var region in world["Regions"]!["Cells"]!.AsArray().Select(node => node!.AsObject()))
        {
            region["Fire"] = 0;
            region["Scorch"] = 0;
        }

        foreach (var settlement in world["Settlements"]!.AsArray().Select(node => node!.AsObject()))
        {
            settlement["Ruins"] = 0;
            settlement["ShieldedUntilTick"] = 0;
        }
    }
}
