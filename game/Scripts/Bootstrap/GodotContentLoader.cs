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

    /// <summary>Loads and validates the presentation content.</summary>
    /// <returns>The palette and the settlement view settings.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or out of range.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static PresentationContent LoadPresentationContent() =>
        PresentationContent.Parse(ReadText(BiomePalette.FileName), ReadText(SettlementViewSettings.FileName));

    /// <summary>Reads a whole content file as text.</summary>
    private static string ReadText(string fileName)
    {
        var path = DataDirectory + fileName;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"Cannot open {path}: {FileAccess.GetOpenError()}.");
        return file.GetAsText();
    }
}
