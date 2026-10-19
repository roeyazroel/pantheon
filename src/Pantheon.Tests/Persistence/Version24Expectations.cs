using System;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 24 (Milestone 15, part 13, ADR-033) adds.</summary>
internal static class Version24Expectations
{
    /// <summary>Applies the expected version 24 changes to a document at version 23 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 24;
        var world = root["World"]!.AsObject();

        // An old world's generation had no starting stores and no forests and stone.
        var generation = world["Generation"]!.AsObject();
        generation["StartingSettlement"]!["Wood"] = 0f;
        generation["StartingSettlement"]!["Stone"] = 0f;
        generation["Resources"] = null;
        foreach (var node in world["Regions"]!["Cells"]!.AsArray())
        {
            var region = node!.AsObject();
            var (factor, stone) = region["Biome"]!.GetValue<string>() switch
            {
                "Grassland" => (0.6, 0.15),
                "Forest" => (1.1, 0.2),
                "Highland" => (1.6, 0.75),
                _ => (0.0, 0.0),
            };
            var vegetation = double.Parse(region["Vegetation"]!.ToJsonString(), CultureInfo.InvariantCulture);
            var forest = (float)Math.Clamp(factor * vegetation, 0.0, 1.0);
            region["Forest"] = forest;
            region["Timber"] = forest;
            region["Stone"] = (float)stone;
        }

        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            settlement!["Wood"] = 30f;
            settlement["Stone"] = 15f;
        }

        world["Roads"] = new JsonArray();
        world["NextRoadId"] = 1;
    }
}
