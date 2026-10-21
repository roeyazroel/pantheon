using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.Gods;
using Pantheon.Game.Culture;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;

namespace Pantheon.Game.Angel;

/// <summary>The angel's answer to a turn (ADR-074).</summary>
/// <param name="Say">What it says to its god.</param>
/// <param name="Suggestions">The deeds it urges, of those it was offered; at most two.</param>
public sealed record AngelTurn(string Say, IReadOnlyList<AngelSuggestion> Suggestions);

/// <summary>
/// What the angel's model is told (ADR-074), and how its answer is read. The instructions say who the
/// angel is, who its god is, what the god's deeds have made it and how it speaks now; each turn brings
/// what moved it, how things stand for its god, what it said lately, and the deeds it may urge.
/// </summary>
public static class AngelPrompt
{
    /// <summary>The most deeds the angel urges at once.</summary>
    public const int MaxUrged = 2;

    // The longest thing it may say, in characters.
    private const int MaxSay = 420;

    // How many villages it is told of.
    private const int VillagesShown = 10;

    /// <summary>The answer's JSON schema.</summary>
    public const string Schema = """{"type":"object","properties":{"say":{"type":"string","description":"What you say to your god: one to three short sentences, in your voice."},"suggestions":{"type":"array","items":{"type":"string"},"description":"The ids of at most two deeds you urge, of those offered; empty for none."}},"required":["say","suggestions"],"additionalProperties":false}""";

    /// <summary>The question for the angel's model.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">Its god.</param>
    /// <param name="name">The angel's name.</param>
    /// <param name="look">How it looks now.</param>
    /// <param name="hooks">What moved it.</param>
    /// <param name="choices">The deeds it may urge.</param>
    /// <param name="said">What it said lately, oldest first.</param>
    /// <param name="content">The angel's content.</param>
    /// <returns>The question.</returns>
    public static ModelQuestion Question(GameSession session, GodState god, string name, AngelLook look, IReadOnlyList<AngelHook> hooks, IReadOnlyList<AngelSuggestion> choices, IReadOnlyList<string> said, AngelContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ModelQuestion
        {
            Instructions = Instructions(god, name, look, content),
            Turns = [new ModelTurn(false, Turn(session, god, hooks, choices, said))],
            Schema = Schema,
            Effort = content.Effort,
            MaxTokens = content.MaxTokens,
        };
    }

    /// <summary>The standing instructions: who the angel is, what its god's deeds made it, and how it speaks.</summary>
    /// <param name="god">Its god.</param>
    /// <param name="name">The angel's name.</param>
    /// <param name="look">How it looks now.</param>
    /// <param name="content">The angel's content.</param>
    /// <returns>The instructions.</returns>
    public static string Instructions(GodState god, string name, AngelLook look, AngelContent content)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(content);
        var stage = content.Stage(look.Stage);
        var traits = god.Traits.Count == 0 ? "none yet" : string.Join(", ", god.Traits.Select(t => CultureText.Name(t).ToLowerInvariant()));
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"""
            You are {name}, the angel of {god.Name}, a young god of an island in PANTHEON, a god game in the spirit of Black & White. You are your god's conscience and its guide: you hover at its hand, you see everything it does, and what it does makes you what you are. The island's people believe in gods; the more believe in a god, the stronger it grows. Gods work miracles, raise Creatures, and make alliances and wars.

            What your god's deeds have made you: {stage.Name}. {stage.Note}
            How you speak now: {stage.Voice}
            The world sees your god as: {traits}.

            How to answer:
            - say: one to three short sentences to your god, in your voice, calling it "you". Speak of what moved you: praise or chide its deeds as your nature would, warn it of danger, and guide it to what to do next. Name the villages, gods and Creatures you speak of.
            - suggestions: the ids of at most {MaxUrged} of the deeds offered, those you urge, or none. Urge only deeds offered; your god chooses. When you are torn, you may set one kind deed and one harsh deed before it.
            - You cannot act: only your god can. Never say it did what it did not.
            Speak as an angel: no talk of games, rules, menus, keys or being an AI. Answer only with the JSON asked for.
            """);
        return text.ToString();
    }

    /// <summary>A turn: what moved the angel, how things stand for its god, what it said lately, and the deeds it may urge.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">Its god.</param>
    /// <param name="hooks">What moved it.</param>
    /// <param name="choices">The deeds it may urge.</param>
    /// <param name="said">What it said lately, oldest first.</param>
    /// <returns>The turn's text.</returns>
    public static string Turn(GameSession session, GodState god, IReadOnlyList<AngelHook> hooks, IReadOnlyList<AngelSuggestion> choices, IReadOnlyList<string> said)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(said);
        var text = new StringBuilder("What moved you:\n");
        foreach (var hook in hooks)
        {
            text.Append("- ").Append(hook.Text).Append('\n');
        }

        text.Append("\nHow things stand for your god (\"you\" below is your god):\n").Append(GodView.Describe(session, god, VillagesShown));
        if (said.Count > 0)
        {
            text.Append("\nWhat you said to your god lately:\n");
            foreach (var line in said)
            {
                text.Append("- “").Append(line).Append("”\n");
            }
        }

        text.Append("\nDeeds you may urge (id: deed, what it is):\n");
        foreach (var choice in choices)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {choice.Id}: {choice.Text} ({choice.Why})\n");
        }

        if (choices.Count == 0)
        {
            text.Append("- none now\n");
        }

        return text.ToString();
    }

    /// <summary>Reads an answer; <c>null</c> when it is not the JSON asked for, or says nothing.</summary>
    /// <param name="json">The answer.</param>
    /// <param name="choices">The deeds it was offered.</param>
    /// <returns>The turn: what it said, and the deeds it urged of those offered.</returns>
    public static AngelTurn? Read(string json, IReadOnlyList<AngelSuggestion> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("say", out var say) || say.ValueKind != JsonValueKind.String
                || say.GetString()?.Trim() is not { Length: > 0 } words)
            {
                return null;
            }

            var urged = root.TryGetProperty("suggestions", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim().ToLowerInvariant())
                    .Select(id => choices.FirstOrDefault(c => c.Id == id)).OfType<AngelSuggestion>().Distinct().Take(MaxUrged).ToList()
                : [];
            return new AngelTurn(words.Length <= MaxSay ? words : words[..(MaxSay - 1)].TrimEnd() + "…", urged);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
