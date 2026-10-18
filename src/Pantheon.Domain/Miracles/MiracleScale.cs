namespace Pantheon.Domain.Miracles;

/// <summary>Scale of a miracle (spec §14).</summary>
public enum MiracleScale
{
    /// <summary>One place or a few people.</summary>
    Minor,

    /// <summary>A whole region.</summary>
    Regional,

    /// <summary>Several regions.</summary>
    Great,

    /// <summary>World-changing and extremely expensive.</summary>
    DivineAct,
}
