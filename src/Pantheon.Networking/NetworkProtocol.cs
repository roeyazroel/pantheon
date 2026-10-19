namespace Pantheon.Networking;

/// <summary>Constants every peer of a session agrees on (ADR-018).</summary>
public static class NetworkProtocol
{
    /// <summary>
    /// Version of the messages; a host refuses players whose version differs. Version 2 (Milestone 15)
    /// adds domains and holy sites, version 3 (Milestone 15, part 2) the rivers and floods, version 4
    /// (part 3) settlements' society and priorities.
    /// </summary>
    public const int Version = 4;

    /// <summary>The UDP port a host listens on unless told otherwise.</summary>
    public const int DefaultPort = 7777;

    /// <summary>The host's peer id; players get ids from 2.</summary>
    public const int HostPeerId = 1;

    /// <summary>Most players connected to one host besides the host itself (one per god the AI plays).</summary>
    public const int MaxClients = 3;

    /// <summary>Encoded messages at least this long are compressed.</summary>
    public const int CompressFromBytes = 512;
}
