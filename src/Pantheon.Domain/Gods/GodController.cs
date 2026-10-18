namespace Pantheon.Domain.Gods;

/// <summary>Who decides a god's actions.</summary>
public enum GodController
{
    /// <summary>A player, local or (later) connected to the host.</summary>
    Human,

    /// <summary>The strategic utility AI (Milestone 5 onwards).</summary>
    Ai,
}
