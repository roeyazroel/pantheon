using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 29 (Milestone 15, part 18, ADR-038) adds.</summary>
internal static class Version29Expectations
{
    /// <summary>Applies the expected version 29 changes to a document at version 28 (or upgraded to it): no landforms in its generation.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 29;
        root["World"]!["Generation"]!["Landforms"] = null;
    }
}
