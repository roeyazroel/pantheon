using System;
using System.Collections.Generic;
using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Presentation tuning for gods and miracles, loaded from <c>game/Data/divine_view.json</c>: god
/// colours, map overlay opacity and the look of each miracle. Only visuals use it.
/// </summary>
public sealed record DivineViewSettings
{
    /// <summary>File name of the divine view content file.</summary>
    public const string FileName = "divine_view.json";

    /// <summary>Colour of each god, by id order (god 1 takes the first); repeated when there are more gods.</summary>
    public required IReadOnlyList<RgbColor> GodColors { get; init; }

    /// <summary>Colour of people who follow no god.</summary>
    public required RgbColor UnaffiliatedColor { get; init; }

    /// <summary>Opacity of the map overlays where they are strongest (0..1).</summary>
    public required float OverlayOpacity { get; init; }

    /// <summary>The look of each miracle.</summary>
    public required IReadOnlyList<MiracleEffectView> Effects { get; init; }

    /// <summary>Parses and validates the divine view content file.</summary>
    /// <param name="json">Text of <c>divine_view.json</c>.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or holds out-of-range values.</exception>
    public static DivineViewSettings Parse(string json)
    {
        var settings = ContentJson.Parse<DivineViewSettings>(json, FileName);
        if (settings.GodColors.Count == 0 || !(settings.OverlayOpacity is > 0f and <= 1f))
        {
            throw new ContentFormatException($"{FileName}: GodColors must not be empty and OverlayOpacity must lie above 0 and at most 1.");
        }

        foreach (var effect in settings.Effects)
        {
            if (!Enum.IsDefined(effect.Style) || !(effect.Seconds > 0f) || string.IsNullOrWhiteSpace(effect.MiracleId))
            {
                throw new ContentFormatException($"{FileName}: effect '{effect.MiracleId}' needs a miracle id, a known style and a positive duration.");
            }
        }

        return settings;
    }

    /// <summary>Colour of a god.</summary>
    /// <param name="godId">God id, from 1.</param>
    /// <returns>The colour.</returns>
    public RgbColor GodColor(int godId) => GodColors[(int)((uint)Math.Max(0, godId - 1) % (uint)GodColors.Count)];

    /// <summary>The look of a miracle, or a golden shower when the file does not list it.</summary>
    /// <param name="miracleId">Miracle id.</param>
    /// <returns>The effect.</returns>
    public MiracleEffectView EffectFor(string miracleId)
    {
        foreach (var effect in Effects)
        {
            if (string.Equals(effect.MiracleId, miracleId, StringComparison.Ordinal))
            {
                return effect;
            }
        }

        return new MiracleEffectView { MiracleId = miracleId, Style = MiracleEffectStyle.Shower, Color = new RgbColor(1f, 0.85f, 0.4f), Seconds = 2.5f };
    }
}

/// <summary>The look of one miracle.</summary>
public sealed record MiracleEffectView
{
    /// <summary>Miracle id (<c>miracles.json</c>).</summary>
    public required string MiracleId { get; init; }

    /// <summary>Effect style.</summary>
    public required MiracleEffectStyle Style { get; init; }

    /// <summary>Effect colour.</summary>
    public required RgbColor Color { get; init; }

    /// <summary>How long the effect lasts, in seconds.</summary>
    public required float Seconds { get; init; }
}
