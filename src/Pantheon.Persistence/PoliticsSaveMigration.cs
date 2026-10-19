using System.Text.Json.Nodes;
using static Pantheon.Persistence.SaveJson;

namespace Pantheon.Persistence;

/// <summary>The save migration of Milestone 15, part 5 (ADR-025), run by <see cref="SaveMigrations"/>.</summary>
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
}
