using System;
using Godot;
using Pantheon.Client.Presentation.Audio;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Bootstrap;

/// <summary>Puts the player's settings into effect (spec §105, ADR-021): the window, frame pacing, shadows, render scale and loudness.</summary>
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
    public static void Apply(GameSettings settings, Viewport viewport, DirectionalLight3D? sun, bool startup)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(viewport);
        var mode = settings.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != mode && !(startup && !settings.Fullscreen))
        {
            DisplayServer.WindowSetMode(mode);
        }

        if (!startup || !settings.VSync)
        {
            DisplayServer.WindowSetVsyncMode(settings.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }

        if (sun is not null)
        {
            sun.ShadowEnabled = settings.Shadows;
        }

        viewport.Scaling3DMode = settings.RenderScale < 1f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = settings.RenderScale;
        AudioPresenter.ApplyVolumes(settings);
    }
}
