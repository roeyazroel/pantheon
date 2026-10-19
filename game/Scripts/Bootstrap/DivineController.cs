using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Presentation.Creatures;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Client.UI;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Hud;
using Pantheon.Game.Presentation;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The player's divine interface: selecting and casting miracles at the cursor (click-to-cast,
/// ADR-009), rewarding, punishing and sending the Creature with the hand (ADR-010), the HUD,
/// alerts, miracle effects and map overlays. It reads the session and sends commands to it; the
/// session decides what happens.
/// </summary>
internal sealed class DivineController
{
    // Alerts stay on screen this many real seconds.
    private const double AlertSeconds = 20.0;

    private readonly GameSession _session;
    private readonly Hud _hud;
    private readonly DivinePresenter _presenter;
    private readonly CreaturePresenter _creatures;
    private readonly DivineHand _hand;
    private readonly DivineViewSettings _view;
    private readonly Action<string> _setStatus;
    private readonly AlertFeed _feed = new();
    private readonly Dictionary<Alert, double> _shownAt = [];
    private double _clock;
    private (OverlayKind Kind, long Step, WorldState World)? _shownOverlay;

    /// <summary>Creates the controller.</summary>
    /// <param name="session">Game session.</param>
    /// <param name="hud">The HUD.</param>
    /// <param name="presenter">Divine visuals.</param>
    /// <param name="view">God colours and miracle looks.</param>
    /// <param name="setStatus">Shows a status line.</param>
    /// <param name="creatures">Creature visuals, for the hand's gestures.</param>
    /// <param name="hand">The god's hand.</param>
    public DivineController(GameSession session, Hud hud, DivinePresenter presenter, DivineViewSettings view, Action<string> setStatus, CreaturePresenter creatures, DivineHand hand)
    {
        _session = session;
        _hud = hud;
        _presenter = presenter;
        _view = view;
        _setStatus = setStatus;
        _creatures = creatures;
        _hand = hand;
        _hud.MiracleClicked += Select;
        _hud.CreatureJudged += JudgeCreature;
        _hud.CreatureSendClicked += BeginSend;
    }

    /// <summary>Whether the next click on the ground sends the Creature there.</summary>
    public bool SendMode { get; private set; }

    /// <summary>The selected miracle, if any.</summary>
    public string? SelectedMiracle { get; private set; }

    /// <summary>The map overlay shown, if any.</summary>
    public OverlayKind? Overlay { get; private set; }

    /// <summary>Selects the miracle on a number key, or deselects it when it is already selected.</summary>
    /// <param name="slot">Zero-based slot of the miracle bar.</param>
    public void SelectSlot(int slot)
    {
        var miracles = _session.Content.Miracles.Miracles;
        if (slot >= 0 && slot < miracles.Count)
        {
            Select(miracles[slot].Id == SelectedMiracle ? null : miracles[slot].Id);
        }
    }

    /// <summary>Selects a miracle, or deselects with <c>null</c>.</summary>
    /// <param name="miracleId">Miracle id.</param>
    public void Select(string? miracleId)
    {
        SelectedMiracle = miracleId;
        SendMode = false;
        if (miracleId is null)
        {
            _presenter.HideTarget();
        }
    }

    /// <summary>Casts the selected miracle at the cursor, or sends the Creature there in send mode.</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    /// <returns><c>true</c> when the click was used.</returns>
    public bool TryCast(NVector3? cursor)
    {
        if (cursor is not { } point)
        {
            return false;
        }

        if (SendMode)
        {
            SendCreature(cursor);
            return true;
        }

        if (SelectedMiracle is not { } miracle)
        {
            return false;
        }

        var power = _session.LocalGod?.DivinePower;
        _setStatus(SessionCommands.CastMiracle(_session, miracle, new GroundPosition(point.X, point.Z)));
        if (_session.LocalGod?.DivinePower < power)
        {
            _hand.Press(point.ToGodot());
        }

        return true;
    }

    /// <summary>Rewards or punishes the Creature, with a pat or a swat of the hand when it lands.</summary>
    /// <param name="kind">Reward or punish.</param>
    public void JudgeCreature(CreatureCommandKind kind)
    {
        var creature = _session.LocalCreature;
        var status = SessionCommands.CommandCreature(_session, kind);
        _setStatus(status);
        if (creature is null || _creatures.Find(creature.Id) is not { } shown)
        {
            return;
        }

        if (status.StartsWith("You reward", StringComparison.Ordinal))
        {
            _hand.Pat(shown.Position, shown.HeadHeight);
            _creatures.Flash(creature.Id, new Color(1f, 0.85f, 0.4f));
        }
        else if (status.StartsWith("You punish", StringComparison.Ordinal))
        {
            _hand.Swat(shown.Position, shown.HeadHeight);
            _creatures.Flash(creature.Id, new Color(1f, 0.15f, 0.1f));
        }
    }

