using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 31 (Milestone 15, part 22, ADR-043) adds.</summary>
internal static class Version31Expectations
{
    /// <summary>Applies the expected version 31 changes to a document at version 30 (or upgraded to it): no god follows a villager.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 31;
        root["World"]!["Villagers"] = new JsonArray();
        root["World"]!["NextVillagerId"] = 1;
    }
}
