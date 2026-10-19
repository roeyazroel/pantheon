using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 16 to 17 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version17Expectations
{
    /// <summary>Applies the expected version 17 changes to a document at version 16 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 17;
        var diplomacy = root["World"]!["Diplomacy"]!.AsObject();
        diplomacy["Treaties"] = new JsonArray();
        diplomacy["NextTreatyId"] = 1;
        diplomacy["Favours"] = new JsonArray();
        diplomacy["Obligations"] = new JsonArray();
        diplomacy["Council"] = JsonNode.Parse("""{ "NextSessionTick": -1, "LastClosedTick": 0, "Session": null, "Tabled": [], "Decrees": [] }""");
        foreach (var proposal in diplomacy["Proposals"]!.AsArray())
        {
            proposal!.AsObject()["Terms"] = null;
            proposal.AsObject()["Ask"] = null;
        }
    }
}
