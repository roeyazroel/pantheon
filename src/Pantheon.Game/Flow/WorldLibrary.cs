using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Game.History;
using Pantheon.Persistence;
using Pantheon.Persistence.Profile;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Flow;

/// <summary>
/// The player's worlds and their saves in the profile (ADR-060): starting a world records it, saving
/// writes the world's versioned JSON beside what the menu shows of it (its god, size, year, rank,
/// followers and time played), and loading takes a save back into the session. The client gives
/// the time, as the core never reads the clock.
/// </summary>
public sealed class WorldLibrary
{
    private readonly IProfileStore _store;
    private readonly GameContent _content;
    private double _unsavedSeconds;

    /// <summary>Creates the library over a profile.</summary>
    /// <param name="store">The profile.</param>
    /// <param name="content">The game's content, for the worlds' sizes and years.</param>
    public WorldLibrary(IProfileStore store, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(content);
        (_store, _content) = (store, content);
    }

    /// <summary>The profile.</summary>
    public IProfileStore Store => _store;

    /// <summary>The world being played, or <c>null</c> for one not in the library (the menu's backdrop, a joined world).</summary>
    public WorldRecord? Current { get; private set; }

    /// <summary>Counts real time played towards the current world, until its next save.</summary>
    /// <param name="seconds">Real seconds.</param>
    public void AddPlayTime(double seconds)
    {
        if (Current is not null && seconds > 0 && double.IsFinite(seconds))
        {
            _unsavedSeconds += seconds;
        }
    }

    /// <summary>Records the session's world as a new world in the library and makes it current.</summary>
    /// <param name="session">The session, just given its new world.</param>
    /// <param name="name">The player's name for it, or empty for one after its first village.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <returns>Its record.</returns>
    public WorldRecord Begin(GameSession session, string? name, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        _unsavedSeconds = 0;
        Current = _store.AddWorld(Describe(session.World, session.LocalGodId, name, nowUtc));
        return Current;
    }

    /// <summary>
    /// Saves the session's world: a world not yet in the library is recorded first. An autosave and a
    /// quicksave replace the world's last of their kind.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="kind">The kind of save.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <returns>A status line.</returns>
    public string Save(GameSession session, SaveKind kind, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsReplica)
        {
            return "In a shared world only the host saves";
        }

        var world = Current ?? Begin(session, null, nowUtc);
        var year = HistoryText.Year(session.World.Tick, _content.History);
        var name = kind switch
        {
            SaveKind.Manual => Format("Year {0}, {1}", year, Season(session) ?? "saved"),
            SaveKind.Auto => "Autosave",
            SaveKind.Quick => "Quicksave",
        };
        try
        {
            var data = ProfileStores.Pack(WorldSaveSerializer.SerializeCompact(session.World));
            _store.WriteSave(world.Id, kind, name, session.World.Tick, nowUtc, WorldSaveSerializer.CurrentSaveVersion, data);
            Current = Describe(session.World, session.LocalGodId, world.Name, world.CreatedUtc) with
            {
                Id = world.Id,
                PlayedUtc = nowUtc,
                PlaySeconds = world.PlaySeconds + _unsavedSeconds,
                Outcome = world.Outcome,
            };
            _unsavedSeconds = 0;
            _store.UpdateWorld(Current);
            return Format("Saved {0}: {1}", world.Name, name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Format("Save failed: {0}", exception.Message);
        }
    }

    /// <summary>Loads a save into the session and makes its world current. On failure the session keeps its world.</summary>
    /// <param name="session">The session.</param>
    /// <param name="saveId">The save.</param>
    /// <returns>A status line.</returns>
    public string Load(GameSession session, long saveId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var record = _store.Worlds().Select(w => (World: w, Save: _store.Saves(w.Id).FirstOrDefault(s => s.Id == saveId))).FirstOrDefault(p => p.Save is not null);
        if (record.Save is null || _store.ReadSave(saveId) is not { } data)
        {
            return "That save is gone";
        }

        try
        {
            session.LoadWorld(WorldSaveSerializer.Deserialize(ProfileStores.Unpack(data)));
            Current = record.World;
            _unsavedSeconds = 0;
            return Format("Loaded {0}: {1}", record.World.Name, record.Save.Name);
        }
        catch (SaveFormatException exception)
        {
            return Format("Load failed, current world kept: {0}", exception.Message);
        }
    }

    /// <summary>The newest save of any world, to continue from, if there is one.</summary>
    /// <returns>The world and its newest save.</returns>
    public (WorldRecord World, SaveRecord Save)? Latest()
    {
        foreach (var world in _store.Worlds())
        {
            if (_store.Saves(world.Id).FirstOrDefault() is { } save)
            {
                return (world, save);
            }
        }

        return null;
    }

    /// <summary>The newest quicksave of the current world, if any.</summary>
    /// <returns>The save.</returns>
    public SaveRecord? LatestQuick() => Current is { } world ? _store.Saves(world.Id).FirstOrDefault(s => s.Kind == SaveKind.Quick) : null;

    /// <summary>Forgets a world and its saves; the current world stays playable, unrecorded.</summary>
    /// <param name="worldId">The world.</param>
    public void Delete(long worldId)
    {
        _store.DeleteWorld(worldId);
        if (Current?.Id == worldId)
        {
            Current = null;
        }
    }

    /// <summary>Leaves the current world (back at the main menu): nothing more is saved to it.</summary>
    public void Leave()
    {
        Current = null;
        _unsavedSeconds = 0;
    }

