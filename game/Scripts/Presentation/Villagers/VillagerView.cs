using System;
using Godot;
using Pantheon.Client.Presentation.People;
using Pantheon.Game.Population;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game.Villagers;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Villagers;

/// <summary>
/// One followed villager on screen (spec §80, ADR-043, ADR-049): the villager as a person, dressed
/// as in the crowd and a little larger, under a marker in their follower's colour, with a label of
/// who they are and what they do. They walk, hoe, hammer or do chores, pray and sit to rest.
/// Presentation only: it follows the simulated position smoothly.
/// </summary>
internal sealed class VillagerView
{
    private const float FollowRate = 4f;
    private const float Scale = 1.3f;
    private const float MarkerHeight = 4.2f;
    private const float LabelHeight = 6f;
    private const float FigureHideDistance = 900f;
    private const float LabelHideDistance = 700f;
    private const float LabelFadeMargin = 120f;

    // Below this speed, in metres a second, the villager stands rather than walks.
    private const float WalkingSpeed = 0.25f;

    private static readonly CylinderMesh Marker = new() { TopRadius = 0.45f, BottomRadius = 0f, Height = 0.8f, RadialSegments = 4, Rings = 1 };

    private readonly Node3D _root;
    private readonly PersonFigure _figure;
    private readonly Node3D _marker;
    private readonly DeclutteredLabel _label;
    private Vector3 _shown;
    private float _heading;
    private float _clock;
    private bool _placed;

    /// <summary>Creates the villager's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the villagers.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="people">The person figures.</param>
    /// <param name="look">Their look, as the crowd shows them.</param>
    /// <param name="godColor">Their follower's colour.</param>
    public VillagerView(Node3D parent, VillagerState villager, PersonAssets people, PersonLook look, Color godColor)
    {
        GodColor = godColor;
        _root = new Node3D { Name = "Villager_" + villager.Id };
        parent.AddChild(_root);
        _figure = new PersonFigure(_root, people, look, Scale, FigureHideDistance);
        _marker = new Node3D { Name = "Marker", Position = new Vector3(0f, MarkerHeight, 0f) };
        _root.AddChild(_marker);
        var glow = new StandardMaterial3D { AlbedoColor = godColor.Lightened(0.3f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = godColor.Lightened(0.3f) };
        _marker.AddChild(new MeshInstance3D { Mesh = Marker, MaterialOverride = glow, VisibilityRangeEnd = FigureHideDistance });
        _label = new DeclutteredLabel
        {
            // A villager's label gives way to every village's and notable's.
            Priority = 0.4f,
            Name = "Label",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f,
            FontSize = 18,
            OutlineSize = 8,
            Modulate = godColor.Lightened(0.6f),
            OutlineModulate = new Color(0.05f, 0.04f, 0.03f, 0.85f),
            NoDepthTest = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Position = new Vector3(0f, LabelHeight, 0f),
            VisibilityRangeEnd = LabelHideDistance,
            VisibilityRangeEndMargin = LabelFadeMargin,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        _root.AddChild(_label);
    }

    /// <summary>Their follower's colour.</summary>
    public Color GodColor { get; }

    /// <summary>Where the figure stands now.</summary>
    public Vector3 Position => _shown;

    /// <summary>Moves and poses the figure for this frame.</summary>
    /// <param name="villager">Their state.</param>
    /// <param name="world">The world, for the label's names.</param>
    /// <param name="rules">Villager rules, for their age.</param>
    /// <param name="terrain">Terrain, for the ground height.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="running">Whether the simulation clock is running.</param>
    public void Sync(VillagerState villager, WorldState world, VillagerRules rules, TerrainField terrain, float delta, bool running)
    {
        var (x, z) = (villager.Position.X, villager.Position.Z);
        var target = new Vector3(x, Mathf.Max(terrain.SampleHeight(x, z), 0f), z);
        var before = _shown;
        _shown = _placed ? _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta)) : target;
        var moved = _placed ? new Vector2(_shown.X - before.X, _shown.Z - before.Z).Length() : 0f;
        _placed = true;
        var next = villager.Path.Count > 0 ? villager.Path[0] : villager.Position;
        var (dx, dz) = (next.X - _shown.X, next.Z - _shown.Z);
        if (villager.Path.Count > 0 && ((dx * dx) + (dz * dz)) > 0.25f)
        {
            _heading = Mathf.LerpAngle(_heading, MathF.Atan2(dx, dz), 1f - Mathf.Exp(-6f * delta));
        }

        if (running)
        {
            _clock += delta;
        }

        _figure.Cover(moved);
        var walking = delta > 0f && moved / delta > WalkingSpeed;
        var motion = walking ? PersonMotion.Walking : Motion(villager);
        var cycle = _clock * Rate(motion);
        var lean = motion is PersonMotion.Hoeing or PersonMotion.Hammering or PersonMotion.Chores ? 0.2f * (1f - MathF.Cos(MathF.Tau * cycle)) : 0f;
        _figure.Pose(motion, cycle, lean, _heading);
        _root.Position = _shown;
        _marker.Position = new Vector3(0f, MarkerHeight + (0.25f * MathF.Sin(_clock * 2f)), 0f);
        _marker.Rotation = new Vector3(0f, _clock * 1.5f, 0f);
        var title = VillagerText.Title(rules, villager, world);
        _label.SetTexts(title + "\n" + VillagerText.Doing(villager, world), villager.Name);
    }

    /// <summary>Frees the villager's nodes.</summary>
    public void Free() => _root.QueueFree();

    /// <summary>What a villager standing still is doing, as a motion.</summary>
    private static PersonMotion Motion(VillagerState villager) => villager.Activity switch
    {
        VillagerActivity.Working => villager.Work switch
        {
            VillagerWork.Farming => PersonMotion.Hoeing,
            VillagerWork.Building => PersonMotion.Hammering,
            VillagerWork.Chores => PersonMotion.Chores,
        },
        VillagerActivity.Worshipping => PersonMotion.Praying,
        VillagerActivity.Resting or VillagerActivity.Eating => PersonMotion.Sitting,
        VillagerActivity.Fleeing or VillagerActivity.Sheltering or VillagerActivity.Seeking => PersonMotion.Standing,
    };

    /// <summary>Cycles a second of a motion that does not walk: work strokes, prayers or breaths.</summary>
    private static float Rate(PersonMotion motion) => motion switch
    {
        PersonMotion.Hoeing or PersonMotion.Hammering or PersonMotion.Chores => 0.7f,
        PersonMotion.Praying => 0.125f,
        PersonMotion.Standing or PersonMotion.Walking or PersonMotion.Sitting => 0.25f,
    };
}
