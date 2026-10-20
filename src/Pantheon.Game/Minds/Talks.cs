using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;

namespace Pantheon.Game.Minds;

/// <summary>
/// The player's conversations with the gods (ADR-066). With a model, the player's words (typed or
/// suggested) are a question to the god's mind, and its reply says something, moves the
/// conversation's goodwill, and takes at most one act. With the scribes, each suggestion asks one
/// act. Either way the game judges the act by the god's own utility, leaning with the goodwill by
/// at most <see cref="TalkRules.PersuasionReach"/>, and does it through the diplomacy authority.
/// Replies are applied on the caller's thread, in between ticks.
/// </summary>
public sealed class Talks
{
    private readonly Dictionary<int, GodTalk> _talks = [];
    private readonly TalkRules _rules;

    /// <summary>Creates the conversations.</summary>
    /// <param name="rules">How talking works.</param>
    public Talks(TalkRules rules) => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

    /// <summary>How talking works.</summary>
    public TalkRules Rules => _rules;

    /// <summary>Forgets every conversation, for a new world.</summary>
    public void Clear() => _talks.Clear();

    /// <summary>The conversation with a god, begun with its greeting when new.</summary>
    /// <param name="session">The session.</param>
    /// <param name="god">The god.</param>
    /// <param name="me">The player's god.</param>
    /// <returns>The conversation.</returns>
    public GodTalk Open(GameSession session, GodState god, int me)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(god);
        if (!_talks.TryGetValue(god.Id, out var talk))
        {
            talk = new GodTalk(god.Id);
            talk.Lines.Add(new TalkLine(TalkSpeaker.God, TalkScribe.Greeting(session.World, god, me)));
            _talks[god.Id] = talk;
        }

        if (!talk.Answering)
        {
            talk.Suggestions = TalkScribe.Suggestions(TalkActs.For(session, god, me), talk.Goodwill(session.World.Tick, _rules), _rules.PersuasionReach, session.World);
        }

