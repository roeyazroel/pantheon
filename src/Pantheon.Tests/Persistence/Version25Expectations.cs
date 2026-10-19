using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 25 (Milestone 15, part 14, ADR-034) adds.</summary>
internal static class Version25Expectations
{
    /// <summary>Applies the expected version 25 changes to a document at version 24 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 25;
        var world = root["World"]!.AsObject();
        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            settlement!["Plague"] = 0f;
            settlement["Immunity"] = 0f;
        }

        world["GreatEvents"] = new JsonObject
        {
            ["Events"] = new JsonArray(),
            ["NextId"] = 1,
            ["NextEclipseTick"] = 0,
            ["LastReformations"] = new JsonObject(),
            ["DrySinceTick"] = null,
        };
    }
}
