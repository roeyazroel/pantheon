using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>A save file as the load menu lists it: its name, when it was written, and the world's seed and tick.</summary>
/// <param name="Path">Full path.</param>
/// <param name="Name">File name without the extension.</param>
/// <param name="WrittenUtc">When the file was last written.</param>
/// <param name="Version">Its save version.</param>
/// <param name="Seed">Its world's seed.</param>
/// <param name="Tick">Its world's tick.</param>
public sealed record SaveSummary(string Path, string Name, DateTime WrittenUtc, int Version, WorldSeed Seed, long Tick);

/// <summary>The saves in a folder (ADR-020's save slots), read without loading the worlds.</summary>
public static class SaveCatalog
{
    /// <summary>Every save in a folder, newest first; files that are not saves are left out.</summary>
    /// <param name="directory">The folder.</param>
    /// <returns>The saves; none when the folder does not exist.</returns>
    public static IReadOnlyList<SaveSummary> List(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory.GetFiles(directory, "*.json").Select(Read).OfType<SaveSummary>().OrderByDescending(s => s.WrittenUtc).ThenBy(s => s.Name, StringComparer.Ordinal)];
    }

    /// <summary>A save's summary, or <c>null</c> when the file is not a save.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The summary.</returns>
    public static SaveSummary? Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("SaveVersion", out var version) || !root.TryGetProperty("World", out var world)
                || !world.TryGetProperty("Seed", out var seed) || !world.TryGetProperty("Tick", out var tick))
            {
                return null;
            }

            return new SaveSummary(path, System.IO.Path.GetFileNameWithoutExtension(path), File.GetLastWriteTimeUtc(path), version.GetInt32(), new WorldSeed(seed.GetUInt64()), tick.GetInt64());
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
