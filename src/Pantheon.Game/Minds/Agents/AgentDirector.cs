using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Game.Minds.Agents;

/// <summary>How the agents may think this frame: the model, whether the player lets them, how many questions an hour, and whether they speak.</summary>
/// <param name="Model">The model the minds are asked, or <c>null</c> for the scribes alone.</param>
/// <param name="Thinking">Whether the agents think for the gods and Creatures (the player's setting).</param>
/// <param name="TurnsPerHour">The most questions in an hour.</param>
/// <param name="Voices">Whether the gods and Creatures speak.</param>
public sealed record AgentOptions(ILanguageModel? Model, bool Thinking, int TurnsPerHour, bool Voices);

/// <summary>
/// The gods' and Creatures' agents (ADR-072). Each AI god and each Creature has a mind, woken by hooks:
/// the moments that matter to it, read from the world's events. With a model, a woken mind is asked, off
/// the simulation's thread, after its hooks have gathered and not more often than the rules allow; its
/// answer is taken here, in between ticks: a god's acts through the authorities, its words, aims and
/// journal; a Creature's words and its choice through its will. The built-in AI stays the gods' and
/// Creatures' instincts between turns. Without a model, when the player turns the agents off, when an
/// hour's questions are spent, or while the agents rest after failures, the scribes give them words, and
/// their instincts do the rest. Only the host (or a player alone) runs them.
/// </summary>
public sealed class AgentDirector
{
    private readonly AgentRules _rules;
    private readonly VoiceLines _voices;
    private readonly Dictionary<Mind, MindState> _minds = [];
    private readonly Queue<double> _asked = new();
    private CancellationTokenSource _cancel = new();
    private WorldState? _world;
    private double _clock;
    private double _restUntil;
    private int _failures;

