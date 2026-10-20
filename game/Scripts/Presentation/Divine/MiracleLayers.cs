using System;
using Godot;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>
/// The layers the miracles' signature looks are made of (ADR-055): a pillar of light, a dome, a
/// cloud, and particles of rain, flame, smoke, water, light, shadow, dust and sparks. The
/// translucent ones come with how to fade them (0 gone, 1 full) over the effect's life.
/// </summary>
internal static class MiracleLayers
{
    private const string GlowShaderPath = "res://Shaders/miracle_glow.gdshader";

    // A cloud's puffs, at most this far out, and how near the camera they fade away.
    private const float CloudReach = 120f;
    private const float CloudFadeNear = 25f;
    private const float CloudFadeFar = 80f;

    /// <summary>A soft round glow for particles.</summary>
    public static readonly GradientTexture2D Soft = new()
    {
        Width = 32,
        Height = 32,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient { Colors = [Colors.White, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f)], Offsets = [0f, 0.45f, 1f] },
    };

    /// <summary>A column of light from high above down to the ground, fading towards its top.</summary>
    public static (Node3D Node, Action<float> Fade) Pillar(Color color, float radius, float height)
    {
        var material = Glow(color, strength: 0.9f, rim: 1.5f, span: height, topFade: 0.85f);
        var node = new MeshInstance3D
        {
            Name = "Pillar",
            Mesh = new CylinderMesh { TopRadius = radius * 1.4f, BottomRadius = radius, Height = height, RadialSegments = 24, Rings = 1, CapTop = false, CapBottom = false, Material = material },
            Position = new Vector3(0f, height / 2f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        return (node, f => material.SetShaderParameter("fade", f));
    }

    /// <summary>A dome of light over the ground, bright at its rim; a great dome is fainter, so the land shows through.</summary>
    public static (Node3D Node, Action<float> Fade) Dome(Color color, float radius)
    {
        var material = Glow(color, strength: 0.8f * Math.Clamp(60f / radius, 0.35f, 1f), rim: 3f, span: radius, topFade: 0f);
        var node = new MeshInstance3D
        {
            Name = "Dome",
            Mesh = new SphereMesh { Radius = radius, Height = radius, IsHemisphere = true, RadialSegments = 48, Rings = 16, Material = material },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        return (node, f => material.SetShaderParameter("fade", f));
    }

    /// <summary>
    /// A dark cloud of soft puffs gathering high over the target, all drawn at once as one set of
    /// particles that swell in and fade out over the effect's life; the puffs near the camera fade away.
    /// </summary>
    public static GpuParticles3D Cloud(Color color, float radius, float height, float seconds)
    {
        var reach = MathF.Min(radius, CloudReach);
        var cloud = Particles(
            "Cloud", new Color(color, 0.7f), (int)Math.Clamp(reach / 3f, 12f, 40f), seconds, new Vector2(40f, 40f), BaseMaterial3D.BillboardModeEnum.Particles,
            new Vector3(0f, height, 0f), new Vector3(reach * 0.75f, 4f, reach * 0.75f), Vector3.Up, 0f, (0f, 0f), Vector3.Zero, (0.75f, 1.25f), blend: false);
        cloud.OneShot = true;
        cloud.Explosiveness = 1f;
        cloud.LocalCoords = true;
        var material = (StandardMaterial3D)((QuadMesh)cloud.DrawPass1).Material;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha;
        material.DistanceFadeMinDistance = CloudFadeNear;
        material.DistanceFadeMaxDistance = CloudFadeFar;
        return cloud;
    }

    /// <summary>Rain pouring from a cloud: long streaks falling fast.</summary>
    public static Node3D Rain(Color color, float radius, float height) => Particles(
        "Rain", color, (int)Math.Clamp(radius * 5f, 300f, 900f), 0.9, new Vector2(0.3f, 4f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, height, 0f), new Vector3(radius, 1f, radius), Vector3.Down, 4f, (40f, 55f), new Vector3(0f, -30f, 0f), (0.8f, 1.2f), blend: false);

    /// <summary>Grain or motes falling.</summary>
    public static Node3D Fall(Color color, float radius, float height, float size) => Particles(
        "Fall", color, (int)Math.Clamp(radius * 6f, 80f, 500f), 2.2, new Vector2(size, size), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, height, 0f), new Vector3(radius * 0.8f, 2f, radius * 0.8f), Vector3.Down, 10f, (4f, 9f), new Vector3(0f, -14f, 0f), (0.6f, 1.3f));

    /// <summary>Light (small bright motes) or shadow (broad dark wisps) rising in a spiral.</summary>
    public static Node3D Spiral(Color color, float radius, bool additive = true) => Particles(
        "Spiral", additive ? color : new Color(color, 0.45f), (int)Math.Clamp(radius * (additive ? 6f : 2f), 120f, 500f), 2.4, additive ? new Vector2(1.4f, 1.4f) : new Vector2(7f, 7f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, 0.5f, 0f), new Vector3(radius * 0.7f, 0.5f, radius * 0.7f), Vector3.Up, 6f, (5f, 9f), new Vector3(0f, 2f, 0f), (0.6f, 1.5f), tangential: 12f, blend: additive);

    /// <summary>Flames bursting up, with embers.</summary>
    public static Node3D Flames(float radius)
    {
        var flames = Particles("Flames", new Color(1f, 0.45f, 0.1f, 0.6f), (int)Math.Clamp(radius * 5f, 100f, 500f), 1.1, new Vector2(3.2f, 3.2f), BaseMaterial3D.BillboardModeEnum.Particles,
            new Vector3(0f, 0.5f, 0f), new Vector3(radius * 0.6f, 0.5f, radius * 0.6f), Vector3.Up, 18f, (6f, 14f), new Vector3(0f, 4f, 0f), (0.8f, 2.2f), endColor: new Color(0.9f, 0.12f, 0.03f, 0.3f));
        flames.AddChild(Particles("Embers", new Color(1f, 0.75f, 0.3f), 160, 2.4, new Vector2(0.35f, 0.35f), BaseMaterial3D.BillboardModeEnum.Particles,
            new Vector3(0f, 1f, 0f), new Vector3(radius * 0.5f, 0.5f, radius * 0.5f), Vector3.Up, 35f, (8f, 18f), new Vector3(0f, -2f, 0f), (0.6f, 1.2f)));
        return flames;
    }

    /// <summary>Smoke drifting up, dark and slow.</summary>
    public static Node3D Smoke(float radius) => Particles(
        "Smoke", new Color(0.22f, 0.2f, 0.19f, 0.3f), (int)Math.Clamp(radius * 1.5f, 30f, 120f), 3.5, new Vector2(9f, 9f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, 8f, 0f), new Vector3(radius * 0.5f, 1f, radius * 0.5f), Vector3.Up, 25f, (4f, 8f), new Vector3(1.5f, 1f, 0f), (1f, 2.5f), blend: false);

    /// <summary>A fountain of water springing up and falling back.</summary>
    public static Node3D Fountain(Color color, float radius) => Particles(
        "Fountain", color, 600, 2.2, new Vector2(0.9f, 0.9f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.3f, 1.2f), Vector3.Up, MathF.Min(25f, 8f + radius * 0.2f), (22f, 32f), new Vector3(0f, -24f, 0f), (0.6f, 1.4f));

    /// <summary>Dust heaving up off the ground in a burst.</summary>
    public static Node3D Dust(float radius) => Particles(
        "Dust", new Color(0.66f, 0.56f, 0.42f, 0.6f), (int)Math.Clamp(radius * 1.5f, 120f, 400f), 3.0, new Vector2(12f, 12f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, 1f, 0f), new Vector3(MathF.Min(radius, 200f) * 0.7f, 0.5f, MathF.Min(radius, 200f) * 0.7f), Vector3.Up, 40f, (2f, 6f), new Vector3(0f, -0.5f, 0f), (1f, 2.5f), blend: false, explosive: true);

    /// <summary>Sparks thrown out from a strike.</summary>
    public static Node3D Sparks(Color color) => Particles(
        "Sparks", color, 140, 0.9, new Vector2(0.4f, 0.4f), BaseMaterial3D.BillboardModeEnum.Particles,
        new Vector3(0f, 1f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Vector3.Up, 80f, (12f, 26f), new Vector3(0f, -25f, 0f), (0.5f, 1.2f), explosive: true);

    /// <summary>A glow that shines at its rim (miracle_glow.gdshader), faded out until the effect swells it in.</summary>
    private static ShaderMaterial Glow(Color color, float strength, float rim, float span, float topFade)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(GlowShaderPath) };
        material.SetShaderParameter("color", color);
        material.SetShaderParameter("strength", strength);
        material.SetShaderParameter("rim", rim);
        material.SetShaderParameter("span", span);
        material.SetShaderParameter("top_fade", topFade);
        material.SetShaderParameter("fade", 0f);
        return material;
    }

    /// <summary>
    /// Particles of soft quads, run on the GPU: a busy world casts many miracles at once, and CPU
    /// particles cost the main thread about a millisecond a frame at sixteen times speed.
    /// </summary>
    /// <summary>A blow landing (ADR-062): sparks and a puff of dust, fired once each time it is restarted.</summary>
    /// <returns>The sparks, with the dust as their child; neither emits until restarted.</returns>
    public static GpuParticles3D Impact()
    {
        var sparks = Particles(
            "Impact", new Color(1f, 0.86f, 0.55f), 70, 0.6, new Vector2(0.55f, 0.55f), BaseMaterial3D.BillboardModeEnum.Particles,
            Vector3.Zero, new Vector3(0.4f, 0.4f, 0.4f), Vector3.Up, 100f, (9f, 20f), new Vector3(0f, -22f, 0f), (0.6f, 1.3f), explosive: true);
        var dust = Particles(
            "ImpactDust", new Color(0.72f, 0.64f, 0.52f, 0.55f), 24, 1.3, new Vector2(4.5f, 4.5f), BaseMaterial3D.BillboardModeEnum.Particles,
            Vector3.Zero, new Vector3(0.6f, 0.6f, 0.6f), Vector3.Up, 80f, (2f, 6f), new Vector3(0f, -1f, 0f), (0.8f, 1.7f), blend: false, explosive: true);
        foreach (var burst in new[] { sparks, dust })
        {
            (burst.OneShot, burst.Emitting, burst.Explosiveness) = (true, false, 1f);
        }

        sparks.AddChild(dust);
        return sparks;
    }

    private static GpuParticles3D Particles(
        string name, Color color, int amount, double lifetime, Vector2 size, BaseMaterial3D.BillboardModeEnum billboard, Vector3 at, Vector3 box, Vector3 direction, float spread,
        (float Min, float Max) speed, Vector3 gravity, (float Min, float Max) scale, bool blend = true, float tangential = 0f, float radial = 0f, bool explosive = false, Color? endColor = null)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = blend ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
            BillboardMode = billboard,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = blend ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            AlbedoTexture = Soft,
            VertexColorUseAsAlbedo = true,

            // Dark smoke, dust and cloud keep their colour: the ramp's colours are sRGB, and the fog would pale them.
            VertexColorIsSrgb = !blend,
            DisableFog = true,
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(color, 0f));
        ramp.SetColor(1, new Color(endColor ?? color, 0f));
        ramp.AddPoint(0.15f, color);
        ramp.AddPoint(0.7f, endColor ?? color);

        // The process material is built whole before the particles take it (see FireView).
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = box,
            Direction = direction,
            Spread = spread,
            InitialVelocityMin = speed.Min,
            InitialVelocityMax = speed.Max,
            Gravity = gravity,
            TangentialAccelMin = tangential,
            TangentialAccelMax = tangential,
            RadialAccelMin = radial,
            RadialAccelMax = radial,
            ScaleMin = scale.Min,
            ScaleMax = scale.Max,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
        };
        var reach = new Vector3(box.X + 40f, 0f, box.Z + 40f);
        return new GpuParticles3D
        {
            Name = name,
            Amount = GraphicsScale.Of(amount),
            Lifetime = lifetime,
            Explosiveness = explosive ? 0.95f : 0.1f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = size, Material = material },
            Position = at,
            VisibilityAabb = new Aabb(new Vector3(-reach.X, -at.Y - 10f, -reach.Z), new Vector3(2f * reach.X, at.Y + 150f, 2f * reach.Z)),
            Emitting = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }
}
