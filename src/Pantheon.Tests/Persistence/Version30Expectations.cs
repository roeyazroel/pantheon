using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 30 (Milestone 15, part 19, ADR-039) adds.</summary>
internal static class Version30Expectations
{
    /// <summary>Applies the expected version 30 changes to a document at version 29 (or upgraded to it): whatever a hand holds carries no stores.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 30;
        foreach (var node in root["World"]!["Gods"]!.AsArray())
        {
            if (node!["Held"] is JsonObject held)
            {
                held["Food"] = 0;
                held["Wood"] = 0;
                held["Stone"] = 0;
            }
        }
    }
}
