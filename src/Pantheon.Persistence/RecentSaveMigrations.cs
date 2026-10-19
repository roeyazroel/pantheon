using System;
using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>
/// The save migration steps from version 6 on (Milestones 7 and later), split from
/// <see cref="SaveMigrations"/>, which runs them in order.
/// </summary>
internal static class RecentSaveMigrations
{
    /// <summary>
    /// Version 6 to 7 (Milestone 7): the world gains weather and every region the water in its soil.
    /// Version 6 had no weather, so the skies are clear, the spell is normal and not yet scheduled,
    /// the prevailing wind blows from the west, and each region's soil holds its climate's moisture,
    /// as a new world's does.
    /// </summary>
    internal static void UpgradeFrom6(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Weather", new JsonObject
        {
            ["Cells"] = new JsonArray(),
            ["NextCellId"] = 1,
            ["Spell"] = "Normal",
            ["SpellEndsTick"] = null,
            ["WindDirection"] = 0f,
        }, fromVersion: 6);

        if (world["Regions"] is JsonObject grid && grid["Cells"] is JsonArray cells)
        {
            foreach (var node in cells)
            {
                if (node is JsonObject region)
                {
                    AddMember(region, "SoilMoisture", (float)Math.Clamp(ReadNumber(region["Moisture"]), 0.0, 1.0), fromVersion: 6);
                }
            }
        }
    }

    /// <summary>
    /// Version 7 to 8 (Milestone 8): the world gains disasters, every region fire and scorch, and
    /// every settlement ruins and a shield. Version 7 had no disasters, so nothing shakes or burns,
    /// no land is scorched, nothing lies in ruins and no settlement is shielded.
    /// </summary>
    internal static void UpgradeFrom7(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Disasters", new JsonObject { ["Earthquakes"] = new JsonArray(), ["NextEarthquakeId"] = 1 }, fromVersion: 7);
        if (world["Regions"] is JsonObject grid && grid["Cells"] is JsonArray cells)
        {
            foreach (var node in cells)
            {
                if (node is JsonObject region)
                {
                    AddMember(region, "Fire", 0f, fromVersion: 7);
                    AddMember(region, "Scorch", 0f, fromVersion: 7);
                }
            }
        }

        if (world["Settlements"] is JsonArray settlements)
        {
            foreach (var node in settlements)
            {
                if (node is JsonObject settlement)
                {
                    AddNode(settlement, "Ruins", 0, fromVersion: 7);
                    AddNode(settlement, "ShieldedUntilTick", 0, fromVersion: 7);
                }
            }
        }
    }

    /// <summary>
    /// Version 8 to 9 (Milestone 9): the one winner becomes the list of winners, so that an alliance
    /// can win together. A version 8 winner becomes a list of that god; no winner, an empty list.
    /// </summary>
    internal static void UpgradeFrom8(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (!world.ContainsKey("WinnerGodId"))
        {
            throw new SaveFormatException("A version 8 save must contain 'WinnerGodId'.");
        }

        var winners = new JsonArray();
        if (world["WinnerGodId"] is JsonValue winner)
        {
            winners.Add((int)ReadNumber(winner));
        }

        world.Remove("WinnerGodId");
        AddNode(world, "WinnerGodIds", winners, fromVersion: 8);
    }

    /// <summary>
    /// Version 9 to 10 (Milestone 10): the world gains migration, every settlement its wish to leave
    /// and every god a refugee policy. Version 9 had no migration, so nobody is on the road, nobody
    /// wants to leave yet, and every god welcomes migrants (AI gods choose their own at their next
    /// evaluation).
    /// </summary>
    internal static void UpgradeFrom9(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        AddNode(world, "Migration", new JsonObject { ["Groups"] = new JsonArray(), ["NextGroupId"] = 1 }, fromVersion: 9);
        if (world["Settlements"] is JsonArray settlements)
        {
            foreach (var node in settlements)
            {
                if (node is JsonObject settlement)
                {
                    AddMember(settlement, "Emigration", 0f, fromVersion: 9);
                }
            }
        }

        if (world["Gods"] is JsonArray gods)
        {
            foreach (var node in gods)
            {
                if (node is JsonObject god)
                {
                    AddNode(god, "RefugeePolicy", "Welcome", fromVersion: 9);
                }
            }
        }
    }

    /// <summary>
    /// Version 10 to 11 (Milestone 11): every Creature gains combat skill, scars and its views of the
    /// other Creatures; its activity and memories may concern another Creature. Version 10 Creatures
    /// had never dealt with each other, so they have no skill, scars or views, and nothing they did
    /// concerned another Creature.
    /// </summary>
    internal static void UpgradeFrom10(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Creatures"] is not JsonArray creatures)
        {
            return;
        }

        foreach (var node in creatures)
        {
            if (node is not JsonObject creature)
            {
                continue;
            }

            AddMember(creature, "CombatSkill", 0f, fromVersion: 10);
            AddNode(creature, "Scars", 0, fromVersion: 10);
            AddNode(creature, "Relations", new JsonArray(), fromVersion: 10);
            if (creature["Activity"] is JsonObject activity)
            {
                AddNullMember(activity, "TargetCreatureId", fromVersion: 10);
            }

            if (creature["Memories"] is JsonArray memories)
            {
                foreach (var memory in memories)
                {
                    if (memory is JsonObject remembered)
                    {
                        AddNullMember(remembered, "OtherCreatureId", fromVersion: 10);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Version 11 to 12 (Milestone 14): the world gains its history. Version 11 kept none, so the
    /// world remembers nothing from before; it remembers from the next significant moment.
    /// </summary>
    internal static void UpgradeFrom11(JsonObject root)
    {
        if (root["World"] is JsonObject world)
        {
            AddNode(world, "History", new JsonArray(), fromVersion: 11);
        }
    }
}
