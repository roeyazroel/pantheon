using System;
using Godot;
using Pantheon.Game.Angel;
using Pantheon.Game.Presentation.Angel;

namespace Pantheon.Client.Presentation.Angel;

/// <summary>
/// The angel's figure (ADR-074), drawn in a little world of its own so the island's hills never cut
/// through it: its body grown in code under its shader, a key light, a rim light in its colour, a glow
/// about it, and motes of light or embers as it leans. The card shows the picture beside the god's
/// hand. Its look eases from one to the next as its god's deeds change it.
/// </summary>
internal sealed partial class AngelFigure : SubViewport
{
    // How fast the look eases towards its god's deeds, a second.
    private const float Ease = 0.8f;

    private ShaderMaterial _material = null!;
    private Node3D _pivot = null!;
    private OmniLight3D _glow = null!;
    private DirectionalLight3D _rim = null!;
    private GpuParticles3D _motes = null!;
    private GpuParticles3D _embers = null!;
    private float _lean;
    private float _formed;
    private float _conflict;
    private float _beat;
    private float _time;

    /// <summary>How much of it shows, from 0 (gone) to 1.</summary>
    public float Appear { get; set; }

    /// <summary>Which way it turns: towards the god's hand, −1 on its left, 1 on its right.</summary>
    public float Facing { get; set; }

    /// <summary>Makes the figure.</summary>
    /// <param name="pixels">The picture's width and height.</param>
    /// <returns>The figure, its picture drawn every frame.</returns>
    public static AngelFigure Create(int pixels)
    {
        var figure = new AngelFigure
        {
            Name = "AngelFigure",
            Size = new Vector2I(pixels, pixels),
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = UpdateMode.Always,
        };
        figure.Build();
        return figure;
    }

    /// <summary>Eases its look towards a new one, and moves its wings, its lights and its motes.</summary>
    /// <param name="look">How its god's deeds make it look.</param>
    /// <param name="delta">Real seconds since the last frame.</param>
    public void Show(AngelLook look, float delta)
    {
        var step = 1f - MathF.Exp(-Ease * 4f * delta);
        (_lean, _formed, _conflict) = (Mathf.Lerp(_lean, look.Lean, step), Mathf.Lerp(_formed, look.Formed, step), Mathf.Lerp(_conflict, look.Conflict, step));
        _time += delta;
        _beat += delta * (1.5f + (0.5f * Mathf.Max(_lean, 0f)));
        _material.SetShaderParameter("lean", _lean);
        _material.SetShaderParameter("formed", _formed);
        _material.SetShaderParameter("conflict", _conflict);
        _material.SetShaderParameter("marks", (int)look.Marks);
        _material.SetShaderParameter("appear", Appear);
        _material.SetShaderParameter("wingbeat", _beat);
        _pivot.Position = new Vector3(0f, 0.05f * MathF.Sin(_time * 1.2f), 0f);
        _pivot.Rotation = new Vector3(0f, (0.18f * Facing) + (0.06f * MathF.Sin(_time * 0.5f)), 0.02f * MathF.Sin(_time * 0.9f));
        var light = Light(_lean);
        _glow.LightColor = light;
        _glow.LightEnergy = (0.25f + (0.9f * MathF.Abs(_lean) * (0.4f + (0.6f * _formed)))) * Appear;
        _rim.LightColor = light;
        _rim.LightEnergy = (0.9f + (0.8f * MathF.Abs(_lean))) * Appear;
        _motes.Emitting = Appear > 0.5f && _lean > 0.12f;
        _embers.Emitting = Appear > 0.5f && _lean < -0.12f;
    }

    /// <summary>The colour of its light: silver when unformed, gold as it grows kind, red as it falls.</summary>
    /// <param name="lean">Its lean.</param>
    /// <returns>The colour.</returns>
    public static Color Light(float lean) => lean >= 0f
        ? new Color(0.78f, 0.82f, 0.92f).Lerp(new Color(1f, 0.86f, 0.5f), Mathf.Clamp(lean * 1.6f, 0f, 1f))
        : new Color(0.78f, 0.82f, 0.92f).Lerp(new Color(1f, 0.3f, 0.12f), Mathf.Clamp(-lean * 1.6f, 0f, 1f));

    /// <summary>Builds its little world: the camera, the lights, the body and the motes.</summary>
    private void Build()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.64f, 0.72f),
            AmbientLightEnergy = 0.22f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        var camera = new Camera3D { Fov = 32f, Current = true };
        AddChild(camera);
        camera.Position = new Vector3(0f, 1.5f, 7.3f);
        camera.LookAtFromPosition(camera.Position, new Vector3(0f, 1.5f, 0f), Vector3.Up);
        var key = new DirectionalLight3D { LightColor = new Color(1f, 0.95f, 0.88f), LightEnergy = 1.05f };
        AddChild(key);
        key.RotationDegrees = new Vector3(-38f, 52f, 0f);
        var fill = new DirectionalLight3D { LightColor = new Color(0.7f, 0.78f, 1f), LightEnergy = 0.3f };
        AddChild(fill);
        fill.RotationDegrees = new Vector3(-10f, -60f, 0f);
        _rim = new DirectionalLight3D { LightEnergy = 1.4f };
        AddChild(_rim);
        _rim.RotationDegrees = new Vector3(-18f, 160f, 0f);
        _pivot = new Node3D { Name = "Pivot" };
        AddChild(_pivot);
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/angel.gdshader") };
        _material.SetShaderParameter("shoulder", AngelMesh.Shoulder.ToGodot());
        _pivot.AddChild(new MeshInstance3D { Mesh = Meshes.ToArrayMesh(AngelMesh.Build(), _material), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // The glow is behind the figure, to shine through its wings and edge its body.
        _glow = new OmniLight3D { OmniRange = 3f, Position = new Vector3(0f, 1.9f, -0.7f) };
        _pivot.AddChild(_glow);
        _motes = Particles(new Color(1f, 0.93f, 0.7f), new Vector3(0f, 0.15f, 0f));
        _embers = Particles(new Color(1f, 0.42f, 0.14f), new Vector3(0f, 0.45f, 0f));
        _pivot.AddChild(_motes);
        _pivot.AddChild(_embers);
    }

    /// <summary>Motes that drift up about the figure, fading in and out.</summary>
    private static GpuParticles3D Particles(Color color, Vector3 rise)
    {
        var fade = new Gradient { Offsets = [0f, 0.2f, 0.75f, 1f], Colors = [color with { A = 0f }, color, color, color with { A = 0f }] };
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1.3f, 1.2f, 0.4f),
            Gravity = rise,
            Direction = Vector3.Up,
            Spread = 50f,
            InitialVelocityMin = 0.02f,
            InitialVelocityMax = 0.2f,
            ScaleMin = 0.5f,
            ScaleMax = 1.3f,
            ColorRamp = new GradientTexture1D { Gradient = fade },
        };
        var draw = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoTexture = Dot(),
        };
        return new GpuParticles3D
        {
            Amount = 36,
            Lifetime = 3.5,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = draw },
            Position = new Vector3(0f, 1.4f, 0.1f),
            VisibilityAabb = new Aabb(new Vector3(-2.5f, -2.5f, -2.5f), new Vector3(5f, 5f, 5f)),
            Emitting = false,
        };
    }

    /// <summary>A soft round dot, white, fading to its edge.</summary>
    private static ImageTexture Dot()
    {
        const int Size = 32;
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var d = new Vector2(x - ((Size - 1) / 2f), y - ((Size - 1) / 2f)).Length() / (Size / 2f);
                var alpha = Mathf.Clamp(1f - d, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
