using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>Hands the world's look to the shaders of the land (ADR-078), eased so that it never jumps, and drifts the clouds' shadows on the wind.</summary>
internal sealed class WorldLookView
{
    // How fast the look eases towards what it should be.
    private const float Rate = 1.5f;

    // The clouds' noise in the shader is this many tiles a metre (world_look.gdshaderinc).
    private const float CloudTiles = 0.00038f;

    private WorldLookState? _shown;
    private Vector2 _drift;

    /// <summary>Creates the view and hands the shaders their noise.</summary>
    public WorldLookView() => RenderingServer.GlobalShaderParameterSet("look_clouds", LookTextures.Clouds);

    /// <summary>How dark the sky is now, 0..1.</summary>
    public float Overcast => _shown?.Overcast ?? 0f;

    /// <summary>Moves the look towards a target and hands it to the shaders.</summary>
    /// <param name="target">The look there should be.</param>
    /// <param name="view">Weather view settings.</param>
    /// <param name="wind">The wind where the camera looks.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Update(WorldLookState target, WeatherViewSettings view, (float Strength, float X, float Z) wind, float delta)
    {
        var step = 1f - Mathf.Exp(-Rate * delta);
        var look = _shown is { } was
            ? new WorldLookState(Mathf.Lerp(was.Spring, target.Spring, step), Mathf.Lerp(was.Autumn, target.Autumn, step), Mathf.Lerp(was.Winter, target.Winter, step), Mathf.Lerp(was.Overcast, target.Overcast, step))
            : target;
        _shown = look;
        _drift += new Vector2(wind.X, wind.Z) * (view.CloudDrift * CloudTiles * delta);
        _drift = new Vector2(Mathf.PosMod(_drift.X, 1f), Mathf.PosMod(_drift.Y, 1f));
        var cover = Mathf.Lerp(view.ClearCloudCover, view.OvercastCloudCover, look.Overcast);
        RenderingServer.GlobalShaderParameterSet("look_season", new Vector4(look.Spring, look.Autumn, look.Winter, 0f));
        RenderingServer.GlobalShaderParameterSet("look_sky", new Vector4(cover, look.Overcast, _drift.X, _drift.Y));
    }
}
