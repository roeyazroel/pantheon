using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the migration to save format version 36 (Milestone 15, part 51) changes in an older save.</summary>
internal static class Version36Expectations
{
    /// <summary>Applies the expected version 36 changes to a document at version 35 (or upgraded to it): every god gains an empty journal.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 36;
        foreach (var node in root["World"]!["Gods"]!.AsArray())
        {
            node!["Journal"] = null;
        }
    }
}
