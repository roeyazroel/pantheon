using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Game.Minds;

/// <summary>
/// The scribes (ADR-065): they write a new world's rival gods from its seed, with no model and no
/// network. Each rival draws a personality of its own, a name made of the lore's syllables, a
/// Creature's name, an epithet, a voice, a desire and a dread, a history of three sentences, and a
/// temper that leans one way more than the others. The same seed always writes the same gods.
/// </summary>
public static class PersonaScribe
{
    // How far a leaning strays at most, and how far the one that defines the god does.
    private const float LeaningSpread = 0.5f;
    private const float DefiningLeaning = 0.8f;

    // Tries for a name not already taken before a number is added.
    private const int NameTries = 40;

    /// <summary>Writes the gods of a world.</summary>
    /// <param name="lore">The scribes' lore.</param>
    /// <param name="setup">The world's gods as the content gives them, at the world's size.</param>
    /// <param name="seed">The world's seed.</param>
    /// <returns>The gods, one per slot.</returns>
    public static BornPantheon Write(PersonaLore lore, PantheonSetup setup, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(lore);
        ArgumentNullException.ThrowIfNull(setup);
        var random = new SimulationRandom(SeedMixer.Derive(seed, "pantheon-personas"));
        var personalities = Enum.GetValues<GodPersonality>().Where(p => p != GodPersonality.None).ToList();
        Shuffle(personalities, random);
        var names = new HashSet<string>(setup.Gods.Where(g => g.Controller == GodController.Human).Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
        var creatures = new HashSet<string>(setup.Gods.Where(g => g.Controller == GodController.Human).Select(g => g.CreatureName), StringComparer.OrdinalIgnoreCase);
        var gods = new List<BornGod>();
        var rival = 0;
        foreach (var slot in setup.Gods)
        {
            if (slot.Controller == GodController.Human)
            {
                gods.Add(new BornGod(slot.Name, slot.CreatureName, GodPersonality.None, null));
                continue;
            }

            var personality = personalities[rival++ % personalities.Count];
            gods.Add(Rival(lore, personality, slot.CreatureSpecies, random, names, creatures));
        }

        return new BornPantheon(gods, BornPantheon.Scribes);
    }

    /// <summary>One rival god as the scribes write it, its name and Creature's name not among those taken.</summary>
    /// <param name="lore">The scribes' lore.</param>
    /// <param name="personality">Its personality.</param>
    /// <param name="species">Its Creature's species.</param>
    /// <param name="random">The scribes' stream.</param>
    /// <param name="names">Gods' names taken; its own is added.</param>
    /// <param name="creatures">Creatures' names taken; its Creature's is added.</param>
    /// <returns>The god.</returns>
    internal static BornGod Rival(PersonaLore lore, GodPersonality personality, string species, SimulationRandom random, ISet<string> names, ISet<string> creatures)
    {
        var name = Unique(names, () => Name(lore, random), StartsLike);
        var creature = Unique(creatures, () => Pick(lore.CreatureNames, random));
        var lines = lore.For(personality);
        string Fill(string text) => text
            .Replace("{name}", name, StringComparison.Ordinal)
            .Replace("{creature}", creature, StringComparison.Ordinal)
            .Replace("{animal}", species.ToLowerInvariant(), StringComparison.Ordinal);
        var history = string.Join(' ', Fill(Pick(lore.Origins, random)), Fill(Pick(lines.Deeds, random)), Fill(Pick(lore.Turns, random)));
        var persona = new GodPersona
        {
            Epithet = Pick(lines.Epithets, random),
            History = history,
            Voice = Pick(lines.Voices, random),
            Desire = Pick(lines.Desires, random),
            Dread = Pick(lines.Dreads, random),
            Temper = Temper(random),
            Author = BornPantheon.Scribes,
        };
        return new BornGod(name, creature, personality, persona);
    }

    /// <summary>A temper: every leaning a little either way, and one that defines the god.</summary>
    private static GodTemper Temper(SimulationRandom random)
    {
        var leanings = new float[7];
        for (var i = 0; i < leanings.Length; i++)
        {
            leanings[i] = Tenth(((random.NextFloat() * 2f) - 1f) * LeaningSpread);
        }

        var defining = random.NextInt(leanings.Length);
        leanings[defining] = random.NextInt(2) == 0 ? -DefiningLeaning : DefiningLeaning;
        return GodTempers.Clamped(new GodTemper
        {
            Aggression = leanings[0],
            Cooperation = leanings[1],
            Treachery = leanings[2],
            Grudge = leanings[3],
            Protect = leanings[4],
            Convert = leanings[5],
            Intimidate = leanings[6],
        });
    }

    /// <summary>A name of a start, a middle and an end.</summary>
    private static string Name(PersonaLore lore, SimulationRandom random)
    {
        var name = Pick(lore.NameStarts, random) + Pick(lore.NameMiddles, random) + Pick(lore.NameEnds, random);
        return name.Length == 0 ? "Nameless" : char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
    }

    /// <summary>
    /// A name not yet taken (nor, when asked, too like one taken), added to those taken; a number
    /// tells two apart when the tries run out.
    /// </summary>
    private static string Unique(ISet<string> taken, Func<string> make, Func<string, string, bool>? alike = null)
    {
        for (var i = 0; i < NameTries; i++)
        {
            var name = make();
            if (!taken.Any(t => alike?.Invoke(t, name) == true) && taken.Add(name))
            {
                return name;
            }
        }

        var first = make();
        for (var n = 2; ; n++)
        {
            var numbered = first + " " + n.ToString(CultureInfo.InvariantCulture);
            if (taken.Add(numbered))
            {
                return numbered;
            }
        }
    }

    /// <summary>Whether two gods' names begin alike, so the player would mix them up: their first three letters.</summary>
    private static bool StartsLike(string a, string b) => a.Length >= 3 && b.Length >= 3 && string.Compare(a, 0, b, 0, 3, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>One of a list's items.</summary>
    private static string Pick(IReadOnlyList<string> items, SimulationRandom random) => items[random.NextInt(items.Count)];

    /// <summary>Shuffles a list in place.</summary>
    private static void Shuffle<T>(List<T> items, SimulationRandom random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>A value rounded to a tenth.</summary>
    private static float Tenth(float value) => MathF.Round(value * 10f) / 10f;
}
