using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Colour of each god, by id order (god 1 takes the first); repeated when there are more gods. A god may hold another (ADR-082).</summary>
    public required IReadOnlyList<RgbColor> GodColors { get; init; }

    /// <summary>The name of each god colour, for the New World page (ADR-082).</summary>
    public required IReadOnlyList<string> GodColorNames { get; init; }

    /// <summary>Colour of people who follow no god.</summary>
    public required RgbColor UnaffiliatedColor { get; init; }

    /// <summary>Opacity of the map overlays where they are strongest (0..1).</summary>
    public required float OverlayOpacity { get; init; }

    /// <summary>The look of each miracle.</summary>
    public required IReadOnlyList<MiracleEffectView> Effects { get; init; }

    /// <summary>The look of each Creature species (ADR-021).</summary>
    public required IReadOnlyList<CreatureLook> CreatureLooks { get; init; }

    /// <summary>The colour of the magical anomaly's column of light (ADR-035).</summary>
    public RgbColor AnomalyColor { get; init; } = RgbColor.ParseHex("#b78cff");

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

        if (settings.GodColorNames.Count != settings.GodColors.Count || settings.GodColorNames.Any(string.IsNullOrWhiteSpace)
            || settings.GodColorNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.GodColorNames.Count)
        {
            throw new ContentFormatException($"{FileName}: GodColorNames needs a name of its own for each of the {settings.GodColors.Count} god colours.");
        }

        foreach (var effect in settings.Effects)
        {
            if (!Enum.IsDefined(effect.Style) || !(effect.Seconds > 0f) || string.IsNullOrWhiteSpace(effect.MiracleId))
            {
                throw new ContentFormatException($"{FileName}: effect '{effect.MiracleId}' needs a miracle id, a known style and a positive duration.");
            }
        }

        foreach (var look in settings.CreatureLooks)
        {
            if (string.IsNullOrWhiteSpace(look.Species) || !(look.Width > 0f) || !(look.Height > 0f) || !(look.Horns >= 0f) || !(look.Glow is >= 0f and <= 1f))
            {
                throw new ContentFormatException($"{FileName}: the look of '{look.Species}' needs a species, a positive width and height, horns not below 0 and a glow from 0 to 1.");
            }

            if (!Animals.AnimalPlans.Names.Contains(look.BodyName))
            {
                throw new ContentFormatException($"{FileName}: the look of '{look.Species}' has the body '{look.BodyName}', which is not one of {string.Join(", ", Animals.AnimalPlans.Names)}.");
            }
        }

        return settings;
    }

    /// <summary>The look of a species, or the first look when the file does not list it.</summary>
    /// <param name="species">The species' name.</param>
    /// <returns>The look.</returns>
    public CreatureLook LookFor(string species)
    {
        foreach (var look in CreatureLooks)
        {
            if (string.Equals(look.Species, species, StringComparison.Ordinal))
            {
                return look;
            }
        }

        return CreatureLooks.Count > 0 ? CreatureLooks[0] : CreatureLook.Default;
    }

    /// <summary>The colour a god of an id has unless it holds another (ADR-082): the colours in turn by id. Use <see cref="GodColor"/> for a world's god.</summary>
    /// <param name="godId">God id, from 1.</param>
    /// <returns>The colour.</returns>
    public RgbColor IdColor(int godId) => GodColors[SlotColor(godId)];

    /// <summary>The colour of a world's god: the one it holds (ADR-082), or its id's.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">God id, from 1.</param>
    /// <returns>The colour.</returns>
    public RgbColor GodColor(Domain.World.WorldState world, int godId) => GodColors[ColorIndex(world, godId)];

    /// <summary>The index of a world's god's colour among <see cref="GodColors"/>: the one it holds (ADR-082), or its id's.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">God id, from 1.</param>
    /// <returns>The index.</returns>
    public int ColorIndex(Domain.World.WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var god in world.Gods)
        {
            if (god.Id == godId)
            {
                return god.Color is { } held && held >= 0 && held < GodColors.Count ? held : SlotColor(godId);
            }
        }

        return SlotColor(godId);
    }

    /// <summary>The index of the colour a god of an id has unless it holds another.</summary>
    /// <param name="godId">God id, from 1.</param>
    /// <returns>The index.</returns>
    public int SlotColor(int godId) => (int)((uint)Math.Max(0, godId - 1) % (uint)GodColors.Count);

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

/// <summary>The look of a Creature species (ADR-021): its colours and proportions.</summary>
public sealed record CreatureLook
{
    /// <summary>The look of a Cow, for content without looks.</summary>
    public static readonly CreatureLook Default = new()
    {
        Species = "Cow",
        Hide = new RgbColor(0.42f, 0.36f, 0.27f),
        Belly = new RgbColor(0.78f, 0.69f, 0.52f),
        Tufts = new RgbColor(0.3f, 0.5f, 0.22f),
        Width = 1f,
        Height = 1f,
        Horns = 1f,
        Glow = 0f,
    };

    /// <summary>The species' name (<c>creatures.json</c>).</summary>
    public required string Species { get; init; }

    /// <summary>Colour of its hide.</summary>
    public required RgbColor Hide { get; init; }

    /// <summary>Colour of its belly and snout.</summary>
    public required RgbColor Belly { get; init; }

    /// <summary>Colour of the tufts on its back, tinted by its god.</summary>
    public required RgbColor Tufts { get; init; }

    /// <summary>Factor on its girth.</summary>
    public required float Width { get; init; }

    /// <summary>Factor on its height.</summary>
    public required float Height { get; init; }

    /// <summary>Factor on its horns' length; 0 for none.</summary>
    public required float Horns { get; init; }

    /// <summary>How much its hide glows (0..1).</summary>
    public required float Glow { get; init; }

    /// <summary>The body it is drawn with (ADR-061), one of the animals' or the Beast's; its species' name when not given.</summary>
    public string? Body { get; init; }

    /// <summary>The body it is drawn with: <see cref="Body"/>, or its species' name.</summary>
    public string BodyName => Body ?? Species;

    /// <summary>Colour of its muzzle or face, if not its belly's.</summary>
    public RgbColor? Muzzle { get; init; }

    /// <summary>Colour of its nose, if not its hide's.</summary>
    public RgbColor? Nose { get; init; }

    /// <summary>Colour inside its ears, if not its belly's.</summary>
    public RgbColor? InnerEar { get; init; }

    /// <summary>Colour of its horns, beak and claws, if not its belly's.</summary>
    public RgbColor? Horn { get; init; }

    /// <summary>Colour of its hooves and soles, if not its hide's.</summary>
    public RgbColor? Hoof { get; init; }

    /// <summary>Colour of a tortoise's shell.</summary>
    public RgbColor? Shell { get; init; }

    /// <summary>Colour of its stripes or patches, if not its tufts'.</summary>
    public RgbColor? Marking { get; init; }
}
