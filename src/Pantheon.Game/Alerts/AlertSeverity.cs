namespace Pantheon.Game.Alerts;

/// <summary>How much an alert matters to the player; higher is more urgent.</summary>
public enum AlertSeverity
{
    /// <summary>Worth knowing.</summary>
    Info,

    /// <summary>Good news.</summary>
    Good,

    /// <summary>Needs attention soon.</summary>
    Warning,

    /// <summary>Needs attention now.</summary>
    Danger,
}
