using System;
using Godot;
using Pantheon.Client.Presentation.People;
using Pantheon.Game.Population;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Game.Notables;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Notables;

/// <summary>
/// One prophet or elder on screen (spec §80, ADR-032, ADR-049): a person in a long robe of their
/// god's colour with a staff, taller than the villagers; a prophet walks under a halo and preaches
/// with raised arms, an elder stands by their village's centre. Presentation only: it follows the
/// simulated position smoothly.
/// </summary>
internal sealed class NotableView
{
    private const float FollowRate = 3f;
    private const float Scale = 1.45f;

    // Below this speed, in metres a second, a notable stands rather than walks.
    private const float WalkingSpeed = 0.25f;
    private const float LabelHeight = 8f;

    // Figures and labels hide beyond these distances from the camera.
    private const float FigureHideDistance = 900f;
    private const float LabelHideDistance = 1000f;
    private const float LabelFadeMargin = 150f;

    // Where each stands in a village, off its centre, so neither hides in the houses' middle.
    private static readonly Vector3 ElderPlace = new(5f, 0f, 5f);
    private static readonly Vector3 PreacherPlace = new(-6f, 0f, 4f);

    private static readonly TorusMesh Halo = new() { InnerRadius = 0.26f, OuterRadius = 0.33f, Rings = 16, RingSegments = 6 };

    private readonly Node3D _root;
    private readonly PersonFigure _figure;
    private readonly DeclutteredLabel _label;
    private Vector3 _shown;
    private float _heading;
    private float _clock;
    private bool _placed;

    /// <summary>Creates the notable's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the notables.</param>
    /// <param name="notable">The notable.</param>
    /// <param name="color">Their god's colour.</param>
    /// <param name="people">The person figures.</param>
    /// <param name="seed">The world seed, for their look.</param>
    public NotableView(Node3D parent, NotableState notable, Color color, PersonAssets people, WorldSeed seed)
    {
        Kind = notable.Kind;
        Color = color;
        _root = new Node3D { Name = notable.Kind + "_" + notable.Id };
        parent.AddChild(_root);
        var robe = new Pantheon.Game.Presentation.RgbColor(color.R, color.G, color.B);
        _figure = new PersonFigure(_root, people, PersonLooks.Notable(people.Settings, seed.Value, notable.Id, robe), Scale, FigureHideDistance);
        if (notable.Kind == NotableKind.Prophet)
        {
            var glow = new StandardMaterial3D { AlbedoColor = color.Lightened(0.5f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = color.Lightened(0.5f) };
            _root.AddChild(new MeshInstance3D { Mesh = Halo, MaterialOverride = glow, Position = new Vector3(0f, _figure.Height + 0.25f, 0f), VisibilityRangeEnd = FigureHideDistance });
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
        var before = _shown;
        _shown = _placed ? _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta)) : target;
        var moved = _placed ? new Vector2(_shown.X - before.X, _shown.Z - before.Z).Length() : 0f;
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

        _figure.Cover(moved);
        var motion = delta > 0f && moved / delta > WalkingSpeed ? PersonMotion.Walking
            : Kind == NotableKind.Prophet ? PersonMotion.Praying : PersonMotion.Standing;
        _figure.Pose(motion, _clock * (motion == PersonMotion.Praying ? 0.125f : 0.25f), 0f, _heading);
        _root.Position = _shown;
        var title = NotableText.Title(notable, world);
        _label.SetTexts(title + "\n" + NotableText.Doing(notable, world), title);
    }

    /// <summary>Frees the notable's nodes.</summary>
    public void Free() => _root.QueueFree();
}
