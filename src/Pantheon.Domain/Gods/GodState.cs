using System.Collections.Generic;

namespace Pantheon.Domain.Gods;

/// <summary>
/// One god in the world (spec §11, §90). Followers and influence are not stored: they follow from
/// the settlements' beliefs, so they can never disagree with them.
/// </summary>
public sealed class GodState
{
    /// <summary>Unique god id within the world, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether a player or the AI decides the god's actions.</summary>
    public required GodController Controller { get; init; }

    /// <summary>Divine Power available to spend on miracles (spec §9).</summary>
    public required float DivinePower { get; set; }

    /// <summary>Highest rank reached; it never falls.</summary>
    public required DivineRank Rank { get; set; }

    /// <summary>Miracles that are cooling down. Entries whose tick has passed may linger until the miracle is cast again.</summary>
    public required List<MiracleCooldown> Cooldowns { get; init; }
}
