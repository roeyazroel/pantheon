using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 12 to 13 migration must change in a save, written out independently of the migration code.</summary>
internal static class Version13Expectations
{
    /// <summary>Applies the expected version 13 changes to a document at version 12 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 13;
        var world = root["World"]!.AsObject();
        world["HolySites"] = new JsonArray();
        foreach (var god in world["Gods"]!.AsArray())
        {
            god!.AsObject()["Domains"] = new JsonArray();
        }

        foreach (var settlement in world["Settlements"]!.AsArray())
        {
            var fields = settlement!.AsObject();
            fields["OldFaith"] = null;
            fields["SanctuaryGodId"] = null;
            fields["SanctuaryUntilTick"] = 0;
        }
    }
}
