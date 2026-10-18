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
