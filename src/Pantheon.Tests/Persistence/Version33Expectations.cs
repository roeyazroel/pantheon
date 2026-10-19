using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 33 (Milestone 15, part 27, ADR-048) adds.</summary>
internal static class Version33Expectations
{
    /// <summary>Applies the expected version 33 changes to a document at version 32 (or upgraded to it): every village's houses whole.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 33;
        foreach (var node in root["World"]!["Settlements"]!.AsArray())
        {
            node!["Integrity"] = 1;
        }
    }
}
