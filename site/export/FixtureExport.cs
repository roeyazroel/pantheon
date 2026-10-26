using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Game.Divine;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Randomness;

namespace Pantheon.SiteExport;

/// <summary>
/// What the game's own scribes and angel answer, written for the tests of the website's JavaScript
/// ports (ADR-076): the gods the scribes write for many seeds, sizes and species, with the words
/// the gods' page shows; and the angel's look and the traits for many conducts.
/// </summary>
internal static class FixtureExport
{
    // Seeds as a player types them: numbers, the largest seed, one too large, leading zeros, words.
    private static readonly string[] TextSeeds =
    [
        "1234", "0", "18446744073709551615", "18446744073709551616", "007", "-5", "first light",
        "  spaced out  ", "Ωmega 🙂", "The Ash-Crowned", "PANTHEON",
    ];

    private static readonly string?[] Species = [null, "Lion", "Cow", "Tortoise", "Wolf", "Ape"];

    // How great a conduct's deeds may be.
    private static readonly float[] Scales = [0.6f, 3f, 8f, 30f];

    // How many numbered seeds, and how many conducts, the fixtures hold.
    private const int NumberedSeeds = 48;
    private const int Conducts = 400;

    /// <summary>Writes the scribes' fixture.</summary>
    /// <param name="content">The game's content.</param>
    /// <param name="minds">The minds' content.</param>
    /// <param name="angel">The angel's content, for its name in each world.</param>
    /// <param name="path">The file.</param>
    /// <returns>How many pantheons it holds.</returns>
    public static int WriteScribes(GameContent content, MindContent minds, AngelContent angel, string path)
    {
        var sizes = content.Sizes.Sizes.Select(s => s.Name).ToList();
        var cases = new List<(string Seed, string Size, string? Species)>();
        foreach (var size in sizes)
        {
            cases.Add(("1234", size, null));
        }

        for (var i = 0; i < NumberedSeeds; i++)
        {
            cases.Add(((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), sizes[i % sizes.Count], Species[i % Species.Length]));
        }

        for (var i = 0; i < TextSeeds.Length; i++)
        {
            cases.Add((TextSeeds[i], sizes[(i + 1) % sizes.Count], Species[(i + 2) % Species.Length]));
        }

        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartArray();
        foreach (var (text, size, species) in cases)
        {
            var seed = WorldSeed.Parse(text);
            var options = new NewWorldOptions(Species: species, Size: size);
            var setup = WorldMaker.PantheonFor(content, options);
            var born = PantheonBirth.Write(content, minds, seed, options);
            json.WriteStartObject();
            json.WriteString("seed", text);
            json.WriteString("size", size);
            json.WriteString("species", species);
            json.WriteString("seedValue", seed.ToString());
            json.WriteString("angel", angel.NameFor(seed, 1));
            json.WriteStartArray("gods");
            for (var i = 0; i < born.Gods.Count; i++)
            {
                var god = born.Gods[i];
                var state = God(i + 1, god.Name, god.Personality, setup.Gods[i].Controller, god.Persona);
                json.WriteStartObject();
                json.WriteString("name", god.Name);
                json.WriteString("creature", god.CreatureName);
                json.WriteString("species", setup.Gods[i].CreatureSpecies);
                json.WriteString("personality", god.Personality.ToString());
                json.WriteString("title", PersonaText.Title(state));
                json.WriteString("kind", DivineText.Personality(god.Personality) is { Length: > 0 } kind ? kind : "A god");
                json.WriteString("history", god.Persona?.History ?? string.Empty);
                json.WriteString("nature", PersonaText.Nature(state));
                json.WriteString("temper", god.Persona is { } persona ? PersonaText.Numbers(persona.Temper) : string.Empty);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.Flush();
        return cases.Count;
    }

    /// <summary>Writes the angel's fixture: conducts drawn from a fixed stream, and what the angel and the world make of each.</summary>
    /// <param name="content">The game's content, for the traits' threshold.</param>
    /// <param name="angel">The angel's content.</param>
    /// <param name="path">The file.</param>
    /// <returns>How many conducts it holds.</returns>
    public static int WriteAngel(GameContent content, AngelContent angel, string path)
    {
        var random = new SimulationRandom(0x5157E);
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartArray();
        for (var i = 0; i < Conducts; i++)
        {
            // Gods that have done almost nothing, a little, much or a great deal, and some deeds never
            // done at all: every stage and every trait is reached.
            var scale = Scales[random.NextInt(Scales.Length)];
            float Deed() => random.NextInt(4) == 0 ? 0f : MathF.Round(random.NextFloat() * scale * 10f) / 10f;
            var conduct = new GodConduct
            {
                Help = Deed(),
                Gifts = Deed(),
                Harm = Deed(),
                Cruelty = Deed(),
                Vengeance = Deed(),
                Upheaval = Deed(),
                Deceit = Deed(),
                Faithfulness = Deed(),
                Aggression = Deed(),
            };
            var traits = ConductModel.Traits(content.DivineRules.Culture, conduct);
            var god = God(1, "Ilyra", GodPersonality.None, GodController.Human, null, conduct);
            god.Traits.AddRange(traits);

            var look = AngelLooks.Of(god, angel);
            json.WriteStartObject();
            json.WriteStartObject("conduct");
            foreach (var (name, value) in Deeds(conduct))
            {
                json.WriteNumber(name, value);
            }

            json.WriteEndObject();
            json.WriteNumber("lean", look.Lean);
            json.WriteNumber("formed", look.Formed);
            json.WriteNumber("conflict", look.Conflict);
            json.WriteString("stage", look.Stage.ToString());
            json.WriteString("tone", look.Tone.ToString());
            json.WriteString("marks", look.Marks.ToString());
            json.WriteStartArray("traits");
            foreach (var trait in traits)
            {
                json.WriteStringValue(trait.ToString());
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.Flush();
        return Conducts;
    }

    /// <summary>A conduct's deeds by name.</summary>
    private static IEnumerable<(string Name, float Value)> Deeds(GodConduct conduct) =>
    [
        (nameof(GodConduct.Help), conduct.Help),
        (nameof(GodConduct.Gifts), conduct.Gifts),
        (nameof(GodConduct.Harm), conduct.Harm),
        (nameof(GodConduct.Cruelty), conduct.Cruelty),
        (nameof(GodConduct.Vengeance), conduct.Vengeance),
        (nameof(GodConduct.Upheaval), conduct.Upheaval),
        (nameof(GodConduct.Deceit), conduct.Deceit),
        (nameof(GodConduct.Faithfulness), conduct.Faithfulness),
        (nameof(GodConduct.Aggression), conduct.Aggression),
    ];

    /// <summary>A god as the founder makes one, for the words that read a god.</summary>
    private static GodState God(int id, string name, GodPersonality personality, GodController controller, GodPersona? persona, GodConduct? conduct = null) => new()
    {
        Id = id,
        Name = name,
        Controller = controller,
        Personality = personality,
        DivinePower = 0f,
        Rank = DivineRank.MinorSpirit,
        DominantSinceTick = null,
        CreatureId = null,
        RefugeePolicy = Domain.Migration.RefugeePolicy.Welcome,
        Cooldowns = [],
        Domains = [],
        Conduct = conduct ?? GodConduct.None(),
        Traits = [],
        Persona = persona,
    };
}
