using System;
using Godot;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game.Villagers;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Villagers;

/// <summary>
/// One followed villager on screen (spec §80, ADR-043): a villager's figure in the colour of their
/// work, a little larger than the crowd's, under a marker in their follower's colour, with a label of
/// who they are and what they do. It walks, bends to work, kneels to pray and sits to rest.
/// Presentation only: it follows the simulated position smoothly.
/// </summary>
internal sealed class VillagerView
{
    private const float FollowRate = 4f;
    private const float StrideHertz = 2.2f;
    private const float Scale = 1.7f;
    private const float MarkerHeight = 4.4f;
    private const float LabelHeight = 6f;
    private const float FigureHideDistance = 900f;
    private const float LabelHideDistance = 700f;
    private const float LabelFadeMargin = 120f;

    private static readonly CylinderMesh Body = new() { TopRadius = 0.17f, BottomRadius = 0.3f, Height = 1.15f, RadialSegments = 10, Rings = 1 };
    private static readonly SphereMesh Head = new() { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 };
    private static readonly CylinderMesh Marker = new() { TopRadius = 0.45f, BottomRadius = 0f, Height = 0.8f, RadialSegments = 4, Rings = 1 };
    private static readonly StandardMaterial3D Skin = new() { AlbedoColor = new Color(0.78f, 0.6f, 0.46f), Roughness = 0.9f };

    private readonly Node3D _root;
    private readonly Node3D _body;
    private readonly Node3D _marker;
    private readonly DeclutteredLabel _label;
    private Vector3 _shown;
    private float _heading;
    private float _clock;
    private bool _placed;

    /// <summary>Creates the villager's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the villagers.</param>
    /// <param name="villager">The villager.</param>
    /// <param name="workColor">The colour of their work, as the crowd shows it.</param>
    /// <param name="godColor">Their follower's colour.</param>
    public VillagerView(Node3D parent, VillagerState villager, Color workColor, Color godColor)
    {
        GodColor = godColor;
        _root = new Node3D { Name = "Villager_" + villager.Id };
        parent.AddChild(_root);
        _body = new Node3D { Name = "Body", Scale = Vector3.One * Scale };
        _root.AddChild(_body);
        Part(_body, Body, new StandardMaterial3D { AlbedoColor = workColor.Lightened(0.15f), Roughness = 0.85f }, new Vector3(0f, 0.575f, 0f));
        Part(_body, Head, Skin, new Vector3(0f, 1.31f, 0f));
        _marker = new Node3D { Name = "Marker", Position = new Vector3(0f, MarkerHeight, 0f) };
        _root.AddChild(_marker);
        var glow = new StandardMaterial3D { AlbedoColor = godColor.Lightened(0.3f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = godColor.Lightened(0.3f) };
        Part(_marker, Marker, glow, Vector3.Zero);
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
        _shown = _placed ? _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta)) : target;
        _placed = true;
        var walking = villager.Path.Count > 0;
        var next = walking ? villager.Path[0] : villager.Position;
        var (dx, dz) = (next.X - _shown.X, next.Z - _shown.Z);
        if (walking && ((dx * dx) + (dz * dz)) > 0.25f)
        {
            _heading = Mathf.LerpAngle(_heading, MathF.Atan2(dx, dz), 1f - Mathf.Exp(-6f * delta));
        }

        if (running)
        {
            _clock += delta;
        }

        var (lift, lean) = Pose(villager.Activity, walking, villager.Activity == VillagerActivity.Fleeing ? 1.8f : 1f);
        _root.Position = _shown;
        _body.Position = new Vector3(0f, lift, 0f);
        _body.Rotation = new Vector3(lean, _heading, 0f);
        _marker.Position = new Vector3(0f, MarkerHeight + (0.25f * MathF.Sin(_clock * 2f)), 0f);
        _marker.Rotation = new Vector3(0f, _clock * 1.5f, 0f);
        var title = VillagerText.Title(rules, villager, world);
        _label.SetTexts(title + "\n" + VillagerText.Doing(villager, world), villager.Name);
    }

    /// <summary>Frees the villager's nodes.</summary>
    public void Free() => _root.QueueFree();

    /// <summary>The body's lift and forward lean for what they do: a stride's bob, a worker's stoop, a kneel, a seat.</summary>
    private (float Lift, float Lean) Pose(VillagerActivity activity, bool walking, float pace)
    {
        if (walking)
        {
            return (MathF.Abs(MathF.Sin(_clock * StrideHertz * pace * MathF.PI)) * 0.12f, activity == VillagerActivity.Fleeing ? 0.25f : 0.05f);
        }

        return activity switch
        {
            VillagerActivity.Working => (0f, 0.3f + (0.2f * MathF.Max(0f, MathF.Sin(_clock * 3f)))),
            VillagerActivity.Worshipping => (-0.45f, 0.35f),
            VillagerActivity.Resting => (-0.55f, -0.1f),
            VillagerActivity.Eating => (-0.5f, 0.1f + (0.05f * MathF.Sin(_clock * 4f))),
            VillagerActivity.Fleeing or VillagerActivity.Sheltering or VillagerActivity.Seeking => (0f, 0f),
        };
    }

    /// <summary>Adds one part of the figure or marker.</summary>
    private static void Part(Node3D parent, Mesh mesh, Material material, Vector3 position) =>
        parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position, VisibilityRangeEnd = FigureHideDistance });
}
