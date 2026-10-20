using System;
using Godot;
using Pantheon.Client.Presentation.Audio;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Puts the player's settings into effect (spec §105, ADR-021, ADR-056): the window, frame pacing,
/// shadows, render scale, particles, the simulation's budget, loudness and the camera's controls.
/// </summary>
internal static class SettingsApplier
{
    /// <summary>Applies the settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="viewport">The main viewport, for the render scale.</param>
    /// <param name="sun">The sun, for shadows, if there is one.</param>
    /// <param name="startup">
    /// Whether the game is starting: then vsync is only turned off, never on, so the engine's own
    /// <c>--disable-vsync</c> for measuring still holds.
    /// </param>
    /// <param name="environment">The world's environment, for the lighting effects.</param>
    /// <param name="session">The session, for the simulation's budget.</param>
    /// <param name="camera">The god camera, for the controls (ADR-058).</param>
    public static void Apply(GameSettings settings, Viewport viewport, DirectionalLight3D? sun, bool startup, Godot.Environment? environment = null, Pantheon.Game.GameSession? session = null, Camera.GodCameraController? camera = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(viewport);
        var mode = settings.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != mode && !(startup && !settings.Fullscreen))
        {
            DisplayServer.WindowSetMode(mode);
        }

        if (startup && !settings.Fullscreen && DisplayServer.GetName() != "headless")
        {
            FitWindow();
        }

        if (viewport is Window window && !Mathf.IsEqualApprox(window.ContentScaleFactor, settings.InterfaceScale))
        {
            window.ContentScaleFactor = settings.InterfaceScale;
        }

        if (!startup || !settings.VSync)
        {
            DisplayServer.WindowSetVsyncMode(settings.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }

        if (sun is not null)
        {
            sun.ShadowEnabled = settings.Shadows;
        }

        if (environment is not null)
        {
            environment.SsaoEnabled = settings.LightingEffects;
            environment.GlowEnabled = settings.LightingEffects;
        }

        Presentation.GraphicsScale.Particles = settings.Particles;
        if (session is not null)
        {
            session.Scheduler.BudgetSeconds = settings.SimulationBudget / 1000.0;
        }

        if (camera is not null)
        {
            camera.EdgeScrollEnabled = settings.EdgeScroll;
            camera.ScrollZooms = settings.TrackpadScrollZooms;
            camera.Speed = settings.CameraSpeed;
        }

        viewport.Scaling3DMode = settings.RenderScale < 1f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = settings.RenderScale;
        AudioPresenter.ApplyVolumes(settings);
    }

    /// <summary>
    /// Opens the window at nine tenths of the screen it is on, at 16:9 and centred (ADR-059). The
    /// project's 1600 by 900 is in physical pixels, a quarter of a Retina screen; the interface
    /// stretches with the window.
    /// </summary>
    private static void FitWindow()
    {
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        var width = (int)(usable.Size.X * 0.9f);
        var height = width * 9 / 16;
        if (height > usable.Size.Y * 0.9f)
        {
            height = (int)(usable.Size.Y * 0.9f);
            width = height * 16 / 9;
        }

        var size = new Vector2I(width, height);
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + ((usable.Size - size) / 2));
    }
}
