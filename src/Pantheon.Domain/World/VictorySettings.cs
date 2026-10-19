namespace Pantheon.Domain.World;

/// <summary>How a world may be won (spec §53, ADR-027), chosen when it is made; with none on it is a sandbox.</summary>
public sealed record VictorySettings
{
    /// <summary>Divine Ascendancy: hold the victory share of all belief long enough, alone or with an ally when pantheon victory is on.</summary>
    public bool Ascendancy { get; init; } = true;

    /// <summary>Conquest: every opponent falls below the conquest share long enough.</summary>
    public bool Conquest { get; init; }

    /// <summary>Cultural victory: lead the cultural share of the world's villages long enough without eliminating rivals.</summary>
    public bool Cultural { get; init; }

    /// <summary>The most gods that may win a pantheon victory together; the divine rules' number when <c>null</c>. Set by the world's size.</summary>
    public int? PantheonMaxGods { get; init; }

    /// <summary>Divine Ascendancy only: the default (spec §53).</summary>
    public static VictorySettings Default { get; } = new();

    /// <summary>No victory: a sandbox.</summary>
    public static VictorySettings Sandbox { get; } = new() { Ascendancy = false };

    /// <summary>Every victory.</summary>
    public static VictorySettings All { get; } = new() { Conquest = true, Cultural = true };

    /// <summary>Whether no victory is on. A method, so saves keep only the choices.</summary>
    /// <returns><c>true</c> for a sandbox.</returns>
    public bool IsSandbox() => !Ascendancy && !Conquest && !Cultural;
}

/// <summary>How a world was won.</summary>
public enum VictoryKind
{
    /// <summary>A god held the victory share of all belief.</summary>
    Ascendancy,

    /// <summary>A god and its ally held it together.</summary>
    Pantheon,

    /// <summary>Every opponent fell below the conquest share.</summary>
    Conquest,

    /// <summary>A god led most of the world's villages while its rivals lived on.</summary>
    Cultural,
}
