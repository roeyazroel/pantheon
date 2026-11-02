using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>Fireflies over the land on warm, dry nights (ADR-080): a few sparks around where the camera looks, each winking in its own time.</summary>
internal sealed class FireflyView
{
    // How far around the camera's focus they fly, in metres, and how high.
    private const float Reach = 70f;
    private const float Height = 3f;

    private readonly GpuParticles3D _sparks;

    /// <summary>Creates the fireflies under a parent.</summary>
    /// <param name="parent">Node that holds the weather visuals.</param>
    public FireflyView(Node3D parent)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = Divine.MiracleLayers.Soft,
            DisableFog = true,
        };
        var (glow, dark) = (new Color(1.6f, 1.9f, 0.5f, 1f), new Color(1.6f, 1.9f, 0.5f, 0f));
        _sparks = new GpuParticles3D
        {
            Name = "Fireflies",
            Amount = GraphicsScale.Of(110),
            Lifetime = 3.2,
            Emitting = false,
            Visible = false,
            LocalCoords = false,
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(Reach, Height / 2f, Reach),
                Direction = Vector3.Up,
                Spread = 180f,
                InitialVelocityMin = 0.2f,
                InitialVelocityMax = 0.8f,
                Gravity = Vector3.Zero,
                ColorRamp = new GradientTexture1D { Gradient = new Gradient { Colors = [dark, glow, dark, glow, dark], Offsets = [0f, 0.2f, 0.45f, 0.7f, 1f] } },
            },
            DrawPass1 = new QuadMesh { Size = new Vector2(0.5f, 0.5f), Material = material },
            VisibilityAabb = new Aabb(new Vector3(-2000f, -200f, -2000f), new Vector3(4000f, 600f, 4000f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(_sparks);
    }

    /// <summary>Shows the fireflies over a place while the night is warm and dry.</summary>
    /// <param name="look">The land's look now.</param>
    /// <param name="ground">The ground the camera looks at.</param>
    public void Update(WorldLookState look, Vector3 ground)
    {
        var shown = DayClock.Night > 0.6f && look.Autumn + look.Winter < 0.5f && look.Overcast < 0.25f;
        if (_sparks.Visible != shown)
        {
            (_sparks.Visible, _sparks.Emitting) = (shown, shown);
        }

        if (shown)
        {
            _sparks.GlobalPosition = ground + (Vector3.Up * (Height / 2f));
        }
    }
}
