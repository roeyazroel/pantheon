using System;
using Godot;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Presentation.Animals;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>
/// One Creature on screen (presentation only, ADR-010, ADR-061): an upright animal of its species.
/// It is drawn between the simulation's last two places by how far the clock has run towards the next
/// tick, so it moves at an even pace rather than in tenths of a second. Whatever it does, each foot is
/// planted on the ground under it and laid along its slope, the hips dropping or rising where a leg
/// cannot reach (ADR-069); the stride follows its speed, it leans gently into the slope, runs when it
/// flees, and poses for what it does: sitting to rest, working with its hands, boxing in a fight,
/// raking in an attack, hopping in play, and dangling from its god's hand. Its face shows how it feels;
/// a label above it says what it is doing, with its health while it fights.
/// </summary>
internal sealed class CreatureView
{
    // How fast the pose eases towards what the Creature does, and its heading towards the simulation's, a second.
    private const float PoseRate = 9f;
    private const float TurnRate = 7f;

    // Body units a second below which it stands rather than walks.
    private const float StillSpeed = 0.25f;

    // Half a foot's length, in body units: the ground is sampled this far behind and ahead of the ankle for its slope.
    private const float FootSpan = 0.35f;

    // A jump in the simulation's place, in metres, that is drawn as one: the hand set it down, or a fight drew it in.
    private const float JumpDistance = 25f;

    // Labels fade out over LabelFadeMargin metres around LabelHideDistance from the camera: the gods'
    // Creatures crowd the strategic view, where their bodies are too small to see anyway.
    private const float LabelHideDistance = 900f;
    private const float LabelFadeMargin = 200f;

    // How long the face shows the god's word after a reward or punishment, in seconds.
    private const float JudgedSeconds = 4f;

    // How long a defeated Creature lies fallen before it is banished from sight, in seconds.
    private const float FallSeconds = 2.5f;

    private readonly Node3D _root;
    private readonly Node3D _frame;
    private readonly FightEffects _fight;
    private readonly DeclutteredLabel _label;
    private readonly Label3D _speech;
    private readonly Color _godColor;
    private readonly float _glow;
    private readonly float _offset;
    private readonly System.Threading.Tasks.Task<AnimalMeshes> _meshes;
    private AnimalBody? _body;
    private BodyPose? _pose;
    private Vector3 _from;
    private Vector3 _to;
    private Vector3 _shown;
    private long _tick = -1;
    private float _heading;
    private float _phase;
    private float _clock;
    private float _flash;
    private Color _flashColor;
    private bool _placed;
    private string _owner;
    private float _scale = 1f;
    private CreatureFeedback _judged;
    private float _judgedSeconds;
    private float _fallen = float.MaxValue;
    private float _hipShift;
    private float _slopeLean;
    private float _speaking;

    /// <summary>Creates the Creature's nodes under a parent; its body appears once its species' meshes are made.</summary>
    /// <param name="parent">Node that holds the Creatures.</param>
    /// <param name="creatureId">The Creature's id.</param>
    /// <param name="godColor">Its god's colour.</param>
    /// <param name="owner">Its god's name, shown for rivals' Creatures; empty for the player's.</param>
    /// <param name="look">Its species' look (ADR-021, ADR-061).</param>
    public CreatureView(Node3D parent, int creatureId, Color godColor, string owner, Pantheon.Game.Presentation.CreatureLook look)
    {
        _owner = owner;
        _godColor = godColor;
        _glow = look.Glow;
        _offset = creatureId * 0.37f;
        _meshes = AnimalMeshes.For(look);
        _root = new Node3D { Name = "Creature_" + creatureId };
        parent.AddChild(_root);
        _frame = new Node3D { Name = "Frame" };
        _root.AddChild(_frame);
        _label = new DeclutteredLabel
        {
            // A Creature's label gives way to every village's (ADR-027).
            Priority = 0f,
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
        // What the Creature says (ADR-072), above its name, always in view while it lasts.
        _speech = new Label3D
        {
            Name = "Speech",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f,
            FontSize = 30,
            OutlineSize = 12,
            Modulate = new Color(1f, 0.97f, 0.88f),
            OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.9f),
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Offset = new Vector2(0f, 80f),
            Width = 900f,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Layers = DeclutteredLabel.Layer,
            VisibilityRangeEnd = LabelHideDistance,
            Visible = false,
        };
        _root.AddChild(_speech);
        _fight = new FightEffects(_root, parent);
    }

    /// <summary>Shows what the Creature says above it for a while (ADR-072).</summary>
    /// <param name="text">Its words.</param>
    /// <param name="seconds">How long they stay.</param>
    public void Say(string text, float seconds)
    {
        _speech.Text = "“" + text.Trim('“', '”', '"') + "”";
        _speaking = seconds;
    }

