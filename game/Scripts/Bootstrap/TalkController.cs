using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Pantheon.Client.UI;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Game;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Minds;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Talking to the gods in the client (ADR-066): Speak on a god's diplomacy card opens the talk panel
/// and pauses the world. With a model, the player's words go to it off the main thread, and its
/// reply is applied here, in between ticks; with the scribes, each suggestion asks one act. Only the
/// host (or a player alone) speaks with the gods: their acts are commands as the gods themselves.
/// </summary>
internal sealed partial class TalkController : Node
{
    // Frames the start's talk (--talk) waits, for the other start options to apply first.
    private const int StartFrames = 5;

    private GameSession _session = null!;
    private Births _births = null!;
    private TalkPanel _panel = null!;
    private Action<string> _setStatus = null!;
    private Talks? _talks;
    private GodTalk? _talk;
    private ILanguageModel? _model;
    private string? _modelProblem;
    private CancellationTokenSource? _asking;
    private bool _wasPaused;
    private (int God, string? Words)? _start;
    private int _frames;
    private int _openedAt;

    /// <summary>Raised when a conversation the player spoke in ends (ADR-072): the god, and its lines since it was opened.</summary>
    public event Action<int, IReadOnlyList<TalkLine>>? Ended;

    /// <summary>Whether the player is speaking with a god.</summary>
    public bool IsOpen => _talk is not null;

    /// <summary>Connects the talk to the game.</summary>
    /// <param name="session">The session.</param>
    /// <param name="births">The births, for the minds' content and the model chosen.</param>
    /// <param name="diplomacy">The diplomacy panel, whose Speak opens the talk.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="options">Launch options: a god to speak with, and words to say, at the start.</param>
    public void Setup(GameSession session, Births births, DiplomacyPanel diplomacy, Action<string> setStatus, Game.Launch.LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(diplomacy);
        ArgumentNullException.ThrowIfNull(options);
        (_session, _births, _setStatus) = (session, births, setStatus);
        _talks = births.Minds is { } minds ? new Talks(minds.Talk) : null;
        _panel = new TalkPanel { Name = "TalkPanel" };
        AddChild(_panel);
        _panel.SuggestionClicked += Suggest;
        _panel.Sent += Speak;
        _panel.Answered += Answer;
        _panel.Closed += Close;
        diplomacy.SpeakClicked += Open;
        session.WorldReplaced += () =>
        {
            Close();
            _talks?.Clear();
        };
        // The start's other options (meeting the gods, advancing time) apply first.
        _start = options.Talk is { } god ? (god, options.TalkSay) : null;
    }

    /// <summary>Opens the conversation with a god, pausing the world.</summary>
    /// <param name="godId">The god.</param>
    public void Open(int godId)
    {
        var world = _session.World;
        if (_talks is null || _session.LocalGodId is not { } me || world.Gods.Find(g => g.Id == godId) is not { Controller: GodController.Ai } god)
        {
            _setStatus("Only the AI gods can be spoken with");
            return;
        }

        if (_session.IsReplica)
        {
            _setStatus("In a shared world only the host speaks with the gods");
            return;
        }

        if (!Acquaintance.Knows(_session.Content.Diplomacy.Acquaintance, world, me, godId))
        {
            _setStatus($"You have not met {god.Name} yet");
            return;
        }

        var opening = _talk is null;
        if (opening)
        {
            _wasPaused = _session.Scheduler.IsPaused;
            _session.Scheduler.IsPaused = true;
        }

        _model ??= _births.OpenModel(out _modelProblem);
        _talk = _talks.Open(_session, god, me);
        _openedAt = opening ? _talk.Lines.Count : _openedAt;
        Refresh();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_start is { } start && ++_frames > StartFrames)
        {
            _start = null;
            Open(start.God);
            if (start.Words is { Length: > 0 } words)
            {
                Speak(words);
            }
        }

