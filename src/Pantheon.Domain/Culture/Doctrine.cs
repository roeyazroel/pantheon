namespace Pantheon.Domain.Culture;

/// <summary>What a settlement's religious culture comes to hold (spec §33, ADR-024).</summary>
public enum Doctrine
{
    /// <summary>Violence is wrong: harm by their own god shakes them, and they welcome the fleeing.</summary>
    Pacifism,

    /// <summary>Strength is holy: war and the god's might rally them.</summary>
    Militarism,

    /// <summary>The god must be appeased: fear makes them worship harder and resent less.</summary>
    Sacrifice,

    /// <summary>Give to those in need: help earns more love, and they share food with hungry neighbours.</summary>
    Charity,

    /// <summary>The land is sacred: blessings move them, and burning or shaking it turns them away.</summary>
    NatureWorship,

    /// <summary>Learn: they grow learned faster, and drift towards reason.</summary>
    KnowledgeSeeking,

    /// <summary>Spread out: they found new villages sooner.</summary>
    Expansionism,

    /// <summary>Keep to ourselves: rival gods and strangers are kept out.</summary>
    Isolationism,
}
