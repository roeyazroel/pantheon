using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>
/// Upgrades saves written by older builds to the current format, one version at a time, before
/// strict deserialization. Each step only edits the JSON tree; anything a step does not recognise
/// is left in place for deserialization to reject.
/// </summary>
/// <remarks>
/// Steps use literal member names, not <c>nameof</c>: they describe the format of their version,
/// which must not change when a C# property is renamed later.
/// </remarks>
internal static class SaveMigrations
{
    /// <summary>The starting preferences of a Milestone 4 Creature (spec §25's "initially").</summary>
    private static readonly (string Situation, string Action, double Weight)[] Version4StartingPreferences =
    [
        ("VillageInNeed", "Feed", 0.4),
        ("VillageInNeed", "Ignore", 0.3),
        ("VillageInNeed", "EatFromStores", 0.2),
        ("VillageInNeed", "Attack", 0.05),
        ("Hungry", "EatFromStores", 0.55),
        ("Hungry", "Forage", 0.45),
        ("Tired", "Rest", 0.8),
        ("Bored", "Wander", 0.5),
        ("Idle", "Wander", 0.4),
        ("Idle", "Rest", 0.3),
        ("VillageBuilding", "HelpBuild", 0.45),
        ("VillageBuilding", "Ignore", 0.3),
        ("VillageBuilding", "Attack", 0.05),
        ("VillageThriving", "Play", 0.45),
        ("VillageThriving", "Ignore", 0.3),
        ("VillageThriving", "Attack", 0.05),
    ];

    /// <summary>Parses an older save and upgrades it to <see cref="WorldSaveSerializer.CurrentSaveVersion"/>.</summary>
    /// <param name="json">Save-file text; its version has been read and checked already.</param>
    /// <param name="fromVersion">Version the save was written with.</param>
    /// <returns>The upgraded document, with <c>SaveVersion</c> set to the current version.</returns>
    /// <exception cref="SaveFormatException">The document cannot be upgraded.</exception>
    public static JsonObject Upgrade(string json, int fromVersion)
    {
        try
        {
            var root = JsonNode.Parse(json)!.AsObject();
            for (var version = fromVersion; version < WorldSaveSerializer.CurrentSaveVersion; version++)
            {
                UpgradeFrom(version, root);
            }

            root["SaveVersion"] = WorldSaveSerializer.CurrentSaveVersion;
            return root;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // JsonObject reports duplicate member names as ArgumentException when it is first read.
            throw new SaveFormatException($"The version {fromVersion} save file is malformed: {exception.Message}", exception);
        }
    }

    /// <summary>Applies the step from <paramref name="version"/> to the next version.</summary>
    private static void UpgradeFrom(int version, JsonObject root)
    {
        switch (version)
        {
            case 1:
                UpgradeFrom1(root);
                break;
            case 2:
                UpgradeFrom2(root);
                break;
            case 3:
                UpgradeFrom3(root);
                break;
            case 4:
                UpgradeFrom4(root);
                break;
            case 5:
                UpgradeFrom5(root);
                break;
            case 6:
                RecentSaveMigrations.UpgradeFrom6(root);
                break;
            case 7:
                RecentSaveMigrations.UpgradeFrom7(root);
                break;
            case 8:
                RecentSaveMigrations.UpgradeFrom8(root);
                break;
            case 9:
                RecentSaveMigrations.UpgradeFrom9(root);
                break;
            case 10:
                RecentSaveMigrations.UpgradeFrom10(root);
                break;
            case 11:
                RecentSaveMigrations.UpgradeFrom11(root);
                break;
            case 12:
                RecentSaveMigrations.UpgradeFrom12(root);
                break;
            case 13:
                RecentSaveMigrations.UpgradeFrom13(root);
                break;
            case 14:
                SocietySaveMigration.UpgradeFrom14(root);
                break;
            case 15:
                SocietySaveMigration.UpgradeFrom15(root);
                break;
            case 16:
                PoliticsSaveMigration.UpgradeFrom16(root);
                break;
            case 17:
                PoliticsSaveMigration.UpgradeFrom17(root);
                break;
            default:
                throw new InvalidOperationException($"No save migration is defined from version {version}.");
        }
    }

    /// <summary>
    /// Version 1 to 2 (Milestone 2): every settlement gains <c>Health</c> and
    /// <c>ConstructionProgress</c>. Version 1 had no health, so its settlements were healthy, and
    /// no house was being built.
    /// </summary>
    private static void UpgradeFrom1(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Settlements"] is not JsonArray settlements)
        {
            return;
        }

        foreach (var node in settlements)
        {
            if (node is JsonObject settlement)
            {
                AddMember(settlement, "Health", 1f, fromVersion: 1);
                AddMember(settlement, "ConstructionProgress", 0f, fromVersion: 1);
            }
        }
    }

