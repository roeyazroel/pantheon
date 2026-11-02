using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Settlements;

/// <summary>
/// Smoke from a village's hearths (ADR-080): one set of particles rising from about half its roofs, as
/// thick as the hour's cooking and the cold call for, and drifting on the wind. Far villages draw none.
/// </summary>
internal sealed class HearthView
{
    // How far from the camera smoke is drawn, in metres.
    private const float Reach = 900f;

    // The most roofs that smoke, and the puffs a roof keeps in the air.
    private const int MostRoofs = 64;
    private const int PuffsPerRoof = 7;

    // A puff's life, in seconds, and the room its smoke needs, in metres.
    private const double Lifetime = 8.0;
    private const float Room = 120f;

    // How much of the wind a puff takes: from above a straight column would hide itself, so the wind must lay it out.
    private const float Carried = 1.2f;

    private static readonly StandardMaterial3D Puff = new()
    {
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = Divine.MiracleLayers.Soft,
        Roughness = 1f,
        DisableFog = true,
    };

    private static readonly CurveTexture Swell = new() { Curve = NewSwell() };

    private readonly GpuParticles3D _smoke;
    private readonly Vector3 _center;

    /// <summary>Creates the empty smoke under <paramref name="root"/>.</summary>
    /// <param name="root">The settlement's node.</param>
    /// <param name="center">The village's middle.</param>
    public HearthView(Node3D root, Vector3 center)
    {
        _center = center;
        _smoke = new GpuParticles3D
        {
            Name = "Hearths",
            Position = center,
            Lifetime = Lifetime,
            Emitting = false,
            Visible = false,
            DrawPass1 = new QuadMesh { Size = new Vector2(3.8f, 3.8f), Material = Puff },
            VisibilityAabb = new Aabb(new Vector3(-Room, -10f, -Room), new Vector3(2f * Room, Room, 2f * Room)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(_smoke);
    }

    /// <summary>Lays the smoke over a village's roofs.</summary>
    /// <param name="houses">Its houses.</param>
    /// <param name="roof">The height smoke rises from, on a house of scale 1.</param>
    public void Show(IReadOnlyList<HutPlacement> houses, float roof)
    {
        var (points, count) = RoofPoints(houses, roof);
        if (count == 0)
        {
            _smoke.Emitting = false;
            _smoke.Visible = false;
            return;
        }

        // Made whole before it is handed over: changing one after leaks its shader (see ForestPresenter's notes).
        _smoke.ProcessMaterial = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points,
            EmissionPointTexture = points,
            EmissionPointCount = count,
            Direction = Vector3.Up,
            Spread = 6f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.2f,
            Gravity = new Vector3(0f, 0.1f, 0f),
            ScaleMin = 0.8f,
            ScaleMax = 1.4f,
            ScaleCurve = Swell,
            ColorRamp = new GradientTexture1D { Gradient = new Gradient { Colors = [new Color(0.84f, 0.83f, 0.82f, 0f), new Color(0.82f, 0.81f, 0.8f, 0.6f), new Color(0.88f, 0.88f, 0.9f, 0f)], Offsets = [0f, 0.15f, 1f] } },
        };
        _smoke.Amount = GraphicsScale.Of(count * PuffsPerRoof);
    }

    /// <summary>Thickens or thins the smoke for the hour, and hides it far from the camera.</summary>
    /// <param name="camera">The camera's place.</param>
    public void Update(Vector3 camera)
    {
        var process = _smoke.ProcessMaterial as ParticleProcessMaterial;
        var shown = DayClock.Hearths > 0.02f && process is not null && camera.DistanceTo(_center) < Reach;
        if (_smoke.Visible != shown)
        {
            (_smoke.Visible, _smoke.Emitting) = (shown, shown);
        }

        if (shown)
        {
            _smoke.AmountRatio = DayClock.Hearths;
            process!.Gravity = new Vector3(DayClock.Wind.X * Carried, 0.1f, DayClock.Wind.Y * Carried);
        }
    }

    /// <summary>A texture of the points smoke rises from, relative to the village's middle, and how many there are.</summary>
    private (ImageTexture Points, int Count) RoofPoints(IReadOnlyList<HutPlacement> houses, float roof)
    {
        var points = new List<Vector3>();
        for (var i = 0; i < houses.Count && points.Count < MostRoofs; i += 2)
        {
            var house = houses[i];
            points.Add(new Vector3(house.Position.X - _center.X, house.Position.Y - _center.Y + (roof * house.Scale), house.Position.Z - _center.Z));
        }

        if (points.Count == 0)
        {
            return (new ImageTexture(), 0);
        }

        var bytes = new byte[points.Count * 12];
        for (var i = 0; i < points.Count; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 12), points[i].X);
            BitConverter.TryWriteBytes(bytes.AsSpan((i * 12) + 4), points[i].Y);
            BitConverter.TryWriteBytes(bytes.AsSpan((i * 12) + 8), points[i].Z);
        }

        return (ImageTexture.CreateFromImage(Image.CreateFromData(points.Count, 1, false, Image.Format.Rgbf, bytes)), points.Count);
    }

    private static Curve NewSwell()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0.35f));
        curve.AddPoint(new Vector2(1f, 1f));
        return curve;
    }
}
