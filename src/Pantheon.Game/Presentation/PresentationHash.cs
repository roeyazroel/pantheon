namespace Pantheon.Game.Presentation;

/// <summary>
/// Deterministic hashes for presentation variety: house jitter, field crops, citizen roles, homes
/// and routines. They never consume a simulation random stream, so drawing the world can never
/// change it.
/// </summary>
public static class PresentationHash
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>Mixes up to four keys into 64 well-distributed bits.</summary>
    /// <param name="a">First key.</param>
    /// <param name="b">Second key.</param>
    /// <param name="c">Third key.</param>
    /// <param name="d">Fourth key.</param>
    /// <returns>The hash.</returns>
    public static ulong Mix(ulong a, ulong b = 0, ulong c = 0, ulong d = 0)
    {
        // Each key goes through the SplitMix64 finaliser, chained so that the key order matters.
        var hash = Finalize(unchecked(a + GoldenGamma));
        hash = Finalize(unchecked(hash ^ (b + (2 * GoldenGamma))));
        hash = Finalize(unchecked(hash ^ (c + (3 * GoldenGamma))));
        return Finalize(unchecked(hash ^ (d + (4 * GoldenGamma))));
    }

    /// <summary>Hash of the keys as a number in [0, 1).</summary>
    /// <param name="a">First key.</param>
    /// <param name="b">Second key.</param>
    /// <param name="c">Third key.</param>
    /// <param name="d">Fourth key.</param>
    /// <returns>A value from 0 (inclusive) to 1 (exclusive) with 24 bits of precision.</returns>
    public static float Unit(ulong a, ulong b = 0, ulong c = 0, ulong d = 0) => (Mix(a, b, c, d) >> 40) / (float)(1UL << 24);

    /// <summary>The SplitMix64 output function.</summary>
    private static ulong Finalize(ulong value)
    {
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        return value ^ (value >> 31);
    }
}
