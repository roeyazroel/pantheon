using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Minds;

/// <summary>
/// What a language model is asked when a world is made (ADR-065), and how its answer is read: for
/// every rival slot, a god with a name, an epithet, a personality of the eight, a Creature's name, a
/// history, a voice, a desire, a dread and a temper. An answer is read strictly: a god whose answer
/// is missing, malformed, too long or named like another is the scribes' instead.
/// </summary>
public static class PantheonPrompt
{
    // The longest of each line the model may write, in characters.
    private const int MaxName = 18;
    private const int MaxEpithet = 40;
    private const int MaxHistory = 700;
    private const int MaxLine = 160;

    // A temper's leanings are asked as whole numbers from -3 to 3.
    private const float TemperSteps = 3f;

    private static readonly string[] Leanings = ["aggression", "cooperation", "treachery", "grudge", "protect", "convert", "intimidate"];

    /// <summary>The standing instructions.</summary>
    public const string Instructions = """
        You are the mythographer of PANTHEON, a god game in the spirit of Black & White. A new world is being made: an island of villages, each believing in one god or another. The player is a minor spirit who must win the island's belief; the other gods are rivals, played by the game's AI, who will ally with the player, trade, betray and make war.

        Write the rival gods. For each rival slot you are given, invent a god:
        - name: one or two words, easy to say aloud, at most 18 characters; invented, never a god of a real religion or mythology, and never the player's name or another rival's.
        - epithet: what the god is called after its name, at most 40 characters, for example "the Ash-Crowned" or "of the Green Deep".
        - personality: one of Guardian, Tyrant, Opportunist, Zealot, NatureKeeper, Trickster, Isolationist, Warlord. Give each rival a different one where you can. The personality decides how the god plays, so make everything else fit it.
        - creature_name: the name of the god's Creature, a giant animal of the species given for the slot, at most 18 characters.
        - history: two to four sentences, at most 700 characters, of the god's past as the island tells it, mentioning its Creature. Give it a wound or a grudge it could carry into the game.
        - voice: how the god speaks, in a few words, so that its words can later be written in character.
        - desire and dread: what it wants above all, and what it fears, a few words each.
        - temper: how the god leans from the usual for its personality, each a whole number from -3 (far less) to 3 (far more), mostly 0 or 1 either way, with one or two that define it: aggression (appetite for war), cooperation (for alliances and peace), treachery (breaking its word when it pays), grudge (remembering wrongs), protect (care for its own people), convert (winning others with kindness), intimidate (ruling by fear).

        Make the rivals distinct from one another and memorable, and let some of them have reasons to resent or court each other. Mythic in tone, no modern words, no gore. Answer only with the JSON asked for.
        """;

    /// <summary>The answer's JSON schema.</summary>
    public static string Schema { get; } = BuildSchema();

