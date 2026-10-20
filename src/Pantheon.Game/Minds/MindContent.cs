using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Persistence;

namespace Pantheon.Game.Minds;

/// <summary>
/// The gods' minds (ADR-065, ADR-067, ADR-072), from <c>game/Data/minds.json</c>: the Claude, GPT and
/// Gemini models offered, the local model's defaults, how the pantheon is asked for, how talk works, how
/// the gods' and Creatures' agents think, and the scribes' lore and lines for minds written offline.
/// </summary>
public sealed record MindContent
{
    /// <summary>The file's name.</summary>
    public const string FileName = "minds.json";

    /// <summary>The Claude models offered, the default first.</summary>
    public required IReadOnlyList<ModelChoice> ClaudeModels { get; init; }

    /// <summary>OpenAI's GPT models offered, the default first (ADR-067).</summary>
    public required IReadOnlyList<ModelChoice> OpenAIModels { get; init; }

    /// <summary>Google's Gemini models offered, the default first (ADR-067).</summary>
    public required IReadOnlyList<ModelChoice> GeminiModels { get; init; }

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

    /// <summary>How talking to a god works (ADR-066).</summary>
    public required TalkRules Talk { get; init; }

    /// <summary>How the gods' and Creatures' agents think (ADR-072).</summary>
    public required Agents.AgentRules Agents { get; init; }

    /// <summary>What the scribes say for the gods and Creatures when no model does (ADR-072).</summary>
    public required Agents.VoiceLines Voices { get; init; }

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
    public ModelChoice ClaudeModel(string? id) => Choose(ClaudeModels, id);

    /// <summary>The GPT model chosen, or the default when the choice is not offered.</summary>
    /// <param name="id">The model's id.</param>
    /// <returns>The model.</returns>
    public ModelChoice OpenAIModel(string? id) => Choose(OpenAIModels, id);

    /// <summary>The Gemini model chosen, or the default when the choice is not offered.</summary>
    /// <param name="id">The model's id.</param>
    /// <returns>The model.</returns>
    public ModelChoice GeminiModel(string? id) => Choose(GeminiModels, id);

    /// <summary>The model of an id among those offered, else the first.</summary>
    private static ModelChoice Choose(IReadOnlyList<ModelChoice> models, string? id) => models.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal)) ?? models[0];

    /// <summary>Throws when anything is missing or out of range.</summary>
    private void Validate()
    {
        foreach (var (name, models) in new[] { (nameof(ClaudeModels), ClaudeModels), (nameof(OpenAIModels), OpenAIModels), (nameof(GeminiModels), GeminiModels) })
        {
            if (models.Count == 0 || models.Any(m => string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Name)) || models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != models.Count)
            {
                throw new ContentFormatException($"{FileName}: {name} needs at least one model, each with its own id and a name.");
            }
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
        Talk.Validate();
        Agents.Validate(this);
        Voices.Validate();
    }
}

/// <summary>A model the settings offer: Claude, GPT or Gemini.</summary>
public sealed record ModelChoice
{
    /// <summary>The model's id, for example <c>claude-opus-5-5</c>, <c>gpt-6.1-sol</c> or <c>gemini-3.8-flash</c>.</summary>
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

/// <summary>
/// How talking to a god works (ADR-066): how far the player's words may move it, how fast that fades,
/// and how its model is asked.
/// </summary>
public sealed record TalkRules
{
    /// <summary>How far the goodwill of a conversation moves the god's judgement of a deal or a hostile act, at its fullest (0..0.5).</summary>
    public required float PersuasionReach { get; init; }

    /// <summary>How far one reply's regard of 3 (the most) moves the conversation's goodwill, from -1 to 1 (0..1).</summary>
    public required float RegardStep { get; init; }

    /// <summary>Seconds of the world's time in which a conversation's goodwill halves.</summary>
    public required float GoodwillHalfLifeSeconds { get; init; }

    /// <summary>How hard the model thinks before it answers.</summary>
    public required ModelEffort Effort { get; init; }

    /// <summary>The longest answer, in tokens.</summary>
    public required int MaxTokens { get; init; }

    /// <summary>How many turns of the conversation the model is given, the newest; older ones are forgotten.</summary>
    public required int TurnsKept { get; init; }

    /// <summary>Throws when a rule is out of range.</summary>
    internal void Validate()
    {
        if (!(PersuasionReach is >= 0f and <= 0.5f) || !(RegardStep is > 0f and <= 1f) || !(GoodwillHalfLifeSeconds > 0f)
            || !Enum.IsDefined(Effort) || MaxTokens is < 500 or > 32000 || TurnsKept is < 2 or > 100)
        {
            throw new ContentFormatException($"{MindContent.FileName}: Talk needs PersuasionReach from 0 to 0.5, RegardStep above 0 up to 1, a positive GoodwillHalfLifeSeconds, an Effort, MaxTokens from 500 to 32000 and TurnsKept from 2 to 100.");
        }
    }
}
