namespace Pantheon.Game.Launch;

/// <summary>A disaster the <c>--disaster</c> launch option starts at the focused settlement (ADR-014).</summary>
public enum DisasterAtStart
{
    /// <summary>An earthquake under it.</summary>
    Quake,

    /// <summary>A fire in its region.</summary>
    Fire,
}
