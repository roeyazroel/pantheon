using System;

namespace Pantheon.Game.Presentation.Nature;

/// <summary>Smooth value noise that tiles, for textures drawn in code (ADR-073). Pure.</summary>
public static class NatureNoise
{
    /// <summary>Fractal noise in 0..1 that repeats every <paramref name="period"/> units in both directions.</summary>
    /// <param name="x">Across, in units.</param>
    /// <param name="y">Down, in units.</param>
    /// <param name="period">How many units it repeats after; the first octave's lattice.</param>
    /// <param name="octaves">How many octaves, each twice as fine and half as strong.</param>
    /// <param name="seed">Which noise.</param>
    /// <returns>The value.</returns>
    public static float Fbm(float x, float y, int period, int octaves, int seed)
    {
        var (sum, weight, amplitude) = (0f, 0f, 1f);
        for (var octave = 0; octave < octaves; octave++)
        {
            var scale = 1 << octave;
            sum += Value(x * scale, y * scale, Math.Max(1, period * scale), seed + (octave * 101)) * amplitude;
            weight += amplitude;
            amplitude *= 0.5f;
        }

        return sum / weight;
    }

    /// <summary>One octave of value noise, 0..1, repeating every <paramref name="period"/> units.</summary>
    private static float Value(float x, float y, int period, int seed)
    {
        var (ix, iy) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - ix, y - iy);
        var (sx, sy) = (fx * fx * (3f - (2f * fx)), fy * fy * (3f - (2f * fy)));
        float Corner(int cx, int cy) => PresentationHash.Unit((ulong)seed, (ulong)Wrap(cx, period), (ulong)Wrap(cy, period));
        var top = Lerp(Corner(ix, iy), Corner(ix + 1, iy), sx);
        var bottom = Lerp(Corner(ix, iy + 1), Corner(ix + 1, iy + 1), sx);
        return Lerp(top, bottom, sy);
    }

    private static int Wrap(int value, int period) => ((value % period) + period) % period;

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
