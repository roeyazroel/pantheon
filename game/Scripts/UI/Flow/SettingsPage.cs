using System;
using Godot;
using Pantheon.Game.Settings;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The settings (spec §105, ADR-021, ADR-056, ADR-058, ADR-059, ADR-065, ADR-068) in six tabs:
/// graphics, sound, controls, the keys, the interface and the gods' minds. Each change is kept in the
/// profile and most apply at once.
/// </summary>
internal static class SettingsPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, holding the settings and reporting changes.</param>
    /// <param name="tab">The tab shown first, by name; the first when <c>null</c>.</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu, string? tab = null)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(UiKit.Heading("Settings"));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(600, 430) };
        column.AddChild(tabs);

        var graphics = Tab(tabs, "Graphics");
        Toggle(menu, graphics, "Fullscreen", s => s.Fullscreen, (s, on) => s with { Fullscreen = on });
        Toggle(menu, graphics, "Vertical sync", s => s.VSync, (s, on) => s with { VSync = on });
        Toggle(menu, graphics, "Shadows", s => s.Shadows, (s, on) => s with { Shadows = on });
        Toggle(menu, graphics, "Lighting effects", s => s.LightingEffects, (s, on) => s with { LightingEffects = on });
        Slide(menu, graphics, "Render scale", s => s.RenderScale, (s, v) => s with { RenderScale = v }, GameSettings.MinRenderScale, 1f);
        Slide(menu, graphics, "Particles", s => s.Particles, (s, v) => s with { Particles = v }, GameSettings.MinParticles, 1f);
        Slide(menu, graphics, "Citizens shown", s => s.Citizens, (s, v) => s with { Citizens = v }, GameSettings.MinCitizens, 1f);
        Slide(menu, graphics, "Vegetation", s => s.Vegetation, (s, v) => s with { Vegetation = v }, GameSettings.MinVegetation, 1f);
        Slide(menu, graphics, "Terrain quality", s => s.TerrainQuality, (s, v) => s with { TerrainQuality = v }, GameSettings.MinTerrainQuality, 1f);
        Toggle(menu, graphics, "Simple terrain", s => s.SimpleTerrain, (s, on) => s with { SimpleTerrain = on });
        graphics.AddChild(UiKit.Text("Citizens, vegetation, terrain quality and simple terrain take effect when the game next starts. A lower render scale draws faster on a large screen; simple terrain suits a computer the detailed land is slow on.", UiTheme.CaptionSize, UiKit.Muted));
        Slide(menu, graphics, "Simulation budget (ms)", s => s.SimulationBudget, (s, v) => s with { SimulationBudget = v }, GameSettings.MinSimulationBudget, GameSettings.MaxSimulationBudget, 1.0);

        var sound = Tab(tabs, "Sound");
        Slide(menu, sound, "Volume", s => s.MasterVolume, (s, v) => s with { MasterVolume = v }, 0f, 1f);
        Slide(menu, sound, "Music", s => s.MusicVolume, (s, v) => s with { MusicVolume = v }, 0f, 1f);
        Slide(menu, sound, "Effects", s => s.EffectsVolume, (s, v) => s with { EffectsVolume = v }, 0f, 1f);
        Slide(menu, sound, "Wind and rain", s => s.AmbienceVolume, (s, v) => s with { AmbienceVolume = v }, 0f, 1f);

        var controls = Tab(tabs, "Controls");
        Toggle(menu, controls, "Pan at the window's edge", s => s.EdgeScroll, (s, on) => s with { EdgeScroll = on });
        Toggle(menu, controls, "Trackpad scrolling zooms", s => s.TrackpadScrollZooms, (s, on) => s with { TrackpadScrollZooms = on });
        Slide(menu, controls, "Camera speed", s => s.CameraSpeed, (s, v) => s with { CameraSpeed = v }, GameSettings.MinCameraSpeed, GameSettings.MaxCameraSpeed);
        controls.AddChild(UiKit.Text("Drag the land with the left button to move, the right button to turn and tilt, and the wheel or a pinch to zoom. On a trackpad two fingers move the view, and Option with two fingers turns it.", UiTheme.CaptionSize, UiKit.Muted));

        Tab(tabs, "Keys").AddChild(KeysPage.Build(Bootstrap.DebugTools.Of(menu.Settings), 380f));

        var view = Tab(tabs, "Interface");
        Slide(menu, view, "Interface size", s => s.InterfaceScale, (s, v) => s with { InterfaceScale = v }, GameSettings.MinInterfaceScale, GameSettings.MaxInterfaceScale);
        Toggle(menu, view, "Debug tools", Bootstrap.DebugTools.Of, (s, on) => s with { DebugTools = on });
        view.AddChild(UiKit.Text("The debug tools try the game out: their keys (F4 for power, J for the council, K to meet a Creature and more), a Debug button on the god's bar with all of them, and the debug overlays. They are on when the game runs from its project.", UiTheme.CaptionSize, UiKit.Muted));

        MindsSettings.Fill(menu, Tab(tabs, "Minds"));
        for (var i = 0; i < tabs.GetTabCount(); i++)
        {
            if (tabs.GetTabTitle(i) == tab)
            {
                tabs.CurrentTab = i;
            }
        }

        return column;
    }

    /// <summary>A tab with a column of rows.</summary>
    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var margin = new MarginContainer { Name = name };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 12);
        tabs.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    /// <summary>A check box that changes a setting.</summary>
    internal static void Toggle(MenuScreen menu, Container parent, string caption, Func<GameSettings, bool> read, Func<GameSettings, bool, GameSettings> change)
    {
        var box = new CheckBox { ButtonPressed = read(menu.Settings), FocusMode = Control.FocusModeEnum.None };
        box.Toggled += on => menu.ChangeSettings(change(menu.Settings, on));
        parent.AddChild(UiKit.Row(caption, box));
    }

    /// <summary>A slider that changes a setting, with its value beside it.</summary>
    internal static void Slide(MenuScreen menu, Container parent, string caption, Func<GameSettings, float> read, Func<GameSettings, float, GameSettings> change, float least, float most, double step = 0.05)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var slider = new HSlider { MinValue = least, MaxValue = most, Step = step, Value = read(menu.Settings), CustomMinimumSize = new Vector2(280, 28), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, FocusMode = Control.FocusModeEnum.None };
        var value = UiKit.Text(Show(read(menu.Settings), step), UiTheme.SmallSize, UiTheme.Ink);
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        value.CustomMinimumSize = new Vector2(52, 0);
        slider.ValueChanged += changed =>
        {
            value.Text = Show((float)changed, step);
            menu.ChangeSettings(change(menu.Settings, (float)changed));
        };
        row.AddChild(slider);
        row.AddChild(value);
        parent.AddChild(UiKit.Row(caption, row));
    }

    /// <summary>A setting's value in words: a percentage, or a whole number for whole steps.</summary>
    private static string Show(float value, double step) => step >= 1.0
        ? value.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
        : (value * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
