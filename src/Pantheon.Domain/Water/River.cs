using System.Collections.Generic;

namespace Pantheon.Domain.Water;

/// <summary>
/// A river of the world (spec §5, §15, ADR-022). Its course is fixed when the world is made; its
/// level rises with the rain and the wet soil along it, and when it overflows it floods the low
/// land downstream.
/// </summary>
public sealed class River
{
    /// <summary>Unique river id within the world.</summary>
    public required int Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>The regions it runs through, from its source to its mouth, each once.</summary>
    public required List<int> Regions { get; init; }

    /// <summary>How full it runs: 0 is dry, about half is ordinary and above 1 it floods.</summary>
    public required float Level { get; set; }
}
