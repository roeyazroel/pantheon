using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Minds;

namespace Pantheon.Game.Angel;

/// <summary>How the angel may speak this frame (ADR-074).</summary>
/// <param name="Model">The model it asks, or <c>null</c> for the scribes alone.</param>
/// <param name="Shown">Whether the player keeps an angel at all.</param>
/// <param name="Thinking">Whether it may ask its model (the player's setting).</param>
/// <param name="Chatter">How often it speaks unasked: 0 rarely, 1 sometimes, 2 often.</param>
/// <param name="Hushed">Whether something else speaks for now (the tutorial): it answers its god's calls only.</param>
public sealed record AngelTerms(ILanguageModel? Model, bool Shown, bool Thinking, int Chatter, bool Hushed = false);

/// <summary>What the angel said (ADR-074), for the client to show.</summary>
/// <param name="Name">The angel's name.</param>
/// <param name="Text">Its words.</param>
/// <param name="Suggestions">The deeds it urges: at most two.</param>
/// <param name="Hook">The moment it spoke of.</param>
/// <param name="Look">How it looked as it spoke.</param>
/// <param name="FromModel">Whether its model spoke, rather than the scribes.</param>
/// <param name="ShowSeconds">How long to show its words, to read them.</param>
public sealed record AngelWords(string Name, string Text, IReadOnlyList<AngelSuggestion> Suggestions, AngelHook Hook, AngelLook Look, bool FromModel, double ShowSeconds);

/// <summary>
/// The player's angel (ADR-074): the god's conscience and guide. It reads its look from what the world
/// remembers of its god, gathers the moments that move it from the world's events and from its god's
/// calls, and when its turn comes asks its model, off the simulation's thread, what to say and which of
/// the deeds its god can do to urge; the answer is taken here, in between ticks. Without a model, when
/// the player keeps it from the model, when the hour's questions are spent or while it rests after
/// failures, the scribes speak for it in its voice. It only speaks: its god does what it chooses.
/// Session state only: its name and look come from the world, and nothing of it is saved.
/// </summary>
public sealed class AngelDirector
{
    // At most this many moments wait for its next words; the oldest are forgotten.
    private const int MaxWaiting = 8;

    // Moments that matter this much wait only a third of the usual time between words.
    private const int Urgent = 60;

    private readonly AngelContent _content;
    private readonly List<AngelHook> _hooks = [];
    private readonly Queue<double> _asked = new();
    private readonly List<string> _said = [];
    private CancellationTokenSource _cancel = new();
    private Task<ModelAnswer>? _asking;
    private IReadOnlyList<AngelHook> _askedHooks = [];
    private IReadOnlyList<AngelSuggestion> _askedChoices = [];
    private WorldState? _world;
    private WorldState? _askedWorld;
    private AngelStage? _lastStage;
    private double _clock;
    private double? _firstHookAt;
    private double _lastWordsAt;
    private double _restUntil;
    private int _failures;
    private bool _welcomed;
    private bool _creatureNeeds;

    /// <summary>Creates the angel.</summary>
    /// <param name="content">Its content.</param>
    public AngelDirector(AngelContent content) => _content = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>Its content.</summary>
    public AngelContent Content => _content;

    /// <summary>Its name in this world.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>How it looks now.</summary>
    public AngelLook Look { get; private set; } = AngelLook.Newborn;

    /// <summary>A stage its look is held at, for captures (<c>--angel=&lt;stage&gt;</c>); its god's deeds decide when <c>null</c>.</summary>
    public AngelStage? ForcedStage { get; set; }

    /// <summary>The moments waiting for its next words.</summary>
    public IReadOnlyList<AngelHook> Waiting => _hooks;

    /// <summary>Its last words.</summary>
    public AngelWords? Last { get; private set; }

    /// <summary>The questions it asked its model.</summary>
    public int Turns { get; private set; }

    /// <summary>Questions asked in the last hour.</summary>
    public int AskedThisHour => _asked.Count;

    /// <summary>Tokens its model read and wrote.</summary>
    public (long Read, long Written) Tokens { get; private set; }

    /// <summary>Why its last question went unanswered, until one is answered.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether its model is being asked now.</summary>
    public bool Thinking => _asking is not null;

    /// <summary>Whether it rests from its model after failures, the scribes speaking meanwhile.</summary>
    public bool Resting => _clock < _restUntil;

    /// <summary>Raised when it speaks.</summary>
    public event Action<AngelWords>? Spoke;

