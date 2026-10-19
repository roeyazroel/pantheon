namespace Pantheon.Networking;

/// <summary>Constants every peer of a session agrees on (ADR-018).</summary>
public static class NetworkProtocol
{
    /// <summary>
    /// Version of the messages; a host refuses players whose version differs. Version 2 (Milestone 15)
    /// adds domains and holy sites, version 3 (Milestone 15, part 2) the rivers and floods, version 4
    /// (part 3) settlements' society and priorities, version 5 (part 5) treaties, requests and the
    /// Divine Council, version 6 (part 6) war goals, veiled miracles, proxy acts and holy wars,
    /// version 7 (part 7) the world's victories and how a victory was won, version 8 (part 8) the
    /// seasons, snow, the land's life and hurricanes, version 9 (part 9) the hazards: tsunamis,
    /// landslides, the volcano, tornadoes, meteors, blight and locusts, version 10 (part 10) the
    /// Divine Hand, Creatures' paths and the Divine Acts, version 11 (part 12) the prophets and elders,
    /// version 12 (part 13) the roads and the land's timber and stone, version 13 (part 14) the great
    /// events and the plague, version 14 (part 15) the ancient beast and the anomaly, version 15
    /// (part 16) the gods each god knows and its goals, version 16 (part 17) Creatures' courage,
    /// protectiveness, jealousy and playfulness, version 17 (part 18) the biomes of §5 and the landforms,
    /// version 18 (part 19) offerings in the hand and the hand's new action, version 19 (part 22) the
    /// villagers the gods follow and the intent to follow one, version 20 (part 24) the land's
    /// reshapings by Divine Acts.
    /// </summary>
    public const int Version = 20;

    /// <summary>The UDP port a host listens on unless told otherwise.</summary>
    public const int DefaultPort = 7777;

    /// <summary>The host's peer id; players get ids from 2.</summary>
    public const int HostPeerId = 1;

    /// <summary>Most players connected to one host besides the host itself (one per god the AI plays).</summary>
    public const int MaxClients = 3;

    /// <summary>Encoded messages at least this long are compressed.</summary>
    public const int CompressFromBytes = 512;
}
