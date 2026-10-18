using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 4 to 5 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version5Expectations
{
    /// <summary>Applies the expected version 5 additions to a document at version 4 (or upgraded to it).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 5;
        var world = root["World"]!.AsObject();
        foreach (var god in world["Gods"]!.AsArray().Select(node => node!.AsObject()))
        {
            god["Personality"] = "None";
            god["DominantSinceTick"] = null;
        }

        world["WinnerGodId"] = null;
    }
}
