using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What migrating a save to version 27 (Milestone 15, part 16, ADR-036) adds.</summary>
internal static class Version27Expectations
{
    /// <summary>Applies the expected version 27 changes to a document at version 26 (or upgraded to it): every god knows every other, holds no goal and plans on its next turn.</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 27;
        var gods = root["World"]!["Gods"]!.AsArray().Select(g => g!.AsObject()).ToList();
        var ids = gods.Select(g => g["Id"]!.GetValue<int>()).Order().ToList();
        foreach (var god in gods)
        {
            var id = god["Id"]!.GetValue<int>();
            god["KnownGods"] = new JsonArray([.. ids.Where(other => other != id).Select(other => (JsonNode)JsonValue.Create(other))]);
            god["Goals"] = new JsonArray();
            god["NextPlanTick"] = 0;
        }
    }
}
