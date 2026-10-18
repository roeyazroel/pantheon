using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Disasters;

/// <summary>
/// Draws disasters (ADR-014) from <see cref="WorldState"/>: a <see cref="FireView"/> per burning
/// region, ash on scorched land, a dome over shielded settlements, a shock wave when an earthquake
/// begins and the camera shaking while the ground shakes under its focus.
/// </summary>
public sealed class DisasterPresenter
{
    private const float ScorchShown = 0.05f;
    private const float AshDepth = 60f;
    private const float ShakeMetres = 3f;
    private const float DomeRadius = 75f;

    private static readonly Color AshColor = new(0.17f, 0.13f, 0.1f);

    private static readonly StandardMaterial3D DomeMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = new Color(0.55f, 0.82f, 1f, 0.12f),
    };

    private readonly Node3D _fires;
    private readonly Node3D _ash;
    private readonly Node3D _domes;
    private readonly Node3D _waves;
    private readonly GodCameraController _camera;
    private readonly Dictionary<int, FireView> _fireViews = [];
    private readonly Dictionary<int, Decal> _ashViews = [];
    private readonly Dictionary<int, MeshInstance3D> _domeViews = [];
    private readonly HashSet<int> _seenQuakes = [];
    private TerrainField? _terrain;
    private float _regionSize;
    private double _time;

    /// <summary>Creates the presenter's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the disaster visuals.</param>
    /// <param name="camera">The camera, shaken by earthquakes.</param>
    public DisasterPresenter(Node3D parent, GodCameraController camera)
    {
        _fires = parent.AddLayer("Fires");
        _ash = parent.AddLayer("Ash");
        _domes = parent.AddLayer("Shields");
        _waves = parent.AddLayer("Quakes");
        _camera = camera;
    }

    /// <summary>Forgets the old world's disasters.</summary>
    /// <param name="terrain">Terrain of the new world.</param>
    /// <param name="regionSize">Side of a region in metres.</param>
    public void Rebuild(TerrainField terrain, float regionSize)
    {
        _terrain = terrain;
        _regionSize = regionSize;
        foreach (var layer in new[] { _fires, _ash, _domes, _waves })
        {
            layer.FreeChildren();
        }

        _fireViews.Clear();
        _ashViews.Clear();
        _domeViews.Clear();
        _seenQuakes.Clear();
    }

    /// <summary>Brings the visuals in line with the disasters and shakes the camera.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="focus">The ground point the camera looks at.</param>
    public void Sync(WorldState world, float delta, GroundPosition focus)
    {
        _time += delta;
        SyncRegions(world);
        SyncDomes(world);
        foreach (var quake in world.Disasters.Earthquakes)
        {
            if (_seenQuakes.Add(quake.Id))
            {
                var look = new MiracleEffectView { MiracleId = "earthquake", Style = MiracleEffectStyle.Wave, Color = RgbColor.ParseHex("#c08a55"), Seconds = 3f };
                _waves.AddChild(MiracleEffect.Create(look, Ground(quake.X, quake.Z), quake.Radius));
            }
        }

        var shaking = DisasterModel.ShakingAt(world.Disasters, focus);
        _camera.ShakeOffset = shaking <= 0f
            ? Vector3.Zero
            : new Vector3(MathF.Sin((float)_time * 37f), 0.5f * MathF.Sin((float)_time * 53f), MathF.Cos((float)_time * 41f)) * (ShakeMetres * shaking);
    }

    /// <summary>Fire views for burning regions and ash for scorched ones.</summary>
    private void SyncRegions(WorldState world)
    {
        foreach (var region in world.Regions.Cells)
        {
            var centre = world.Regions.GetRegionCenter(region.Id);
            if (region.Fire > 0f)
            {
                if (!_fireViews.TryGetValue(region.Id, out var fire))
                {
                    fire = FireView.Create(Ground(centre.X, centre.Z), _regionSize);
                    _fireViews[region.Id] = fire;
                    _fires.AddChild(fire);
                }

                fire.Show(region.Fire);
            }
            else if (_fireViews.Remove(region.Id, out var spent))
            {
                spent.QueueFree();
            }

            if (region.Scorch >= ScorchShown)
            {
                if (!_ashViews.TryGetValue(region.Id, out var ash))
                {
                    ash = new Decal { Name = "Ash", TextureAlbedo = Weather.SoftDiscTexture.Texture, Size = new Vector3(_regionSize * 1.5f, AshDepth, _regionSize * 1.5f), Position = Ground(centre.X, centre.Z), UpperFade = 0.1f, LowerFade = 0.1f };
                    _ashViews[region.Id] = ash;
                    _ash.AddChild(ash);
                }

                ash.Modulate = new Color(AshColor, Mathf.Clamp(region.Scorch, 0f, 0.85f));
            }
            else if (_ashViews.Remove(region.Id, out var regrown))
            {
                regrown.QueueFree();
            }
        }
    }

    /// <summary>A dome over every shielded settlement.</summary>
    private void SyncDomes(WorldState world)
    {
        foreach (var settlement in world.Settlements)
        {
            var shielded = DisasterModel.IsShielded(settlement, world.Tick);
            if (shielded && !_domeViews.ContainsKey(settlement.Id))
            {
                var dome = new MeshInstance3D
                {
                    Name = "Shield",
                    Mesh = new SphereMesh { Radius = DomeRadius, Height = DomeRadius * 2f, Material = DomeMaterial },
                    Position = Ground(settlement.Position.X, settlement.Position.Z),
                    Scale = new Vector3(1f, 0.6f, 1f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                _domeViews[settlement.Id] = dome;
                _domes.AddChild(dome);
            }
            else if (!shielded && _domeViews.Remove(settlement.Id, out var dropped))
            {
                dropped.QueueFree();
            }
        }
    }

    /// <summary>The ground point (or sea surface) below a position.</summary>
    private Vector3 Ground(float x, float z) => new(x, _terrain is null ? 0f : Mathf.Max(_terrain.SampleHeight(x, z), 0f), z);
}
