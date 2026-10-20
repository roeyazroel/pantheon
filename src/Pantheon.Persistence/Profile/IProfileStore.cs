using System;
using System.Collections.Generic;

namespace Pantheon.Persistence.Profile;

/// <summary>
/// The player's local profile (ADR-060): settings, the worlds started and their saves, and small
/// values such as the tutorial's progress. It lives in one SQLite file, or in plain files when
/// SQLite cannot be loaded. It is never a server: the game stays offline.
/// </summary>
public interface IProfileStore : IDisposable
{
    /// <summary>What keeps the profile, in words ("SQLite 3.46.1" or "files").</summary>
    string Kind { get; }

    /// <summary>A small value, or <c>null</c> when it was never written.</summary>
    /// <param name="key">Its key.</param>
    /// <returns>The value.</returns>
    string? ReadValue(string key);

    /// <summary>Writes a small value.</summary>
    /// <param name="key">Its key.</param>
    /// <param name="value">The value.</param>
    void WriteValue(string key, string value);

    /// <summary>Stores a new world.</summary>
    /// <param name="world">The world; its id is ignored.</param>
    /// <returns>The world with its id.</returns>
    WorldRecord AddWorld(WorldRecord world);

    /// <summary>Replaces a stored world's record.</summary>
    /// <param name="world">The world, with its id.</param>
    void UpdateWorld(WorldRecord world);

    /// <summary>Every world, the most recently played first.</summary>
    /// <returns>The worlds.</returns>
    IReadOnlyList<WorldRecord> Worlds();

    /// <summary>Forgets a world and all its saves.</summary>
    /// <param name="worldId">The world.</param>
    void DeleteWorld(long worldId);

    /// <summary>
    /// Writes a save of a world. A world keeps only its newest autosave and quicksave, and every save
    /// the player made.
    /// </summary>
    /// <param name="worldId">The world.</param>
    /// <param name="kind">How it came to be.</param>
    /// <param name="name">Its name.</param>
    /// <param name="tick">The world's tick.</param>
    /// <param name="savedUtc">When it is written, in UTC.</param>
    /// <param name="version">Its save version.</param>
    /// <param name="data">The compressed save.</param>
    /// <returns>The save's record.</returns>
    SaveRecord WriteSave(long worldId, SaveKind kind, string name, long tick, DateTime savedUtc, int version, byte[] data);

    /// <summary>A world's saves, the newest first.</summary>
    /// <param name="worldId">The world.</param>
    /// <returns>The saves.</returns>
    IReadOnlyList<SaveRecord> Saves(long worldId);

    /// <summary>The compressed data of a save, or <c>null</c> when there is none.</summary>
    /// <param name="saveId">The save.</param>
    /// <returns>Its data.</returns>
    byte[]? ReadSave(long saveId);

    /// <summary>Forgets one save.</summary>
    /// <param name="saveId">The save.</param>
    void DeleteSave(long saveId);
}
