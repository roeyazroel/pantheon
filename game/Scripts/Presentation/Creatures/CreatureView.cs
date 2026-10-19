using System;
using Godot;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// One Creature on screen (presentation only, ADR-010): it follows the simulated position and
/// heading smoothly, grows with its level, walks with a stride that matches its speed, and poses
/// for what it is doing: lying down to rest, working with its arms, hopping in play, lunging in
/// an attack, trading blows in a fight (ADR-017). Its scars show on its chest. A label above it
/// says what it is doing, with its health while it fights.
/// </summary>
internal sealed class CreatureView
{
    private const float FollowRate = 10f;
    private const float StrideLength = 3.2f;
    private const float LabelHeight = 9.5f;

    // Labels fade out over LabelFadeMargin metres around LabelHideDistance from the camera: four
    // gods' Creatures crowd the strategic view, where their bodies are too small to see anyway.
    private const float LabelHideDistance = 900f;
    private const float LabelFadeMargin = 200f;

    private readonly Node3D _root;
    private readonly Node3D _pose;
    private readonly CreatureBody _body;
    private readonly Label3D _label;
    private Vector3 _shown;
    private float _heading;
    private float _stride;
    private float _clock;
    private float _flash;
    private Color _flashColor;
    private readonly float _phase;
    private bool _placed;
    private string _owner = string.Empty;
    private float _height = 1f;

