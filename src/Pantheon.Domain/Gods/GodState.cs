using System.Collections.Generic;
using Pantheon.Domain.Migration;

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

    /// <summary>The AI's strategic personality; <see cref="GodPersonality.None"/> for a player.</summary>
    public required GodPersonality Personality { get; init; }

    /// <summary>Divine Power available to spend on miracles (spec §9).</summary>
    public required float DivinePower { get; set; }

    /// <summary>Highest rank reached; it never falls.</summary>
    public required DivineRank Rank { get; set; }

    /// <summary>Tick from which the god has held the victory share of the world's belief without a break, if it holds it now.</summary>
    public required long? DominantSinceTick { get; set; }

    /// <summary>The god's Creature, if it has one.</summary>
    public required int? CreatureId { get; init; }

    /// <summary>What the god does with migrants arriving where it is followed most (ADR-016).</summary>
    public required RefugeePolicy RefugeePolicy { get; set; }

    /// <summary>Miracles that are cooling down. Entries whose tick has passed may linger until the miracle is cast again.</summary>
    public required List<MiracleCooldown> Cooldowns { get; init; }
}
