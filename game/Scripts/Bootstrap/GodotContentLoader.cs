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
    /// <returns>World generation, settlement and divine rules, the miracles and the gods.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or out of range.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static GameContent LoadGameContent() => GameContent.Parse(GameContentFiles.Read(ReadText));

    /// <summary>Loads and validates the presentation content.</summary>
    /// <returns>The palette, the settlement view settings and the divine view settings.</returns>
    /// <exception cref="ContentFormatException">A file is malformed or out of range.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static PresentationContent LoadPresentationContent() =>
        PresentationContent.Parse(ReadText(BiomePalette.FileName), ReadText(SettlementViewSettings.FileName), ReadText(DivineViewSettings.FileName), ReadText(WeatherViewSettings.FileName));

    /// <summary>Loads and validates the gods' minds (ADR-065): the Claude models offered and the scribes' lore.</summary>
    /// <returns>The minds' content.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or out of range.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static Pantheon.Game.Minds.MindContent LoadMindContent() => Pantheon.Game.Minds.MindContent.Parse(ReadText(Pantheon.Game.Minds.MindContent.FileName));

    /// <summary>Loads the orchestra's music (ADR-071).</summary>
    /// <returns>The validated music.</returns>
    public static Pantheon.Game.Audio.Orchestra.MusicContent LoadMusicContent() => Pantheon.Game.Audio.Orchestra.MusicContent.Parse(ReadText(Pantheon.Game.Audio.Orchestra.MusicContent.FileName));

    /// <summary>Loads the player's angel (ADR-074).</summary>
    /// <returns>The validated angel's content.</returns>
    public static Pantheon.Game.Angel.AngelContent LoadAngelContent() => Pantheon.Game.Angel.AngelContent.Parse(ReadText(Pantheon.Game.Angel.AngelContent.FileName));

    /// <summary>Reads a whole content file as text.</summary>
    private static string ReadText(string fileName)
    {
        var path = DataDirectory + fileName;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new IOException($"Cannot open {path}: {FileAccess.GetOpenError()}.");
        return file.GetAsText();
    }
}