    /// <summary>Creates the Creature's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the Creatures.</param>
    /// <param name="creatureId">The Creature's id.</param>
    /// <param name="godColor">Its god's colour.</param>
    /// <param name="owner">Its god's name, shown for rivals' Creatures; empty for the player's.</param>
    /// <param name="look">Its species' look (ADR-021).</param>
    public CreatureView(Node3D parent, int creatureId, Color godColor, string owner, Pantheon.Game.Presentation.CreatureLook look)
    {
        _height = look.Height;
        _owner = owner;
        _phase = creatureId * 1.7f;
        _root = new Node3D { Name = "Creature_" + creatureId };
        parent.AddChild(_root);
        _pose = new Node3D { Name = "Pose" };
        _root.AddChild(_pose);
        _body = new CreatureBody(_pose, godColor, look);
        _label = new Label3D
        {
            Name = "Label",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f,
            FontSize = 26,
            OutlineSize = 9,
            Modulate = godColor.Lightened(0.35f),
            OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            VisibilityRangeEnd = LabelHideDistance,
            VisibilityRangeEndMargin = LabelFadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _root.AddChild(_label);
    }

    /// <summary>Where the Creature is drawn, for the camera and the hand.</summary>
    public Vector3 ShownPosition => _shown;

    /// <summary>Height of the Creature's head above its feet at its current size.</summary>
    public float HeadHeight => 6.8f * _height * _root.Scale.Y;

    /// <summary>Flashes the Creature's colour briefly, for a reward (warm) or a punishment (red).</summary>
    /// <param name="color">Flash colour.</param>
    public void Flash(Color color)
    {
        _flash = 1f;
        _flashColor = color;
    }

    /// <summary>Moves and poses the Creature for this frame.</summary>
    /// <param name="creature">Its state.</param>
    /// <param name="world">The world, for the label.</param>
    /// <param name="terrain">Terrain, for the ground height.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="simulationRunning">Whether the simulation clock is running; poses freeze while paused.</param>
    public void Sync(CreatureState creature, WorldState world, TerrainField terrain, float delta, bool simulationRunning)
    {
        var banished = creature.Activity.Phase == CreaturePhase.Banished;
        _root.Visible = !banished;
        if (banished)
        {
            _placed = false;
            return;
        }

        var target = Ground(terrain, creature.Position);
        var blend = _placed ? 1f - Mathf.Exp(-FollowRate * delta) : 1f;
        var before = _shown;
        _shown = _placed ? _shown.Lerp(target, blend) : target;
        _heading = _placed ? Mathf.LerpAngle(_heading, creature.Heading, blend) : creature.Heading;
        _placed = true;
        if (simulationRunning)
        {
            _clock += delta;
        }

        var moved = new Vector2(_shown.X - before.X, _shown.Z - before.Z).Length();
        _stride += moved / StrideLength;
        var scale = 1.8f + (0.2f * (creature.Level - 1));
        _root.Position = _shown;
        _root.Rotation = new Vector3(0f, _heading, 0f);
        _root.Scale = new Vector3(scale, scale, scale);

        // The label keeps its on-screen size whatever the Creature's size.
        _label.Scale = new Vector3(1f / scale, 1f / scale, 1f / scale);
        Pose(creature.Activity, moved / Math.Max(delta, 1e-3f));
        UpdateFlash(delta);
        _body.ShowScars(creature.Scars);
        var title = _owner.Length > 0 ? $"{creature.Name} ({_owner})" : creature.Name;
        var fighting = creature.Activity.Action == CreatureAction.Fight && creature.Activity.Phase == CreaturePhase.Performing;
        var health = fighting ? $"  {creature.Health:0%}" : string.Empty;
        var text = $"{title}\n{CreatureText.DescribeActivity(creature, world)}{health}";
        if (_label.Text != text)
        {
            _label.Text = text;
        }

        _label.Position = new Vector3(0f, LabelHeight * (creature.Activity.Action == CreatureAction.Rest && creature.Activity.Phase == CreaturePhase.Performing ? 0.5f : 1f), 0f);
    }

    /// <summary>Frees the Creature's nodes.</summary>
    public void Free() => _root.QueueFree();

    /// <summary>Sets the pose for the activity: walking when moving, otherwise the action's pose.</summary>
    private void Pose(CreatureActivity activity, float speed)
    {
        var walking = speed > 0.4f;
        var swing = walking ? Mathf.Sin(_stride * Mathf.Tau) * 0.5f : 0f;
        var bob = walking ? Mathf.Abs(Mathf.Sin(_stride * Mathf.Tau)) * 0.25f : 0f;
        var breathe = Mathf.Sin(_clock * 1.6f) * 0.03f;
        var lean = 0.08f;
        var lift = 0f;
        var lie = 0f;
        var headPitch = 0f;
        var arms = new Vector2(-swing, swing);
        if (!walking && activity.Phase == CreaturePhase.Performing)
        {
            switch (activity.Action)
            {
                case CreatureAction.Rest:
                    lie = 1.35f;
                    lift = -1.1f;
                    breathe *= 3f;
                    break;
                case CreatureAction.Feed:
                case CreatureAction.HelpBuild:
                case CreatureAction.Forage:
                case CreatureAction.EatFromStores:
                    var work = Mathf.Sin(_clock * 5f);
                    arms = new Vector2(-1.1f + (0.6f * work), -1.1f - (0.6f * work));
                    lean = 0.35f;
                    headPitch = 0.35f;
                    break;
                case CreatureAction.Play:
                    lift = Mathf.Abs(Mathf.Sin(_clock * 4f)) * 1.4f;
                    arms = new Vector2(-2.6f, -2.6f);
                    break;
                case CreatureAction.Attack:
                    var lunge = Mathf.Max(0f, Mathf.Sin(_clock * 6f));
                    lean = 0.2f + (0.45f * lunge);
                    arms = new Vector2(-2.4f + lunge, -2.4f + lunge);
                    headPitch = -0.2f;
                    break;
                case CreatureAction.Fight:
                    // Blows trade back and forth: each fighter's swing is offset by its id.
                    var blow = Mathf.Max(0f, Mathf.Sin((_clock * 7f) + _phase));
                    lean = 0.25f + (0.5f * blow);
                    arms = new Vector2(-2.6f + (1.4f * blow), -1.2f - (1.2f * blow));
                    lift = 0.3f * Mathf.Abs(Mathf.Sin(_clock * 3.5f));
                    headPitch = -0.25f;
                    break;
                case CreatureAction.Befriend:
                    lift = Mathf.Abs(Mathf.Sin((_clock * 4f) + _phase)) * 1.2f;
                    arms = new Vector2(-2.4f, -1.4f + Mathf.Sin(_clock * 4f));
                    break;
                case CreatureAction.Flee:
                    lean = 0.3f;
                    breathe *= 4f;
                    break;
                case CreatureAction.GoTo:
                case CreatureAction.Wander:
                case CreatureAction.Ignore:
                    _body.Head.Rotation = new Vector3(0f, Mathf.Sin(_clock * 0.7f) * 0.6f, 0f);
                    break;
            }
        }

        _pose.Rotation = new Vector3(-lie, 0f, 0f);
        _pose.Position = new Vector3(0f, lift + bob, 0f);
        _body.Torso.Rotation = new Vector3(lean + breathe, 0f, 0f);
        _body.Head.Rotation = new Vector3(headPitch, _body.Head.Rotation.Y * 0.9f, 0f);
        _body.LeftArm.Rotation = new Vector3(arms.X, 0f, 0.12f);
        _body.RightArm.Rotation = new Vector3(arms.Y, 0f, -0.12f);
        _body.LeftLeg.Rotation = new Vector3(swing, 0f, 0f);
        _body.RightLeg.Rotation = new Vector3(-swing, 0f, 0f);
    }

    /// <summary>Fades a reward or punishment flash out of the body's emission.</summary>
    private void UpdateFlash(float delta)
    {
        if (_flash <= 0f)
        {
            return;
        }

        _flash = Mathf.Max(0f, _flash - (delta * 1.5f));
        foreach (var material in _body.Materials)
        {
            material.EmissionEnabled = true;
            material.Emission = _flashColor;
            material.EmissionEnergyMultiplier = _flash * 1.5f;
        }
    }

    /// <summary>The ground (or sea surface) point under a position.</summary>
    private static Vector3 Ground(TerrainField terrain, GroundPosition position) =>
        new(position.X, Mathf.Max(terrain.SampleHeight(position.X, position.Z), 0f), position.Z);
}
