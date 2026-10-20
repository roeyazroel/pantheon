using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 34 (Milestone 15, part 40, ADR-061) changes.</summary>
internal static class Version34Expectations
{
    /// <summary>Applies the expected version 34 changes to a document at version 33 (or upgraded to it): the Creatures become animals.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 34;
        foreach (var node in root["World"]!["Creatures"]!.AsArray())
        {
            node!["Species"] = node["Species"]!.GetValue<string>() switch
            {
                "Mossback" => "Cow",
                "Stonehide" => "Tortoise",
                "Swiftclaw" => "Tiger",
                "Lumen" => "Ape",
                var other => other,
            };
        }
    }
}
