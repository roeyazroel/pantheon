using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Pantheon.Game.Presentation;

/// <summary>sRGB colour with channels in 0..1. In JSON it is written as <c>"#rrggbb"</c>.</summary>
/// <param name="R">Red channel.</param>
/// <param name="G">Green channel.</param>
/// <param name="B">Blue channel.</param>
[JsonConverter(typeof(RgbColorJsonConverter))]
public readonly record struct RgbColor(float R, float G, float B)
{
    /// <summary>Parses <c>#rrggbb</c> (the leading <c>#</c> is optional).</summary>
    /// <param name="text">Hex colour text.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="FormatException">The text is not a 6-digit hex colour.</exception>
    public static RgbColor ParseHex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var digits = text.StartsWith('#') ? text[1..] : text;
        if (digits.Length != 6 || !uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var packed))
        {
            throw new FormatException($"'{text}' is not a #rrggbb colour.");
        }

        return new RgbColor(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f);
    }

    /// <summary>Linear blend between two colours.</summary>
    /// <param name="from">Colour at <paramref name="t"/> = 0.</param>
    /// <param name="to">Colour at <paramref name="t"/> = 1.</param>
    /// <param name="t">Blend factor, clamped to 0..1.</param>
    /// <returns>The blended colour.</returns>
    public static RgbColor Lerp(RgbColor from, RgbColor to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new RgbColor(from.R + ((to.R - from.R) * t), from.G + ((to.G - from.G) * t), from.B + ((to.B - from.B) * t));
    }

    /// <summary>Multiplies every channel and clamps the result to 0..1.</summary>
    /// <param name="factor">Brightness factor.</param>
    /// <returns>The scaled colour.</returns>
    public RgbColor Scale(float factor) =>
        new(Math.Clamp(R * factor, 0f, 1f), Math.Clamp(G * factor, 0f, 1f), Math.Clamp(B * factor, 0f, 1f));

    /// <summary>Formats the colour as <c>#rrggbb</c>.</summary>
    /// <returns>Hex colour text.</returns>
    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{ToByte(R):x2}{ToByte(G):x2}{ToByte(B):x2}");

    /// <summary>Converts a 0..1 channel to a byte, rounding to the nearest value.</summary>
    /// <param name="channel">Channel value.</param>
    /// <returns>The channel as 0..255.</returns>
    public static byte ToByte(float channel) => (byte)MathF.Round(Math.Clamp(channel, 0f, 1f) * 255f);
}
