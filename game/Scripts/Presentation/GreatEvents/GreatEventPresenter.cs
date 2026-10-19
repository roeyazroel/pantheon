using System;
using Godot;
using Pantheon.Client.Presentation.Creatures;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Game.GreatEvents;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.GreatEvents;

/// <summary>
/// Draws the great events that have a body or a place (ADR-035): the ancient beast, asleep in its lair,
/// awake and walking or ravaging, or fallen; and the anomaly's column of light. Presentation only.
/// </summary>
public sealed class GreatEventPresenter
{
    private const float FollowRate = 2f;
    private const float LabelHeight = 22f;

    private readonly Node3D _parent;
    private readonly DivineViewSettings _view;
    private Node3D? _beast;
    private Node3D? _beastBody;
    private DeclutteredLabel? _beastLabel;
    private Node3D? _anomaly;
    private OmniLight3D? _anomalyLight;
    private int _anomalyId;
    private TerrainField? _terrain;
    private Vector3 _shown;
    private float _heading;
    private float _clock;

    /// <summary>Creates the presenter; nothing is shown until <see cref="Rebuild"/>.</summary>
    /// <param name="parent">Node that holds the great events' visuals.</param>
    /// <param name="view">Divine view settings, for the beast's look and the anomaly's colour.</param>
    public GreatEventPresenter(Node3D parent, DivineViewSettings view)
    {
        _parent = parent;
        _view = view;
    }

    /// <summary>Drops every visual, for a new or loaded world.</summary>
    /// <param name="terrain">Terrain of the new world.</param>
    public void Rebuild(TerrainField terrain)
    {
        _parent.FreeChildren();
        (_beast, _beastBody, _beastLabel, _anomaly, _anomalyLight, _anomalyId) = (null, null, null, null, null, 0);
        _terrain = terrain;
    }

    /// <summary>Moves the beast and lights the anomaly for this frame.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Sync(WorldState world, double delta)
    {
        if (_terrain is null)
        {
            return;
        }

        _clock += (float)delta;
        SyncBeast(world, (float)delta);
        SyncAnomaly(world);
    }

    /// <summary>The beast: where it is, how it lies, and its label.</summary>
    private void SyncBeast(WorldState world, float delta)
    {
        if (world.GreatEvents.Beast is not { } beast)
        {
            return;
        }

        if (_beast is null)
        {
            _beast = new Node3D { Name = "Beast" };
            _parent.AddChild(_beast);
            _beastBody = new Node3D { Name = "Body" };
            _beast.AddChild(_beastBody);
            _ = new CreatureBody(_beastBody, new Color(0.7f, 0.2f, 0.1f), _view.LookFor("Beast"));
            _beastLabel = new DeclutteredLabel
            {
                Priority = 1.5f, Name = "Label", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.001f, FontSize = 24, OutlineSize = 8,
                Modulate = new Color(1f, 0.55f, 0.4f), OutlineModulate = new Color(0.05f, 0.03f, 0.02f, 0.9f), NoDepthTest = true, VerticalAlignment = VerticalAlignment.Bottom,
                Position = new Vector3(0f, LabelHeight, 0f),
            };
            _beast.AddChild(_beastLabel);
            _shown = Ground(beast.Position);
        }

        var target = Ground(beast.Position);
        var (dx, dz) = (target.X - _shown.X, target.Z - _shown.Z);
        if ((dx * dx) + (dz * dz) > 1f)
        {
            _heading = Mathf.LerpAngle(_heading, MathF.Atan2(dx, dz), 1f - Mathf.Exp(-3f * delta));
        }

        _shown = _shown.Lerp(target, 1f - Mathf.Exp(-FollowRate * delta));
        _beast.Position = _shown;
        var (sink, lean) = beast.Phase switch
        {
            BeastPhase.Sleeping => (-4f, 0f),
            BeastPhase.Slain => (-1f, Mathf.Pi / 2f),
            BeastPhase.Awake or BeastPhase.Returning => (MathF.Abs(MathF.Sin(_clock * 2f)) * 0.6f, 0f),
        };
        _beastBody!.Position = new Vector3(0f, sink, 0f);
        _beastBody.Rotation = new Vector3(0f, _heading, lean);
        var text = beast.Phase switch
        {
            BeastPhase.Sleeping => "The lair of " + beast.Name,
            BeastPhase.Slain => "Here fell " + beast.Name,
            BeastPhase.Awake or BeastPhase.Returning => GreatEventText.Beast(world),
        };
        _beastLabel!.SetTexts(text, beast.Name);
    }

    /// <summary>The anomaly's column of light, while one surges.</summary>
    private void SyncAnomaly(WorldState world)
    {
        var surge = world.GreatEvents.Events.Find(e => e.Kind == GreatEventKind.Anomaly && e.Position is not null);
        if (surge is null || surge.Id != _anomalyId)
        {
            _anomaly?.QueueFree();
            (_anomaly, _anomalyLight, _anomalyId) = (null, null, surge?.Id ?? 0);
        }

        if (surge is not { Position: { } place })
        {
            return;
        }

        if (_anomaly is null)
        {
            var color = _view.AnomalyColor.ToGodot();
            _anomaly = new Node3D { Name = "Anomaly", Position = Ground(place) };
            _parent.AddChild(_anomaly);
            var glow = new StandardMaterial3D
            {
                AlbedoColor = new Color(color, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled, DisableFog = true,
            };
            _anomaly.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 2f, BottomRadius = 9f, Height = 140f, Material = glow }, Position = new Vector3(0f, 70f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _anomalyLight = new OmniLight3D { LightColor = color, LightEnergy = 4f, OmniRange = 90f, Position = new Vector3(0f, 12f, 0f) };
            _anomaly.AddChild(_anomalyLight);
            var label = new DeclutteredLabel
            {
                Priority = 1.4f, Name = "Label", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, PixelSize = 0.001f, FontSize = 22, OutlineSize = 8,
                Modulate = color.Lightened(0.3f), OutlineModulate = new Color(0.05f, 0.03f, 0.08f, 0.9f), NoDepthTest = true, VerticalAlignment = VerticalAlignment.Bottom,
                Position = new Vector3(0f, 40f, 0f),
            };
            label.SetTexts(GreatEventText.Capitalised(surge.Name) + "\nmiracles cost less here", GreatEventText.Capitalised(surge.Name));
            _anomaly.AddChild(label);
        }

        _anomalyLight!.LightEnergy = 4f + (2f * MathF.Sin(_clock * 1.5f));
    }

    /// <summary>The ground (or the sea's surface) under a point.</summary>
    private Vector3 Ground(GroundPosition point) => new(point.X, Mathf.Max(_terrain!.SampleHeight(point.X, point.Z), 0f), point.Z);
}
