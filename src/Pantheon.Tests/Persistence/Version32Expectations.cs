using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 32 (Milestone 15, part 24, ADR-045) adds.</summary>
internal static class Version32Expectations
{
    /// <summary>Applies the expected version 32 changes to a document at version 31 (or upgraded to it): the land as the seed made it.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 32;
        root["World"]!["Land"] = new JsonObject { ["Reshapes"] = new JsonArray(), ["NextReshapeId"] = 1 };
    }
}
