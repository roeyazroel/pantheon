using System;
using Godot;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Weather;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>
/// One weather cell drawn (presentation only, ADR-013): a cluster of soft puffs that follows the cell
/// smoothly, darkens with storms and fades as it dries, rain falling beneath it, and lightning.
/// Puffs fade out near the camera; up close, rain and the dimmed light show the weather.
/// </summary>
internal sealed partial class CloudView : Node3D
{
    // How quickly the view catches up with the simulated position (per second).
    private const float FollowRate = 3f;

    // Snow falls white and slow (ADR-028).
    private const float SnowSpeed = 0.25f;
    private static readonly Color SnowColor = new(0.96f, 0.97f, 1f, 0.85f);

    private StandardMaterial3D _material = null!;
    private StandardMaterial3D _streak = null!;
    private GpuParticles3D _rain = null!;
    private int _maxDrops;
    private long _lastStrikeSlot = long.MinValue;

    /// <summary>Id of the cell this view shows.</summary>
    public int CellId { get; private set; }

    /// <summary>Builds the view of a cell.</summary>
    /// <param name="seed">World seed, for the puff layout.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="view">Weather view settings.</param>
    /// <returns>The view, ready to be added to the tree.</returns>
    public static CloudView Create(WorldSeed seed, WeatherCell cell, WeatherViewSettings view)
    {
        var cloud = new CloudView { Name = "Cloud_" + cell.Id, CellId = cell.Id, Position = new Vector3(cell.X, 0f, cell.Z) };
        cloud.AddPuffs(seed, cell, view);
        cloud.AddRain(cell, view);
        return cloud;
    }

    /// <summary>Follows the cell, updates its look and rain, and reports a lightning strike when one falls.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="cell">The cell's current state.</param>
    /// <param name="rules">Weather rules.</param>
    /// <param name="view">Weather view settings.</param>
    /// <param name="seconds">Simulation time in seconds.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <returns>The ground offset of a new lightning strike, or <c>null</c>.</returns>
    public Vector2? Update(WorldSeed seed, WeatherCell cell, WeatherRules rules, WeatherViewSettings view, double seconds, float delta)
    {
        var target = new Vector3(cell.X, 0f, cell.Z);
        Position = Position.Lerp(target, 1f - MathF.Exp(-FollowRate * delta));
        var (color, opacity) = CloudLayout.Look(cell, view);
        _material.AlbedoColor = color.ToGodot(opacity);
        var drops = CloudLayout.RainDrops(cell, view);
        var snowing = WeatherModel.Classify(rules, cell) == WeatherKind.Snow;
        _streak.AlbedoColor = snowing ? SnowColor : view.RainColor.ToGodot(0.55f);
        _rain.SpeedScale = snowing ? SnowSpeed : 1f;
        _rain.Emitting = drops > 0;
        _rain.AmountRatio = _maxDrops > 0 ? Math.Clamp((float)drops / _maxDrops, 0f, 1f) : 0f;

        if (CloudLayout.Lightning(seed, cell, rules, view, seconds) is not { } strike || strike.Slot == _lastStrikeSlot)
        {
            return null;
        }

        _lastStrikeSlot = strike.Slot;
        return new Vector2(strike.OffsetX, strike.OffsetZ);
    }

    /// <summary>The puffs, as soft camera-facing sprites in one MultiMesh sharing one fading material.</summary>
    private void AddPuffs(WorldSeed seed, WeatherCell cell, WeatherViewSettings view)
    {
        _material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
            AlbedoTexture = SoftDiscTexture.Texture,
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
            DistanceFadeMinDistance = view.FadeNearDistance,
            DistanceFadeMaxDistance = view.FadeFarDistance,
            AlbedoColor = view.ClearColor.ToGodot(0f),

            // Clouds stay their own colour through the haze, and melt into hills instead of cutting them.
            DisableFog = true,
            ProximityFadeEnabled = true,
            ProximityFadeDistance = 60f,
        };
        var puffs = CloudLayout.Puffs(seed, cell, view);
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new QuadMesh { Size = Vector2.One, Material = _material },
            InstanceCount = puffs.Count,
        };
        for (var i = 0; i < puffs.Count; i++)
        {
            var puff = puffs[i];
            var basis = Basis.FromScale(new Vector3(puff.Size, puff.Size * 0.7f, 1f));
            multimesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3(puff.OffsetX, puff.Height, puff.OffsetZ)));
        }

        AddChild(new MultiMeshInstance3D { Name = "Puffs", Multimesh = multimesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Rain streaks falling from the cloud base to the sea, as many as the cell's size allows.</summary>
    private void AddRain(WeatherCell cell, WeatherViewSettings view)
    {
        var size = MathF.Min(1f, cell.Radius / view.FullRainRadius);
        _maxDrops = Math.Max(1, (int)MathF.Round(view.MaxRainDrops * size * size));
        var fall = view.CloudHeight;
        var speed = 60f;
        var spread = cell.Radius * 0.65f;
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(spread, 25f, spread),
            Direction = Vector3.Down,
            Spread = 0f,
            InitialVelocityMin = speed * 0.9f,
            InitialVelocityMax = speed * 1.1f,
            Gravity = Vector3.Zero,
        };
        // Streaks are seen up close only: from afar thousands of them would merge into a grey sheet.
        _streak = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
            AlbedoColor = view.RainColor.ToGodot(0.55f),
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
            DistanceFadeMinDistance = view.FadeFarDistance + 150f,
            DistanceFadeMaxDistance = view.FadeNearDistance + 150f,
        };
        _rain = new GpuParticles3D
        {
            Name = "Rain",
            Amount = GraphicsScale.Of(_maxDrops),
            Lifetime = fall / speed,
            Position = new Vector3(0f, view.CloudHeight, 0f),
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.35f, 6f), Material = _streak },
            LocalCoords = false,
            VisibilityAabb = new Aabb(new Vector3(-cell.Radius, -fall - 10f, -cell.Radius), new Vector3(2f * cell.Radius, fall + 20f, 2f * cell.Radius)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Emitting = false,
        };
        AddChild(_rain);
    }
}
