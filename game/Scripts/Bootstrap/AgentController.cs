using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Minds;
using Pantheon.Game.Minds.Agents;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The gods' and Creatures' agents in the client (ADR-072): every frame it hands them the events handled
/// and the real time passed, with the model the settings choose; it shows what the Creatures say above
/// them, puts the gods' words to the player into their conversations, and wakes a god with what was
/// said when a talk with it ends. Its log lines begin "Mind:".
/// </summary>
internal sealed partial class AgentController : Node
{
    // Frames the start's wake (--wake) waits, for the other start options to apply first.
    private const int WakeFrames = 8;

    private readonly List<WorldEvent> _heard = [];
    private GameSession _session = null!;
    private Births _births = null!;
    private DivineController _divine = null!;
    private TalkController _talk = null!;
    private Func<GameSettings> _settings = null!;
    private Func<bool> _title = null!;
    private AgentDirector? _agents;
    private ILanguageModel? _model;
    private GameSettings? _openedFor;
    private bool? _forced;
    private int? _wake;
    private int _frames;

    /// <summary>Connects the agents to the game.</summary>
    /// <param name="session">The session.</param>
    /// <param name="births">The births, for the minds' content and the model.</param>
    /// <param name="divine">The divine controller, for the events handled and the Creatures' speech.</param>
    /// <param name="talk">The talk, for the gods' words to the player and the conversations' ends.</param>
    /// <param name="options">Launch options: the agents on or off, and a god to wake.</param>
    /// <param name="settings">The player's settings now.</param>
    /// <param name="title">Whether the title screen is open: the island behind it is a backdrop, and no mind thinks or speaks there.</param>
    public void Setup(GameSession session, Births births, DivineController divine, TalkController talk, Game.Launch.LaunchOptions options, Func<GameSettings> settings, Func<bool> title)
    {
        ArgumentNullException.ThrowIfNull(options);
        (_session, _births, _divine, _talk, _settings, _title) = (session, births, divine, talk, settings, title);
        if (births.Minds is not { } minds)
        {
            return;
        }

        _agents = new AgentDirector(minds.Agents, minds.Voices);
        _agents.Answered += Report;
        divine.EventsHandled += _heard.AddRange;
        talk.Ended += Talked;
        session.WorldReplaced += () =>
        {
            _agents.Clear();
            _heard.Clear();
        };
        (_forced, _wake) = (options.Agents, options.Wake);
    }

    /// <summary>The agents' lines for the debug panel.</summary>
    /// <returns>The lines; none without the minds' content.</returns>
    public IReadOnlyList<string> Lines() => _agents is null ? [] : AgentReadout.Lines(_agents, _session.World, _model?.Name, _settings().AgentTurnsPerHour);

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_agents is null)
        {
            return;
        }

        var settings = _settings();
        Reopen(settings);
        if (_wake is { } wake && ++_frames > WakeFrames)
        {
            _wake = null;
            Wake(wake);
        }

        var events = _heard.ToList();
        _heard.Clear();
        if (_title())
        {
            return;
        }

        Show(events);
        _agents.Update(_session, events, delta, new AgentOptions(_model, _forced ?? settings.Agents, settings.AgentTurnsPerHour, settings.Voices));
    }

    /// <summary>Opens the agents' model again when the minds' settings change.</summary>
    private void Reopen(GameSettings settings)
    {
        if (_openedFor is { } before && Minds(before) == Minds(settings))
        {
            return;
        }

        (_model as IDisposable)?.Dispose();
        _model = _session.IsReplica ? null : _births.OpenAgentModel(settings, _forced, out var problem);
        _openedFor = settings;
        GD.Print(_model is not null
            ? $"Mind: the gods and Creatures think with {_model.Name}"
            : $"Mind: the scribes speak for the gods and Creatures{(_session.IsReplica ? " (the host thinks for them)" : string.Empty)}");
    }

    /// <summary>The settings that choose the agents' model.</summary>
    private (MindProvider, string, string, string, string, string, bool, bool) Minds(GameSettings settings) =>
        (_births.Provider, settings.ClaudeModel, settings.OpenAIModel, settings.GeminiModel, settings.LocalUrl, settings.LocalModel, _forced ?? settings.Agents, settings.AgentsQuick);

    /// <summary>Shows the Creatures' words above them, and puts the gods' words to the player into their conversations.</summary>
    private void Show(List<WorldEvent> events)
    {
        foreach (var worldEvent in events)
        {
            if (worldEvent is CreatureSpoke said)
            {
                _divine.ShowSpeech(said.CreatureId, said.Text, _agents!.Rules.SpeechSeconds);
                GD.Print($"Mind: {_session.World.Creatures.Find(c => c.Id == said.CreatureId)?.Name} says “{said.Text}”");
            }
            else if (worldEvent is GodSpoke spoke && spoke.ToGodId == _session.LocalGodId)
            {
                _talk.Heard(spoke.GodId, spoke.Text);
                GD.Print($"Mind: {_session.World.Gods.Find(g => g.Id == spoke.GodId)?.Name} says to you “{spoke.Text}”");
            }
        }
    }

    /// <summary>A talk with a god ended: the god is woken with what was said.</summary>
    private void Talked(int godId, IReadOnlyList<TalkLine> lines)
    {
        var me = _session.LocalGod?.Name ?? "a god";
        var said = string.Join(" ", lines.TakeLast(8).Select(l => l.Speaker switch
        {
            TalkSpeaker.Player => $"{me}: “{l.Text}”",
            TalkSpeaker.God => $"You: {l.Text}",
            TalkSpeaker.Note => $"({l.Text})",
        }));
        _agents?.Wake(Mind.God(godId), new Hook(HookKind.Talked, _session.World.Tick, $"{me} spoke with you. {said}", OtherGodId: _session.LocalGodId));
    }

    /// <summary>Wakes an AI god, or every one, to look about it (<c>--wake</c>).</summary>
    private void Wake(int godId)
    {
        foreach (var god in _session.World.Gods.Where(g => g.Controller == GodController.Ai && (godId == 0 || g.Id == godId)))
        {
            _agents?.Wake(Mind.God(god.Id), new Hook(HookKind.Reflect, _session.World.Tick, "It is time to look about you."));
            GD.Print($"Mind: {god.Name} is woken to look about it");
        }
    }

    /// <summary>Logs a mind's turn.</summary>
    private void Report(MindState state)
    {
        var world = _session.World;
        var name = state.Mind.Kind == MindKind.God ? world.Gods.Find(g => g.Id == state.Mind.Id)?.Name : world.Creatures.Find(c => c.Id == state.Mind.Id)?.Name;
        if (state.LastProblem is { } problem)
        {
            GD.Print($"Mind: {name} went unanswered ({problem})");
            return;
        }

        var why = state.LastReason.Length > 0 ? $" because {state.LastReason}" : string.Empty;
        var did = state.LastResults.Count > 0 ? $"; {string.Join("; ", state.LastResults)}" : string.Empty;
        var said = state.LastSaid.Length > 0 ? $"; said {state.LastSaid}" : string.Empty;
        GD.Print($"Mind: {name} ({state.Mind.Kind}) woke for {string.Join(" / ", state.LastHooks.Take(2))}{why}{did}{said}");
    }
}
