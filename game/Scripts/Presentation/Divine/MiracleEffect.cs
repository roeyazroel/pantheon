using System;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>
/// One miracle's visual effect (presentation only): motes falling or rising, a spreading ring or a
/// lightning bolt, or a signature look (ADR-055): rain from a gathering cloud, a golden pillar with
/// falling grain, a healing spiral under a dome, flames and smoke, a quake's dust, a shield's dome, a
/// fountain, a pillar of holy light, a dark swirl, a wheeling storm. A glow swells and fades, and so
/// do its translucent layers. It frees itself when it is over.
/// </summary>
internal sealed partial class MiracleEffect : Node3D
{
    private const float ShowerHeight = 45f;
    private const float BoltHeight = 160f;
    private const int BoltSegments = 9;

    private float _age;
    private float _seconds;
    private float _radius;
    private float _boltHeight = BoltHeight;
    private OmniLight3D? _light;
    private float _lightEnergy;
    private Decal? _ring;
    private Node3D? _bolt;
    private CpuParticles3D? _motes;
    private readonly System.Collections.Generic.List<Action<float>> _fades = [];
    private Node3D? _grows;

    /// <summary>Creates an effect at a ground point.</summary>
    /// <param name="view">The miracle's look.</param>
    /// <param name="ground">Ground point of the target.</param>
    /// <param name="radius">Radius of the miracle in metres.</param>
    /// <param name="boltHeight">Height a bolt falls from, in metres above the ground; a miracle's when <c>null</c>.</param>
    /// <returns>The effect node, ready to be added to the tree.</returns>
    public static MiracleEffect Create(MiracleEffectView view, Vector3 ground, float radius, float? boltHeight = null)
    {
        var effect = new MiracleEffect { Name = "Miracle_" + view.MiracleId, Position = ground, _seconds = view.Seconds, _radius = MathF.Max(radius, 6f), _boltHeight = MathF.Max(boltHeight ?? BoltHeight, 20f) };
        var color = view.Color.ToGodot();
        switch (view.Style)
        {
            case MiracleEffectStyle.Shower:
                effect.AddMotes(color, falling: true);
                effect.AddLight(color, energy: 4f, height: 18f);
                break;
            case MiracleEffectStyle.Rise:
                effect.AddMotes(color, falling: false);
                effect.AddLight(color, energy: 3f, height: 6f);
                break;
            case MiracleEffectStyle.Wave:
                effect.AddRing(color);
                effect.AddMotes(color, falling: false);
                effect.AddLight(color, energy: 3f, height: 10f);
                break;
            case MiracleEffectStyle.Bolt:
                effect.AddBolt(color);
                effect.AddLight(color, energy: 16f, height: 12f);
                break;
            case MiracleEffectStyle.Strike:
                effect.AddBolt(color);
                effect.AddLight(color, energy: 16f, height: 12f);
                effect.AddRing(color);
                effect.AddChild(MiracleLayers.Sparks(color));
                break;
            case MiracleEffectStyle.Rain:
                effect.AddChild(MiracleLayers.Cloud(new Color(0.35f, 0.38f, 0.45f), effect._radius * 0.9f, 48f, effect._seconds));
                effect.AddChild(MiracleLayers.Rain(color, effect._radius * 0.9f, 44f));
                effect.AddLight(color, energy: 1.5f, height: 20f);
                break;
            case MiracleEffectStyle.Harvest:
                effect.Fade(MiracleLayers.Pillar(color, MathF.Max(3f, effect._radius * 0.25f), 90f));
                effect.AddChild(MiracleLayers.Fall(color, effect._radius, ShowerHeight, 0.6f));
                effect.AddLight(color, energy: 6f, height: 14f);
                break;
            case MiracleEffectStyle.Healing:
                effect.AddChild(MiracleLayers.Spiral(color, effect._radius));
                effect.Grow(MiracleLayers.Dome(color, effect._radius));
                effect.AddLight(color, energy: 4f, height: 8f);
                break;
            case MiracleEffectStyle.Flames:
                effect.AddChild(MiracleLayers.Flames(effect._radius));
                effect.AddChild(MiracleLayers.Smoke(effect._radius));
                effect.AddLight(new Color(1f, 0.5f, 0.15f), energy: 12f, height: 6f);
                break;
            case MiracleEffectStyle.Quake:
                effect.AddRing(color);
                effect.AddChild(MiracleLayers.Dust(effect._radius));
                break;
            case MiracleEffectStyle.Shield:
                effect.Grow(MiracleLayers.Dome(color, effect._radius));
                effect.AddMotes(color, falling: false);
                effect.AddLight(color, energy: 3f, height: 10f);
                break;
            case MiracleEffectStyle.Fountain:
                effect.AddChild(MiracleLayers.Fountain(color, effect._radius));
                effect.AddRing(color);
                break;
            case MiracleEffectStyle.Holy:
                effect.Fade(MiracleLayers.Pillar(color, MathF.Max(4f, effect._radius * 0.35f), 220f));
                effect.AddMotes(color, falling: false);
                effect.AddLight(color, energy: 8f, height: 16f);
                break;
            case MiracleEffectStyle.Shadow:
                effect.AddChild(MiracleLayers.Spiral(color.Darkened(0.4f), effect._radius, additive: false));
                effect.AddRing(color);
                effect.AddLight(color, energy: 2f, height: 6f);
                break;
            case MiracleEffectStyle.Storm:
                var storm = MiracleLayers.Cloud(new Color(0.3f, 0.32f, 0.4f), effect._radius * 0.8f, 48f, effect._seconds);
                effect.AddChild(storm);
                effect._grows = storm;
                effect.AddChild(MiracleLayers.Rain(color, effect._radius * 0.6f, 44f));
                break;
        }

        return effect;
    }

