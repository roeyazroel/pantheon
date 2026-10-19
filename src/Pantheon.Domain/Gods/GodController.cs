namespace Pantheon.Domain.Gods;

/// <summary>Who decides a god's actions.</summary>
public enum GodController
{
    /// <summary>The host's local player.</summary>
    Human,

    /// <summary>The strategic utility AI (Milestone 5 onwards).</summary>
    Ai,

    /// <summary>A player connected to the host (ADR-018). Without the connection the AI takes the god back.</summary>
    Remote,
}
