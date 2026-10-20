using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 35 (Milestone 15, part 44, ADR-065) changes.</summary>
internal static class Version35Expectations
{
    /// <summary>Applies the expected version 35 changes to a document at version 34 (or upgraded to it): every god gains an empty persona.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 35;
        foreach (var node in root["World"]!["Gods"]!.AsArray())
        {
            node!["Persona"] = null;
        }
    }
}
