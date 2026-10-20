using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// What the agents do with a mind's answer, in between ticks (ADR-072): a god's acts through its tools,
/// its words to the gods it knows, its journal; a Creature's words and its choice through its will; and
/// the scribes' words when no model speaks.
/// </summary>
internal static class AgentReplies
{
    /// <summary>Takes a god's answer.</summary>
    /// <param name="session">The session.</param>
    /// <param name="state">The god's mind.</param>
    /// <param name="answer">The model's answer.</param>
    /// <param name="rules">The agents' rules.</param>
    /// <param name="voices">Whether the gods speak.</param>
    /// <returns>Whether it was an answer that could be read.</returns>
    public static bool God(GameSession session, MindState state, ModelAnswer answer, AgentRules rules, bool voices)
    {
        var world = session.World;
        if (answer.Outcome != ModelOutcome.Answered || GodAgentPrompt.Read(answer.Json, rules) is not { } turn || world.Gods.Find(g => g.Id == state.Mind.Id) is not { } god)
        {
            return false;
        }

        var results = GodTools.Do(session, god, turn.Acts, rules);
        var said = new List<string>();
        foreach (var words in turn.Words)
        {
            if (GodTools.Other(world, god, words.To) is { } other && Acquaintance.Knows(session.Content.Diplomacy.Acquaintance, world, god.Id, other.Id))
            {
                if (voices)
                {
                    Say(session, god.Id, other.Id, words.Text);
                }

                said.Add(string.Create(CultureInfo.InvariantCulture, $"to {other.Name}: “{words.Text}”"));
            }
            else
            {
                results.Add(string.Create(CultureInfo.InvariantCulture, $"words to {words.To}: not sent, for you know no such god"));
            }
        }

        if (turn.Journal.Length > 0)
        {
            session.Command((_, _) => god.Journal = turn.Journal);
        }

        state.LastReason = turn.Reason;
        state.LastResults = results;
        state.LastSaid = string.Join(" ", said);
        return true;
    }

    /// <summary>Takes a Creature's answer: its words, and its choice through its will.</summary>
    /// <param name="session">The session.</param>
    /// <param name="state">The Creature's mind.</param>
    /// <param name="answer">The model's answer.</param>
    /// <param name="voices">Whether the Creatures speak.</param>
    /// <returns>Whether it was an answer that could be read.</returns>
    public static bool Creature(GameSession session, MindState state, ModelAnswer answer, bool voices)
    {
        var world = session.World;
        if (answer.Outcome != ModelOutcome.Answered || CreatureAgentPrompt.Read(answer.Json) is not { } turn || world.Creatures.Find(c => c.Id == state.Mind.Id) is not { } creature)
        {
            return false;
        }

        if (turn.Say.Length > 0)
        {
            if (voices)
            {
                session.Command((_, events) =>
                {
                    events.Add(new CreatureSpoke { Tick = world.Tick, CreatureId = creature.Id, Text = turn.Say });
                    return true;
                });
            }

            state.LastSaid = turn.Say;
        }

        state.LastReason = string.Empty;
        state.LastResults = [Choose(session, state, creature.Id, turn.Choice)];
        return true;
    }

    /// <summary>The scribes' words for a mind of a moment: a god's to a player's god, a Creature's aloud.</summary>
    /// <param name="session">The session.</param>
    /// <param name="state">The mind.</param>
    /// <param name="hook">The moment.</param>
    /// <param name="voices">The scribes' lines.</param>
    /// <returns>Whether it spoke.</returns>
    public static bool Scribe(GameSession session, MindState state, Hook hook, VoiceLines voices)
    {
        var world = session.World;
        if (state.Mind.Kind == MindKind.God)
        {
            // The scribes speak only to gods a player plays: the AI gods hear no one without a model.
            if (world.Gods.Find(g => g.Id == state.Mind.Id) is not { } god || hook.OtherGodId is not { } to
                || world.Gods.Find(g => g.Id == to) is not { Controller: not GodController.Ai } || AgentScribe.GodLine(voices, world, god, hook) is not { } line)
            {
                return false;
            }

            Say(session, god.Id, to, line);
            state.LastSaid = line;
            return true;
        }

        if (world.Creatures.Find(c => c.Id == state.Mind.Id) is not { } creature || AgentScribe.CreatureLine(voices, world, creature, hook) is not { } said)
        {
            return false;
        }

        session.Command((_, events) =>
        {
            events.Add(new CreatureSpoke { Tick = world.Tick, CreatureId = creature.Id, Text = said });
            return true;
        });
        state.LastSaid = said;
        return true;
    }

    /// <summary>A god's words to another, as an event.</summary>
    private static void Say(GameSession session, int godId, int toGodId, string text) => session.Command((_, events) =>
    {
        events.Add(new GodSpoke { Tick = session.World.Tick, GodId = godId, ToGodId = toGodId, Text = text });
        return true;
    });

    /// <summary>Carries out a Creature's choice by its id, through its will; tells what came of it.</summary>
    private static string Choose(GameSession session, MindState state, int creatureId, string choice)
    {
        var id = choice.Trim().ToLowerInvariant();
        if (id == CreatureAgentPrompt.NoChoice || id.Length == 0)
        {
            return "went on as it was";
        }

        var index = Enumerable.Range(0, state.Choices.Count).FirstOrDefault(i => CreatureAgentPrompt.Id(i) == id, -1);
        if (index < 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"chose {choice}, which was no choice");
        }

        var option = state.Choices[index];
        var world = session.World;
        var rejection = session.Command((authorities, _) => authorities.Will?.Choose(world, creatureId, option.Action, option.SettlementId, option.CreatureId) ?? WillRejection.NotFree);
        var deed = CreatureText.Doing(option.Action, CreatureText.Concerning(world, option.SettlementId, option.CreatureId));
        return rejection switch
        {
            WillRejection.None => "chose: " + deed,
            WillRejection.UnknownCreature => "is gone",
            WillRejection.NotFree => "could not choose " + deed + ": it is not free",
            WillRejection.Obeying => "could not choose " + deed + ": it is on its god's errand",
            WillRejection.NotAnOption => "could not choose " + deed + ": it no longer can",
            WillRejection.Unreachable => "could not choose " + deed + ": it cannot get there",
        };
    }
}
