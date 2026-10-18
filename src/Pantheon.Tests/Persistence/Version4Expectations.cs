using System.Linq;
using System.Text.Json.Nodes;

namespace Pantheon.Tests.Persistence;

/// <summary>What the version 3 to 4 migration must add to a save, written out independently of the migration code.</summary>
internal static class Version4Expectations
{
    /// <summary>Applies the expected version 4 additions to a document at version 3 (or upgraded to it).</summary>
    /// <param name="root">Save document to change; its one god is followed in its first settlement.</param>
    public static void Apply(JsonObject root)
    {
        root["SaveVersion"] = 4;
        var world = root["World"]!.AsObject();
        var god = world["Gods"]!.AsArray().Single()!.AsObject();
        god["CreatureId"] = 1;
        var home = world["Settlements"]!.AsArray()[0]!.AsObject();
        var position = home["Position"]!.DeepClone();
        world["Creatures"] = new JsonArray(new JsonObject
        {
            ["Id"] = 1,
            ["GodId"] = 1,
            ["Name"] = "Bramble",
            ["Species"] = "Mossback",
            ["Position"] = position.DeepClone(),
            ["Heading"] = 0,
            ["Level"] = 1,
            ["Experience"] = 0,
            ["Health"] = 1,
            ["Loyalty"] = 0.7,
            ["Morality"] = 0,
            ["Needs"] = new JsonObject { ["Hunger"] = 0.2, ["Fatigue"] = 0.1, ["Boredom"] = 0.3 },
            ["Personality"] = new JsonObject { ["Kindness"] = 0.5, ["Aggression"] = 0.5, ["Laziness"] = 0.5, ["Curiosity"] = 0.5 },
            ["Activity"] = new JsonObject
            {
                ["Action"] = "Rest",
                ["Situation"] = "Idle",
                ["Phase"] = "Deciding",
                ["TargetSettlementId"] = null,
                ["Target"] = position.DeepClone(),
                ["Path"] = new JsonArray(),
                ["TicksLeft"] = 0,
            },
            ["Preferences"] = new JsonArray(
                Preference("VillageInNeed", "Feed", 0.4),
                Preference("VillageInNeed", "Ignore", 0.3),
                Preference("VillageInNeed", "EatFromStores", 0.2),
                Preference("VillageInNeed", "Attack", 0.05),
                Preference("Hungry", "EatFromStores", 0.55),
                Preference("Hungry", "Forage", 0.45),
                Preference("Tired", "Rest", 0.8),
                Preference("Bored", "Wander", 0.5),
                Preference("Idle", "Wander", 0.4),
                Preference("Idle", "Rest", 0.3),
                Preference("VillageBuilding", "HelpBuild", 0.45),
                Preference("VillageBuilding", "Ignore", 0.3),
                Preference("VillageBuilding", "Attack", 0.05),
                Preference("VillageThriving", "Play", 0.45),
                Preference("VillageThriving", "Ignore", 0.3),
                Preference("VillageThriving", "Attack", 0.05)),
            ["Memories"] = new JsonArray(),
            ["HomeSettlementId"] = home["Id"]!.DeepClone(),
        });
    }

    /// <summary>One preference.</summary>
    private static JsonObject Preference(string situation, string action, double weight) =>
        new() { ["Situation"] = situation, ["Action"] = action, ["Weight"] = weight };
}
