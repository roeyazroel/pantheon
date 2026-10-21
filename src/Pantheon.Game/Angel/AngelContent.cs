using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pantheon.Domain.World;
using Pantheon.Game.Minds;
using Pantheon.Persistence;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Game.Angel;

/// <summary>
/// The player's angel (ADR-074), from <c>game/Data/angel.json</c>: its names, how each kind of deed
/// weighs in its look, how soon it forms and when it is torn, how often it speaks and asks its model,
/// how each stage looks and speaks, and the scribes' lines for every moment in each of its voices.
/// </summary>
public sealed partial record AngelContent
{
    /// <summary>The file's name.</summary>
    public const string FileName = "angel.json";

    /// <summary>What a line may name: the angel, its god, a village, another god, its god's Creature, a visiting Creature, and a detail (a miracle, a trait or a rank).</summary>
    public static IReadOnlyList<string> Placeholders { get; } = ["name", "god", "village", "other", "creature", "visitor", "detail"];

    /// <summary>The angels' names: one is chosen for each world from its seed.</summary>
    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>How each kind of deed weighs in the angel's look.</summary>
    public required AngelWeights Weights { get; init; }

    /// <summary>The weight of deeds at which the angel is half formed.</summary>
    public required float Softness { get; init; }

    /// <summary>How torn the angel must be before it shows it (0..1).</summary>
    public required float TornConflict { get; init; }

    /// <summary>How often it speaks and asks its model.</summary>
    public required AngelTiming Timing { get; init; }

    /// <summary>How hard its model thinks.</summary>
    public required ModelEffort Effort { get; init; }

    /// <summary>The longest answer it may take, in tokens.</summary>
    public required int MaxTokens { get; init; }

    /// <summary>The most deeds it is offered to suggest from.</summary>
    public required int MaxChoices { get; init; }

    /// <summary>How many of its last words it remembers when it is asked.</summary>
    public required int Remembered { get; init; }

    /// <summary>How each stage looks and speaks, by the stage's name.</summary>
    public required IReadOnlyDictionary<string, AngelStageText> Stages { get; init; }

    /// <summary>The scribes' lines, by the moment's name.</summary>
    public required IReadOnlyDictionary<string, AngelLines> Lines { get; init; }

    /// <summary>Parses and validates the file.</summary>
    /// <param name="json">Its text.</param>
    /// <returns>The validated content.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or out of range.</exception>
    public static AngelContent Parse(string json)
    {
        var content = ContentJson.Parse<AngelContent>(json, FileName);
        content.Validate();
        return content;
    }

    /// <summary>The angel's name in a world.</summary>
    /// <param name="seed">The world's seed.</param>
    /// <param name="godId">Its god.</param>
    /// <returns>The name.</returns>
    public string NameFor(WorldSeed seed, int godId) => Names[(int)(SeedMixer.Derive(seed, "angel", godId) % (ulong)Names.Count)];

    /// <summary>How a stage looks and speaks.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>Its text.</returns>
    public AngelStageText Stage(AngelStage stage) => Stages[stage.ToString()];

    /// <summary>The scribes' lines for a moment in a voice.</summary>
    /// <param name="moment">The moment.</param>
    /// <param name="tone">The voice.</param>
    /// <returns>The lines.</returns>
    public IReadOnlyList<string> LinesFor(AngelMoment moment, AngelTone tone) => Lines[moment.ToString()].Of(tone);

    /// <summary>Throws when anything is missing or out of range.</summary>
    private void Validate()
    {
        if (Names.Count < 4 || Names.Any(string.IsNullOrWhiteSpace) || Names.Distinct(StringComparer.Ordinal).Count() != Names.Count)
        {
            throw new ContentFormatException($"{FileName}: Names needs at least four different names.");
        }

        if (!(Softness is >= 0.5f and <= 1000f) || !(TornConflict is >= 0.05f and <= 1f) || !Enum.IsDefined(Effort) || MaxTokens is < 300 or > 16000
            || MaxChoices is < 2 or > 12 || Remembered is < 0 or > 20)
        {
            throw new ContentFormatException($"{FileName}: needs Softness from 0.5 to 1000, TornConflict from 0.05 to 1, an Effort, MaxTokens from 300 to 16000, MaxChoices from 2 to 12 and Remembered from 0 to 20.");
        }

        Weights.Validate();
        Timing.Validate();
        Check("Stages", Stages.Keys, Enum.GetNames<AngelStage>());
        if (Stages.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Value.Name) || string.IsNullOrWhiteSpace(s.Value.Note) || string.IsNullOrWhiteSpace(s.Value.Voice)) is { Key: { } empty })
        {
            throw new ContentFormatException($"{FileName}: Stages.{empty} needs a Name, a Note and a Voice.");
        }

        Check("Lines", Lines.Keys, Enum.GetNames<AngelMoment>());
        foreach (var (moment, lines) in Lines)
        {
            foreach (var tone in Enum.GetValues<AngelTone>())
            {
                var said = lines.Of(tone);
                if (said.Count == 0 || said.Any(string.IsNullOrWhiteSpace))
                {
                    throw new ContentFormatException($"{FileName}: Lines.{moment} needs lines for the {tone} voice.");
                }

                if (said.SelectMany(l => Placeholder().Matches(l)).Select(m => m.Groups[1].Value).FirstOrDefault(n => !Placeholders.Contains(n, StringComparer.Ordinal)) is { } unknown)
                {
                    throw new ContentFormatException($"{FileName}: Lines.{moment}.{tone} names '{{{unknown}}}', which no line may.");
                }
            }
        }
    }

    /// <summary>Throws unless a dictionary has every name, and no other.</summary>
    private static void Check(string what, IEnumerable<string> keys, IReadOnlyList<string> names)
    {
        var given = keys.ToList();
        if (names.FirstOrDefault(n => !given.Contains(n, StringComparer.Ordinal)) is { } missing)
        {
            throw new ContentFormatException($"{FileName}: {what} needs '{missing}'.");
        }

        if (given.FirstOrDefault(k => !names.Contains(k, StringComparer.Ordinal)) is { } unknown)
        {
            throw new ContentFormatException($"{FileName}: {what} has no '{unknown}'.");
        }
    }

    [GeneratedRegex(@"\{([a-z]+)\}")]
    private static partial Regex Placeholder();
}

