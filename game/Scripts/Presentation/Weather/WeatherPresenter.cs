using System.Collections.Generic;
using Godot;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>
/// Draws the weather (ADR-013): one <see cref="CloudView"/> per weather cell, created when a cell
/// appears and freed when it is gone, and lightning strikes. It only reads <see cref="WorldState"/>.
/// </summary>
public sealed class WeatherPresenter
{
    private const float StrikeSeconds = 0.7f;

    // How quickly the light follows the weather overhead (per second).
    private const float GloomRate = 1.5f;

    // How much harder a strong wind carries what drifts on it.
    private const float BreezeDrift = 4f;

    private readonly Node3D _clouds;
    private readonly Node3D _strikes;
    private readonly WeatherViewSettings _view;
    private readonly WeatherRules _rules;
    private readonly Dictionary<int, CloudView> _views = [];
    private readonly HashSet<int> _seen = [];
    private readonly DirectionalLight3D? _sun;
    private readonly Godot.Environment? _environment;
    private readonly float _skyEnergy;
    private readonly float _haze;
    private readonly float _lowHaze;
    private readonly WorldLookView _look = new();
    private readonly FireflyView _fireflies;
    private readonly DaylightView _daylight;
    private TerrainField? _terrain;
    private readonly Divine.MapOverlay _snow;
    private float _snowWorldSize;
    private double _snowAge = SnowRefreshSeconds;

    // The snow on the land changes slowly: it is redrawn this often.
    private const double SnowRefreshSeconds = 1.0;

    /// <summary>Creates the presenter's nodes under a parent.</summary>
    /// <param name="parent">Node that holds the weather visuals.</param>
    /// <param name="view">Weather view settings.</param>
    /// <param name="rules">Weather rules, for when a cell is a thunderstorm.</param>
    /// <param name="day">How the day passes (ADR-079).</param>
    /// <param name="sun">The light of the sun and moon, dimmed under storms; <c>null</c> for none.</param>
    /// <param name="environment">The scene's environment, whose sky and ambient light an eclipse dims (ADR-034); <c>null</c> for none.</param>
    public WeatherPresenter(Node3D parent, WeatherViewSettings view, WeatherRules rules, DayNightSettings day, DirectionalLight3D? sun, Godot.Environment? environment = null)
    {
        _clouds = parent.AddLayer("Clouds");
        _strikes = parent.AddLayer("Lightning");
        _snow = new Divine.MapOverlay(parent.AddLayer("Snow"));
        _view = view;
        _rules = rules;
        _sun = sun;
        _environment = environment;
        _skyEnergy = environment?.BackgroundEnergyMultiplier ?? 1f;
        _haze = environment?.FogDensity ?? 0f;
        _lowHaze = environment?.FogHeightDensity ?? 0f;
        _fireflies = new FireflyView(parent);
        _daylight = new DaylightView(day, sun, environment);
    }

    /// <summary>Forgets the old world's clouds.</summary>
    /// <param name="terrain">Terrain of the new world, for where lightning strikes.</param>
    public void Rebuild(TerrainField terrain)
    {
        _terrain = terrain;
        _clouds.FreeChildren();
        _strikes.FreeChildren();
        _views.Clear();
        _snowWorldSize = 0f;
        _snowAge = SnowRefreshSeconds;
    }

    /// <summary>Brings the clouds in line with the weather, plays new lightning strikes and dims the sun under storms.</summary>
    /// <param name="world">Current world.</param>
    /// <param name="seconds">Simulation time in seconds.</param>
    /// <param name="delta">Frame time in seconds.</param>
    /// <param name="focus">The ground point the camera looks at.</param>
    public void Sync(WorldState world, double seconds, float delta, GroundPosition focus)
    {
        SyncSnow(world, delta);
        var daylight = _daylight.Update(world.Tick, delta);
        var look = WorldLook.Of(_rules, world, focus);
        var breeze = Breeze.At(world.Weather, focus);
        _look.Update(look, _view, breeze, delta);
        DayClock.Wind = new Vector2(breeze.X, breeze.Z) * (1f + (BreezeDrift * breeze.Strength));
        _fireflies.Update(look, new Vector3(focus.X, _terrain is null ? 0f : Mathf.Max(_terrain.SampleHeight(focus.X, focus.Z), 0f), focus.Z));
        if (_sun is not null)
        {
            _sun.LightColor = daylight.Light.ToGodot().Lerp(_view.StormLight.ToGodot(), _look.Overcast);
            var target = daylight.Energy * (1f - (_view.StormGloom * CloudLayout.Gloom(_rules, world.Weather, focus)))
                * (1f - (_view.EclipseDarkness * Pantheon.Game.GreatEvents.GreatEventText.Darkness(world)));
            _sun.LightEnergy = Mathf.Lerp(_sun.LightEnergy, target, 1f - Mathf.Exp(-GloomRate * delta));
        }

        if (_environment is not null)
        {
            // An eclipse darkens the sky and its light on the land too (ADR-034).
            var eclipse = 1f - (_view.EclipseDarkness * Pantheon.Game.GreatEvents.GreatEventText.Darkness(world));
            _environment.AmbientLightEnergy = daylight.Ambient * eclipse;
            _environment.FogLightColor = daylight.Haze.ToGodot();
            _environment.BackgroundEnergyMultiplier = _skyEnergy * eclipse;
            _environment.FogDensity = _haze * (1f + ((_view.StormHaze - 1f) * _look.Overcast));
            _environment.FogHeightDensity = _lowHaze * (1f + daylight.Mist);
        }

        _seen.Clear();
        foreach (var cell in world.Weather.Cells)
        {
            _seen.Add(cell.Id);
            if (!_views.TryGetValue(cell.Id, out var cloud))
            {
                cloud = CloudView.Create(world.Seed, cell, _view);
                _views[cell.Id] = cloud;
                _clouds.AddChild(cloud);
            }

            if (cloud.Update(world.Seed, cell, _rules, _view, seconds, delta) is { } offset)
            {
                Strike(cloud.Position.X + offset.X, cloud.Position.Z + offset.Y);
            }
        }

        foreach (var id in new List<int>(_views.Keys))
        {
            if (!_seen.Contains(id))
            {
                _views[id].QueueFree();
                _views.Remove(id);
            }
        }
    }

    /// <summary>Drapes the snow lying on the land over the terrain, redrawn once a second (ADR-028).</summary>
    private void SyncSnow(WorldState world, float delta)
    {
        var size = world.Regions.Columns * world.Regions.RegionSize;
        if (size != _snowWorldSize)
        {
            _snowWorldSize = size;
            _snow.Fit(size);
        }

        _snowAge += delta;
        if (_snowAge >= SnowRefreshSeconds)
        {
            _snowAge = 0.0;
            _snow.Show(SnowCover.Build(world));
        }
    }

    /// <summary>A lightning bolt from the cloud base to the ground.</summary>
    private void Strike(float x, float z)
    {
        var ground = _terrain is null ? 0f : Mathf.Max(_terrain.SampleHeight(x, z), 0f);
        var look = new MiracleEffectView { MiracleId = "storm", Style = MiracleEffectStyle.Bolt, Color = _view.LightningColor, Seconds = StrikeSeconds };
        _strikes.AddChild(MiracleEffect.Create(look, new Vector3(x, ground, z), 10f, _view.CloudHeight - ground));
    }
}
