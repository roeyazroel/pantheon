using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pantheon.Domain.World;

namespace Pantheon.Game.Flow;

/// <summary>
/// What the player chose on the New World page (ADR-082), as the page shows it: names typed, items
/// picked by name, and numbers. It becomes a world's <see cref="NewWorldOptions"/>, and the profile
/// keeps the last choices so the page opens on them. The seed and the world's name are never kept.
/// </summary>
public sealed record NewWorldChoices
{
    /// <summary>The profile's key for the last choices.</summary>
    public const string ProfileKey = "new-world-choices";

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>The player's god's name; the content's when empty.</summary>
    public string GodName { get; init; } = string.Empty;

    /// <summary>The player's Creature's name; the content's when empty.</summary>
    public string CreatureName { get; init; } = string.Empty;

    /// <summary>The Creature's species, by name; the content's when <c>null</c>.</summary>
    public string? Species { get; init; }

    /// <summary>The world's size, by name; the first when <c>null</c>.</summary>
    public string? Size { get; init; }

    /// <summary>How many villages the island is founded with.</summary>
    public VillageCount Villages { get; init; } = VillageCount.Normal;

    /// <summary>The climate: four seasons, or an even one.</summary>
    public bool Seasons { get; init; } = true;

    /// <summary>The player's colour, an index into the god colours; the first god's when <c>null</c>.</summary>
    public int? Color { get; init; }

    /// <summary>How many gods; the size's own when <c>null</c>.</summary>
    public int? Gods { get; init; }

    /// <summary>The rivals' temper.</summary>
    public RivalTemper Temper { get; init; } = RivalTemper.Mixed;

    /// <summary>The rivals' strength.</summary>
    public RivalStrength Rivals { get; init; } = RivalStrength.Even;

    /// <summary>Whether the gods know one another from the start (ADR-068).</summary>
    public bool GodsMet { get; init; }

    /// <summary>The victory, by its name among <see cref="VictoryText.Choices"/>; the first when unknown.</summary>
    public string Victory { get; init; } = VictoryText.Choices[0].Name;

    /// <summary>How long a victory is held, in minutes; the divine rules' own when <c>null</c>.</summary>
    public int? HoldMinutes { get; init; }

    /// <summary>How often calamities strike.</summary>
    public CalamityLevel Calamities { get; init; } = CalamityLevel.Normal;

    /// <summary>The index of the chosen victory among <see cref="VictoryText.Choices"/>; 0 when unknown.</summary>
    [JsonIgnore]
    public int VictoryIndex => Math.Max(0, VictoryText.Choices.ToList().FindIndex(c => string.Equals(c.Name, Victory, StringComparison.Ordinal)));

    /// <summary>The options of a world made from these choices, with names and numbers the content knows.</summary>
    /// <param name="content">Validated content.</param>
    /// <returns>The options; the content's own where a choice is unknown or out of reach.</returns>
    public NewWorldOptions Options(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var size = WorldMaker.Find(content, Size)?.Name;
        var victory = VictoryIndex;
        return new NewWorldOptions(
            Simulation.Creatures.CreatureModel.FindSpecies(content.Creatures, Species)?.Name,
            size,
            victory > 0 ? VictoryText.Choices[victory].Settings : null,
            Seasons,
            GodsMet: GodsMet)
        {
            GodName = WorldChoices.CleanName(GodName),
            CreatureName = WorldChoices.CleanName(CreatureName),
            Gods = Gods is { } gods ? Math.Clamp(gods, WorldChoices.MinGods, WorldChoices.MaxGods(content, size, Villages)) : null,
            Villages = Villages,
            Color = Color is >= 0 ? Color : null,
            Temper = Temper,
            Rivals = Rivals,
            Calamities = Calamities,
            HoldMinutes = HoldMinutes is { } minutes && WorldChoices.HoldMinutes(content).Contains(minutes) ? minutes : null,
        };
    }

    /// <summary>The choices as the profile keeps them.</summary>
    /// <returns>JSON.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The choices the profile kept, or the defaults when it kept none or they cannot be read.</summary>
    /// <param name="json">What the profile kept.</param>
    /// <returns>The choices, every enum a named member.</returns>
    public static NewWorldChoices FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }

        try
        {
            var read = JsonSerializer.Deserialize<NewWorldChoices>(json, Json) ?? new();
            return Enum.IsDefined(read.Villages) && Enum.IsDefined(read.Temper) && Enum.IsDefined(read.Rivals) && Enum.IsDefined(read.Calamities)
                ? read with { GodName = read.GodName ?? string.Empty, CreatureName = read.CreatureName ?? string.Empty, Victory = read.Victory ?? VictoryText.Choices[0].Name }
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