/// <summary>How each kind of deed weighs in the angel's look (ADR-074), from 0 to 5.</summary>
public sealed record AngelWeights
{
    /// <summary>Help given to people in need.</summary>
    public required float Help { get; init; }

    /// <summary>Gifts to people who did not need them.</summary>
    public required float Gifts { get; init; }

    /// <summary>Peace made and alliances kept.</summary>
    public required float Faithfulness { get; init; }

    /// <summary>Harm done to people.</summary>
    public required float Harm { get; init; }

    /// <summary>Harm done to the god's own followers.</summary>
    public required float Cruelty { get; init; }

    /// <summary>Harm done to those who wronged it.</summary>
    public required float Vengeance { get; init; }

    /// <summary>Disasters set loose.</summary>
    public required float Upheaval { get; init; }

    /// <summary>Alliances broken and discord sown.</summary>
    public required float Deceit { get; init; }

    /// <summary>Wars declared.</summary>
    public required float Aggression { get; init; }

    /// <summary>Throws when a weight is out of range.</summary>
    internal void Validate()
    {
        if (new[] { Help, Gifts, Faithfulness, Harm, Cruelty, Vengeance, Upheaval, Deceit, Aggression }.Any(w => !(w is >= 0f and <= 5f)))
        {
            throw new ContentFormatException($"{AngelContent.FileName}: every one of Weights must be from 0 to 5.");
        }
    }
}

/// <summary>How often the angel speaks and asks its model (ADR-074), in real seconds.</summary>
public sealed record AngelTiming
{
    /// <summary>How long it waits after a moment for others to come with it.</summary>
    public required float GatherSeconds { get; init; }

    /// <summary>The least time between its answers when its god calls it.</summary>
    public required float CallSeconds { get; init; }

    /// <summary>The least time between its words when it speaks unasked: rarely, sometimes and often, as the player chooses.</summary>
    public required IReadOnlyList<float> WordsSeconds { get; init; }

    /// <summary>How long a quiet lasts before it offers what to do next: rarely, sometimes and often.</summary>
    public required IReadOnlyList<float> QuietSeconds { get; init; }

    /// <summary>The most questions it asks its model in an hour.</summary>
    public required int TurnsPerHour { get; init; }

    /// <summary>How long it rests from its model after three failures in a row, speaking by the scribes.</summary>
    public required float RestSeconds { get; init; }

    /// <summary>The least time its words stay on the screen.</summary>
    public required float ShowSeconds { get; init; }

    /// <summary>Time added for each word, to read it.</summary>
    public required float SecondsPerWord { get; init; }

    /// <summary>Throws when a time is out of range.</summary>
    internal void Validate()
    {
        if (!(GatherSeconds is >= 0f and <= 30f) || !(CallSeconds is >= 1f and <= 60f) || WordsSeconds.Count != 3 || QuietSeconds.Count != 3
            || WordsSeconds.Any(s => !(s is >= 10f and <= 1800f)) || QuietSeconds.Any(s => !(s is >= 30f and <= 3600f))
            || TurnsPerHour is < 5 or > 600 || !(RestSeconds is >= 10f and <= 3600f) || !(ShowSeconds is >= 3f and <= 60f) || !(SecondsPerWord is >= 0f and <= 2f))
        {
            throw new ContentFormatException($"{AngelContent.FileName}: Timing needs GatherSeconds from 0 to 30, CallSeconds from 1 to 60, three WordsSeconds from 10 to 1800, three QuietSeconds from 30 to 3600, TurnsPerHour from 5 to 600, RestSeconds from 10 to 3600, ShowSeconds from 3 to 60 and SecondsPerWord from 0 to 2.");
        }
    }
}

/// <summary>How a stage of the angel looks and speaks (ADR-074).</summary>
public sealed record AngelStageText
{
    /// <summary>The stage's name for the player, for example "Radiant".</summary>
    public required string Name { get; init; }

    /// <summary>What it says of the god, for the player: a short sentence.</summary>
    public required string Note { get; init; }

    /// <summary>How the angel speaks at this stage, for its model.</summary>
    public required string Voice { get; init; }
}

/// <summary>The scribes' lines for a moment, in each of the angel's voices (ADR-074).</summary>
public sealed record AngelLines
{
    /// <summary>A radiant or kind angel's.</summary>
    public required IReadOnlyList<string> Light { get; init; }

    /// <summary>An unformed angel's.</summary>
    public required IReadOnlyList<string> Silver { get; init; }

    /// <summary>A torn angel's.</summary>
    public required IReadOnlyList<string> Torn { get; init; }

    /// <summary>A stern or fallen angel's.</summary>
    public required IReadOnlyList<string> Dark { get; init; }

    /// <summary>The lines of a voice.</summary>
    /// <param name="tone">The voice.</param>
    /// <returns>Its lines.</returns>
    public IReadOnlyList<string> Of(AngelTone tone) => tone switch
    {
        AngelTone.Light => Light,
        AngelTone.Silver => Silver,
        AngelTone.Torn => Torn,
        AngelTone.Dark => Dark,
    };
}
