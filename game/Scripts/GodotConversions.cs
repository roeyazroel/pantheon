using Godot;
using Pantheon.Game.Presentation;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace Pantheon.Client;

/// <summary>Conversions between the engine-agnostic types of the core libraries and Godot types.</summary>
internal static class GodotConversions
{
    /// <summary>Converts a palette colour to a Godot colour.</summary>
    /// <param name="color">sRGB palette colour.</param>
    /// <param name="alpha">Opacity.</param>
    /// <returns>The Godot colour.</returns>
    public static Color ToGodot(this RgbColor color, float alpha = 1f) => new(color.R, color.G, color.B, alpha);

    /// <summary>Converts a vector to Godot.</summary>
    /// <param name="value">System.Numerics vector.</param>
    /// <returns>The Godot vector.</returns>
    public static Vector3 ToGodot(this NVector3 value) => new(value.X, value.Y, value.Z);

    /// <summary>Converts a Godot vector to System.Numerics.</summary>
    /// <param name="value">Godot vector.</param>
    /// <returns>The System.Numerics vector.</returns>
    public static NVector3 ToNumerics(this Vector3 value) => new(value.X, value.Y, value.Z);

    /// <summary>Converts a Godot 2D vector (for example a screen position) to System.Numerics.</summary>
    /// <param name="value">Godot vector.</param>
    /// <returns>The System.Numerics vector.</returns>
    public static NVector2 ToNumerics(this Vector2 value) => new(value.X, value.Y);

    /// <summary>Converts an array of vectors to Godot.</summary>
    /// <param name="values">System.Numerics vectors.</param>
    /// <returns>A new array of Godot vectors.</returns>
    public static Vector3[] ToGodot(this NVector3[] values)
    {
        var result = new Vector3[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = values[i].ToGodot();
        }

        return result;
    }

    /// <summary>Converts RGBA vectors (channels in 0..1) to Godot colours.</summary>
    /// <param name="values">RGBA vectors.</param>
    /// <returns>A new array of Godot colours.</returns>
    public static Color[] ToGodotColors(this NVector4[] values)
    {
        var result = new Color[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = new Color(values[i].X, values[i].Y, values[i].Z, values[i].W);
        }

        return result;
    }
}