    /// <summary>Its god calls it, or asks it something: it answers soon, even while the world is paused.</summary>
    /// <param name="tick">The world's tick.</param>
    /// <param name="question">What its god asks, or <c>null</c> when it only calls.</param>
    public void Call(long tick, string? question)
    {
        var text = string.IsNullOrWhiteSpace(question) ? "Your god calls you to its side, wanting your counsel." : $"Your god asks you: “{question.Trim()}”";
        _hooks.RemoveAll(h => h.Moment == AngelMoment.Called);
        Add(new AngelHook(AngelMoment.Called, tick, text));
    }

    /// <summary>Forgets the world: its moments, its words and the question it is asking, for a new world.</summary>
    public void Clear()
    {
        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        (_asking, _world, _lastStage, _firstHookAt, _failures, _welcomed, _creatureNeeds, Last, Problem) = (null, null, null, null, 0, false, false, null, null);
        _hooks.Clear();
        _said.Clear();
    }

    /// <summary>Takes its model's answer, reads its look and the frame's moments, and speaks when its turn comes.</summary>
    /// <param name="session">The session.</param>
    /// <param name="events">The events handled this frame.</param>
    /// <param name="realSeconds">Real time since the last frame.</param>
    /// <param name="terms">How it may speak.</param>
    public void Update(GameSession session, IReadOnlyList<WorldEvent> events, double realSeconds, AngelTerms terms)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(terms);
        if (session.LocalGod is not { } god)
        {
            return;
        }

        if (_world is not null && !ReferenceEquals(session.World, _world))
        {
            Clear();
        }

        if (_world is null)
        {
            _world = session.World;
            Name = _content.NameFor(session.World.Seed, god.Id);
            _lastWordsAt = _clock;
        }

        _clock += Math.Max(0d, realSeconds);
        while (_asked.Count > 0 && _clock - _asked.Peek() > 3600d)
        {
            _asked.Dequeue();
        }

        var deeds = AngelLooks.Of(god, _content);
        Look = ForcedStage is { } forced ? AngelLooks.Forced(forced, deeds.Marks) : deeds;
        Finish(session, god);
        if (!terms.Shown)
        {
            _hooks.Clear();
            return;
        }

