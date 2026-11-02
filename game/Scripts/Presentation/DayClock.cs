using Godot;

namespace Pantheon.Client.Presentation;

/// <summary>Where the day stands for whatever draws by it (ADR-079): shared as <see cref="GraphicsScale"/> is, since many views read it and none owns it.</summary>
internal static class DayClock
{
    /// <summary>Whether the day passes; when not, it is always the settings' held hour.</summary>
    public static bool On { get; set; } = true;

    /// <summary>Hours added to the world's clock (the debug key).</summary>
    public static float Shift { get; set; }

    /// <summary>An hour to hold, for captures (<c>--hour</c>).</summary>
    public static float? Held { get; set; }

    /// <summary>The hour now.</summary>
    public static float Hour { get; set; }

    /// <summary>The part of the day now, in words, for the calendar.</summary>
    public static string Word { get; set; } = string.Empty;

    /// <summary>How much of night it is now, 0..1.</summary>
    public static float Night { get; set; }

    /// <summary>The share of windows with a lamp in them now, 0..1.</summary>
    public static float Lamps { get; set; }

    /// <summary>How many hearths are burning now, 0..1.</summary>
    public static float Hearths { get; set; }

    /// <summary>How much birdsong there is now, 0..1.</summary>
    public static float Birds { get; set; }

    /// <summary>The wind where the camera looks, for what drifts on it: strength times the way it blows.</summary>
    public static Vector2 Wind { get; set; }

    /// <summary>How bright unlit things in the sky should be now: clouds and rain, 0..1.</summary>
    public static float Brightness { get; set; } = 1f;
}
