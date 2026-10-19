using Godot;

namespace Pantheon.Client.Presentation.Disasters;

/// <summary>The nodes that draw the hazards of ADR-029 (presentation only): a tornado's funnel, a locust swarm and the volcano's plume.</summary>
internal static class HazardViews
{
    private const float FunnelHeight = 90f;

    private static readonly StandardMaterial3D FunnelMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = new Color(0.42f, 0.4f, 0.38f, 0.55f),
    };

    private static readonly StandardMaterial3D SoftMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Weather.SoftDiscTexture.Texture,
    };

    private static readonly StandardMaterial3D SwarmMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Weather.SoftDiscTexture.Texture,
        DisableFog = true,
    };

    private static readonly StandardMaterial3D GlowMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Weather.SoftDiscTexture.Texture,
    };

    /// <summary>A tornado: a grey funnel, wide at the cloud and narrow at the ground, with dust whirling at its foot.</summary>
    /// <param name="radius">How far around it tears things down, in metres.</param>
    /// <returns>The view; its origin is the funnel's foot.</returns>
    public static Node3D Tornado(float radius)
    {
        var view = new Node3D { Name = "Tornado" };
        view.AddChild(new MeshInstance3D
        {
            Name = "Funnel",
            Mesh = new CylinderMesh { TopRadius = radius * 0.6f, BottomRadius = radius * 0.08f, Height = FunnelHeight, RadialSegments = 20, Rings = 6, Material = FunnelMaterial },
            Position = new Vector3(0f, FunnelHeight / 2f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        var dust = Particles("Dust", 60, 2.5, new Vector3(radius * 0.4f, 2f, radius * 0.4f), SoftMaterial, new Vector2(14f, 14f), 4f, 12f, new Color(0.45f, 0.38f, 0.3f, 0.7f), new Color(0.5f, 0.45f, 0.4f, 0f));
        view.AddChild(dust);
        return view;
    }

    /// <summary>A locust swarm: a cloud of dark specks.</summary>
    /// <param name="radius">How far around it eats, in metres.</param>
    /// <returns>The swarm; its origin is the swarm's middle.</returns>
    public static GpuParticles3D Swarm(float radius)
    {
        var process = Process(Vector3.Zero, 1f, 6f, new Color(0.05f, 0.045f, 0.02f, 1f), new Color(0.1f, 0.08f, 0.03f, 0.85f));
        (process.EmissionShape, process.EmissionSphereRadius, process.Spread) = (ParticleProcessMaterial.EmissionShapeEnum.Sphere, radius * 0.25f, 180f);
        return Emitter("Locusts", 2000, 3.0, process, SwarmMaterial, new Vector2(2.6f, 2.6f), new Aabb(new Vector3(-radius, -radius, -radius), new Vector3(2f * radius, 2f * radius, 2f * radius)));
    }

    /// <summary>The volcano's plume: smoke rising from the crater, with glowing embers and a red light while it erupts.</summary>
    /// <param name="regionSize">Side of a region in metres.</param>
    /// <returns>The plume; its origin is the crater.</returns>
    public static Node3D Plume(float regionSize)
    {
        var view = new Node3D { Name = "Volcano" };
        view.AddChild(Particles("Smoke", 90, 14.0, new Vector3(regionSize * 0.2f, 4f, regionSize * 0.2f), SoftMaterial, new Vector2(70f, 70f), 6f, 10f, new Color(0.1f, 0.09f, 0.085f, 0.9f), new Color(0.32f, 0.31f, 0.3f, 0f)));
        view.AddChild(Particles("Embers", 50, 2.5, new Vector3(regionSize * 0.1f, 2f, regionSize * 0.1f), GlowMaterial, new Vector2(4f, 4f), 18f, 30f, new Color(1f, 0.42f, 0.08f, 0.7f), new Color(0.7f, 0.1f, 0.02f, 0f)));
        view.AddChild(new OmniLight3D { Name = "Glow", LightColor = new Color(1f, 0.35f, 0.1f), OmniRange = regionSize * 3f, ShadowEnabled = false, Position = new Vector3(0f, 20f, 0f) });
        return view;
    }

    /// <summary>Shows the plume for the volcano's phase: thin smoke while it rumbles, thick smoke, embers and glow while it erupts.</summary>
    /// <param name="plume">A plume from <see cref="Plume"/>.</param>
    /// <param name="erupting">Whether it erupts.</param>
    public static void ShowPlume(Node3D plume, bool erupting)
    {
        plume.GetNode<GpuParticles3D>("Smoke").AmountRatio = erupting ? 1f : 0.3f;
        plume.GetNode<GpuParticles3D>("Embers").Emitting = erupting;
        plume.GetNode<OmniLight3D>("Glow").LightEnergy = erupting ? 8f : 0f;
    }

    /// <summary>A looping particle emitter rising from a box.</summary>
    private static GpuParticles3D Particles(string name, int amount, double lifetime, Vector3 extents, StandardMaterial3D material, Vector2 size, float minSpeed, float maxSpeed, Color from, Color to) =>
        Emitter(name, amount, lifetime, Process(extents, minSpeed, maxSpeed, from, to), material, size, new Aabb(new Vector3(-extents.X - 60f, -20f, -extents.Z - 60f), new Vector3((2f * extents.X) + 120f, 400f, (2f * extents.Z) + 120f)));

    /// <summary>How particles rise from a box, coloured along their life.</summary>
    private static ParticleProcessMaterial Process(Vector3 extents, float minSpeed, float maxSpeed, Color from, Color to) => new()
    {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
        EmissionBoxExtents = extents,
        Direction = Vector3.Up,
        Spread = 25f,
        InitialVelocityMin = minSpeed,
        InitialVelocityMax = maxSpeed,
        Gravity = Vector3.Zero,
        ScaleMin = 0.6f,
        ScaleMax = 1.4f,
        ColorRamp = new GradientTexture1D { Gradient = new Gradient { Colors = [from, to], Offsets = [0f, 1f] } },
    };

    /// <summary>A looping emitter of camera-facing quads.</summary>
    private static GpuParticles3D Emitter(string name, int amount, double lifetime, ParticleProcessMaterial process, StandardMaterial3D material, Vector2 size, Aabb bounds) => new()
    {
        Name = name,
        Amount = amount,
        Lifetime = lifetime,
        ProcessMaterial = process,
        DrawPass1 = new QuadMesh { Size = size, Material = material },
        VisibilityAabb = bounds,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Emitting = true,
    };
}
