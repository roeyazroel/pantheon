using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 8 to 9 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version9Expectations
{
    /// <summary>Applies the expected version 9 changes to a document at version 8 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 9;
        var world = root["World"]!.AsObject();
        var winners = new JsonArray();
        if (world["WinnerGodId"] is JsonValue winner)
        {
            winners.Add(winner.GetValue<int>());
        }

        world.Remove("WinnerGodId");
        world["WinnerGodIds"] = winners;
    }
}
