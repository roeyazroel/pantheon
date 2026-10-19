using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 22 (Milestone 15, part 10, ADR-030) adds.</summary>
internal static class Version22Expectations
{
    /// <summary>Applies the expected version 22 changes to a document at version 21 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 22;
        var world = root["World"]!.AsObject();
        foreach (var region in world["Regions"]!["Cells"]!.AsArray())
        {
            region!["Hallowed"] = 0f;
        }

        foreach (var creature in world["Creatures"]!.AsArray())
        {
            creature!["Specialisations"] = new JsonArray();
            creature["Callings"] = new JsonObject
            {
                ["Guardian"] = 0f,
                ["Warrior"] = 0f,
                ["Destroyer"] = 0f,
                ["Healer"] = 0f,
                ["NatureKeeper"] = 0f,
                ["Missionary"] = 0f,
                ["Trickster"] = 0f,
                ["Commander"] = 0f,
            };
        }

        foreach (var god in world["Gods"]!.AsArray())
        {
            god!["Held"] = null;
        }
    }
}
