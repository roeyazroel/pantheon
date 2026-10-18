using Godot;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using IOException = System.IO.IOException;

namespace Pantheon.Client.Bootstrap;

/// <summary>Reads the data-driven content files from <c>res://Data</c> through Godot's file system.</summary>
public static class GodotContentLoader
{
    private const string DataDirectory = "res://Data/";

    /// <summary>Loads and validates the simulation content.</summary>
    /// <returns>World generation settings and settlement rules.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or out of range.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static GameContent LoadGameContent() =>
        GameContent.Parse(ReadText(ContentJson.WorldGenFileName), ReadText(ContentJson.SettlementRulesFileName));

    /// <summary>Loads and validates the presentation palette.</summary>
    /// <returns>The palette.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or out of range.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static BiomePalette LoadBiomePalette() => BiomePalette.Parse(ReadText(BiomePalette.FileName));

    /// <summary>Reads a whole content file as text.</summary>
    private static string ReadText(string fileName)
    {
        var path = DataDirectory + fileName;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"Cannot open {path}: {FileAccess.GetOpenError()}.");
        return file.GetAsText();
    }
}
