using System;
using System.Text;

namespace Pantheon.Domain.World;

/// <summary>
/// Platform-independent string hashing used for seeds and RNG stream names.
/// Unlike <see cref="string.GetHashCode()"/>, results are identical across processes and runtimes.
/// </summary>
public static class StableHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>Computes the 64-bit FNV-1a hash of the UTF-8 bytes of <paramref name="text"/>.</summary>
    /// <param name="text">Text to hash.</param>
    /// <returns>The stable 64-bit hash.</returns>
    public static ulong Fnv1a64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = FnvOffsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }
}
