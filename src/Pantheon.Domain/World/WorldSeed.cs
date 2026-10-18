using System;
using System.Globalization;

namespace Pantheon.Domain.World;

/// <summary>
/// Seed that, together with the generation settings, fully determines a generated world.
/// </summary>
/// <param name="Value">Raw 64-bit seed value.</param>
public readonly record struct WorldSeed(ulong Value)
{
    /// <summary>
    /// Parses a seed from user input. Decimal integers are used verbatim; any other text is hashed
    /// with <see cref="StableHash.Fnv1a64"/>, so memorable phrases work as seeds too.
    /// </summary>
    /// <param name="text">Seed text, for example <c>"1234"</c> or <c>"first light"</c>.</param>
    /// <returns>The parsed seed.</returns>
    /// <exception cref="ArgumentException">The text is empty or whitespace.</exception>
    public static WorldSeed Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Seed text must not be empty.", nameof(text));
        }

        return ulong.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric)
            ? new WorldSeed(numeric)
            : new WorldSeed(StableHash.Fnv1a64(trimmed));
    }

    /// <summary>Returns the decimal seed value, which <see cref="Parse"/> accepts back unchanged.</summary>
    /// <returns>The seed as an invariant-culture decimal string.</returns>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
