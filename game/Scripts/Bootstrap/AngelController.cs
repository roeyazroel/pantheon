using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.UI;
using Pantheon.Domain.Events;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The player's angel in the client (ADR-074): every frame it hands the angel the events handled and
/// the real time passed, with the model the settings choose; it shows the angel beside the god's hand
/// when it speaks, and shows the god where a deed it urges is done; the angel key (') and the bar's
/// button call it, and Ask puts the god's question to it. During the tutorial it stands beside the
/// tutorial's card, which it speaks. Its log lines begin "Angel:".
/// </summary>
internal sealed partial class AngelController : Node
{
    // Frames the start's call (--angel=<stage>, --angel-ask) waits, for the other start options to apply first.
    private const int CallFrames = 12;

    // How close the camera comes to what the angel shows.
    private const float ShowDistance = 150f;

    private readonly List<WorldEvent> _heard = [];
    private GameSession _session = null!;
    private Births _births = null!;
    private DivineController _divine = null!;
    private GodCameraController _camera = null!;
    private UI.DiplomacyPanel _diplomacy = null!;
    private TutorialController _tutorial = null!;
    private Func<GameSettings> _settings = null!;
    private Func<bool> _title = null!;
    private Action<string> _setStatus = null!;
    private AngelDirector? _angel;
    private AngelCard _card = null!;
    private ILanguageModel? _model;
    private GameSettings? _openedFor;
    private bool? _forced;
    private (bool Due, string? Question) _startCall;
    private int _frames;

    /// <summary>Connects the angel to the game.</summary>
    /// <param name="session">The session.</param>
    /// <param name="births">The births, for the model.</param>
    /// <param name="divine">The divine controller, for the events handled and the deeds shown.</param>
    /// <param name="camera">The camera, which flies to what the angel shows.</param>
    /// <param name="ui">The diplomacy panel, for an answer it urges, and the tutorial, which it speaks.</param>
    /// <param name="options">Launch options: the angel on or off, a stage to hold it at, a question to ask it.</param>
    /// <param name="settings">The player's settings now.</param>
    /// <param name="title">Whether the title screen is open: the angel keeps away from it.</param>
    /// <param name="setStatus">Shows a status line.</param>
    public void Setup(GameSession session, Births births, DivineController divine, GodCameraController camera, (UI.DiplomacyPanel Diplomacy, TutorialController Tutorial) ui, Game.Launch.LaunchOptions options, Func<GameSettings> settings, Func<bool> title, Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(options);
        (_session, _births, _divine, _camera, _diplomacy, _tutorial, _settings, _title, _setStatus) = (session, births, divine, camera, ui.Diplomacy, ui.Tutorial, settings, title, setStatus);
        if (Startup.TryLoadAngel() is not { } content)
        {
            return;
        }

        _angel = new AngelDirector(content) { ForcedStage = options.AngelLook };
        _angel.Spoke += Spoke;
        _card = new AngelCard { Name = "AngelCard" };
        AddChild(_card);
        _card.SuggestionChosen += Show;
        _card.Asked += Ask;
        divine.EventsHandled += _heard.AddRange;
        session.WorldReplaced += () =>
        {
            _angel.Clear();
            _heard.Clear();
            _card.Close();
        };
        _forced = options.Angel;
        _startCall = (options.AngelLook is not null || options.AngelAsk is not null, options.AngelAsk);
    }

