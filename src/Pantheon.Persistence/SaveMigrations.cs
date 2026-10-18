using System;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>Adds a member that the older version could not contain; a present one means the save was edited or corrupted.</summary>
    private static void AddNode(JsonObject target, string name, JsonNode value, int fromVersion)
    {
        if (target.ContainsKey(name))
        {
            throw new SaveFormatException($"A version {fromVersion} save must not contain '{name}'.");
        }

        target[name] = value;
    }

    /// <summary>Adds a member that the older version could not contain; a present one means the save was edited or corrupted.</summary>
    private static void AddMember(JsonObject target, string name, float value, int fromVersion)
    {
        if (target.ContainsKey(name))
        {
            throw new SaveFormatException($"A version {fromVersion} save must not contain '{name}'.");
        }

        target[name] = value;
    }
}
