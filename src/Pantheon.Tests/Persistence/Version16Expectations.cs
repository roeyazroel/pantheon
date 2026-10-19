using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 15 to 16 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version16Expectations
{
    /// <summary>Applies the expected version 16 changes to a document at version 15 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 16;
        var world = root["World"]!.AsObject();
        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            var fields = settlement!.AsObject();
            fields["Culture"] = JsonNode.Parse("""{ "Pacifism": 0, "Militarism": 0, "Sacrifice": 0, "Charity": 0, "NatureWorship": 0, "KnowledgeSeeking": 0, "Expansionism": 0, "Isolationism": 0, "Secularism": 0 }""");
            foreach (var belief in fields["Beliefs"]!.AsArray())
            {
                belief!.AsObject()["Dependency"] = 0;
                belief.AsObject()["Resentment"] = 0;
            }
        }

        foreach (var group in world["Migration"]?["Groups"]?.AsArray() ?? [])
        {
            foreach (var belief in group!["Beliefs"]!.AsArray())
            {
                belief!.AsObject()["Dependency"] = 0;
                belief.AsObject()["Resentment"] = 0;
            }
        }

        foreach (var god in world["Gods"]!.AsArray())
        {
            god!.AsObject()["Conduct"] = JsonNode.Parse("""{ "Help": 0, "Gifts": 0, "Harm": 0, "Cruelty": 0, "Vengeance": 0, "Upheaval": 0, "Deceit": 0, "Faithfulness": 0, "Aggression": 0 }""");
            god.AsObject()["Traits"] = new JsonArray();
        }
    }
}
