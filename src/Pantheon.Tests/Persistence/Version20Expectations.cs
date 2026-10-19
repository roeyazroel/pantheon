using System;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 19 to 20 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version20Expectations
{
    /// <summary>Applies the expected version 20 changes to a document at version 19 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 20;
        var world = root["World"]!.AsObject();
        foreach (var region in world["Regions"]!["Cells"]!.AsArray())
        {
            var sea = region!["Biome"]!.GetValue<string>() == "Sea";
            region["Snow"] = 0f;
            region["Wildlife"] = sea ? 0f : Math.Clamp(region["Vegetation"]!.GetValue<float>(), 0f, 1f);
            region["Damage"] = 0f;
            region["Scar"] = 0f;
            region["Pollution"] = 0f;
        }

        world["Seasons"] = null;
    }
}