    /// <summary>Where the Creature is drawn, for the camera and the hand.</summary>
    public Vector3 ShownPosition => _shown;

    /// <summary>Height of the Creature's head above its feet at its current size, in metres.</summary>
    public float HeadHeight => (_body?.StandingHeight ?? 7f) * _scale;

    /// <summary>
    /// Flashes the Creature's colour briefly, for a reward (warm) or a punishment (red), and its face
    /// shows the god's word for a few seconds, even for a deed it has not finished (ADR-055).
    /// </summary>
    /// <param name="color">Flash colour.</param>
    /// <param name="feedback">The reward or punishment.</param>
    public void Flash(Color color, CreatureFeedback feedback)
    {
        _flash = 1f;
        _flashColor = color;
        (_judged, _judgedSeconds) = (feedback, JudgedSeconds);
    }

    /// <summary>Moves and poses the Creature for this frame.</summary>
    /// <param name="creature">Its state.</param>
    /// <param name="world">The world, for the label.</param>
    /// <param name="terrain">Terrain, for the ground under it and under each foot.</param>
    /// <param name="frame">Frame time in seconds, whether the simulation runs, and how far its clock has run towards the next tick (0..1).</param>
    /// <param name="heldAt">Where its god's hand holds it, if it does (ADR-030).</param>
    public void Sync(CreatureState creature, WorldState world, TerrainField terrain, (float Delta, bool Running, float Alpha) frame, Vector3? heldAt = null)
    {
        // A defeated Creature falls and lies a moment before it is banished from sight (ADR-062).
        var banished = creature.Activity.Phase == CreaturePhase.Banished;
        if (banished && _placed && _fallen == float.MaxValue)
        {
            _fallen = 0f;
            _fight.Fall(_shown);
        }

        _fallen = banished ? _fallen + frame.Delta : float.MaxValue;
        _root.Visible = !banished || _fallen < FallSeconds;
        if (banished)
        {
            if (_fallen < FallSeconds && _body is { } fallen)
            {
                _pose = _pose is null ? AnimalMotion.Fallen(fallen.Plan.Body, _fallen) : BodyPose.Lerp(_pose, AnimalMotion.Fallen(fallen.Plan.Body, _fallen), 1f - Mathf.Exp(-8f * frame.Delta));
                fallen.Apply(_pose);
                _label.SetTexts($"{creature.Name}\nDefeated", creature.Name);
            }
            else
            {
                _placed = false;
            }

            return;
        }

        if (_body is null && _meshes.IsCompletedSuccessfully)
        {
            _body = new AnimalBody(_frame, _meshes.Result, _godColor, _glow);
        }

        var delta = frame.Delta;
        var before = _shown;
        Follow(creature, world.Tick, terrain, heldAt, frame.Alpha, delta);
        if (frame.Running)
        {
            _clock += delta;
        }

        _scale = 1.8f + (0.2f * (creature.Level - 1));
        _root.Position = _shown;
        _root.Rotation = new Vector3(0f, _heading, 0f);
        _root.Scale = new Vector3(_scale, _scale, _scale);
        _label.Scale = new Vector3(1f / _scale, 1f / _scale, 1f / _scale);
        if (_body is { } body)
        {
            var moved = new Vector2(_shown.X - before.X, _shown.Z - before.Z).Length() / _scale;
            Animate(body, creature, terrain, heldAt is not null, moved, delta);
            body.Align(creature.Morality);
            _judgedSeconds = Mathf.Max(0f, _judgedSeconds - delta);
            body.Express(Pantheon.Game.Presentation.CreatureExpression.FaceOf(creature, world.Tick, _judgedSeconds > 0f ? _judged : null), _clock);
            var uprooting = creature.Activity.Action == CreatureAction.GatherWood && creature.Activity.Phase == CreaturePhase.Performing && heldAt is null;
            body.ShowTree(uprooting ? Mathf.Max(0f, Mathf.Sin(_clock * 2.6f)) : null);
            UpdateFlash(body, delta);
            body.ShowScars(creature.Scars);
            _fight.Update(creature, world, _shown, body.StandingHeight, _scale, (_clock, _offset), delta);
        }

        var title = _owner.Length > 0 ? $"{creature.Name} ({_owner})" : creature.Name;
        var fighting = creature.Activity.Action == CreatureAction.Fight && creature.Activity.Phase == CreaturePhase.Performing;
        var health = fighting ? $"  {creature.Health:0%}" : string.Empty;
        _label.SetTexts($"{title}\n{CreatureText.DescribeActivity(creature, world)}{health}", title);
        // While it fights, its health bar sits over its head and the label rises above the bar.
        _label.Position = new Vector3(0f, (_body?.StandingHeight ?? 7f) * (Resting(creature) ? 0.55f : fighting ? 1.22f : 1.05f), 0f);
        _speaking = Mathf.Max(0f, _speaking - delta);
        _speech.Visible = _speaking > 0f;
        _speech.Position = _label.Position;
        _speech.Scale = _label.Scale;
        _speech.Modulate = _speech.Modulate with { A = Mathf.Clamp(_speaking / 0.5f, 0f, 1f) };
        _speech.OutlineModulate = _speech.OutlineModulate with { A = 0.9f * Mathf.Clamp(_speaking / 0.5f, 0f, 1f) };
    }

