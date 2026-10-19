namespace Pantheon.Game.Launch;

/// <summary>The culture the <c>--doctrine</c> launch option gives the focused settlement at the start (ADR-024).</summary>
public enum CultureAtStart
{
    /// <summary>It holds to pacifism.</summary>
    Pacifism,

    /// <summary>It holds to militarism.</summary>
    Militarism,

    /// <summary>It holds to sacrifice.</summary>
    Sacrifice,

    /// <summary>It holds to charity.</summary>
    Charity,

    /// <summary>It holds to nature worship.</summary>
    NatureWorship,

    /// <summary>It holds to the search for knowledge.</summary>
    KnowledgeSeeking,

    /// <summary>It holds to expansion.</summary>
    Expansionism,

    /// <summary>It holds to isolation.</summary>
    Isolationism,

    /// <summary>It has turned against the gods.</summary>
    Secular,
}
