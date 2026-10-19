using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 13 to 14 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version14Expectations
{
    /// <summary>Applies the expected version 14 changes to a document at version 13 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 14;
        var world = root["World"]!.AsObject();
        world["Water"] = new JsonObject { ["Rivers"] = new JsonArray(), ["Lakes"] = new JsonArray() };

        // The world was generated without rivers, and its settlements without water to store or seek.
        var generation = world["Generation"]!.AsObject();
        generation["Hydrology"] = null;
        generation["StartingSettlement"]!.AsObject()["Water"] = 0;
        generation["StartingSettlement"]!.AsObject()["WaterPreference"] = 0;
        foreach (var region in world["Regions"]!["Cells"]!.AsArray())
        {
            region!.AsObject()["FreshWater"] = 0;
            region.AsObject()["Flood"] = 0;
        }

        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            var fields = settlement!.AsObject();
            fields["Water"] = fields["Housing"]!.GetValue<int>() * 6;
        }
    }
}