    /// <summary>Fades the effect and frees it at the end.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _age += (float)delta;
        var t = Math.Clamp(_age / _seconds, 0f, 1f);
        if (_light is not null)
        {
            // Lightning flickers, the others swell and fade.
            _light.LightEnergy = _bolt is not null
                ? _lightEnergy * (1f - t) * (MathF.Sin(_age * 60f) > -0.3f ? 1f : 0.2f)
                : _lightEnergy * MathF.Sin(MathF.PI * t);
        }

        if (_ring is not null)
        {
            var size = _radius * 2f * MathF.Sqrt(t);
            _ring.Size = new Vector3(MathF.Max(size, 0.5f), 80f, MathF.Max(size, 0.5f));
            _ring.Modulate = new Color(_ring.Modulate, 1f - t);
        }

        if (_bolt is not null)
        {
            _bolt.Visible = t < 0.45f && MathF.Sin(_age * 45f) > -0.6f;
        }

        if (_motes is not null && t > 0.6f)
        {
            _motes.Emitting = false;
        }

        // Translucent layers swell in and fade out; domes rise; a storm's cloud wheels.
        var envelope = MathF.Min(1f, MathF.Sin(MathF.PI * t) * 1.6f);
        foreach (var fade in _fades)
        {
            fade(envelope);
        }

        if (_grows is MeshInstance3D dome)
        {
            dome.Scale = Vector3.One * MathF.Min(1f, t * 3f);
        }
        else if (_grows is not null)
        {
            _grows.Rotation = new Vector3(0f, _age * 0.6f, 0f);
        }

        foreach (var child in GetChildren())
        {
            if (child is GpuParticles3D { OneShot: false } particles && t > 0.7f)
            {
                particles.Emitting = false;
            }
        }

        if (_age >= _seconds)
        {
            QueueFree();
        }
    }

    /// <summary>Adds a translucent layer that swells in and fades out over the effect's life.</summary>
    private void Fade((Node3D Node, Action<float> Fade) layer)
    {
        _fades.Add(layer.Fade);
        AddChild(layer.Node);
    }

    /// <summary>Adds a dome that rises over the radius and fades.</summary>
    private void Grow((Node3D Node, Action<float> Fade) layer)
    {
        Fade(layer);
        _grows = layer.Node;
        layer.Node.Scale = Vector3.One * 0.01f;
    }

    /// <summary>Glowing motes over the radius, falling from the sky or rising from the ground.</summary>
    private void AddMotes(Color color, bool falling)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            AlbedoTexture = MiracleLayers.Soft,
            VertexColorUseAsAlbedo = true,
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(color, 0f));
        ramp.SetColor(1, new Color(color, 0f));
        ramp.AddPoint(0.2f, color);
        ramp.AddPoint(0.75f, color);
        _motes = new CpuParticles3D
        {
            Name = "Motes",
            Amount = (int)Math.Clamp(_radius * 4f, 60f, 400f),
            Lifetime = falling ? 1.8 : 1.6,
            Explosiveness = 0.1f,
            Mesh = new QuadMesh { Size = new Vector2(1.6f, 1.6f), Material = material },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(_radius, 2f, _radius),
            Position = new Vector3(0f, falling ? ShowerHeight : 1f, 0f),
            Direction = falling ? Vector3.Down : Vector3.Up,
            Spread = 12f,
            InitialVelocityMin = falling ? 6f : 4f,
            InitialVelocityMax = falling ? 12f : 9f,
            Gravity = falling ? new Vector3(0f, -22f, 0f) : new Vector3(0f, 3f, 0f),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            ColorRamp = ramp,
            Emitting = true,
        };
        AddChild(_motes);
    }

    /// <summary>A glow above the target.</summary>
    private void AddLight(Color color, float energy, float height)
    {
        _lightEnergy = energy;
        _light = new OmniLight3D
        {
            Name = "Glow",
            LightColor = color,
            LightEnergy = 0f,
            OmniRange = MathF.Max(_radius * 2.2f, 40f),
            Position = new Vector3(0f, height, 0f),
            ShadowEnabled = false,
        };
        AddChild(_light);
    }

    /// <summary>A ring of light on the ground that spreads to the radius.</summary>
    private void AddRing(Color color)
    {
        _ring = new Decal { Name = "Wave", TextureAlbedo = RingTexture.Texture, Modulate = color, Size = new Vector3(1f, 80f, 1f), EmissionEnergy = 2f, CullMask = DeclutteredLabel.GroundOnly };
        AddChild(_ring);
    }

    /// <summary>A jagged bolt from the sky to the target, built from thin glowing boxes.</summary>
    private void AddBolt(Color color)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 6f,
        };
        _bolt = new Node3D { Name = "Bolt" };
        var random = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
        var from = new Vector3(0f, _boltHeight, 0f);
        for (var i = 1; i <= BoltSegments; i++)
        {
            var spread = (1f - ((float)i / BoltSegments)) * 9f;
            var to = i == BoltSegments
                ? Vector3.Zero
                : new Vector3(random.RandfRange(-spread, spread), _boltHeight * (1f - ((float)i / BoltSegments)), random.RandfRange(-spread, spread));
            var length = from.DistanceTo(to);
            var segment = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.6f, length, 0.6f), Material = material },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _bolt.AddChild(segment);
            segment.Position = (from + to) / 2f;
            var axis = (to - from).Normalized();
            if (!axis.IsEqualApprox(Vector3.Down) && !axis.IsEqualApprox(Vector3.Up))
            {
                segment.Basis = new Basis(new Quaternion(Vector3.Up, axis));
            }

            from = to;
        }

        AddChild(_bolt);
    }
}
