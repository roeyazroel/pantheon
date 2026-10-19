using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>A Creature's character in §26's words (ADR-037): brave or cowardly, kind or sadistic, and so on. Pure.</summary>
public static class TemperamentText
{
    /// <summary>A trait this high or more earns its name.</summary>
    public const float Strong = 0.65f;

    /// <summary>A trait this low or less earns its opposite's name (cowardly).</summary>
    public const float Weak = 0.35f;

    /// <summary>Loyalty from which a Creature is loyal.</summary>
    public const float Loyal = 0.8f;

    /// <summary>Loyalty up to which a Creature is independent.</summary>
    public const float Independent = 0.4f;

    /// <summary>The words <c>--temperament</c> knows, each of §26's twelve.</summary>
    public static IReadOnlyList<string> Words { get; } = ["brave", "cowardly", "kind", "sadistic", "curious", "lazy", "protective", "jealous", "playful", "aggressive", "loyal", "independent"];

    /// <summary>The traits a Creature's character earns, in a fixed order.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example ["brave", "playful", "loyal"]; empty for an even-tempered Creature.</returns>
    public static IReadOnlyList<string> Traits(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var p = creature.Personality;
        var words = new List<string>();
        void Name(bool earned, string word)
        {
            if (earned)
            {
                words.Add(word);
            }
        }

        Name(p.Courage >= Strong, "brave");
        Name(p.Courage <= Weak, "cowardly");
        Name(p.Kindness >= Strong, "kind");
        Name(p.Kindness <= 0.3f && p.Aggression >= 0.6f, "sadistic");
        Name(p.Protectiveness >= Strong, "protective");
        Name(p.Playfulness >= Strong, "playful");
        Name(p.Curiosity >= Strong, "curious");
        Name(p.Laziness >= Strong, "lazy");
        Name(p.Aggression >= Strong, "aggressive");
        Name(p.Jealousy >= Strong, "jealous");
        Name(creature.Loyalty >= Loyal, "loyal");
        Name(creature.Loyalty <= Independent, "independent");
        return words;
    }

    /// <summary>A Creature's character in a phrase.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "brave, playful and loyal", or "even-tempered".</returns>
    public static string Describe(CreatureState creature)
    {
        var words = Traits(creature);
        return words.Count switch
        {
            0 => "even-tempered",
            1 => words[0],
            _ => string.Join(", ", ((List<string>)words).GetRange(0, words.Count - 1)) + " and " + words[^1],
        };
    }

    /// <summary>Every trait's value, for the debug view and the book.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>For example "kind 0.72 aggressive 0.36 lazy 0.63 curious 0.60 brave 0.55 protective 0.48 jealous 0.31 playful 0.70".</returns>
    public static string Values(CreaturePersonality personality)
    {
        ArgumentNullException.ThrowIfNull(personality);
        return string.Format(
            CultureInfo.InvariantCulture,
            "kind {0:0.00} aggressive {1:0.00} lazy {2:0.00} curious {3:0.00} brave {4:0.00} protective {5:0.00} jealous {6:0.00} playful {7:0.00}",
            personality.Kindness,
            personality.Aggression,
            personality.Laziness,
            personality.Curiosity,
            personality.Courage,
            personality.Protectiveness,
            personality.Jealousy,
            personality.Playfulness);
    }

    /// <summary>Gives a Creature a trait by its word, strongly (<c>--temperament</c>).</summary>
    /// <param name="creature">The Creature, which is changed.</param>
    /// <param name="word">One of <see cref="Words"/>.</param>
    /// <returns><c>false</c> for an unknown word.</returns>
    public static bool Give(CreatureState creature, string word)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(word);
        var p = creature.Personality;
        switch (word.Trim().ToLowerInvariant())
        {
            case "brave": p.Courage = 0.9f; break;
            case "cowardly": p.Courage = 0.1f; break;
            case "kind": p.Kindness = 0.9f; break;
            case "sadistic": (p.Kindness, p.Aggression) = (0.1f, 0.9f); break;
            case "curious": p.Curiosity = 0.9f; break;
            case "lazy": p.Laziness = 0.9f; break;
            case "protective": p.Protectiveness = 0.9f; break;
            case "jealous": p.Jealousy = 0.9f; break;
            case "playful": p.Playfulness = 0.9f; break;
            case "aggressive": p.Aggression = 0.9f; break;
            case "loyal": creature.Loyalty = 1f; break;
            case "independent": creature.Loyalty = 0.3f; break;
            default: return false;
        }

        return true;
    }
}
