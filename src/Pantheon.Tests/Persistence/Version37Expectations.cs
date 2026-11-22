using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the migration to save format version 37 (Milestone 15, part 59) changes in an older save.</summary>
internal static class Version37Expectations
{
    /// <summary>
    /// Applies the expected version 37 changes to a document at version 36 (or upgraded to it): the world gains
    /// the setup of a world made before the New World page's choices, its victory no hold of its own, and every
    /// god no chosen colour.
    /// </summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 37;
        var world = root["World"]!.AsObject();
        world["Setup"] = new JsonObject { ["Calamities"] = "Normal", ["Rivals"] = "Even", ["Temper"] = "Mixed", ["Villages"] = "Normal" };
        world["Victory"]!["HoldSeconds"] = null;
        foreach (var node in world["Gods"]!.AsArray())
        {
            node!["Color"] = null;
        }
    }
}
