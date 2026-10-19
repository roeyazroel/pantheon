using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 28 (Milestone 15, part 17, ADR-037) adds.</summary>
internal static class Version28Expectations
{
    /// <summary>Applies the expected version 28 changes to a document at version 27 (or upgraded to it): every Creature's new traits at 0.5.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 28;
        foreach (var node in root["World"]!["Creatures"]?.AsArray() ?? [])
        {
            var personality = node!["Personality"]!.AsObject();
            personality["Courage"] = 0.5;
            personality["Protectiveness"] = 0.5;
            personality["Jealousy"] = 0.5;
            personality["Playfulness"] = 0.5;
        }
    }
}