    /// <summary>Frees the Creature's nodes.</summary>
    public void Free()
    {
        _fight.Free();
        _root.QueueFree();
    }

    /// <summary>
    /// Draws the Creature between the simulation's last two places: when a tick brings a new one, the
    /// old becomes where it is drawn now, so it never jumps back; a great jump is drawn at once.
    /// </summary>
    private void Follow(CreatureState creature, long tick, TerrainField terrain, Vector3? heldAt, float alpha, float delta)
    {
        if (heldAt is { } grip)
        {
            // It hangs by its scruff, just above its shoulders, from the hand's grip.
            var held = grip - new Vector3(0f, Scruff * _scale, 0f);
            var hold = 1f - Mathf.Exp(-14f * delta);
            _shown = _placed ? _shown.Lerp(held, hold) : held;
            (_from, _to, _placed) = (_shown, _shown, true);
            _heading = Mathf.LerpAngle(_heading, creature.Heading, 1f - Mathf.Exp(-TurnRate * delta));
            return;
        }

        var target = Ground(terrain, creature.Position.X, creature.Position.Z);
        if (!_placed || target.DistanceTo(_to) > JumpDistance)
        {
            (_from, _to, _shown, _heading, _placed) = (target, target, target, creature.Heading, true);
        }
        else if (tick != _tick)
        {
            (_from, _to) = (_shown, target);
        }

        _tick = tick;
        var at = _from.Lerp(_to, Mathf.Clamp(alpha, 0f, 1f));
        _shown = new Vector3(at.X, Ground(terrain, at.X, at.Z).Y, at.Z);
        _heading = Mathf.LerpAngle(_heading, creature.Heading, 1f - Mathf.Exp(-TurnRate * delta));
    }

    /// <summary>Chooses the pose for what the Creature does, eases towards it, and leans with the slope.</summary>
    private void Animate(AnimalBody body, CreatureState creature, TerrainField terrain, bool held, float moved, float delta)
    {
        var plan = body.Plan.Body;
        var speed = moved / Mathf.Max(delta, 1e-4f);
        var fleeing = creature.Activity.Action == CreatureAction.Flee;
        var pace = fleeing ? 1f : Mathf.Clamp((speed - 3f) / 6f, 0f, 0.6f);
        BodyPose target;
        if (held)
        {
            target = AnimalMotion.Dangle(plan, _clock);
        }
        else if (speed > StillSpeed)
        {
            _phase += moved / AnimalMotion.Stride(plan, pace);
            target = AnimalMotion.Walk(plan, _phase, pace);
        }
        else
        {
            target = Still(plan, creature);
        }

        // Walking follows the stride closely, so planted feet stay planted; other poses ease in.
        var rate = speed > StillSpeed && !held ? PoseRate * 2.5f : PoseRate;
        _pose = _pose is null ? target : BodyPose.Lerp(_pose, target, 1f - Mathf.Exp(-rate * delta));
        Lean(terrain, plan, held, delta);
        body.Apply(held ? _pose : OnGround(terrain, plan, _pose, Resting(creature), delta));
    }

    /// <summary>
    /// The pose with its feet on the ground under each (ADR-069), leaning a little into the slope. The hips
    /// rise at once when a foot needs room, so it never sinks, and drop smoothly. A sitting Creature
    /// rises over the higher ground under its legs.
    /// </summary>
    private BodyPose OnGround(TerrainField terrain, AnimalProportions plan, BodyPose pose, bool sitting, float delta)
    {
        var left = GroundUnder(terrain, plan, pose.LeftLeg, 1f);
        var right = GroundUnder(terrain, plan, pose.RightLeg, -1f);
        var leaning = pose with { Lean = pose.Lean + _slopeLean };
        var wanted = sitting ? Mathf.Max(0f, Mathf.Max(left.Height, right.Height)) : AnimalFooting.HipShift(pose, plan, left, right);
        _hipShift = wanted > _hipShift ? wanted : Mathf.Lerp(_hipShift, wanted, 1f - Mathf.Exp(-10f * delta));
        return sitting ? leaning with { HipHeight = leaning.HipHeight + _hipShift } : AnimalFooting.Plant(leaning, plan, left, right, _hipShift);
    }