        Watch(session, god, events, terms);
        Turn(session, god, terms);
    }

    /// <summary>Gathers the moments: a change of stage, the first meeting, the frame's events, its god's Creature's needs, and a long quiet.</summary>
    private void Watch(GameSession session, GodState god, IReadOnlyList<WorldEvent> events, AngelTerms terms)
    {
        var world = session.World;
        if (_lastStage is { } before && before != Look.Stage)
        {
            var light = AngelLooks.Forced(before).Light;
            var rose = Look.Light > light || (Look.Light == light && Look.Stage == AngelStage.Unformed);
            Add(new AngelHook(rose ? AngelMoment.StageRose : AngelMoment.StageFell, world.Tick,
                $"You changed: you were {_content.Stage(before).Name.ToLowerInvariant()}, and your god's deeds have made you {_content.Stage(Look.Stage).Name.ToLowerInvariant()}.", Detail: _content.Stage(Look.Stage).Name.ToLowerInvariant()));
        }

        _lastStage = Look.Stage;
        if (!_welcomed && !terms.Hushed)
        {
            _welcomed = true;
            Add(new AngelHook(AngelMoment.Welcome, world.Tick, "You meet your god for the first time in this world: greet it, tell it who you are, and what it might do first."));
        }

        foreach (var hook in AngelHooks.Read(events, world, session.Content, god.Id))
        {
            Add(hook);
        }

        if (session.LocalCreature is { } creature)
        {
            var rules = session.Content.Creatures;
            var needs = creature.Needs.Hunger >= rules.HungryThreshold || creature.Needs.Fatigue >= rules.TiredThreshold;
            if (needs && !_creatureNeeds)
            {
                Add(new AngelHook(AngelMoment.CreatureNeeds, world.Tick, $"Your Creature {creature.Name} grew {(creature.Needs.Hunger >= rules.HungryThreshold ? "hungry" : "tired")}."));
            }

            _creatureNeeds = needs;
        }

        if (terms.Hushed)
        {
            // While something else speaks, the angel keeps only its god's calls.
            _hooks.RemoveAll(h => h.Moment != AngelMoment.Called);
            return;
        }

        var quiet = _content.Timing.QuietSeconds[Math.Clamp(terms.Chatter, 0, 2)];
        if (!session.Scheduler.IsPaused && _hooks.Count == 0 && _asking is null && _clock - _lastWordsAt >= quiet)
        {
            Add(new AngelHook(AngelMoment.Quiet, world.Tick, "Nothing has been said for a while: your god may not know what to do next."));
        }
    }

    /// <summary>Speaks of the moments waiting when its turn comes: by its model when it may ask it, else by the scribes.</summary>
    private void Turn(GameSession session, GodState god, AngelTerms terms)
    {
        var called = _hooks.Exists(h => h.Moment == AngelMoment.Called);
        if (_hooks.Count == 0 || _asking is not null || (!called && (terms.Hushed || session.Scheduler.IsPaused)))
        {
            return;
        }

        var timing = _content.Timing;
        var words = timing.WordsSeconds[Math.Clamp(terms.Chatter, 0, 2)];
        var soon = called || _hooks.Exists(h => h.Moment == AngelMoment.Welcome);
        var wait = soon ? timing.CallSeconds : _hooks.Max(h => h.Weight) >= Urgent ? words / 3d : words;
        if ((!called && _clock - (_firstHookAt ?? _clock) < timing.GatherSeconds) || _clock - _lastWordsAt < wait)
        {
            return;
        }

        // A call is answered at once, with the greeting if it has not been given; other moments wait their turn.
        var hooks = (called ? _hooks.Where(h => h.Moment is AngelMoment.Called or AngelMoment.Welcome) : _hooks).ToList();
        _hooks.RemoveAll(hooks.Contains);
        _firstHookAt = _hooks.Count > 0 ? _clock : null;
        var choices = AngelChoices.Of(session, god, hooks, _content.MaxChoices);
        _lastWordsAt = _clock;
        if (terms.Model is { } model && terms.Thinking && !Resting && _asked.Count < timing.TurnsPerHour)
        {
            var question = AngelPrompt.Question(session, god, Name, Look, hooks, choices, _said, _content);
            (_askedHooks, _askedChoices, _askedWorld) = (hooks, choices, session.World);
            _asked.Enqueue(_clock);
            Turns++;
            _asking = Ask(model, question, _cancel.Token);
            return;
        }

        Scribe(session, god, hooks, choices);
    }

    /// <summary>Takes its model's answer when it has come: its words, or the scribes' when it failed.</summary>
    private void Finish(GameSession session, GodState god)
    {
        if (_asking is not { IsCompleted: true } task)
        {
            return;
        }

        _asking = null;
        if (!ReferenceEquals(_askedWorld, session.World))
        {
            return;
        }

        var answer = task.IsCompletedSuccessfully ? task.Result : ModelAnswer.Without(ModelOutcome.Failed, task.Exception?.InnerException?.Message ?? "the question was dropped");
        Tokens = (Tokens.Read + answer.InputTokens, Tokens.Written + answer.OutputTokens);
        if (answer.Outcome == ModelOutcome.Answered && AngelPrompt.Read(answer.Json, _askedChoices) is { } turn)
        {
            (_failures, Problem) = (0, null);
            Speak(_askedHooks.MaxBy(h => h.Weight)!, turn.Say, turn.Suggestions, fromModel: true);
            return;
        }

        Problem = answer.Problem ?? "its answer could not be read";
        if (++_failures >= 3)
        {
            (_restUntil, _failures) = (_clock + _content.Timing.RestSeconds, 0);
        }

        Scribe(session, god, _askedHooks, _askedChoices);
    }

    /// <summary>The scribes speak for it of the weightiest moment.</summary>
    private void Scribe(GameSession session, GodState god, IReadOnlyList<AngelHook> hooks, IReadOnlyList<AngelSuggestion> choices)
    {
        var hook = hooks.MaxBy(h => h.Weight)!;
        Speak(hook, AngelScribe.Line(_content, Look, hook, session.World, god, Name), AngelScribe.Pick(choices, Look), fromModel: false);
    }

    /// <summary>It says its words.</summary>
    private void Speak(AngelHook hook, string text, IReadOnlyList<AngelSuggestion> suggestions, bool fromModel)
    {
        var timing = _content.Timing;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Last = new AngelWords(Name, text, suggestions, hook, Look, fromModel, Math.Max(timing.ShowSeconds, words * timing.SecondsPerWord));
        _lastWordsAt = _clock;
        _said.Add(text);
        if (_said.Count > _content.Remembered)
        {
            _said.RemoveRange(0, _said.Count - _content.Remembered);
        }

        Spoke?.Invoke(Last);
    }

    /// <summary>Adds a moment, keeping the newest few.</summary>
    private void Add(AngelHook hook)
    {
        _hooks.Add(hook);
        if (_hooks.Count > MaxWaiting)
        {
            _hooks.Remove(_hooks.Where(h => h.Moment != AngelMoment.Called).MinBy(h => h.Weight)!);
        }

        _firstHookAt ??= _clock;
    }

    /// <summary>Asks the model, turning a throw into a failed answer.</summary>
    private static Task<ModelAnswer> Ask(ILanguageModel model, ModelQuestion question, CancellationToken cancel)
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
}