    /// <summary>
    /// Version 2 to 3 (Milestone 3): the world gains gods, settlements beliefs and regions a
    /// blessing, and the founding settings describe one settlement. Version 2 had no gods, so the
    /// world gets the Milestone 3 player god, Ilyra, with 100 Divine Power, followed in the first
    /// settlement as a new world's home settlement is; the other settlements know no god yet.
    /// </summary>
    private static void UpgradeFrom2(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Gods", new JsonArray(new JsonObject
        {
            ["Id"] = 1,
            ["Name"] = "Ilyra",
            ["Controller"] = "Human",
            ["DivinePower"] = 100f,
            ["Rank"] = "MinorSpirit",
            ["Cooldowns"] = new JsonArray(),
        }), fromVersion: 2);

        if (world["Settlements"] is JsonArray settlements)
        {
            var first = true;
            foreach (var node in settlements)
            {
                if (node is JsonObject settlement)
                {
                    AddNode(settlement, "Beliefs", first ? new JsonArray(HomeBelief()) : new JsonArray(), fromVersion: 2);
                    first = false;
                }
            }
        }

        if (world["Regions"] is JsonObject regions && regions["Cells"] is JsonArray cells)
        {
            foreach (var node in cells)
            {
                if (node is JsonObject cell)
                {
                    AddMember(cell, "Blessing", 0f, fromVersion: 2);
                }
            }
        }

        if (world["Generation"] is JsonObject generation && generation["StartingSettlement"] is JsonObject founding)
        {
            AddNode(founding, "Count", 1, fromVersion: 2);
            AddNode(founding, "MinimumSpacing", 5, fromVersion: 2);
            AddMember(founding, "PopulationVariation", 0f, fromVersion: 2);
        }
    }

    /// <summary>
    /// Version 3 to 4 (Milestone 4): the world gains Creatures. Version 3 had none, so every god
    /// gets the Milestone 4 newborn Creature, Bramble, at the settlement where most of the people
    /// follow it (the first settlement when none does), with an even temper and the Milestone 4
    /// starting preferences, and the god's <c>CreatureId</c> points to it.
    /// </summary>
    private static void UpgradeFrom3(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Gods"] is not JsonArray gods)
        {
            return;
        }

        var creatures = new JsonArray();
        foreach (var node in gods)
        {
            if (node is not JsonObject god || ReadNumber(god["Id"]) is var number && double.IsNaN(number))
            {
                continue;
            }

            var id = (int)number;

            AddNode(god, "CreatureId", id, fromVersion: 3);
            if (FindHome(world, id) is { } home)
            {
                creatures.Add(Version4Creature(id, home));
            }
        }

        AddNode(world, "Creatures", creatures, fromVersion: 3);
    }

    /// <summary>
    /// Version 4 to 5 (Milestone 5): gods gain a personality and the tick since which they dominate,
    /// and the world a winner. Every god of a version 4 save was a player's, so each gets no
    /// personality and no dominance, and nobody has won.
    /// </summary>
    private static void UpgradeFrom4(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (world["Gods"] is JsonArray gods)
        {
            foreach (var node in gods)
            {
                if (node is JsonObject god)
                {
                    AddNode(god, "Personality", "None", fromVersion: 4);
                    AddNullMember(god, "DominantSinceTick", fromVersion: 4);
                }
            }
        }

        AddNullMember(world, "WinnerGodId", fromVersion: 4);
    }

    /// <summary>
    /// Version 5 to 6 (Milestone 6): the world gains diplomacy. Version 5 gods had no relations, so
    /// they start with none: no relations, every pair neutral, no proposals.
    /// </summary>
    private static void UpgradeFrom5(JsonObject root)
    {
        if (root["World"] is JsonObject world)
        {
            AddNode(world, "Diplomacy", new JsonObject { ["Relations"] = new JsonArray(), ["Stances"] = new JsonArray(), ["Proposals"] = new JsonArray() }, fromVersion: 5);
        }
    }

    /// <summary>The settlement where the largest share of the people follow a god, or the first settlement.</summary>
    private static (int Id, double X, double Z)? FindHome(JsonObject world, int godId)
    {
        if (world["Settlements"] is not JsonArray settlements || settlements.Count == 0)
        {
            return null;
        }

        JsonObject? best = null;
        var bestShare = -1.0;
        foreach (var node in settlements)
        {
            if (node is not JsonObject settlement || settlement["Beliefs"] is not JsonArray beliefs)
            {
                continue;
            }

            foreach (var beliefNode in beliefs)
            {
                if (beliefNode is JsonObject belief && ReadNumber(belief["GodId"]) == godId && ReadNumber(belief["Adherence"]) > bestShare)
                {
                    bestShare = ReadNumber(belief["Adherence"]);
                    best = settlement;
                }
            }
        }

        best ??= settlements[0] as JsonObject;
        if (best?["Position"] is not JsonObject position)
        {
            return null;
        }

        return ((int)ReadNumber(best["Id"]), ReadNumber(position["X"]), ReadNumber(position["Z"]));
    }

    /// <summary>The Milestone 4 newborn Creature, as the version 4 format writes it.</summary>
    private static JsonObject Version4Creature(int godId, (int Id, double X, double Z) home)
    {
        var preferences = new JsonArray();
        foreach (var (situation, action, weight) in Version4StartingPreferences)
        {
            preferences.Add(new JsonObject { ["Situation"] = situation, ["Action"] = action, ["Weight"] = weight });
        }

        var position = new JsonObject { ["X"] = home.X, ["Z"] = home.Z };
        return new JsonObject
        {
            ["Id"] = godId,
            ["GodId"] = godId,
            ["Name"] = "Bramble",
            ["Species"] = "Mossback",
            ["Position"] = position,
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
            ["Preferences"] = preferences,
            ["Memories"] = new JsonArray(),
            ["HomeSettlementId"] = home.Id,
        };
    }


    /// <summary>The Milestone 3 home belief of the player god, given to the first settlement of a version 2 save.</summary>
    private static JsonObject HomeBelief() => new()
    {
        ["GodId"] = 1,
        ["Adherence"] = 0.6f,
        ["Faith"] = 0.5f,
        ["Love"] = 0.3f,
        ["Fear"] = 0.1f,
        ["Doubt"] = 0.15f,
    };
}
