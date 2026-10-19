using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 26 (Milestone 15, part 15, ADR-035) adds.</summary>
internal static class Version26Expectations
{
    /// <summary>Applies the expected version 26 changes to a document at version 25 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 26;
        var great = root["World"]!["GreatEvents"]!.AsObject();
        foreach (var node in great["Events"]!.AsArray())
        {
            node!["Position"] = null;
        }

        great["Beast"] = null;
        great["NextAnomalyTick"] = 0;
    }
}
