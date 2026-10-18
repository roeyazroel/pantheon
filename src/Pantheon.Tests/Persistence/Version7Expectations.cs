using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 6 to 7 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version7Expectations
{
    /// <summary>Applies the expected version 7 additions to a document at version 6 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 7;
        var world = root["World"]!.AsObject();
        world["Weather"] = new JsonObject
        {
            ["Cells"] = new JsonArray(),
            ["NextCellId"] = 1,
            ["Spell"] = "Normal",
            ["SpellEndsTick"] = null,
            ["WindDirection"] = 0,
        };
        foreach (var region in world["Regions"]!["Cells"]!.AsArray().Select(node => node!.AsObject()))
        {
            region["SoilMoisture"] = region["Moisture"]!.DeepClone();
        }
    }
}
