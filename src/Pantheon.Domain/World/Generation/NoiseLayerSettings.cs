namespace Pantheon.Domain.World.Generation;

/// <summary>Fractal noise parameters for one FastNoiseLite layer.</summary>
public sealed record NoiseLayerSettings
{
    /// <summary>Base frequency in cycles per metre.</summary>
    public required float Frequency { get; init; }

    /// <summary>Number of fractal octaves.</summary>
    public required int Octaves { get; init; }

    /// <summary>Frequency multiplier between octaves.</summary>
    public required float Lacunarity { get; init; }

    /// <summary>Amplitude multiplier between octaves.</summary>
    public required float Gain { get; init; }
}