    /// <summary>Creates the agents.</summary>
    /// <param name="rules">How they think.</param>
    /// <param name="voices">What the scribes say for them.</param>
    public AgentDirector(AgentRules rules, VoiceLines voices)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _voices = voices ?? throw new ArgumentNullException(nameof(voices));
    }

    /// <summary>How they think.</summary>
    public AgentRules Rules => _rules;

    /// <summary>Every mind woken so far in this world.</summary>
    public IReadOnlyCollection<MindState> Minds => _minds.Values;

    /// <summary>Questions asked in the last hour.</summary>
    public int AskedThisHour => _asked.Count;

    /// <summary>Tokens the model read and wrote for the agents.</summary>
    public (long Read, long Written) Tokens { get; private set; }

    /// <summary>Why the last question went unanswered, until one is answered.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether the agents rest after failures, and the scribes speak meanwhile.</summary>
    public bool Resting => _clock < _restUntil;

    /// <summary>Raised when a mind's answer has been taken, or has failed, for the log.</summary>
    public event Action<MindState>? Answered;

    /// <summary>Wakes a mind with a hook from outside the world's events, such as a talk with the player.</summary>
    /// <param name="mind">The mind.</param>
    /// <param name="hook">What woke it.</param>
    public void Wake(Mind mind, Hook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Add(State(mind), hook);
    }

    /// <summary>Forgets every mind and drops the questions asked, for a new world.</summary>
    public void Clear()
    {
        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        _minds.Clear();
        _failures = 0;
        _world = null;
    }

    /// <summary>Takes the answers that came, reads the frame's hooks, and asks the minds whose turn it is, or lets the scribes speak.</summary>
    /// <param name="session">The session.</param>
    /// <param name="events">The events handled this frame.</param>
    /// <param name="realSeconds">Real time since the last frame.</param>
    /// <param name="options">How the agents may think.</param>
    public void Update(GameSession session, IReadOnlyList<WorldEvent> events, double realSeconds, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);
        if (session.IsReplica)
        {
            return;
        }

        if (_world is not null && !ReferenceEquals(session.World, _world))
        {
            Clear();
        }

        _world = session.World;

        _clock += Math.Max(0d, realSeconds);
        while (_asked.Count > 0 && _clock - _asked.Peek() > 3600d)
        {
            _asked.Dequeue();
        }

        Finish(session, options);
        foreach (var (mind, hook) in HookReader.Read(events, session.World, session.Content))
        {
            Add(State(mind), hook);
        }

        Watch(session, Mindful(options));
        foreach (var state in _minds.Values.Where(s => s.Hooks.Count > 0).ToList())
        {
            // Asked again for each mind, so the hour's last question goes to one mind and the scribes speak for the rest.
            Turn(session, state, Mindful(options), options);
        }
    }

    /// <summary>Whether a mind may be asked now: a model, the player's leave, no rest after failures, and questions left in the hour.</summary>
    private bool Mindful(AgentOptions options) => options.Model is not null && options.Thinking && !Resting && _asked.Count < options.TurnsPerHour;

    /// <summary>A mind with hooks: asked when its turn comes, else spoken for by the scribes.</summary>
    private void Turn(GameSession session, MindState state, bool mindful, AgentOptions options)
    {
        var world = session.World;
        if (!Lives(world, state.Mind))
        {
            state.Hooks.Clear();
            return;
        }

        if (!mindful)
        {
            if (!session.Scheduler.IsPaused)
            {
                Speak(session, state, state.Hooks, options.Voices);
            }

            state.Hooks.Clear();
            state.FirstHookAt = null;
            return;
        }

        // Words from another AI god wait for something that matters to wake the god, so two gods cannot talk the hour away.
        var waking = state.Hooks.FindAll(h => !Passive(world, h));
        if (waking.Count == 0)
        {
            return;
        }

        var cooldown = state.Mind.Kind == MindKind.God ? _rules.GodTurnSeconds : _rules.CreatureTurnSeconds;
        var gathered = waking.TrueForAll(h => h.Kind == HookKind.Reflect) || _clock - (state.FirstHookAt ?? _clock) >= _rules.GatherSeconds;
        if (state.Asking is null && gathered && _clock - state.LastTurnAt >= cooldown && !session.Scheduler.IsPaused
            && _minds.Values.Count(s => s.Thinking) < _rules.MaxAsking)
        {
            Ask(session, state, options.Model!);
        }
    }

    /// <summary>Asks a mind its turn.</summary>
    private void Ask(GameSession session, MindState state, ILanguageModel model)
    {
        var world = session.World;
        ModelQuestion question;
        if (state.Mind.Kind == MindKind.God)
        {
            var god = world.Gods.Single(g => g.Id == state.Mind.Id);
            question = GodAgentPrompt.Question(session, god, state.Hooks, state.LastResults, _rules);
            state.NextReflectTick = world.Tick + ReflectTicks;
        }
        else
        {
            var creature = world.Creatures.Single(c => c.Id == state.Mind.Id);
            state.Choices = session.Inspect(a => a.Will?.Options(world, creature)) ?? [];
            question = CreatureAgentPrompt.Question(session, creature, state.Hooks, state.Choices, _rules);
        }

        state.Asked = [.. state.Hooks];
        state.LastHooks = state.Asked.Select(h => h.Text).ToList();
        state.Hooks.Clear();
        state.FirstHookAt = null;
        state.LastTurnAt = _clock;
        state.Turns++;
        state.AskedWorld = world;
        _asked.Enqueue(_clock);
        state.Asking = Question(model, question, _cancel.Token);
    }

    /// <summary>Takes every answer that has come.</summary>
    private void Finish(GameSession session, AgentOptions options)
    {
        foreach (var state in _minds.Values.Where(s => s.Asking is { IsCompleted: true }).ToList())
        {
            var task = state.Asking!;
            state.Asking = null;
            var answer = task.IsCompletedSuccessfully ? task.Result : ModelAnswer.Without(ModelOutcome.Failed, task.Exception?.InnerException?.Message ?? "the question was dropped");
            if (!ReferenceEquals(state.AskedWorld, session.World) || !Lives(session.World, state.Mind))
            {
                continue;
            }

            Tokens = (Tokens.Read + answer.InputTokens, Tokens.Written + answer.OutputTokens);
            var taken = state.Mind.Kind == MindKind.God
                ? AgentReplies.God(session, state, answer, _rules, options.Voices)
                : AgentReplies.Creature(session, state, answer, options.Voices);
            if (taken)
            {
                (_failures, Problem, state.LastProblem) = (0, null, null);
                Answered?.Invoke(state);
                continue;
            }

            (Problem, state.LastProblem) = (answer.Problem ?? "its answer could not be read", answer.Problem ?? "its answer could not be read");
            Speak(session, state, state.Asked, options.Voices);
            if (++_failures >= 3)
            {
                (_restUntil, _failures) = (_clock + _rules.RestSeconds, 0);
            }

            Answered?.Invoke(state);
        }
    }

    /// <summary>Wakes Creatures that grew hungry or tired, and gods whose time has come to look about them.</summary>
    private void Watch(GameSession session, bool mindful)
    {
        var world = session.World;
        var creatures = session.Content.Creatures;
        foreach (var creature in world.Creatures.Where(c => c.Activity.Phase != CreaturePhase.Banished))
        {
            var state = State(Mind.Creature(creature.Id));
            var (hungry, tired) = (creature.Needs.Hunger >= creatures.HungryThreshold, creature.Needs.Fatigue >= creatures.TiredThreshold);
            if (hungry && !state.Hungry)
            {
                Add(state, new Hook(HookKind.Need, world.Tick, "You grew hungry.", "Need.Hungry"));
            }

            if (tired && !state.Tired)
            {
                Add(state, new Hook(HookKind.Need, world.Tick, "You grew tired.", "Need.Tired"));
            }

            (state.Hungry, state.Tired) = (hungry, tired);
        }

        if (!mindful)
        {
            return;
        }

        var order = 0;
        foreach (var god in world.Gods.Where(g => g.Controller == GodController.Ai))
        {
            var state = State(Mind.God(god.Id));
            if (state.NextReflectTick < 0)
            {
                // The gods first look about them in turn, a little after the world begins for the agents.
                state.NextReflectTick = world.Tick + (long)(ReflectTicks * (0.15 + (0.2 * order)));
            }

            order++;
            if (world.Tick >= state.NextReflectTick && !state.Thinking && !state.Hooks.Exists(h => h.Kind == HookKind.Reflect))
            {
                Add(state, new Hook(HookKind.Reflect, world.Tick, "It is time to look about you."));
                state.NextReflectTick = world.Tick + ReflectTicks;
            }
        }
    }

    /// <summary>The scribes speak for a mind of the latest moment they have words for, at most as often as the rules allow.</summary>
    private void Speak(GameSession session, MindState state, IReadOnlyList<Hook> hooks, bool voices)
    {
        var world = session.World;
        if (!voices || world.Tick < state.NextScribeTick)
        {
            return;
        }

        foreach (var hook in hooks.Reverse())
        {
            if (AgentReplies.Scribe(session, state, hook, _voices))
            {
                var seconds = state.Mind.Kind == MindKind.God ? _rules.ScribeGodSeconds : _rules.ScribeCreatureSeconds;
                state.NextScribeTick = world.Tick + (long)Math.Round(seconds * SimulationTime.TicksPerSecond);
                return;
            }
        }
    }

    /// <summary>Adds a hook to a mind, keeping the newest few.</summary>
    private void Add(MindState state, Hook hook)
    {
        state.Hooks.Add(hook);
        if (state.Hooks.Count > 8)
        {
            state.Hooks.RemoveAt(0);
        }

        state.FirstHookAt ??= _clock;
    }

    /// <summary>A mind's state, made when first needed.</summary>
    private MindState State(Mind mind)
    {
        if (!_minds.TryGetValue(mind, out var state))
        {
            state = new MindState(mind);
            _minds[mind] = state;
        }

        return state;
    }

    /// <summary>Whether a hook waits for another to wake the mind: another AI god's words.</summary>
    private static bool Passive(WorldState world, Hook hook) =>
        hook.Kind == HookKind.Words && world.Gods.Exists(g => g.Id == hook.OtherGodId && g.Controller == GodController.Ai);

    /// <summary>Whether a mind has a god or Creature to think for: an AI god, or a Creature not banished.</summary>
    private static bool Lives(WorldState world, Mind mind) => mind.Kind == MindKind.God
        ? world.Gods.Exists(g => g.Id == mind.Id && g.Controller == GodController.Ai)
        : world.Creatures.Exists(c => c.Id == mind.Id && c.Activity.Phase != CreaturePhase.Banished);

    /// <summary>Asks the model, turning a throw into a failed answer.</summary>
    private static Task<ModelAnswer> Question(ILanguageModel model, ModelQuestion question, CancellationToken cancel)
    {
        try
        {
            return model.Ask(question, cancel);
        }
        catch (Exception problem) when (problem is not OutOfMemoryException)
        {
            return Task.FromResult(ModelAnswer.Without(ModelOutcome.Failed, problem.Message));
        }
    }

    /// <summary>The world's ticks between a god's unprompted looks about it.</summary>
    private long ReflectTicks => Math.Max(1L, (long)Math.Round(_rules.ReflectSeconds * SimulationTime.TicksPerSecond));
}