        if (_talk is not null)
        {
            Refresh();
        }
    }

    /// <summary>Ends the conversation, letting the world run again if it ran before.</summary>
    private void Close()
    {
        _panel.Close();
        if (_talk is null)
        {
            return;
        }

        var lines = _talk.Lines.Skip(_openedAt).ToList();
        if (lines.Exists(l => l.Speaker == TalkSpeaker.Player))
        {
            Ended?.Invoke(_talk.GodId, lines);
        }

        _talk = null;
        _session.Scheduler.IsPaused = _wasPaused;
        _asking?.Cancel();
        if (_asking is null)
        {
            Retire();
        }
    }

    /// <summary>Words a god sent the player unasked (ADR-072): a line of its conversation, shown at once if it is open.</summary>
    /// <param name="godId">The god.</param>
    /// <param name="words">What it said.</param>
    public void Heard(int godId, string words)
    {
        if (_talks is null || _session.LocalGodId is not { } me || _session.World.Gods.Find(g => g.Id == godId) is not { } god)
        {
            return;
        }

        _talks.Heard(_session, god, me, words);
        if (_talk?.GodId == godId)
        {
            Refresh();
        }
    }

    /// <summary>A suggestion: the model's question when there is a model, the scribes' answer when not.</summary>
    private void Suggest(TalkSuggestion suggestion)
    {
        if (_model is not null)
        {
            Speak(suggestion.Text);
        }
        else if (_talk is { } talk && _session.LocalGodId is { } me)
        {
            _talks!.Choose(_session, talk, me, suggestion);
            Refresh();
        }
    }

    /// <summary>The player's words to the god's model, asked off the main thread and answered here.</summary>
    private async void Speak(string words)
    {
        if (_model is not { } model || _talk is not { Answering: false } talk || _session.LocalGodId is not { } me || _talks is null)
        {
            return;
        }

        var world = _session.World;
        var question = _talks.Ask(_session, talk, me, words);
        Refresh();
        _asking = new CancellationTokenSource();
        ModelAnswer answer;
        try
        {
            answer = await model.Ask(question, _asking.Token);
        }
        catch (OperationCanceledException)
        {
            answer = ModelAnswer.Without(ModelOutcome.Failed, "you left before it answered");
        }
        finally
        {
            _asking.Dispose();
            _asking = null;
        }

        if (!IsInstanceValid(this) || !ReferenceEquals(world, _session.World))
        {
            return;
        }

        // Applied here, on the main thread and in between ticks, even if the player has closed the panel.
        _talks.Answer(_session, talk, me, answer);
        if (_talk is null)
        {
            Retire();
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>The player's answer to the god's proposal.</summary>
    private void Answer(bool accept)
    {
        if (_talk is not { } talk)
        {
            return;
        }

        var text = SessionCommands.CommandDiplomacy(_session, talk.GodId, accept ? DiplomaticCommandKind.Accept : DiplomaticCommandKind.Decline);
        talk.Lines.Add(new TalkLine(TalkSpeaker.Note, text));
        Refresh();
    }

    /// <summary>Lets go of the model, for the next conversation to open the one chosen then.</summary>
    private void Retire()
    {
        (_model as IDisposable)?.Dispose();
        _model = null;
    }

    /// <summary>Shows the conversation as it is now.</summary>
    private void Refresh()
    {
        if (_talk is not { } talk || _session.World.Gods.Find(g => g.Id == talk.GodId) is not { } god || _session.LocalGodId is not { } me)
        {
            return;
        }

        var world = _session.World;
        var view = DiplomacyModel.FindRelation(world, god.Id, me);
        var voice = _model is not null ? $" · its words are {_model.Name}'s" : _modelProblem is not null ? $" · the scribes speak for it ({_modelProblem})" : " · the scribes speak for it";
        var subtitle = $"{DiplomacyText.Status(DiplomacyModel.StatusOf(world, god.Id, me))} · it {DiplomacyText.Attitude(view)}{voice}";
        var proposal = DiplomacyAuthority.FindProposal(world, god.Id, me) is not null
            ? Game.Politics.PoliticsReadout.ForGod(_session, god.Id, Game.Politics.TreatyChoice.Default).Proposal.Replace("It ", god.Name + " ", StringComparison.Ordinal)
            : null;
        _panel.Show(new TalkView(PersonaText.Title(god), subtitle, talk.Lines, talk.Suggestions, talk.Answering ? god.Name : null, _model is not null, proposal is { Length: > 0 } ? proposal : null));
    }
}
