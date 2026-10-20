using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Persistence;

namespace Pantheon.Game.Minds;

/// <summary>
/// The gods' minds (ADR-065), from <c>game/Data/minds.json</c>: the Claude models offered, the local
/// model's defaults, how the pantheon is asked for, and the scribes' lore for gods written offline.
/// </summary>
public sealed record MindContent
{
    /// <summary>The file's name.</summary>
    public const string FileName = "minds.json";

    /// <summary>The Claude models offered, the default first.</summary>
    public required IReadOnlyList<ClaudeModelChoice> ClaudeModels { get; init; }

    /// <summary>Where a local model is served unless the player says otherwise.</summary>
    public required string LocalUrl { get; init; }

    /// <summary>The local model asked unless the player names another.</summary>
    public required string LocalModel { get; init; }

    /// <summary>How hard the model thinks about the pantheon.</summary>
    public required ModelEffort BirthEffort { get; init; }

    /// <summary>The longest answer the pantheon may take, in tokens.</summary>
    public required int BirthMaxTokens { get; init; }

    /// <summary>How long a question may take before the scribes write instead, in seconds.</summary>
    public required float TimeoutSeconds { get; init; }

    /// <summary>The scribes' lore.</summary>
    public required PersonaLore Lore { get; init; }

    /// <summary>Parses and validates the file.</summary>
    /// <param name="json">Its text.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or out of range.</exception>
    public static MindContent Parse(string json)
    {
        var content = ContentJson.Parse<MindContent>(json, FileName);
        content.Validate();
        return content;
    }

    /// <summary>The model chosen, or the default when the choice is not offered.</summary>
    /// <param name="id">The model's id.</param>
    /// <returns>The model.</returns>
    public ClaudeModelChoice ClaudeModel(string? id) => ClaudeModels.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal)) ?? ClaudeModels[0];

    /// <summary>Throws when anything is missing or out of range.</summary>
    private void Validate()
    {
        if (ClaudeModels.Count == 0 || ClaudeModels.Any(m => string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Name)) || ClaudeModels.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != ClaudeModels.Count)
        {
            throw new ContentFormatException($"{FileName}: ClaudeModels needs at least one model, each with its own id and a name.");
        }

        if (!Uri.TryCreate(LocalUrl, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) || string.IsNullOrWhiteSpace(LocalModel))
        {
            throw new ContentFormatException($"{FileName}: LocalUrl must be an http address and LocalModel a name.");
        }

        if (!Enum.IsDefined(BirthEffort) || BirthMaxTokens is < 1000 or > 64000 || !(TimeoutSeconds is >= 5f and <= 900f))
        {
            throw new ContentFormatException($"{FileName}: BirthEffort must be Low, Medium or High, BirthMaxTokens from 1000 to 64000 and TimeoutSeconds from 5 to 900.");
        }

        Lore.Validate();
    }
}

/// <summary>A Claude model the settings offer.</summary>
public sealed record ClaudeModelChoice
{
    /// <summary>The model's id, for example <c>claude-opus-5-5</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Its name, for example "Claude Opus 5.5".</summary>
    public required string Name { get; init; }

    /// <summary>What sets it apart, in a few words.</summary>
    public required string About { get; init; }
}

/// <summary>
/// What the scribes write gods from when no model is asked, or when it fails (ADR-065): names in
/// syllables, and for each personality its epithets, voices, desires, dreads and deeds. A text's
/// <c>{name}</c>, <c>{creature}</c> and <c>{animal}</c> become the god's name, its Creature's name and
/// its Creature's animal.
/// </summary>
public sealed record PersonaLore
{
    /// <summary>A name's first syllable.</summary>
    public required IReadOnlyList<string> NameStarts { get; init; }

    /// <summary>A name's middle syllable; an empty one makes a shorter name.</summary>
    public required IReadOnlyList<string> NameMiddles { get; init; }

    /// <summary>A name's last syllable.</summary>
    public required IReadOnlyList<string> NameEnds { get; init; }

    /// <summary>Names for Creatures.</summary>
    public required IReadOnlyList<string> CreatureNames { get; init; }

    /// <summary>How a god came to be: a sentence.</summary>
    public required IReadOnlyList<string> Origins { get; init; }

    /// <summary>What a god did since: a sentence.</summary>
    public required IReadOnlyList<string> Turns { get; init; }

    /// <summary>Each personality's lines.</summary>
    public required IReadOnlyList<PersonalityLore> Personalities { get; init; }

    /// <summary>A personality's lines; the first when the lore has none for it.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>Its lines.</returns>
    public PersonalityLore For(GodPersonality personality) => Personalities.FirstOrDefault(p => p.Personality == personality) ?? Personalities[0];

    /// <summary>Throws when a list is empty or a personality is missing.</summary>
    internal void Validate()
    {
        if (NameStarts.Count == 0 || NameMiddles.Count == 0 || NameEnds.Count == 0 || CreatureNames.Count < 8 || Origins.Count == 0 || Turns.Count == 0)
        {
            throw new ContentFormatException($"{MindContent.FileName}: the lore needs name syllables, at least eight Creature names, origins and turns.");
        }

        foreach (var personality in Enum.GetValues<GodPersonality>().Where(p => p != GodPersonality.None))
        {
            if (Personalities.FirstOrDefault(p => p.Personality == personality) is not { } lore
                || lore.Epithets.Count == 0 || lore.Voices.Count == 0 || lore.Desires.Count == 0 || lore.Dreads.Count == 0 || lore.Deeds.Count == 0)
            {
                throw new ContentFormatException($"{MindContent.FileName}: the lore needs epithets, voices, desires, dreads and deeds for the {personality}.");
            }
        }
    }
}

/// <summary>A personality's lines in the scribes' lore.</summary>
public sealed record PersonalityLore
{
    /// <summary>The personality.</summary>
    public required GodPersonality Personality { get; init; }

    /// <summary>Epithets, for example "the Ash-Crowned".</summary>
    public required IReadOnlyList<string> Epithets { get; init; }

    /// <summary>How such a god speaks.</summary>
    public required IReadOnlyList<string> Voices { get; init; }

    /// <summary>What such a god wants.</summary>
    public required IReadOnlyList<string> Desires { get; init; }

    /// <summary>What such a god dreads.</summary>
    public required IReadOnlyList<string> Dreads { get; init; }

    /// <summary>A deed such a god is known for: a sentence.</summary>
    public required IReadOnlyList<string> Deeds { get; init; }
}
