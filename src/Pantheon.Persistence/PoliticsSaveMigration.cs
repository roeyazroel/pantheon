using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migrations of Milestone 15, parts 5 and 6 (ADR-025, ADR-026), run by <see cref="SaveMigrations"/>.</summary>
internal static class PoliticsSaveMigration
{
    /// <summary>
    /// Version 16 to 17 (ADR-025): diplomacy gains treaties, favours, promises and the Divine
    /// Council. Version 16 knew none of these, so no treaty binds anyone, no favour or promise is
    /// owed, and the council has not met; the politics step schedules its first session.
    /// </summary>
    internal static void UpgradeFrom16(JsonObject root)
    {
        if (root["World"] is not JsonObject world || world["Diplomacy"] is not JsonObject diplomacy)
        {
            return;
        }

        AddNode(diplomacy, "Treaties", new JsonArray(), fromVersion: 16);
        AddNode(diplomacy, "NextTreatyId", JsonValue.Create(1), fromVersion: 16);
        AddNode(diplomacy, "Favours", new JsonArray(), fromVersion: 16);
        AddNode(diplomacy, "Obligations", new JsonArray(), fromVersion: 16);
        AddNode(
            diplomacy,
            "Council",
            new JsonObject { ["NextSessionTick"] = -1, ["LastClosedTick"] = 0, ["Session"] = null, ["Tabled"] = new JsonArray(), ["Decrees"] = new JsonArray() },
            fromVersion: 16);
    }

    /// <summary>
    /// Version 17 to 18 (ADR-026): diplomacy gains the wars' records, and settlements their holy
    /// wars. Version 17 kept neither, so wars under way have no goal or score yet (the politics
    /// step forgets nothing it has not recorded) and no settlement wages a holy war.
    /// </summary>
    internal static void UpgradeFrom17(JsonObject root)
    {
        if (root["World"] is not JsonObject world)
        {
            return;
        }

        if (world["Diplomacy"] is JsonObject diplomacy)
        {
            AddNode(diplomacy, "Wars", new JsonArray(), fromVersion: 17);
        }

        if (world["Settlements"] is JsonArray settlements)
        {
            foreach (var node in settlements)
            {
                if (node is JsonObject settlement && settlement["Society"] is JsonObject society)
                {
                    if (!society.ContainsKey("HolyWar"))
                    {
                        society["HolyWar"] = null;
                    }

                    AddNode(society, "HolyWarEndedTick", JsonValue.Create(0L), fromVersion: 17);
                }
            }
        }
    }
}
