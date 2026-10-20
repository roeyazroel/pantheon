using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Game.Divine;

namespace Pantheon.Game.Minds;

/// <summary>A god's persona (ADR-065) as the player reads it: its title, its tale and its temper in words.</summary>
public static class PersonaText
{
    // A leaning this strong is told; a weaker one is not.
    private const float Told = 0.3f;

    /// <summary>A god's name and what it is called: its epithet, or its personality.</summary>
    /// <param name="god">The god.</param>
    /// <returns>For example "Vael the Ash-Crowned", "Vael the Tyrant" or "Ilyra".</returns>
    public static string Title(GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        return god.Persona is { } persona ? god.Name + " " + persona.Epithet
            : DivineText.Personality(god.Personality) is { Length: > 0 } personality ? god.Name + " the " + personality
            : god.Name;
    }

    /// <summary>What a god is like: its personality, voice, desire, dread and how its temper leans.</summary>
    /// <param name="god">The god.</param>
    /// <returns>For example "A Tyrant, quicker to war than most. Speaks cold and imperious. Wants every village on its knees; dreads being laughed at."; empty without a persona.</returns>
    public static string Nature(GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        if (god.Persona is not { } persona)
        {
            return string.Empty;
        }

        var personality = DivineText.Personality(god.Personality);
        var leanings = Leanings(persona.Temper);
        var kind = personality.Length == 0 ? string.Empty
            : leanings.Count == 0 ? $"{Article(personality)} {personality}. "
            : $"{Article(personality)} {personality}, {string.Join(" and ", leanings)} than most. ";
        return $"{kind}Speaks {Lower(persona.Voice)}. Wants {Lower(persona.Desire)}; dreads {Lower(persona.Dread)}.";
    }

    /// <summary>The leanings strong enough to tell, strongest first.</summary>
    /// <param name="temper">The temper.</param>
    /// <returns>For example "quicker to war", "slower to forgive".</returns>
    public static IReadOnlyList<string> Leanings(GodTemper temper)
    {
        ArgumentNullException.ThrowIfNull(temper);
        var all = new (float Leaning, string More, string Less)[]
        {
            (temper.Aggression, "quicker to war", "slower to war"),
            (temper.Cooperation, "readier to make peace", "less ready to make peace"),
            (temper.Treachery, "readier to break its word", "truer to its word"),
            (temper.Grudge, "slower to forgive", "quicker to forgive"),
            (temper.Protect, "more careful of its own", "less careful of its own"),
            (temper.Convert, "keener to win converts", "less keen to win converts"),
            (temper.Intimidate, "fonder of fear", "less fond of fear"),
        };
        return all.Where(l => MathF.Abs(l.Leaning) >= Told).OrderByDescending(l => MathF.Abs(l.Leaning)).Take(2).Select(l => l.Leaning > 0f ? l.More : l.Less).ToList();
    }

    /// <summary>A temper's leanings in numbers, for the debug panel.</summary>
    /// <param name="temper">The temper.</param>
    /// <returns>For example "aggression +0.7, grudge -0.3"; "even" when it leans nowhere.</returns>
    public static string Numbers(GodTemper temper)
    {
        ArgumentNullException.ThrowIfNull(temper);
        var all = new (string Name, float Leaning)[]
        {
            ("aggression", temper.Aggression),
            ("cooperation", temper.Cooperation),
            ("treachery", temper.Treachery),
            ("grudge", temper.Grudge),
            ("protect", temper.Protect),
            ("convert", temper.Convert),
            ("intimidate", temper.Intimidate),
        };
        var leaning = all.Where(l => l.Leaning != 0f).Select(l => l.Name + " " + l.Leaning.ToString("+0.0;-0.0", System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return leaning.Count == 0 ? "even" : string.Join(", ", leaning);
    }

    /// <summary>"A" or "an" for a word.</summary>
    private static string Article(string word) => "AEIOU".Contains(char.ToUpperInvariant(word[0]), StringComparison.Ordinal) ? "An" : "A";

    /// <summary>A line with its first letter small, to sit inside a sentence, and no full stop at its end.</summary>
    private static string Lower(string line)
    {
        var text = line.Trim().TrimEnd('.');
        return text.Length > 1 && char.IsUpper(text[0]) && !char.IsUpper(text[1]) ? char.ToLowerInvariant(text[0]) + text[1..] : text;
    }
}