    /// <summary>The pose for what the Creature does while it stands in one place.</summary>
    private BodyPose Still(AnimalProportions plan, CreatureState creature)
    {
        if (creature.Activity.Phase != CreaturePhase.Performing)
        {
            return AnimalMotion.Stand(plan, _clock);
        }

        return creature.Activity.Action switch
        {
            CreatureAction.Rest => AnimalMotion.Sit(plan, _clock),
            CreatureAction.Feed or CreatureAction.HelpBuild or CreatureAction.Forage or CreatureAction.EatFromStores => AnimalMotion.Work(plan, _clock),
            CreatureAction.Farm => AnimalMotion.Farm(plan, _clock),
            CreatureAction.GatherWood => AnimalMotion.Uproot(plan, _clock),
            CreatureAction.Fight => AnimalMotion.Fight(plan, _clock, _offset),
            CreatureAction.Attack => AnimalMotion.Attack(plan, _clock),
            CreatureAction.Play or CreatureAction.Befriend => AnimalMotion.Play(plan, _clock, _offset),
            CreatureAction.Wander or CreatureAction.GoTo or CreatureAction.Ignore or CreatureAction.Flee => AnimalMotion.Stand(plan, _clock) with { HeadYaw = Mathf.Sin(_clock * 0.7f) * 0.5f },
        };
    }

    /// <summary>The ground under a foot, from where its leg puts it: its height above the ground under the body and its rise ahead, in body units.</summary>
    private FootGround GroundUnder(TerrainField terrain, AnimalProportions plan, LimbAngles leg, float side)
    {
        var (sin, cos) = Mathf.SinCos(_heading);
        var (reach, _) = AnimalFooting.Ankle(plan, leg);
        float At(float forward)
        {
            var x = _shown.X + (cos * side * plan.HipWidth * _scale) + (sin * forward * _scale);
            var z = _shown.Z - (sin * side * plan.HipWidth * _scale) + (cos * forward * _scale);
            return (Ground(terrain, x, z).Y - _shown.Y) / _scale;
        }

        var (behind, ahead) = (At(reach - FootSpan), At(reach + FootSpan));
        return new FootGround(At(reach), Mathf.Atan2(ahead - behind, 2f * FootSpan));
    }

    /// <summary>Leans the torso a little into the slope ahead (ADR-069); the legs stay upright and find the ground themselves.</summary>
    private void Lean(TerrainField terrain, AnimalProportions plan, bool held, float delta)
    {
        var target = 0f;
        if (!held)
        {
            var (sin, cos) = Mathf.SinCos(_heading);
            var reach = (plan.Thigh + plan.Shin) * 0.6f * _scale;
            var front = Ground(terrain, _shown.X + (sin * reach), _shown.Z + (cos * reach)).Y;
            var back = Ground(terrain, _shown.X - (sin * reach), _shown.Z - (cos * reach)).Y;
            target = Mathf.Atan2(front - back, 2f * reach) * 0.35f;
        }

        _slopeLean = Mathf.Lerp(_slopeLean, target, 1f - Mathf.Exp(-6f * delta));
    }

    /// <summary>How high its scruff lies above its feet, in body units, as it dangles.</summary>
    private float Scruff => _body is { } body ? body.Plan.Body.HipHeight + (body.Plan.Body.Torso * 1.02f) : 6f;

    /// <summary>Whether the Creature sits to rest.</summary>
    private static bool Resting(CreatureState creature) => creature.Activity.Action == CreatureAction.Rest && creature.Activity.Phase == CreaturePhase.Performing;

    /// <summary>Fades a reward or punishment flash out of the body's emission.</summary>
    private void UpdateFlash(AnimalBody body, float delta)
    {
        if (_flash <= 0f)
        {
            return;
        }

        _flash = Mathf.Max(0f, _flash - (delta * 1.5f));
        foreach (var material in body.Materials)
        {
            material.EmissionEnabled = true;
            material.Emission = _flashColor;
            material.EmissionEnergyMultiplier = _flash * 1.5f;
        }

        if (_flash <= 0f)
        {
            body.RestoreGlow();
        }
    }

    /// <summary>The ground (or sea surface) point under a place.</summary>
    private static Vector3 Ground(TerrainField terrain, float x, float z) => new(x, Mathf.Max(terrain.SampleHeight(x, z), 0f), z);
}
