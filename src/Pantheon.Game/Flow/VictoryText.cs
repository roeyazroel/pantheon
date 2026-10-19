using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.History;
using Pantheon.Domain.World;

namespace Pantheon.Game.Flow;

/// <summary>Words for how a world may be, and was, won (spec §53, ADR-027). Pure and culture-invariant.</summary>
public static class VictoryText
{
    /// <summary>What the winners did, as a sentence's end.</summary>
    /// <param name="kind">How the world was won.</param>
    /// <returns>For example "conquered the world".</returns>
    public static string Deed(VictoryKind kind) => kind switch
    {
        VictoryKind.Ascendancy or VictoryKind.Pantheon => "won the world's belief",
        VictoryKind.Conquest => "conquered the world",
        VictoryKind.Cultural => "won the world's culture",
    };

    /// <summary>Why the local god won, told to it.</summary>
    /// <param name="kind">How the world was won.</param>
    /// <returns>For example "your followers held the world's belief for ten minutes".</returns>
    public static string Reason(VictoryKind kind) => kind switch
    {
        VictoryKind.Ascendancy or VictoryKind.Pantheon => "your followers held the world's belief for ten minutes",
        VictoryKind.Conquest => "every rival's following was a remnant for ten minutes",
        VictoryKind.Cultural => "most of the world's villages looked to you for ten minutes",
    };

    /// <summary>A history title of a victory.</summary>
    /// <param name="kind">How the world was won.</param>
    /// <param name="winners">The winners' names.</param>
    /// <returns>For example "The Conquest of the World by Vael".</returns>
    public static string Title(VictoryKind kind, string winners) => kind switch
    {
        VictoryKind.Ascendancy or VictoryKind.Pantheon => "The Ascendancy of " + winners,
        VictoryKind.Conquest => "The Conquest of the World by " + winners,
        VictoryKind.Cultural => "The Cultural Triumph of " + winners,
    };

    /// <summary>How the world was won: the latest victory in its history, or ascendancy when the history forgot it.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The kind.</returns>
    public static VictoryKind KindOf(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var record = world.History.FindLast(r => r.Kind == HistoryKind.Victory);
        return record is not null && Enum.IsDefined((VictoryKind)record.Count) ? (VictoryKind)record.Count : VictoryKind.Ascendancy;
    }

    /// <summary>The ways a new world may be won that the start screen offers, the default first, with what each asks.</summary>
    public static IReadOnlyList<(string Name, string Description, VictorySettings Settings)> Choices { get; } =
    [
        ("Divine Ascendancy", "Hold 60% of all belief for ten minutes, alone or with an ally.", VictorySettings.Default),
        ("Every victory", "Ascendancy, conquest (every rival below 5% of belief) or culture (lead three villages in four while rivals live on), each for ten minutes.", VictorySettings.All),
        ("Conquest", "Bring every rival below 5% of all belief for ten minutes.", new VictorySettings { Ascendancy = false, Conquest = true }),
        ("Culture", "Lead three of every four villages for ten minutes while your rivals live on.", new VictorySettings { Ascendancy = false, Cultural = true }),
        ("Sandbox", "No victory: the world goes on as long as you like.", VictorySettings.Sandbox),
    ];

    /// <summary>The victories a world was made with, in words.</summary>
    /// <param name="victory">The settings.</param>
    /// <returns>For example "Ascendancy and conquest", or "Sandbox: no victory".</returns>
    public static string Settings(VictorySettings victory)
    {
        ArgumentNullException.ThrowIfNull(victory);
        if (victory.IsSandbox())
        {
            return "Sandbox: no victory";
        }

        var parts = new List<string>();
        if (victory.Ascendancy)
        {
            parts.Add("ascendancy");
        }

        if (victory.Conquest)
        {
            parts.Add("conquest");
        }

        if (victory.Cultural)
        {
            parts.Add("cultural victory");
        }

        var text = parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[^1];
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>Invariant-culture string formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
