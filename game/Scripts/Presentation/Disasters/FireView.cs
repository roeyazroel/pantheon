using Godot;

namespace Pantheon.Client.Presentation.Disasters;

/// <summary>One burning region drawn (presentation only, ADR-014): flames, a column of smoke and an orange glow, all scaled by the fire's intensity.</summary>
internal sealed partial class FireView : Node3D
{
    private const float FlameHeight = 6f;
    private const float SmokeRise = 12f;

    private static readonly StandardMaterial3D FlameMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Weather.SoftDiscTexture.Texture,
    };

    private static readonly StandardMaterial3D SmokeMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Weather.SoftDiscTexture.Texture,
    };

    private GpuParticles3D _flames = null!;
    private GpuParticles3D _smoke = null!;
    private OmniLight3D _glow = null!;

    /// <summary>Creates the view of a burning region.</summary>
    /// <param name="ground">Ground point at the region's centre.</param>
    /// <param name="regionSize">Side of a region in metres.</param>
    /// <returns>The view, ready to be added to the tree.</returns>
    public static FireView Create(Vector3 ground, float regionSize)
    {
        var view = new FireView { Name = "Fire", Position = ground };
        var half = regionSize * 0.45f;
        view._flames = CreateParticles("Flames", 140, 1.2, new Vector3(half, 1f, half), FlameMaterial, new Vector2(10f, 14f), 5f, 10f, Gradient(new Color(1f, 0.85f, 0.4f, 0.95f), new Color(1f, 0.35f, 0.1f, 0f)));
        view._flames.Position = new Vector3(0f, FlameHeight * 0.3f, 0f);
        view._smoke = CreateParticles("Smoke", 40, 9.0, new Vector3(half * 0.8f, 2f, half * 0.8f), SmokeMaterial, new Vector2(34f, 34f), SmokeRise * 0.8f, SmokeRise * 1.2f, Gradient(new Color(0.22f, 0.2f, 0.19f, 0.6f), new Color(0.5f, 0.49f, 0.48f, 0f)));
        view._smoke.Position = new Vector3(0f, FlameHeight, 0f);
        view._glow = new OmniLight3D { Name = "Glow", LightColor = new Color(1f, 0.55f, 0.2f), OmniRange = regionSize * 1.4f, ShadowEnabled = false, Position = new Vector3(0f, 10f, 0f) };
        view.AddChild(view._flames);
        view.AddChild(view._smoke);
        view.AddChild(view._glow);
        return view;
    }

    /// <summary>Scales the flames, smoke and glow to the fire's intensity.</summary>
    /// <param name="intensity">Fire intensity (0..1).</param>
    public void Show(float intensity)
    {
        var ratio = Mathf.Clamp(0.25f + (0.75f * intensity), 0f, 1f);
        _flames.AmountRatio = ratio;
        _smoke.AmountRatio = ratio;
        _glow.LightEnergy = 6f * intensity;
    }

    /// <summary>A looping particle emitter rising from a box.</summary>
    private static GpuParticles3D CreateParticles(string name, int amount, double lifetime, Vector3 extents, StandardMaterial3D material, Vector2 size, float minSpeed, float maxSpeed, GradientTexture1D ramp)
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = extents,
            Direction = Vector3.Up,
            Spread = 12f,
            InitialVelocityMin = minSpeed,
            InitialVelocityMax = maxSpeed,
            Gravity = Vector3.Zero,
            ScaleMin = 0.6f,
            ScaleMax = 1.4f,
            ColorRamp = ramp,
        };
        return new GpuParticles3D
        {
            Name = name,
            Amount = GraphicsScale.Of(amount),
            Lifetime = lifetime,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = size, Material = material },
            VisibilityAabb = new Aabb(new Vector3(-extents.X - 20f, -5f, -extents.Z - 20f), new Vector3((2f * extents.X) + 40f, 120f, (2f * extents.Z) + 40f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Emitting = true,
        };
    }

    /// <summary>A colour ramp over a particle's life.</summary>
    private static GradientTexture1D Gradient(Color from, Color to) =>
        new() { Gradient = new Gradient { Colors = [from, to], Offsets = [0f, 1f] } };
}
