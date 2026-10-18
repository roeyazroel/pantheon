using Pantheon.Domain.World.Generation;

namespace Pantheon.Simulation.Noise;

/// <summary>Fractal shape of a noise layer.</summary>
internal enum NoiseFractal
{
    /// <summary>Fractal Brownian motion: smooth, rolling terrain.</summary>
    Fbm,

    /// <summary>Ridged multifractal: sharp crests, used for mountain ranges.</summary>
    Ridged,
}

/// <summary>One configured OpenSimplex2 FastNoiseLite layer, sampled in world metres.</summary>
internal sealed class NoiseLayer
{
    private readonly FastNoiseLite _noise;

    /// <summary>Configures a noise layer from data-driven settings.</summary>
    /// <param name="settings">Frequency and fractal parameters.</param>
    /// <param name="seed">Layer seed, derived with <see cref="Randomness.SeedMixer"/>.</param>
    /// <param name="fractal">Fractal shape.</param>
    public NoiseLayer(NoiseLayerSettings settings, int seed, NoiseFractal fractal)
    {
        _noise = new FastNoiseLite(seed);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetFrequency(settings.Frequency);
        _noise.SetFractalType(ToFastNoiseFractal(fractal));
        _noise.SetFractalOctaves(settings.Octaves);
        _noise.SetFractalLacunarity(settings.Lacunarity);
        _noise.SetFractalGain(settings.Gain);
    }

    /// <summary>Samples the layer at a world position and maps the result to 0..1.</summary>
    /// <param name="x">World X in metres.</param>
    /// <param name="z">World Z in metres.</param>
    /// <returns>Noise value in 0..1.</returns>
    public float Sample01(float x, float z) => SimMath.Clamp01((_noise.GetNoise(x, z) * 0.5f) + 0.5f);

    /// <summary>Maps the project's fractal choice onto the FastNoiseLite enum.</summary>
    private static FastNoiseLite.FractalType ToFastNoiseFractal(NoiseFractal fractal) => fractal switch
    {
        NoiseFractal.Fbm => FastNoiseLite.FractalType.FBm,
        NoiseFractal.Ridged => FastNoiseLite.FractalType.Ridged,
    };
}
