using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Pantheon.Domain.Gods;
using Pantheon.Game.Divine;

namespace Pantheon.Game.Minds.Agents;

/// <summary>Words a god's mind sends another god.</summary>
/// <param name="To">The god's name.</param>
/// <param name="Text">The words.</param>
public sealed record GodWords(string To, string Text);

/// <summary>A god's mind's answer to a turn.</summary>
/// <param name="Reason">Why it acts as it does, in a sentence, for the record.</param>
/// <param name="Acts">What it does.</param>
/// <param name="Words">What it says, and to whom.</param>
/// <param name="Journal">What it keeps in mind now.</param>
public sealed record GodTurn(string Reason, IReadOnlyList<GodAct> Acts, IReadOnlyList<GodWords> Words, string Journal);

/// <summary>
/// What a god's mind is told when something wakes it (ADR-072), and how its answer is read. The
/// instructions say who the god is, what it may do and the rules it keeps, and stay the same for the
/// god, so they are cached; each turn brings what woke it, how things stand, its journal and what came
/// of its last acts.
/// </summary>
public static class GodAgentPrompt
{
    // The longest a reason and a message may be, in characters.
    private const int MaxReason = 300;
    private const int MaxWords = 400;

    /// <summary>The answer's JSON schema.</summary>
    public const string Schema = """{"type":"object","properties":{"reason":{"type":"string","description":"Why you act as you do, in one short sentence."},"actions":{"type":"array","items":{"type":"object","properties":{"tool":{"type":"string","description":"The act's id."},"target":{"type":"string","description":"A village, god, Creature or holy site by name, or empty."},"detail":{"type":"string","description":"A miracle's id, a treaty's or an aim's kind, or empty."}},"required":["tool","target","detail"],"additionalProperties":false}},"messages":{"type":"array","items":{"type":"object","properties":{"to":{"type":"string","description":"The god's name."},"text":{"type":"string","description":"What you say to it, in character."}},"required":["to","text"],"additionalProperties":false}},"journal":{"type":"string","description":"What you keep in mind from now on."}},"required":["reason","actions","messages","journal"],"additionalProperties":false}""";

    /// <summary>The question for the god's model.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <param name="hooks">What woke it.</param>
    /// <param name="results">What came of its last acts.</param>
    /// <param name="rules">The agents' rules.</param>
    /// <returns>The question.</returns>
    public static ModelQuestion Question(GameSession session, GodState god, IReadOnlyList<Hook> hooks, IReadOnlyList<string> results, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new ModelQuestion
        {
            Instructions = Instructions(god, rules),
            Turns = [new ModelTurn(false, Turn(session, god, hooks, results, rules))],
            Schema = Schema,
            Effort = rules.Effort,
            MaxTokens = rules.MaxTokens,
        };
    }

    /// <summary>The standing instructions: who the god is, what it may do and the rules it keeps.</summary>
    /// <param name="god">The god.</param>
    /// <param name="rules">The agents' rules.</param>
    /// <returns>The instructions.</returns>
    public static string Instructions(GodState god, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(rules);
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"You are {PersonaText.Title(god)}, a god of an island in PANTHEON, a god game in the spirit of Black & White. Gods win the island's people by miracles, raise Creatures, and make alliances, treaties and wars with each other.\n");
        text.Append(CultureInfo.InvariantCulture, $"Your personality is {DivineText.Personality(god.Personality)}.");
        if (god.Persona is { } persona)
        {
            text.Append(CultureInfo.InvariantCulture, $" {persona.History} You speak {persona.Voice}. You want {persona.Desire}; you dread {persona.Dread}.");
        }

