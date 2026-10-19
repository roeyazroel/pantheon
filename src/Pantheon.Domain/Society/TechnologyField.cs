namespace Pantheon.Domain.Society;

/// <summary>The fields a civilization improves in (spec §36, ADR-023); fantasy-bound, never modern.</summary>
public enum TechnologyField
{
    /// <summary>Farming: richer harvests.</summary>
    Agriculture,

    /// <summary>Building: houses take less work and stand better.</summary>
    Architecture,

    /// <summary>Healing: faster recovery, fewer deaths.</summary>
    Medicine,

    /// <summary>Arms: attacks harm the settlement less.</summary>
    Warfare,

    /// <summary>Roads and boats: migrants travel faster, and trade brings more.</summary>
    Navigation,

    /// <summary>Works: preparations take less work, and floods and fires do less damage.</summary>
    Engineering,

    /// <summary>Learning: education grows faster.</summary>
    Science,

    /// <summary>Rites: worship raises more power.</summary>
    Religion,
}