        return talk;
    }

    /// <summary>The question for the god's model, the player's words added to the conversation.</summary>
    /// <param name="session">The session.</param>
    /// <param name="talk">The conversation.</param>
    /// <param name="me">The player's god.</param>
    /// <param name="words">What the player says.</param>
    /// <returns>The question.</returns>
    public ModelQuestion Ask(GameSession session, GodTalk talk, int me, string words)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(talk);
        var world = session.World;
        var god = world.Gods.Single(g => g.Id == talk.GodId);
        var player = world.Gods.Single(g => g.Id == me);
        var acts = TalkActs.For(session, god, me);
        talk.Lines.Add(new TalkLine(TalkSpeaker.Player, words.Trim()));
        talk.Turns.Add(new ModelTurn(false, TalkPrompt.Turn(session, god, me, acts, talk.Goodwill(world.Tick, _rules), _rules.PersuasionReach, words)));
        talk.Answering = true;
        talk.Suggestions = [];
        return new ModelQuestion
        {
            Instructions = TalkPrompt.Instructions(god, player),
            Turns = talk.Turns.Skip(Math.Max(0, talk.Turns.Count - _rules.TurnsKept)).SkipWhile(t => t.FromModel).ToList(),
            Schema = TalkPrompt.Schema,
            Effort = _rules.Effort,
            MaxTokens = _rules.MaxTokens,
        };
    }

    /// <summary>Takes the model's answer: the god's words, the goodwill they moved, and its act, judged and done.</summary>
    /// <param name="session">The session.</param>
    /// <param name="talk">The conversation.</param>
    /// <param name="me">The player's god.</param>
    /// <param name="answer">The model's answer.</param>
    public void Answer(GameSession session, GodTalk talk, int me, ModelAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(talk);
        ArgumentNullException.ThrowIfNull(answer);
        talk.Answering = false;
        var god = session.World.Gods.Find(g => g.Id == talk.GodId);
        var reply = answer.Outcome == ModelOutcome.Answered ? TalkPrompt.Read(answer.Json) : null;
        if (god is null || reply is null)
        {
            // Unanswered, the player's words leave the conversation, so it only ever grows by answered turns.
            if (talk.Turns.Count > 0 && !talk.Turns[^1].FromModel)
            {
                talk.Turns.RemoveAt(talk.Turns.Count - 1);
            }

            talk.Lines.Add(new TalkLine(TalkSpeaker.Note, $"{god?.Name ?? "The god"} does not answer ({answer.Problem ?? "its words could not be read"})."));
            Refresh(session, talk, god, me);
            return;
        }

        talk.Turns.Add(new ModelTurn(true, answer.Json));
        talk.Lines.Add(new TalkLine(TalkSpeaker.God, "“" + reply.Say.Trim('“', '”', '"') + "”"));
        talk.Move(session.World.Tick, reply.Regard, _rules);
        if (TalkActs.For(session, god, me).FirstOrDefault(a => string.Equals(a.Id, reply.ActId, StringComparison.Ordinal)) is { } act)
        {
            Do(session, talk, god, me, act);
        }

        talk.Suggestions = reply.Suggestions.Count > 0 ? reply.Suggestions.Select(s => new TalkSuggestion(s)).ToList() : Offered(session, talk, god, me);
    }

    /// <summary>The player asks what a suggestion asks, without a model: the scribes answer, and the god does it if it is willing.</summary>
    /// <param name="session">The session.</param>
    /// <param name="talk">The conversation.</param>
    /// <param name="me">The player's god.</param>
    /// <param name="suggestion">The suggestion.</param>
    public void Choose(GameSession session, GodTalk talk, int me, TalkSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(talk);
        ArgumentNullException.ThrowIfNull(suggestion);
        if (session.World.Gods.Find(g => g.Id == talk.GodId) is not { } god)
        {
            return;
        }

        talk.Lines.Add(new TalkLine(TalkSpeaker.Player, suggestion.Text));
        var act = TalkActs.For(session, god, me).FirstOrDefault(a => string.Equals(a.Id, suggestion.ActId, StringComparison.Ordinal));
        if (act is null)
        {
            talk.Lines.Add(new TalkLine(TalkSpeaker.God, TalkScribe.Answer(god, null, willing: true)));
        }
        else
        {
            var willing = act.Willingness(talk.Goodwill(session.World.Tick, _rules), _rules.PersuasionReach) == TalkWillingness.Willing;
            talk.Lines.Add(new TalkLine(TalkSpeaker.God, TalkScribe.Answer(god, act, willing)));
            if (willing)
            {
                Do(session, talk, god, me, act);
            }
        }

        Refresh(session, talk, god, me);
    }

    /// <summary>Does an act the god chose if, with the conversation's goodwill, it is willing; tells what happened.</summary>
    private void Do(GameSession session, GodTalk talk, GodState god, int me, TalkAct act)
    {
        if (act.Willingness(talk.Goodwill(session.World.Tick, _rules), _rules.PersuasionReach) != TalkWillingness.Willing)
        {
            talk.Lines.Add(new TalkLine(TalkSpeaker.Note, act.Hostile ? $"{god.Name} holds back, for now." : $"{god.Name} is not yet persuaded."));
            return;
        }

        var (done, text) = TalkActs.Do(session, act, god, me);
        if (text.Length > 0)
        {
            talk.Lines.Add(new TalkLine(TalkSpeaker.Note, done ? text : $"It cannot be done: {text}"));
        }
    }

    /// <summary>The scribes' suggestions, for when the model offers none.</summary>
    private IReadOnlyList<TalkSuggestion> Offered(GameSession session, GodTalk talk, GodState god, int me) =>
        TalkScribe.Suggestions(TalkActs.For(session, god, me), talk.Goodwill(session.World.Tick, _rules), _rules.PersuasionReach, session.World);

    /// <summary>Offers the scribes' suggestions again.</summary>
    private void Refresh(GameSession session, GodTalk talk, GodState? god, int me) =>
        talk.Suggestions = god is null ? [] : Offered(session, talk, god, me);
}
