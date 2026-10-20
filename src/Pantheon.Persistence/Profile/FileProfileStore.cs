using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Pantheon.Persistence.Profile;

/// <summary>
/// The profile in plain files (ADR-060), for when SQLite's native library cannot be loaded: an
/// index in <c>profile.json</c> and each save's data in its own file beside it.
/// </summary>
public sealed class FileProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly Index _index;

    private FileProfileStore(string directory, Index index)
    {
        _directory = directory;
        _index = index;
    }

    /// <inheritdoc />
    public string Kind => "files";

    /// <summary>Opens (or creates) a profile in a directory.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The store.</returns>
    public static FileProfileStore Open(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "profile.json");
        Index? index = null;
        if (File.Exists(path))
        {
            try
            {
                index = JsonSerializer.Deserialize<Index>(File.ReadAllText(path), Options);
            }
            catch (JsonException)
            {
                index = null;
            }
        }

        return new FileProfileStore(directory, index ?? new Index());
    }

    /// <inheritdoc />
    public string? ReadValue(string key) => _index.Values.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc />
    public void WriteValue(string key, string value)
    {
        _index.Values[key] = value;
        Flush();
    }

    /// <inheritdoc />
    public WorldRecord AddWorld(WorldRecord world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var stored = world with { Id = ++_index.NextWorldId };
        _index.Worlds.Add(stored);
        Flush();
        return stored;
    }

    /// <inheritdoc />
    public void UpdateWorld(WorldRecord world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var at = _index.Worlds.FindIndex(w => w.Id == world.Id);
        if (at >= 0)
        {
            _index.Worlds[at] = world;
            Flush();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WorldRecord> Worlds() => [.. _index.Worlds.OrderByDescending(w => w.PlayedUtc).ThenByDescending(w => w.Id)];

    /// <inheritdoc />
    public void DeleteWorld(long worldId)
    {
        foreach (var save in _index.Saves.Where(s => s.WorldId == worldId).ToList())
        {
            Remove(save);
        }

        _index.Worlds.RemoveAll(w => w.Id == worldId);
        Flush();
    }

    /// <inheritdoc />
    public SaveRecord WriteSave(long worldId, SaveKind kind, string name, long tick, DateTime savedUtc, int version, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (kind != SaveKind.Manual)
        {
            foreach (var old in _index.Saves.Where(s => s.WorldId == worldId && s.Kind == kind).ToList())
            {
                Remove(old);
            }
        }

        var save = new SaveRecord(++_index.NextSaveId, worldId, kind, name, tick, savedUtc, version, data.LongLength);
        Directory.CreateDirectory(Path.Combine(_directory, "saves"));
        File.WriteAllBytes(DataPath(save.Id), data);
        _index.Saves.Add(save);
        Flush();
        return save;
    }

    /// <inheritdoc />
    public IReadOnlyList<SaveRecord> Saves(long worldId) => [.. _index.Saves.Where(s => s.WorldId == worldId).OrderByDescending(s => s.SavedUtc).ThenByDescending(s => s.Id)];

    /// <inheritdoc />
    public byte[]? ReadSave(long saveId) => File.Exists(DataPath(saveId)) ? File.ReadAllBytes(DataPath(saveId)) : null;

    /// <inheritdoc />
    public void DeleteSave(long saveId)
    {
        if (_index.Saves.Find(s => s.Id == saveId) is { } save)
        {
            Remove(save);
            Flush();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Forgets a save and its data.</summary>
    private void Remove(SaveRecord save)
    {
        _index.Saves.Remove(save);
        File.Delete(DataPath(save.Id));
    }

    /// <summary>Where a save's data lies.</summary>
    private string DataPath(long saveId) => Path.Combine(_directory, "saves", saveId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".sav");

    /// <summary>Writes the index, through a temporary file so a crash never leaves half of one.</summary>
    private void Flush()
    {
        var path = Path.Combine(_directory, "profile.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_index, Options));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The index file's shape.</summary>
    private sealed class Index
    {
        public Dictionary<string, string> Values { get; set; } = [];

        public List<WorldRecord> Worlds { get; set; } = [];

        public List<SaveRecord> Saves { get; set; } = [];

        public long NextWorldId { get; set; }

        public long NextSaveId { get; set; }
    }
}
