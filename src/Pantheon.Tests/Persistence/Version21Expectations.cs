using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 20 to 21 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version21Expectations
{
    /// <summary>Applies the expected version 21 changes to a document at version 20 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 21;
        var world = root["World"]!.AsObject();
        foreach (var region in world["Regions"]!["Cells"]!.AsArray())
        {
            region!["Blight"] = 0f;
            region["Rubble"] = 0f;
        }

        var disasters = world["Disasters"]!.AsObject();
        disasters["Tornadoes"] = new JsonArray();
        disasters["Swarms"] = new JsonArray();
        disasters["NextHazardId"] = 1;
        disasters["Volcano"] = null;
        disasters["Marks"] = new JsonArray();
        disasters["BlightedSettlementIds"] = new JsonArray();
    }
}
