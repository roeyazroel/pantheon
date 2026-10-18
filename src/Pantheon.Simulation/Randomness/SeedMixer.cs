using Pantheon.Domain.World;

namespace Pantheon.Simulation.Randomness;

/// <summary>
/// Derives independent, reproducible sub-seeds from a <see cref="WorldSeed"/>, one per purpose
/// (for example <c>"terrain.elevation"</c> or <c>"simulation"</c>). Adding a new purpose never
/// changes the values derived for existing ones.
/// </summary>
public static class SeedMixer
{
    /// <summary>Derives a 64-bit sub-seed for a named purpose.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="purpose">Stable purpose name.</param>
    /// <returns>A well-mixed 64-bit seed.</returns>
    public static ulong Derive(WorldSeed seed, string purpose) =>
        Mix(seed.Value ^ Mix(StableHash.Fnv1a64(purpose)));

    /// <summary>Derives a 32-bit seed for FastNoiseLite, which only accepts <see cref="int"/> seeds.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="purpose">Stable purpose name.</param>
    /// <returns>A 32-bit noise seed.</returns>
    public static int DeriveNoiseSeed(WorldSeed seed, string purpose)
    {
        var derived = Derive(seed, purpose);
        return unchecked((int)(derived ^ (derived >> 32)));
    }

    /// <summary>SplitMix64 finaliser: an invertible avalanche mix of all 64 bits.</summary>
    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
