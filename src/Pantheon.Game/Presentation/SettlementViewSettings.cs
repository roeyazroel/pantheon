using System;
using System.Collections.Generic;
using Pantheon.Game.Population;
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

    /// <summary>Representative citizens (Tier 2).</summary>
    public required CitizenSettings Citizens { get; init; }

    /// <summary>How people look (ADR-049).</summary>
    public required PeopleSettings People { get; init; }

    /// <summary>The wildlife near the camera (ADR-054).</summary>
    public required WildlifeSettings Wildlife { get; init; }

    /// <summary>The ground cover near the camera (ADR-053).</summary>
    public required GroundCoverSettings GroundCover { get; init; }

    /// <summary>The trees' species, colours and sway (ADR-053).</summary>
    public required TreeSettings Trees { get; init; }

    /// <summary>How villages build (ADR-052).</summary>
    public required ArchitectureSettings Architecture { get; init; }

    /// <summary>How religion shows in villages (ADR-042).</summary>
    public required StyleSettings Style { get; init; }

    /// <summary>World streaming (ADR-050).</summary>
    public required StreamingSettings Streaming { get; init; }

    /// <summary>The debris that flies when houses fall or a thrown thing strikes (ADR-048).</summary>
    public required DebrisSettings Debris { get; init; }

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

    /// <summary>Throws when a value is out of range, listing the problems of every part.</summary>
    /// <exception cref="ArgumentException">At least one value is invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();
        Collect(errors, nameof(Layout), Layout.Validate);
        Collect(errors, nameof(Citizens), Citizens.Validate);
        Collect(errors, nameof(People), People.Validate);
        Collect(errors, nameof(Style), Style.Validate);
        Collect(errors, nameof(Architecture), Architecture.Validate);
        Collect(errors, nameof(Trees), Trees.Validate);
        Collect(errors, nameof(GroundCover), GroundCover.Validate);
        Collect(errors, nameof(Wildlife), Wildlife.Validate);
        Collect(errors, nameof(Debris), Debris.Validate);
        Collect(errors, nameof(Streaming), Streaming.Validate);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    /// <summary>Runs one part's validation and records its message, prefixed with the part's name.</summary>
    private static void Collect(List<string> errors, string part, Action validate)
    {
        try
        {
            validate();
        }
        catch (ArgumentException exception)
        {
            errors.Add($"{part}: {exception.Message}");
        }
    }
}
