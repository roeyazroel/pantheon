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
        _hud.CreatureFightClicked += BeginFight;
        _hud.CreatureWorkClicked += BeginWork;
    }

    /// <summary>Whether the next click on the ground sends the Creature there.</summary>
    public bool SendMode { get; private set; }

    /// <summary>The selected miracle, if any.</summary>
    public string? SelectedMiracle { get; private set; }

    /// <summary>Whether a miracle is chosen or an order waits for its click, so Esc cancels it rather than opening the menu (ADR-068).</summary>
    public bool Aiming => SelectedMiracle is not null || SendMode || FightMode || WorkMode;

    /// <summary>The map overlay shown, if any.</summary>
    public OverlayKind? Overlay { get; private set; }

    /// <summary>Selects the miracle on a number key, or deselects it when it is already selected.</summary>
    /// <param name="slot">Zero-based slot of the miracle bar.</param>
    public void SelectSlot(int slot)
    {
        var miracles = HudReadout.AvailableMiracles(_session);
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
        FightMode = false;
        WorkMode = false;
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

        if (FightMode)
        {
            FightAt(cursor);
            return true;
        }

        if (WorkMode)
        {
            WorkAt(cursor);
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
            _creatures.Flash(creature.Id, new Color(1f, 0.85f, 0.4f), Pantheon.Domain.Creatures.CreatureFeedback.Rewarded);
        }
        else if (status.StartsWith("You punish", StringComparison.Ordinal))
        {
            _hand.Swat(shown.Position, shown.HeadHeight);
            _creatures.Flash(creature.Id, new Color(1f, 0.15f, 0.1f), Pantheon.Domain.Creatures.CreatureFeedback.Punished);
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

    /// <summary>Raised when the player's order to work is accepted, for the tutorial (ADR-064).</summary>
    public event Action? WorkOrdered;

    /// <summary>Whether the next click sets the Creature to work at the village or forest there (ADR-063).</summary>
    public bool WorkMode { get; private set; }

    /// <summary>The next click on a village or a forest sets the Creature to work there (ADR-063).</summary>
    public void BeginWork()
    {
        SelectedMiracle = null;
        (SendMode, FightMode) = (false, false);
        WorkMode = _session.LocalCreature is not null;
        _setStatus(WorkMode ? "Click a village to work there, or a wood to fetch timber (Esc cancels)" : "You have no Creature");
    }

    /// <summary>Sets the Creature to work at the village or forest at a ground point (ADR-063).</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    public void WorkAt(NVector3? cursor)
    {
        WorkMode = false;
        if (cursor is not { } point)
        {
            return;
        }

        var status = SessionCommands.CommandCreature(_session, CreatureCommandKind.Work, new GroundPosition(point.X, point.Z));
        _setStatus(status);
        _hand.Press(point.ToGodot());
        if (status.Contains(" goes to ", StringComparison.Ordinal))
        {
            WorkOrdered?.Invoke();
        }
    }

    /// <summary>Sets the Creature to work for a village, or in the forest richest in timber near it (<c>--work</c>, for captures).</summary>
    /// <param name="village">The village.</param>
    /// <param name="forest">Whether to fetch wood from its forest rather than work in the village.</param>
    public void WorkFor(Pantheon.Domain.Settlements.SettlementState village, bool forest)
    {
        var at = village.Position;
        if (forest && Pantheon.Simulation.Resources.ResourceModel.RichestForest(_session.Content.SettlementRules.Resources, _session.World.Regions, village) is { } stand)
        {
            at = _session.World.Regions.GetRegionCenter(stand.Id);
        }

        WorkAt(new NVector3(at.X, 0f, at.Z));
    }

    /// <summary>Whether the next click on the ground sets the Creature on the rival nearest it (ADR-062).</summary>
    public bool FightMode { get; private set; }

    /// <summary>The next click near a rival Creature sets the player's on it (ADR-062).</summary>
    public void BeginFight()
    {
        SelectedMiracle = null;
        SendMode = false;
        FightMode = _session.LocalCreature is not null;
        _setStatus(FightMode ? "Click near a rival Creature to set yours on it (Esc cancels)" : "You have no Creature");
    }

    /// <summary>Sets the Creature on the rival nearest a ground point, to hunt it down and fight it (ADR-062).</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    public void FightAt(NVector3? cursor)
    {
        FightMode = false;
        if (cursor is not { } point)
        {
            return;
        }

        _setStatus(SessionCommands.CommandCreature(_session, CreatureCommandKind.Fight, new GroundPosition(point.X, point.Z)));
        _hand.Press(point.ToGodot());
    }

    /// <summary>Sets the Creature on the rival nearest it (<c>--fight</c>, for captures).</summary>
    public void FightNearest()
    {
        if (_session.LocalCreature is not { } mine)
        {
            return;
        }

        var rival = System.Linq.Enumerable.MinBy(
            _session.World.Creatures.FindAll(c => c.GodId != mine.GodId && c.Activity.Phase != Pantheon.Domain.Creatures.CreaturePhase.Banished),
            c => ((c.Position.X - mine.Position.X) * (c.Position.X - mine.Position.X)) + ((c.Position.Z - mine.Position.Z) * (c.Position.Z - mine.Position.Z)));
        if (rival is not null)
        {
            FightAt(new NVector3(rival.Position.X, 0f, rival.Position.Z));
        }
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

    /// <summary>Uses the hand at the cursor (R, ADR-030): picks up, sets down, or with Shift throws what it holds.</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public void UseHand(NVector3? cursor, bool shift, bool ctrl = false)
    {
        if (cursor is not { } point)
        {
            _setStatus("Point at the ground to use your hand");
            return;
        }

        var held = _session.LocalGod?.Held;
        var action = Pantheon.Game.Hand.HandCommands.ActionFor(_session, shift, ctrl);
        var status = Pantheon.Game.Hand.HandCommands.Use(_session, action, new GroundPosition(point.X, point.Z));
        _setStatus(status);
        if (action == Pantheon.Domain.Hand.HandAction.Throw && held is not null && _session.LocalGod?.Held is null)
        {
            _hand.Fling(point.ToGodot(), held.Kind);
        }
        else
        {
            _hand.Press(point.ToGodot());
        }
    }

    /// <summary>Where the player's Creature hangs from the hand, while the hand holds it.</summary>
    /// <param name="cursor">Ground point under the cursor.</param>
    /// <returns>The point, or <c>null</c> when the hand does not hold it.</returns>
    public Vector3? HeldCreatureAt(NVector3? cursor)
    {
        // A held Creature hangs by its scruff from the hand's grip (ADR-061), so the hand rises high
        // enough that its feet clear the ground; it swings as the hand moves (ADR-057).
        if (_session.LocalGod?.Held?.Kind != Pantheon.Domain.Hand.HeldKind.Creature || cursor is null || _session.LocalCreature is not { } creature)
        {
            _hand.HoldLift = 0f;
            return null;
        }

        var height = _creatures.Find(creature.Id)?.HeadHeight ?? 12f;
        _hand.HoldLift = height + 3f;
        return _hand.Grip + new Vector3(_hand.Swing.X * height * CreatureSwing, 0f, _hand.Swing.Y * height * CreatureSwing);
    }

    // How far, in metres, a held Creature swings out at the hand's fullest swing (ADR-057).
    // How far a held Creature's feet swing out, as a share of its height for a full swing of the hand.
    private const float CreatureSwing = 0.5f;

    /// <summary>Shows the next map overlay, or hides it after the last one; the debug overlays only with the debug tools on (ADR-068).</summary>
    public void CycleOverlay() => ShowOverlay(OverlayMap.Next(Overlay, DebugTools.On));

    /// <summary>Shows a map overlay, or hides it.</summary>
    /// <param name="kind">The overlay, or <c>null</c> for none.</param>
    public void ShowOverlay(OverlayKind? kind)
    {
        Overlay = kind;
        _shownOverlay = null;
        _setStatus(Overlay is { } shown ? $"{shown} overlay" : "Overlay off");
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
        _hand.Hold(_session.LocalGod?.Held?.Kind);
        HandleEvents();
        var target = cursor is { } point ? new GroundPosition(point.X, point.Z) : (GroundPosition?)null;
        var hud = HudReadout.Build(_session, SelectedMiracle, target);
        _hud.ShowModel(hud);
        if (SelectedMiracle is not null && cursor is { } ground)
        {
            _presenter.ShowTarget(ground, hud.TargetRadius, hud.TargetValid);
        }
        else if (FightMode && cursor is { } aim)
        {
            // The ring shows how near a rival must be to the click; it is valid when one is (ADR-062).
            var reach = _session.Content.Creatures.Bonds.HuntReach;
            var rival = _session.World.Creatures.Exists(c => c.GodId != _session.LocalGodId && c.Activity.Phase != Pantheon.Domain.Creatures.CreaturePhase.Banished
                && ((c.Position.X - aim.X) * (c.Position.X - aim.X)) + ((c.Position.Z - aim.Z) * (c.Position.Z - aim.Z)) <= reach * reach);
            _presenter.ShowTarget(aim, reach, rival);
        }
        else
        {
            _presenter.HideTarget();
        }

        ShowAlerts();
        UpdateOverlay();
    }

    /// <summary>Raised with the events handled this frame, for the game flow (the guide, ADR-020).</summary>
    public event Action<IReadOnlyList<WorldEvent>>? EventsHandled;

    /// <summary>Plays the effects of new miracles and turns events into alerts.</summary>
    private void HandleEvents()
    {
        var events = _session.DrainEvents();
        if (events.Count == 0)
        {
            return;
        }

        EventsHandled?.Invoke(events);

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
