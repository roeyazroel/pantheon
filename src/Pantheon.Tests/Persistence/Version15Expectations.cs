using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 14 to 15 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version15Expectations
{
    /// <summary>Applies the expected version 15 changes to a document at version 14 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 15;
        foreach (var settlement in root["World"]!["Settlements"]!.AsArray())
        {
            settlement!.AsObject()["Society"] = JsonNode.Parse("""
                {
                  "Safety": 1, "Happiness": 0.6, "Wealth": 0, "Education": 0.1, "Stability": 0.7,
                  "Technology": { "Agriculture": 0, "Architecture": 0, "Medicine": 0, "Warfare": 0, "Navigation": 0, "Engineering": 0, "Science": 0, "Religion": 0 },
                  "Buildings": [], "Priority": "None", "PriorityGodId": null, "PrioritySinceTick": 0, "Project": null, "ProjectProgress": 0, "LastFoundingTick": 0
                }
                """);
        }
    }
}
