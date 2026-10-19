using System;
using Godot;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Game.Notables;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Notables;

/// <summary>
/// One prophet or elder on screen (spec §80, ADR-032): a robed figure in their god's colour, taller
/// than the villagers; a prophet carries a staff under a halo, an elder stands by their village's
/// centre. Presentation only: it follows the simulated position smoothly.
/// </summary>
internal sealed class NotableView
{
    private const float FollowRate = 3f;
    private const float StrideHertz = 1.4f;
    private const float Scale = 1.6f;
    private const float LabelHeight = 8f;

    // Figures and labels hide beyond these distances from the camera.
    private const float FigureHideDistance = 900f;
    private const float LabelHideDistance = 1000f;
    private const float LabelFadeMargin = 150f;

    // Where each stands in a village, off its centre, so neither hides in the houses' middle.
    private static readonly Vector3 ElderPlace = new(5f, 0f, 5f);
    private static readonly Vector3 PreacherPlace = new(-6f, 0f, 4f);

    private static readonly CylinderMesh Robe = new() { TopRadius = 0.32f, BottomRadius = 0.95f, Height = 2.4f, RadialSegments = 10, Rings = 1 };
    private static readonly SphereMesh Head = new() { Radius = 0.42f, Height = 0.84f, RadialSegments = 10, Rings = 6 };
    private static readonly CylinderMesh Staff = new() { TopRadius = 0.07f, BottomRadius = 0.07f, Height = 3.4f, RadialSegments = 6, Rings = 1 };
    private static readonly TorusMesh Halo = new() { InnerRadius = 0.5f, OuterRadius = 0.62f, Rings = 16, RingSegments = 6 };
    private static readonly StandardMaterial3D Skin = new() { AlbedoColor = new Color(0.78f, 0.6f, 0.46f), Roughness = 0.9f };
    private static readonly StandardMaterial3D Wood = new() { AlbedoColor = new Color(0.42f, 0.3f, 0.18f), Roughness = 0.95f };

    private readonly Node3D _root;
    private readonly Node3D _body;
    private readonly DeclutteredLabel _label;
    private Vector3 _shown;
    private float _heading;
    private float _clock;
    private bool _placed;

    /// <summary>Creates the notable's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the notables.</param>
    /// <param name="notable">The notable.</param>
    /// <param name="color">Their god's colour.</param>
    public NotableView(Node3D parent, NotableState notable, Color color)
    {
        Kind = notable.Kind;
        Color = color;
        _root = new Node3D { Name = notable.Kind + "_" + notable.Id };
        parent.AddChild(_root);
        _body = new Node3D { Name = "Body", Scale = Vector3.One * Scale };
        _root.AddChild(_body);
        var robe = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f };
        Part(Robe, robe, new Vector3(0f, 1.2f, 0f));
        Part(Head, Skin, new Vector3(0f, 2.8f, 0f));
        if (notable.Kind == NotableKind.Prophet)
        {
            Part(Staff, Wood, new Vector3(0.75f, 1.7f, 0.2f));
            var glow = new StandardMaterial3D { AlbedoColor = color.Lightened(0.5f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = color.Lightened(0.5f) };
            Part(Halo, glow, new Vector3(0f, 3.45f, 0f));
        }

        _label = new DeclutteredLabel
        {
            // A notable's label gives way to every village's, but not to a group on the road; a
            // prophet's, who walks, comes before an elder's, who stays.
            Priority = notable.Kind == NotableKind.Prophet ? 0.6f : 0.5f,
            Name = "Label",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.001f,
            FontSize = 20,
            OutlineSize = 8,
            Modulate = color.Lightened(0.55f),
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

    /// <summary>A prophet or an elder.</summary>
    public NotableKind Kind { get; }

    /// <summary>Their god's colour.</summary>
    public Color Color { get; }

    /// <summary>Moves the figure for this frame.</summary>
    /// <param name="notable">Their state.</param>
    /// <param name="world">The world, for the label's names.</param>
    /// <param name="terrain">Terrain, for the ground height.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="running">Whether the simulation clock is running.</param>
    public void Sync(NotableState notable, WorldState world, TerrainField terrain, float delta, bool running)
    {
        var walking = notable.TargetSettlementId is not null;
        var place = walking ? Vector3.Zero : notable.Kind == NotableKind.Elder ? ElderPlace : PreacherPlace;
        var (x, z) = (notable.Position.X + place.X, notable.Position.Z + place.Z);
        var target = new Vector3(x, Mathf.Max(terrain.SampleHeight(x, z), 0f), z);
        _shown = _placed ? _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta)) : target;
        _placed = true;
        var next = walking && notable.Path.Count > 0 ? notable.Path[0] : notable.Position;
        var (dx, dz) = (next.X - _shown.X, next.Z - _shown.Z);
        if (walking && ((dx * dx) + (dz * dz)) > 1f)
        {
            _heading = Mathf.LerpAngle(_heading, MathF.Atan2(dx, dz), 1f - Mathf.Exp(-4f * delta));
        }

        if (running)
        {
            _clock += delta;
        }

        var bob = walking ? MathF.Abs(MathF.Sin(_clock * StrideHertz * MathF.PI)) * 0.2f : 0f;
        _root.Position = _shown;
        _body.Position = new Vector3(0f, bob, 0f);
        _body.Rotation = new Vector3(0f, _heading, 0f);
        var title = NotableText.Title(notable, world);
        _label.SetTexts(title + "\n" + NotableText.Doing(notable, world), title);
    }

    /// <summary>Frees the notable's nodes.</summary>
    public void Free() => _root.QueueFree();

    /// <summary>Adds one part of the figure.</summary>
    private void Part(Mesh mesh, Material material, Vector3 position) =>
        _body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = position, VisibilityRangeEnd = FigureHideDistance });
}