        text.Append(CultureInfo.InvariantCulture, $"""


            You are this god's will. Your instincts keep the god about its daily work: everyday miracles, tending its Creature, small dealings. You are woken when something that matters happens, and from time to time to look about you; then you decide what the god does about it. Each message tells you what woke you, how things stand from your side, what you keep in mind, and what came of your last acts.

            What you may do, at most {rules.MaxActions} acts a turn, each a tool with a target and a detail:
            - cast: target a village, detail a miracle's id. It works on villages within your reach that you can afford.
            - send_creature: target a village or a holy site. Your Creature goes there.
            - creature_fight: target a rival's Creature. Your Creature hunts it down and fights it.
            - creature_work: target a village. Your Creature works its fields, fetches wood or builds there.
            - offer_alliance, request_peace, declare_war, break_alliance: target a god.
            - accept, decline: target a god whose proposal waits for your answer.
            - propose_treaty: target a god; detail one of non_aggression, mutual_defense, shared_influence, religious_access, creature_peace, disaster_aid, territory, migration, tribute (they pay you), defensive_pact:Name or joint_war:Name (against the god named).
            - aim: what you mean to do over the next minutes; your instincts then lean that way. Detail protect_home (target your village), convert (target a village), befriend or weaken (target a god), prepare, slay_beast or recover. The aims of one turn replace those you held.

            How to answer:
            - Only what the island's laws allow is done: a miracle needs your influence and power, a god must be known to you, a treaty must suit you both. What is refused comes back to you next time.
            - Act as your nature and your aims would. Most turns need one act or none: do not act for the sake of acting.
            - messages: at most {rules.MaxMessages}, to gods you know, by name, in character, one or two sentences each: warnings, threats, offers, thanks, taunts. Speak only when you have something to say. Your words reach only the god you address. Never claim to have done what you did not do as an act.
            - journal: rewrite what you keep in mind, at most {rules.JournalLength} characters: how you see each god, grudges and debts, promises made to you or by you, and your plans. It is all you will remember next time.
            - reason: one short sentence on why you act as you do, for the record.
            Think and speak as a god: no talk of games, rules, menus or being an AI. Answer only with the JSON asked for.
            """);
        return text.ToString();
    }

    /// <summary>A turn: what woke the god, how things stand, its journal and what came of its last acts.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <param name="hooks">What woke it.</param>
    /// <param name="results">What came of its last acts.</param>
    /// <param name="rules">The agents' rules.</param>
    /// <returns>The turn's text.</returns>
    public static string Turn(GameSession session, GodState god, IReadOnlyList<Hook> hooks, IReadOnlyList<string> results, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(results);
        var text = new StringBuilder("What woke you:\n");
        var woken = hooks.Where(h => h.Kind != HookKind.Reflect).ToList();
        foreach (var hook in woken)
        {
            text.Append("- ").Append(hook.Text).Append('\n');
        }

        if (woken.Count == 0)
        {
            text.Append("- Nothing in particular: it is time to look about you.\n");
        }

        text.Append('\n').Append(GodView.Describe(session, god, rules));
        text.Append("\nWhat you keep in mind: ").Append(string.IsNullOrWhiteSpace(god.Journal) ? "nothing yet; this is your first thought." : god.Journal).Append('\n');
        if (results.Count > 0)
        {
            text.Append("\nWhat came of your last acts:\n");
            foreach (var result in results)
            {
                text.Append("- ").Append(result).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Reads an answer; <c>null</c> when it is not the JSON asked for.</summary>
    /// <param name="json">The answer.</param>
    /// <param name="rules">The agents' rules: how many acts and words, and how long a journal.</param>
    /// <returns>The turn, trimmed to the rules.</returns>
    public static GodTurn? Read(string json, AgentRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var acts = Items(root, "actions").Select(a => new GodAct(Text(a, "tool"), Text(a, "target"), Text(a, "detail"))).Where(a => a.Tool.Length > 0).Take(rules.MaxActions).ToList();
            var words = Items(root, "messages").Select(m => new GodWords(Text(m, "to"), Trim(Text(m, "text"), MaxWords))).Where(w => w.To.Length > 0 && w.Text.Length > 0).Take(rules.MaxMessages).ToList();
            return new GodTurn(Trim(Text(root, "reason"), MaxReason), acts, words, Trim(Text(root, "journal"), rules.JournalLength));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>An array's objects, or none.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement root, string name) =>
        root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList() : [];

    /// <summary>A string property, trimmed; empty when missing.</summary>
    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;

    /// <summary>Text cut to a length, marked when cut.</summary>
    private static string Trim(string text, int length) => text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";
}
