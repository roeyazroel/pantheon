using System;
using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Presentation tuning for settlements, loaded from <c>game/Data/settlement_view.json</c>. Only
/// visuals use it; the simulation never does.
/// </summary>
public sealed record SettlementViewSettings
{
    /// <summary>File name of the settlement view content file.</summary>
    public const string FileName = "settlement_view.json";

    /// <summary>Layout of houses, the building site and fields.</summary>
    public required SettlementLayoutSettings Layout { get; init; }

    /// <summary>Parses and validates the settlement view content file.</summary>
    /// <param name="json">Text of <c>settlement_view.json</c>.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or holds out-of-range values.</exception>
    public static SettlementViewSettings Parse(string json)
    {
        var settings = ContentJson.Parse<SettlementViewSettings>(json, FileName);
        try
        {
            settings.Validate();
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{FileName}: {exception.Message}", exception);
        }

        return settings;
    }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate() => Layout.Validate();
}
