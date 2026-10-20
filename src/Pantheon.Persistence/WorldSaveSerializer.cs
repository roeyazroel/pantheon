using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>
/// Reads and writes versioned, human-readable JSON saves of <see cref="WorldState"/>.
/// The version is checked before the rest of the file is read, so newer or malformed saves fail
/// with a clear <see cref="SaveFormatException"/>. Older saves are upgraded by
/// <see cref="SaveMigrations"/> first and then read with the same strict options as current ones.
/// </summary>
public static class WorldSaveSerializer
{
    /// <summary>
    /// Format version written by this build. Bump it, and add a step to <see cref="SaveMigrations"/>,
    /// when the save shape changes. Version 2 (Milestone 2) added settlement health and construction
    /// progress; version 3 (Milestone 3) added gods, settlement beliefs, region blessings and the
    /// founding settings for several settlements; version 4 (Milestone 4) added Creatures; version 5
    /// (Milestone 5) added god personalities, dominance and the winner; version 6 (Milestone 6) added
    /// diplomacy; version 7 (Milestone 7) added the weather and the soil's water; version 8
    /// (Milestone 8) added disasters, regions' fire and scorch, and settlements' ruins and shields;
    /// version 9 (Milestone 9) replaced the one winner with the winners, for victories won together;
    /// version 10 (Milestone 10) added migration, settlements' wish to leave and gods' refugee policies;
    /// version 11 (Milestone 11) added Creatures' combat skill, scars and views of each other;
    /// version 12 (Milestone 14) added the world's history; version 13 (Milestone 15) added gods'
    /// domains, holy sites, and settlements' Old Faiths and sanctuaries; version 14 (Milestone 15,
    /// part 2) added rivers and lakes, regions' fresh water and floods, and settlements' water;
    /// version 15 (part 3) added settlements' society: safety, happiness, wealth, education,
    /// stability, technology, buildings and priority; version 16 (part 4) added beliefs' dependency
    /// and resentment, settlements' culture, and gods' conduct and traits; version 17 (part 5) added
    /// treaties, favours, promises and the Divine Council; version 18 (part 6) added wars' goals and
    /// scores and settlements' holy wars; versions 19 to 22 (parts 7 to 10) added victories, seasons
    /// and the land's life, hazards, and Creatures' paths and gods' hands; version 23 (part 12) added
    /// prophets and elders; version 24 (part 13) added the land's forests and stone, villages' wood
    /// and stone, and roads; version 25 (part 14) added the plague, villages' immunity and the great
    /// events; version 26 (part 15) added the ancient beast, the anomaly and great events' places;
    /// version 27 (part 16) added the gods each god knows, its goals and its next plan; version 28
    /// (part 17) added Creatures' courage, protectiveness, jealousy and playfulness; version 29
    /// (part 18) added the generation's landforms; version 30 (part 19) added the stores of an
    /// offering in a god's hand; version 31 (part 22) added the villagers the gods follow; version 32
    /// (part 24) added the land's reshapings by Divine Acts; version 33 (part 27) added the structural
    /// integrity of every settlement's houses.
    /// </summary>
    public const int CurrentSaveVersion = 34;

    /// <summary>Oldest format version this build can read.</summary>
    public const int MinimumSupportedSaveVersion = 1;

    private const string TemporaryFileSuffix = ".tmp";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Serializes a world into save-file JSON.</summary>
    /// <param name="world">World to save.</param>
    /// <returns>Indented JSON whose first member is <c>SaveVersion</c>.</returns>
    public static string Serialize(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var save = new SaveGame { SaveVersion = CurrentSaveVersion, World = world };
        return JsonSerializer.Serialize(save, PantheonJson.SaveOptions);
    }

    /// <summary>Serializes a world as compact save JSON, for sending it over the network (ADR-019); <see cref="Deserialize"/> reads it.</summary>
    /// <param name="world">World to send.</param>
    /// <returns>JSON without indentation whose first member is <c>SaveVersion</c>.</returns>
    public static string SerializeCompact(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var save = new SaveGame { SaveVersion = CurrentSaveVersion, World = world };
        return JsonSerializer.Serialize(save, PantheonJson.WireOptions);
    }

    /// <summary>Parses save-file JSON back into a world.</summary>
    /// <param name="json">Save-file text.</param>
    /// <returns>The loaded world.</returns>
    /// <exception cref="SaveFormatException">The JSON is malformed, inconsistent or has an unsupported version.</exception>
    public static WorldState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var version = ReadSaveVersion(json);
        EnsureSupportedVersion(version);
        var currentJson = version == CurrentSaveVersion ? json : SaveMigrations.Upgrade(json, version).ToJsonString();

        SaveGame? save;
        try
        {
            save = JsonSerializer.Deserialize<SaveGame>(currentJson, PantheonJson.SaveOptions);
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException($"The save file is malformed: {exception.Message}", exception);
        }

        if (save is null)
        {
            throw new SaveFormatException("The save file does not contain a save object.");
        }

        SaveIntegrity.Validate(save.World);
        return save.World;
    }

    /// <summary>
    /// Writes a save file. The JSON goes to a temporary file first and then replaces the target, so
    /// a crash mid-write never leaves a truncated save behind.
    /// </summary>
    /// <param name="world">World to save.</param>
    /// <param name="path">Destination file path; missing directories are created.</param>
    public static void SaveToFile(WorldState world, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = Serialize(world);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = fullPath + TemporaryFileSuffix;
        File.WriteAllText(temporaryPath, json, Utf8WithoutBom);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }

    /// <summary>Reads a save file.</summary>
    /// <param name="path">Save file path.</param>
    /// <returns>The loaded world.</returns>
    /// <exception cref="SaveFormatException">The file content is not a valid save.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static WorldState LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Reads only the <c>SaveVersion</c> member, without binding the rest of the document.</summary>
    private static int ReadSaveVersion(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException($"The save file is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new SaveFormatException("The save file must contain a JSON object.");
            }

            if (!root.TryGetProperty(nameof(SaveGame.SaveVersion), out var element))
            {
                throw new SaveFormatException($"The save file has no {nameof(SaveGame.SaveVersion)}.");
            }

            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var version))
            {
                throw new SaveFormatException($"{nameof(SaveGame.SaveVersion)} must be an integer.");
            }

            return version;
        }
    }

    /// <summary>Rejects versions this build cannot read.</summary>
    private static void EnsureSupportedVersion(int version)
    {
        if (version > CurrentSaveVersion)
        {
            throw new SaveFormatException(
                $"The save uses format version {version}, but this build reads versions {MinimumSupportedSaveVersion} to {CurrentSaveVersion}. A newer build of the game is required.");
        }

        if (version < MinimumSupportedSaveVersion)
        {
            throw new SaveFormatException($"Save format version {version} is not supported.");
        }
    }
}