    /// <summary>Notes how the current world ended.</summary>
    /// <param name="won">Whether the player won.</param>
    public void End(bool won)
    {
        if (Current is { } world)
        {
            Current = world with { Outcome = won ? "Won" : "Lost" };
            _store.UpdateWorld(Current);
        }
    }

    /// <summary>
    /// Brings save files from before the profile into it, once (ADR-060): each becomes a world with
    /// one save, named after its file.
    /// </summary>
    /// <param name="directory">Where the old saves lie.</param>
    /// <param name="nowUtc">The time now, for files whose time cannot be read.</param>
    /// <returns>How many were brought in.</returns>
    public int ImportFiles(string directory, DateTime nowUtc)
    {
        if (_store.ReadValue("imported_save_files") is not null || !Directory.Exists(directory))
        {
            _store.WriteValue("imported_save_files", "yes");
            return 0;
        }

        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var world = WorldSaveSerializer.LoadFromFile(path);
                var written = File.GetLastWriteTimeUtc(path);
                var when = written.Year > 2000 ? written : nowUtc;
                var record = _store.AddWorld(Describe(world, null, Path.GetFileNameWithoutExtension(path), when) with { PlayedUtc = when });
                _store.WriteSave(record.Id, SaveKind.Manual, "Imported", world.Tick, when, WorldSaveSerializer.CurrentSaveVersion, ProfileStores.Pack(WorldSaveSerializer.SerializeCompact(world)));
                count++;
            }
            catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
            {
                // A file that is not a save stays where it is.
            }
        }

        _store.WriteValue("imported_save_files", "yes");
        return count;
    }

    /// <summary>Every world as the menu shows it, the most recently played first.</summary>
    /// <returns>The cards.</returns>
    public IReadOnlyList<WorldCard> Cards() =>
        [.. _store.Worlds().Select(world => new WorldCard(world, _store.Saves(world.Id), Summary(world), Details(world)))];

    /// <summary>A world's first line under its name: its god, rank and followers.</summary>
    private static string Summary(WorldRecord world) =>
        world.Rank.Length == 0 ? world.God : Format("{0} · {1} · {2:N0} followers", world.God, world.Rank, world.Followers);

    /// <summary>A world's second line: its size, seed, year, time played and when.</summary>
    private string Details(WorldRecord world) =>
        Format(
            "{0} island · seed {1} · year {2} · played {3} · {4}{5}",
            world.Size,
            world.Seed,
            HistoryText.Year(world.Tick, _content.History),
            Played(world.PlaySeconds),
            world.PlayedUtc.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            world.Outcome.Length > 0 ? " · " + world.Outcome : string.Empty);

    /// <summary>A world as the library records it.</summary>
    private WorldRecord Describe(WorldState world, int? localGodId, string? name, DateTime nowUtc)
    {
        var god = (localGodId is { } id ? world.Gods.Find(g => g.Id == id) : null) ?? world.Gods.Find(g => g.Controller == GodController.Human) ?? world.Gods.FirstOrDefault();
        var creature = god is null ? null : world.Creatures.Find(c => c.GodId == god.Id);
        var home = world.Settlements.FirstOrDefault()?.Name;
        return new WorldRecord
        {
            Name = !string.IsNullOrWhiteSpace(name) ? name.Trim() : home is null ? "A new world" : Format("The isle of {0}", home),
            God = god?.Name ?? "nobody",
            Seed = world.Seed.Value,
            Size = SizeOf(world),
            Victory = VictoryText.Settings(world.Victory),
            Seasons = world.Seasons is not null,
            Species = creature?.Species.ToString() ?? string.Empty,
            CreatedUtc = nowUtc,
            PlayedUtc = nowUtc,
            Tick = world.Tick,
            Followers = god is null ? 0 : GodStanding.CountFollowers(world, god.Id),
            Rank = god is null ? string.Empty : DivineText.Rank(god.Rank),
        };
    }

    /// <summary>The name of a world's size, by its land's width (each size has its own).</summary>
    private string SizeOf(WorldState world)
    {
        var width = world.Generation.HeightmapResolution * world.Generation.VertexSpacing;
        return _content.Sizes.Sizes.FirstOrDefault(s => Math.Abs((s.HeightmapResolution * s.VertexSpacing) - width) < width * 0.01f)?.Name
            ?? Format("{0:0} km", width / 1000f);
    }

    /// <summary>The season in words, if the world has seasons.</summary>
    private string? Season(GameSession session) =>
        Simulation.Weather.ClimateModel.SeasonOf(_content.Weather, session.World) is { } season ? Weather.SeasonText.Name(season).ToLowerInvariant() : null;

    /// <summary>Time played, such as "1 h 12 min" or "8 min".</summary>
    private static string Played(double seconds) =>
        seconds >= 3600 ? Format("{0} h {1} min", (int)(seconds / 3600), (int)(seconds % 3600 / 60)) : Format("{0} min", Math.Max(0, (int)(seconds / 60)));

    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}

/// <summary>A world as the menu lists it (ADR-060).</summary>
/// <param name="World">Its record.</param>
/// <param name="Saves">Its saves, the newest first.</param>
/// <param name="Summary">Its god, rank and followers.</param>
/// <param name="Details">Its size, seed, year, time played and when it was last played.</param>
public sealed record WorldCard(WorldRecord World, IReadOnlyList<SaveRecord> Saves, string Summary, string Details);