    /// <summary>The angel's lines for the debug panel.</summary>
    /// <returns>The lines; none without the angel.</returns>
    public IReadOnlyList<string> Lines() => _angel is null ? [] : AngelReadout.Lines(_angel, _session.LocalGod, _model?.Name);

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_angel is null)
        {
            return;
        }

        var settings = _settings();
        Reopen(settings);
        var events = _heard.ToList();
        _heard.Clear();
        var shown = (_forced ?? settings.Angel) && !_title() && _session.LocalGod is not null;
        if (_startCall.Due && ++_frames > CallFrames && shown)
        {
            _startCall.Due = false;
            Call(_startCall.Question);
        }

        _tutorial.Speaker = _angel.Name.Length > 0 ? _angel.Name + ", your angel" : "Tutorial";
        _angel.Update(_session, events, delta, new AngelTerms(_model, shown, settings.AngelThinks, settings.AngelChatter, Hushed: _tutorial.IsActive));
        _card.Beside = shown && _tutorial.IsActive ? _tutorial.CardRect : null;
        if (!shown && _card.Open)
        {
            _card.Close();
        }

        _card.Frame(Hand(), _angel.Look, delta);
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_angel is not null && @event.IsActionPressed(InputActions.AngelCall) && !_title())
        {
            if (_card.Open)
            {
                _card.Close();
            }
            else
            {
                Call(null);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the angel's model again when the settings that choose it change.</summary>
    private void Reopen(GameSettings settings)
    {
        var choice = (_births.Provider, settings.ClaudeModel, settings.OpenAIModel, settings.GeminiModel, settings.LocalUrl, settings.LocalModel, settings.AngelThinks, settings.AgentsQuick);
        if (_openedFor is { } before && (_births.Provider, before.ClaudeModel, before.OpenAIModel, before.GeminiModel, before.LocalUrl, before.LocalModel, before.AngelThinks, before.AgentsQuick) == choice)
        {
            return;
        }

        (_model as IDisposable)?.Dispose();
        _model = _births.OpenAgentModel(settings, settings.AngelThinks, out _);
        _openedFor = settings;
        GD.Print(_model is not null ? $"Angel: thinks with {_model.Name}" : "Angel: the scribes speak for it");
    }

    /// <summary>The god calls its angel, or asks it something: it comes at once, listening, and answers soon.</summary>
    private void Call(string? question)
    {
        if (_angel is null || _session.LocalGod is null)
        {
            return;
        }

        _angel.Call(_session.World.Tick, question);
        _card.Listen(_angel.Name.Length > 0 ? _angel.Name : "Your angel", _angel.Content.Stage(_angel.Look.Stage), _angel.Look);
        GD.Print(question is null ? "Angel: called" : $"Angel: asked “{question}”");
    }

    /// <summary>The god asks its angel something from the card.</summary>
    private void Ask(string question) => Call(question);

    /// <summary>The angel speaks: its card shows its words.</summary>
    private void Spoke(AngelWords words)
    {

        _card.Say(words, _angel!.Content.Stage(words.Look.Stage));
        var urged = words.Suggestions.Count > 0 ? $" (urges: {string.Join("; ", words.Suggestions.Select(s => s.Text))})" : string.Empty;
        GD.Print($"Angel: {words.Name}, {words.Look.Stage}, by {(words.FromModel ? "its model" : "the scribes")} of {words.Hook.Moment}: “{words.Text}”{urged}");
    }

    /// <summary>Shows the god where a deed the angel urges is done, ready to do it.</summary>
    private void Show(AngelSuggestion suggestion)
    {
        _camera.FocusOn(suggestion.Target, ShowDistance, snap: false);
        switch (suggestion.Deed)
        {
            case AngelDeed.Cast:
                _divine.Select(suggestion.MiracleId);
                _setStatus($"{suggestion.Text}: click the village to cast it");
                break;
            case AngelDeed.CreatureWork:
                _divine.BeginWork();
                break;
            case AngelDeed.CreatureFight:
                _divine.BeginFight();
                break;
            case AngelDeed.Answer:
                _diplomacy.PanelVisible = true;
                _setStatus($"{suggestion.Text}: the gods' panel is open");
                break;
            case AngelDeed.Look:
                _setStatus(suggestion.Text);
                break;
        }

        _card.Close();
    }

    /// <summary>Where the god's hand is on the screen: over the ground the pointer is on, or the middle of the view when the pointer is away.</summary>
    private Vector2 Hand()
    {
        var screen = GetViewport().GetVisibleRect().Size;
        if (_camera.CursorGroundPoint is { } ground && !_camera.ViewCamera.IsPositionBehind(ground.ToGodot()))
        {
            var point = _camera.ViewCamera.UnprojectPosition(ground.ToGodot());
            if (new Rect2(Vector2.Zero, screen).HasPoint(point))
            {
                return point;
            }
        }

        return screen / 2f;
    }
}
