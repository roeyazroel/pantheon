namespace Pantheon.Domain.World.Generation;

/// <summary>Island mask, mountain blending and the mapping from normalised elevation to metres.</summary>
public sealed record TerrainShapeSettings
{
    /// <summary>Normalised distance from the centre (0 = centre, 1 = edge midpoint) where the island starts sinking.</summary>
    public required float IslandFalloffStart { get; init; }

    /// <summary>Normalised distance from the centre beyond which everything is sea.</summary>
    public required float IslandFalloffEnd { get; init; }

    /// <summary>Weight of ridged mountain noise relative to the continent noise.</summary>
    public required float RidgeWeight { get; init; }

    /// <summary>Continent value (0..1) at which mountains start to appear.</summary>
    public required float RidgeMaskStart { get; init; }

    /// <summary>Continent value (0..1) at which mountains reach full strength.</summary>
    public required float RidgeMaskEnd { get; init; }

    /// <summary>Normalised elevation (0..1) that maps to sea level (0 m).</summary>
    public required float SeaLevel { get; init; }

    /// <summary>Exponent applied to land elevation. Values above 1 flatten lowlands and sharpen peaks.</summary>
    public required float LandExponent { get; init; }

    /// <summary>Height in metres of the highest possible land.</summary>
    public required float MaxElevation { get; init; }

    /// <summary>Depth in metres of the deepest possible sea floor.</summary>
    public required float MaxDepth { get; init; }
}
