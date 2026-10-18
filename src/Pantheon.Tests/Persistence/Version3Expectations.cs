using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 2 to 3 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version3Expectations
{
    /// <summary>Applies the expected version 3 additions to a version 2 document (or a version 1 document already upgraded to 2).</summary>
    /// <param name="root">Save document to change.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 3;
        var world = root["World"]!.AsObject();
        world["Gods"] = new JsonArray(new JsonObject
        {
            ["Id"] = 1,
            ["Name"] = "Ilyra",
            ["Controller"] = "Human",
            ["DivinePower"] = 100,
            ["Rank"] = "MinorSpirit",
            ["Cooldowns"] = new JsonArray(),
        });

        var first = true;
        foreach (var settlement in world["Settlements"]!.AsArray().Select(node => node!.AsObject()))
        {
            settlement["Beliefs"] = first
                ? new JsonArray(new JsonObject
                {
                    ["GodId"] = 1,
                    ["Adherence"] = 0.6,
                    ["Faith"] = 0.5,
                    ["Love"] = 0.3,
                    ["Fear"] = 0.1,
                    ["Doubt"] = 0.15,
                })
                : new JsonArray();
            first = false;
        }

        foreach (var cell in world["Regions"]!["Cells"]!.AsArray().Select(node => node!.AsObject()))
        {
            cell["Blessing"] = 0;
        }

        var founding = world["Generation"]!["StartingSettlement"]!.AsObject();
        founding["Count"] = 1;
        founding["MinimumSpacing"] = 5;
        founding["PopulationVariation"] = 0;
    }
}
