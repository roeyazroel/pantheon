using System;
using Godot;
using Pantheon.Client.Presentation.People;
using Pantheon.Game.Population;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game.Migration;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Migration;

/// <summary>
/// One migration group on screen (spec §82, ADR-016): a few walking figures in its god's colour,
/// strung out along the way, standing for everyone in the group, and a label saying who they are.
/// Presentation only: it follows the simulated position smoothly.
/// </summary>
internal sealed class MigrationView
{
    private const float FollowRate = 3f;
    private const float Spacing = 3.2f;
    private const int PeoplePerFigure = 3;
    private const int MinFigures = 3;
    private const int MaxFigures = 24;
    private const float LabelHeight = 6f;

    // Labels fade out over LabelFadeMargin metres around LabelHideDistance from the camera.
    private const float LabelHideDistance = 1100f;
    private const float LabelFadeMargin = 200f;

    // The walkers are drawn a little larger than life, as the crowds are, so they read from afar.
    private const float FigureScale = 1.3f;

    private readonly Node3D _root;
    private readonly MultiMesh _figures;
    private readonly PersonAssets _people;
    private readonly WorldSeed _seed;
    private readonly int _groupId;
    private float _strides;
    private readonly DeclutteredLabel _label;
    private Vector3 _shown;
    private float _heading;
    private float _clock;
    private bool _placed;

    /// <summary>Creates the group's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the groups.</param>
    /// <param name="groupId">The group's id.</param>
    /// <param name="color">Its main god's colour.</param>
    /// <param name="people">The person figures.</param>
    /// <param name="seed">The world seed, for the walkers' looks.</param>
    public MigrationView(Node3D parent, int groupId, Color color, PersonAssets people, WorldSeed seed)
    {
        (_people, _seed, _groupId) = (people, seed, groupId);
        _root = new Node3D { Name = "Group_" + groupId };
        parent.AddChild(_root);
        _figures = people.NewMultiMesh();
        _root.AddChild(new MultiMeshInstance3D { Name = "People", Multimesh = _figures });
        _label = new DeclutteredLabel
        {
            // A group's label gives way to every village's (ADR-027).
            Priority = 0f,
            Name = "Label",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f,
            FontSize = 22,
            OutlineSize = 8,
            Modulate = color.Lightened(0.4f),
            OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Position = new Vector3(0f, LabelHeight, 0f),
            VisibilityRangeEnd = LabelHideDistance,
            VisibilityRangeEndMargin = LabelFadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _root.AddChild(_label);
        Color = color;
    }

    /// <summary>The figures' colour.</summary>
    public Color Color { get; }

    /// <summary>Moves the group and its walkers for this frame.</summary>
    /// <param name="group">Its state.</param>
    /// <param name="world">The world, for the label's names.</param>
    /// <param name="terrain">Terrain, for the ground height.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="running">Whether the simulation clock is running; the walkers stand still while paused.</param>
    public void Sync(MigrationGroup group, WorldState world, TerrainField terrain, float delta, bool running)
    {
        var target = Ground(terrain, group.Position.X, group.Position.Z);
        var before = _shown;
        _shown = _placed ? _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta)) : target;
        _strides += _placed ? new Vector2(_shown.X - before.X, _shown.Z - before.Z).Length() / (2f * _people.StrideLength * FigureScale) : 0f;
        _placed = true;
        var next = group.Path.Count > 0 ? group.Path[0] : group.Position;
        var (dx, dz) = (next.X - _shown.X, next.Z - _shown.Z);
        if ((dx * dx) + (dz * dz) > 1f)
        {
            _heading = Mathf.LerpAngle(_heading, MathF.Atan2(dx, dz), 1f - Mathf.Exp(-4f * delta));
        }

        if (running)
        {
            _clock += delta;
        }

        _root.Position = _shown;
        Walk(terrain, Math.Clamp(group.Population / PeoplePerFigure, MinFigures, MaxFigures));
        var text = MigrationText.Group(group, world);
        _label.SetTexts(text, text.Split('\n')[0]);
    }

    /// <summary>Frees the group's nodes.</summary>
    public void Free() => _root.QueueFree();

    /// <summary>Places the walkers two abreast behind the group's head, each in step with the ground covered, out of step with the others.</summary>
    private void Walk(TerrainField terrain, int count)
    {
        if (_figures.InstanceCount != count)
        {
            _figures.InstanceCount = count;
            for (var i = 0; i < count; i++)
            {
                _figures.SetInstanceColor(i, PersonAssets.InstanceColor(Look(i)));
            }
        }

        var back = new Vector3(-MathF.Sin(_heading), 0f, -MathF.Cos(_heading));
        var side = new Vector3(back.Z, 0f, -back.X);
        var moving = _strides != _lastStrides;
        _lastStrides = _strides;
        for (var i = 0; i < count; i++)
        {
            var row = i / 2;
            var offset = (back * (row * Spacing)) + (side * (((i % 2) - 0.5f) * Spacing * 0.8f));
            var world = _shown + offset;
            var ground = Mathf.Max(terrain.SampleHeight(world.X, world.Z), 0f) - _shown.Y;
            var look = Look(i);
            var basis = new Basis(Vector3.Up, _heading).Scaled(Vector3.One * FigureScale * look.Height);
            _figures.SetInstanceTransform(i, new Transform3D(basis, new Vector3(offset.X, ground, offset.Z)));
            var motion = moving ? PersonMotion.Walking : PersonMotion.Standing;
            _figures.SetInstanceCustomData(i, PersonAssets.InstanceCustom(motion, moving ? _strides + (i * 0.37f) : _clock * 0.25f, 0f, look));
        }
    }

    private float _lastStrides;

    /// <summary>A walker's look: the people of a group look as varied as a village's.</summary>
    private PersonLook Look(int index) => PersonLooks.Of(_people.Settings, _seed.Value, -1_000_000 - _groupId, index, CitizenRole.Villager);

    /// <summary>The ground (or sea surface) point under a position.</summary>
    private static Vector3 Ground(TerrainField terrain, float x, float z) => new(x, Mathf.Max(terrain.SampleHeight(x, z), 0f), z);
}