    /// <summary>The question for a world's gods.</summary>
    /// <param name="setup">The world's gods as the content gives them, at the world's size.</param>
    /// <param name="world">The world in a few words, for example "a large island of 20 villages, with four seasons".</param>
    /// <param name="minds">The minds' content, for the effort and length.</param>
    /// <returns>The question.</returns>
    public static ModelQuestion Question(PantheonSetup setup, string world, MindContent minds)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(minds);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"The world: {world}.\n");
        for (var i = 0; i < setup.Gods.Count; i++)
        {
            var slot = setup.Gods[i];
            text.Append(slot.Controller == GodController.Human
                ? string.Create(CultureInfo.InvariantCulture, $"Slot {i}: the player, {slot.Name}, whose Creature is {slot.CreatureName} the {slot.CreatureSpecies.ToLowerInvariant()}. Do not write this god.\n")
                : string.Create(CultureInfo.InvariantCulture, $"Slot {i}: a rival god whose Creature is a {slot.CreatureSpecies.ToLowerInvariant()}.\n"));
        }

        text.Append("Write one god for every rival slot, giving its slot number.");
        return new ModelQuestion
        {
            Instructions = Instructions,
            Turns = [new ModelTurn(false, text.ToString())],
            Schema = Schema,
            Effort = minds.BirthEffort,
            MaxTokens = minds.BirthMaxTokens,
        };
    }

    /// <summary>Reads a model's answer: each rival it wrote well, the scribes' gods for the rest.</summary>
    /// <param name="json">The answer.</param>
    /// <param name="scribed">The scribes' gods for the same world, one per slot.</param>
    /// <param name="author">The model's name.</param>
    /// <returns>The gods, one per slot, and why any are the scribes'.</returns>
    public static BornPantheon Read(string json, BornPantheon scribed, string author)
    {
        ArgumentNullException.ThrowIfNull(scribed);
        var written = new Dictionary<int, BornGod>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("gods", out var gods) && gods.ValueKind == JsonValueKind.Array)
            {
                foreach (var god in gods.EnumerateArray())
                {
                    if (ReadGod(god, author) is ({ } slot, { } born))
                    {
                        written.TryAdd(slot, born);
                    }
                }
            }
        }
        catch (JsonException)
        {
            return scribed with { Problem = $"{author}'s answer was not JSON, so the scribes wrote the gods." };
        }

        var players = scribed.Gods.Where(g => g.Persona is null).ToList();
        var names = new HashSet<string>(players.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
        var creatures = new HashSet<string>(players.Select(g => g.CreatureName), StringComparer.OrdinalIgnoreCase);
        var result = new List<BornGod>();
        var fallbacks = 0;
        for (var i = 0; i < scribed.Gods.Count; i++)
        {
            var mine = scribed.Gods[i];
            if (mine.Persona is null)
            {
                result.Add(mine);
                continue;
            }

            var god = written.TryGetValue(i, out var theirs) && !names.Contains(theirs.Name) && !creatures.Contains(theirs.CreatureName) ? theirs : mine;
            if (ReferenceEquals(god, mine))
            {
                fallbacks++;
            }

            // A scribes' god whose name a model's god took is numbered.
            god = god with
            {
                Name = names.Add(god.Name) ? god.Name : Untaken(names, god.Name),
                CreatureName = creatures.Add(god.CreatureName) ? god.CreatureName : Untaken(creatures, god.CreatureName),
            };
            result.Add(god);
        }

        var rivals = scribed.Gods.Count - players.Count;
        var problem = fallbacks == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"{author} did not write {fallbacks} of the {rivals} gods well, so the scribes wrote them.");
        return new BornPantheon(result, fallbacks == rivals ? BornPantheon.Scribes : author, problem);
    }

    /// <summary>One god of the answer, with its slot; <c>null</c> when anything is missing or out of bounds.</summary>
    private static (int? Slot, BornGod? God) ReadGod(JsonElement god, string author)
    {
        if (god.ValueKind != JsonValueKind.Object
            || !god.TryGetProperty("slot", out var slotElement) || !slotElement.TryGetInt32(out var slot)
            || Line(god, "name", MaxName) is not { } name || !IsName(name)
            || Line(god, "creature_name", MaxName) is not { } creature || !IsName(creature)
            || Line(god, "epithet", MaxEpithet) is not { } epithet
            || Line(god, "history", MaxHistory) is not { } history
            || Line(god, "voice", MaxLine) is not { } voice
            || Line(god, "desire", MaxLine) is not { } desire
            || Line(god, "dread", MaxLine) is not { } dread
            || Line(god, "personality", 20) is not { } personalityName
            || !Enum.TryParse<GodPersonality>(personalityName, ignoreCase: false, out var personality) || personality == GodPersonality.None || !Enum.IsDefined(personality)
            || !god.TryGetProperty("temper", out var temperElement) || ReadTemper(temperElement) is not { } temper)
        {
            return (null, null);
        }

        var persona = new GodPersona { Epithet = epithet, History = history, Voice = voice, Desire = desire, Dread = dread, Temper = temper, Author = author };
        return (slot, new BornGod(name, creature, personality, persona));
    }

    /// <summary>A temper from whole leanings of -3 to 3, each brought into range.</summary>
    private static GodTemper? ReadTemper(JsonElement temper)
    {
        if (temper.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var values = new float[Leanings.Length];
        for (var i = 0; i < Leanings.Length; i++)
        {
            if (!temper.TryGetProperty(Leanings[i], out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            {
                return null;
            }

            values[i] = (float)number / TemperSteps;
        }

        return GodTempers.Clamped(new GodTemper
        {
            Aggression = values[0],
            Cooperation = values[1],
            Treachery = values[2],
            Grudge = values[3],
            Protect = values[4],
            Convert = values[5],
            Intimidate = values[6],
        });
    }

    /// <summary>A property's text, trimmed, when it is there and not too long.</summary>
    private static string? Line(JsonElement owner, string property, int longest) =>
        owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text && text.Length <= longest ? text : null;

    /// <summary>Whether a name is letters, with spaces, hyphens and apostrophes between them.</summary>
    private static bool IsName(string name) => char.IsLetter(name[0]) && char.IsLetter(name[^1]) && name.All(c => char.IsLetter(c) || c is ' ' or '-' or '\'');

    /// <summary>A name not taken, numbered when it is.</summary>
    private static string Untaken(HashSet<string> taken, string name)
    {
        for (var n = 2; ; n++)
        {
            var numbered = name + " " + n.ToString(CultureInfo.InvariantCulture);
            if (taken.Add(numbered))
            {
                return numbered;
            }
        }
    }

    /// <summary>The answer's schema: an object of gods, each with every property required.</summary>
    private static string BuildSchema()
    {
        static object Text(string about) => new Dictionary<string, object> { ["type"] = "string", ["description"] = about };
        var temper = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = Leanings.ToDictionary(l => l, l => (object)new Dictionary<string, object> { ["type"] = "integer", ["description"] = "From -3 to 3." }),
            ["required"] = Leanings,
            ["additionalProperties"] = false,
        };
        var god = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["slot"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "The rival slot's number." },
                ["name"] = Text("The god's name."),
                ["epithet"] = Text("What it is called after its name."),
                ["personality"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = Enum.GetNames<GodPersonality>().Where(n => n != nameof(GodPersonality.None)).ToArray(),
                },
                ["creature_name"] = Text("Its Creature's name."),
                ["history"] = Text("Two to four sentences of its past."),
                ["voice"] = Text("How it speaks."),
                ["desire"] = Text("What it wants above all."),
                ["dread"] = Text("What it fears."),
                ["temper"] = temper,
            },
            ["required"] = new[] { "slot", "name", "epithet", "personality", "creature_name", "history", "voice", "desire", "dread", "temper" },
            ["additionalProperties"] = false,
        };
        var root = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object> { ["gods"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = god } },
            ["required"] = new[] { "gods" },
            ["additionalProperties"] = false,
        };
        return JsonSerializer.Serialize(root);
    }
}