    /// <summary>The next click on the ground sends the Creature there.</summary>
    public void BeginSend()
    {
        SelectedMiracle = null;
        SendMode = _session.LocalCreature is not null;
        _presenter.HideTarget();
        _setStatus(SendMode ? "Click where your Creature should go (Esc cancels)" : "You have no Creature");
    }

    /// <summary>Sends the Creature to a ground point.</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    public void SendCreature(NVector3? cursor)
    {
        SendMode = false;
        if (cursor is not { } point)
        {
            return;
        }

        _setStatus(SessionCommands.CommandCreature(_session, CreatureCommandKind.GoTo, new GroundPosition(point.X, point.Z)));
        _hand.Press(point.ToGodot());
    }

    /// <summary>Shows the next map overlay, or hides it after the last one.</summary>
    public void CycleOverlay()
    {
        Overlay = OverlayMap.Next(Overlay);
        _shownOverlay = null;
        _setStatus(Overlay is { } kind ? $"{kind} overlay" : "Overlay off");
    }

    /// <summary>Forgets the old world's alerts and effects after a new or loaded world.</summary>
    public void Reset()
    {
        _feed.Clear();
        _shownAt.Clear();
        _shownOverlay = null;
        _session.DrainEvents();
    }

    /// <summary>Handles new events, then refreshes the HUD, the targeting ring and the overlay.</summary>
    /// <param name="delta">Real frame time in seconds.</param>
    /// <param name="cursor">Ground point under the cursor, if any.</param>
    /// <param name="cameraDistance">Camera distance in metres, for the hand's size.</param>
    public void Process(double delta, NVector3? cursor, float cameraDistance)
    {
        _clock += delta;
        _hand.Follow(cursor?.ToGodot(), cameraDistance);
        HandleEvents();
        var target = cursor is { } point ? new GroundPosition(point.X, point.Z) : (GroundPosition?)null;
        var hud = HudReadout.Build(_session, SelectedMiracle, target);
        _hud.ShowModel(hud);
        if (SelectedMiracle is not null && cursor is { } ground)
        {
            _presenter.ShowTarget(ground, hud.TargetRadius, hud.TargetValid);
        }
        else
        {
            _presenter.HideTarget();
        }

        ShowAlerts();
        UpdateOverlay();
    }

    /// <summary>Plays the effects of new miracles and turns events into alerts.</summary>
    private void HandleEvents()
    {
        var events = _session.DrainEvents();
        if (events.Count == 0)
        {
            return;
        }

        foreach (var worldEvent in events)
        {
            if (worldEvent is MiracleCast cast && _session.Content.Miracles.Find(cast.MiracleId) is { } miracle)
            {
                _presenter.Play(cast, miracle.Radius);
            }
        }

        foreach (var alert in _feed.Push(events, _session.World, _session.LocalGodId, _session.SimulationSeconds))
        {
            _shownAt[alert] = _clock;
        }
    }

    /// <summary>Shows the recent alerts that are still fresh.</summary>
    private void ShowAlerts()
    {
        var fresh = new List<Alert>();
        var kept = new HashSet<Alert>(_feed.Recent);
        foreach (var alert in new List<Alert>(_shownAt.Keys))
        {
            if (!kept.Contains(alert))
            {
                _shownAt.Remove(alert);
            }
        }

        foreach (var alert in _feed.Recent)
        {
            if (_shownAt.TryGetValue(alert, out var shown) && _clock - shown < AlertSeconds)
            {
                fresh.Add(alert);
            }
        }

        _hud.ShowAlerts(fresh);
    }

    /// <summary>Rebuilds the overlay when it changed or a belief step has passed.</summary>
    private void UpdateOverlay()
    {
        var world = _session.World;
        var step = world.Tick / _session.Content.DivineRules.StepIntervalTicks;
        (OverlayKind, long, WorldState)? wanted = Overlay is { } kind ? (kind, step, world) : null;
        if (wanted == _shownOverlay)
        {
            return;
        }

        _shownOverlay = wanted;
        var image = Overlay is { } shown && _session.LocalGodId is { } god
            ? OverlayMap.Build(world, shown, god, _session.Content.DivineRules, _session.Content.SettlementRules, _view, settlement => _session.MigrationPressureOn(settlement).Value)
            : null;
        _presenter.ShowOverlay(image);
        _hud.ShowOverlayLegend(image?.Legend);
    }
}
