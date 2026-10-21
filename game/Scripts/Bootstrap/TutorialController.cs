using System;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Client.UI;
using Pantheon.Client.UI.Flow;
using Pantheon.Game;
using Pantheon.Game.Flow;
using NVector2 = System.Numerics.Vector2;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Runs the tutorial (ADR-064): it shows the tutorial each frame what the player does (the camera,
/// the hand, the time, the diplomacy panel), passes on the world's events and the work the player
/// orders, shows the step's card with a frame around what it speaks of, and raises a beacon of light
/// over the home village when a step points there. Finishing it is remembered in the profile.
/// </summary>
internal sealed partial class TutorialController : Node
{
    /// <summary>The profile's key for a finished tutorial.</summary>
    public const string DoneKey = "tutorial_done";

    private readonly Tutorial _tutorial = new();
    private GameSession _session = null!;
    private GodCameraController _camera = null!;
    private DiplomacyPanel _diplomacy = null!;
    private Hud _hud = null!;
    private Node3D _world = null!;
    private Action<string> _setStatus = null!;
    private TutorialPanel _panel = null!;
    private (Node3D Node, Action<float> Fade)? _beacon;

    /// <summary>Whether the tutorial is under way.</summary>
    public bool IsActive => _tutorial.IsActive;

    /// <summary>Where the tutorial's card is on the screen while it shows, for the angel to stand beside it (ADR-074).</summary>
    public Rect2? CardRect => _panel?.CardRect;

    /// <summary>Who speaks the tutorial: the player's angel by name (ADR-074).</summary>
    public string Speaker
    {
        set
        {
            if (_panel is not null)
            {
                _panel.Speaker = value;
            }
        }
    }

    /// <summary>Connects the tutorial to the game.</summary>
    /// <param name="session">The session.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="divine">The divine controller, for its events and the work ordered.</param>
    /// <param name="ui">The diplomacy panel and the HUD.</param>
    /// <param name="world">The world's node, for the beacon.</param>
    /// <param name="setStatus">Shows a status line.</param>
    public void Setup(GameSession session, GodCameraController camera, DivineController divine, (DiplomacyPanel Diplomacy, Hud Hud) ui, Node3D world, Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(divine);
        (_session, _camera, _diplomacy, _hud, _world, _setStatus) = (session, camera, ui.Diplomacy, ui.Hud, world, setStatus);
        _panel = new TutorialPanel { Name = "TutorialPanel" };
        AddChild(_panel);
        _panel.NextClicked += _tutorial.Next;
        _panel.LeaveClicked += Stop;
        divine.EventsHandled += events => _tutorial.Observe(events, _session.LocalGodId, _session.LocalCreature?.Id);
        divine.WorkOrdered += () => _tutorial.Notice(TutorialSignal.WorkOrdered);
    }

    /// <summary>Starts the tutorial from its first step, or from another (for captures).</summary>
    /// <param name="step">The step to start at.</param>
    public void Start(TutorialStep step = TutorialStep.Welcome) => _tutorial.Start(step);

    /// <summary>Ends the tutorial where it stands; the world plays on.</summary>
    public void Stop()
    {
        _tutorial.Stop();
        _setStatus("You left the tutorial: the island is yours to play");
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }

        var wasActive = _tutorial.IsActive;
        if (wasActive)
        {
            var model = _camera.Model;
            var scheduler = _session.Scheduler;
            _tutorial.See(new TutorialView(
                model.Focus,
                model.Distance,
                model.Yaw,
                _camera.IsFollowing,
                _session.LocalGod?.Held is not null,
                scheduler.IsPaused ? 0.0 : scheduler.TimeScale,
                _diplomacy.PanelVisible));
            if (!_tutorial.IsActive)
            {
                Finish();
            }
        }

        var home = Home();
        var text = _tutorial.Text(_session.LocalGod?.Name ?? "a god", _session.LocalCreature?.Name ?? "your Creature", home?.Name ?? "your village");
        _panel.ShowStep(_tutorial, text, _hud.TargetRect(_tutorial.Target));
        Beacon(_tutorial.MarksHome ? home : null);
    }

    /// <summary>Remembers the finished tutorial in the profile.</summary>
    private void Finish()
    {
        Startup.Profile.WriteValue(DoneKey, "yes");
        _setStatus("Tutorial complete: the island is yours");
    }

    /// <summary>The player's home village: its Creature's home.</summary>
    private Pantheon.Domain.Settlements.SettlementState? Home() =>
        _session.LocalCreature is { } creature ? _session.World.Settlements.Find(s => s.Id == creature.HomeSettlementId) : null;

    /// <summary>A pulsing column of light over a village, or none.</summary>
    private void Beacon(Pantheon.Domain.Settlements.SettlementState? village)
    {
        if (village is null)
        {
            if (_beacon is { } shown)
            {
                shown.Node.Visible = false;
            }

            return;
        }

        _beacon ??= MiracleLayers.Pillar(UiTheme.Gold, 9f, 160f);
        var (node, fade) = _beacon.Value;
        if (node.GetParent() is null)
        {
            _world.AddChild(node);
        }

        var ground = Mathf.Max(_session.Terrain.SampleHeight(village.Position.X, village.Position.Z), 0f);
        node.Position = new Vector3(village.Position.X, ground + 80f, village.Position.Z);
        node.Visible = true;
        fade(0.55f + (0.45f * Mathf.Sin((float)Time.GetTicksMsec() / 400f)));
    }
}
