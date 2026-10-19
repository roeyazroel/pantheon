namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The options the next start of the main scene uses instead of the command line: the start
/// screen sets them to join a host or to go back to the menu, then reloads the scene (ADR-020).
/// </summary>
internal static class PendingLaunch
{
    /// <summary>The options waiting for the next start, or <c>null</c> to read the command line.</summary>
    public static string[]? Arguments { get; set; }

    /// <summary>Takes the waiting options, or the command line's when none wait.</summary>
    /// <returns>The user arguments.</returns>
    public static string[] Take()
    {
        var arguments = Arguments ?? Godot.OS.GetCmdlineUserArgs();
        Arguments = null;
        return arguments;
    }
}
