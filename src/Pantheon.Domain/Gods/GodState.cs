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

    /// <summary>Whether a player or the AI decides the god's actions; a player joining or leaving the host changes it (ADR-018).</summary>
    public required GodController Controller { get; set; }

    /// <summary>The AI's strategic personality; <see cref="GodPersonality.None"/> for a player.</summary>
    public required GodPersonality Personality { get; init; }

    /// <summary>Divine Power available to spend on miracles (spec §9).</summary>
    public required float DivinePower { get; set; }

    /// <summary>Highest rank reached; it never falls.</summary>
    public required DivineRank Rank { get; set; }

    /// <summary>Tick from which the god has held the victory share of the world's belief without a break, if it holds it now.</summary>
    public required long? DominantSinceTick { get; set; }

    /// <summary>Tick since which every opponent has been below the conquest share, if it is so (ADR-027).</summary>
    public long? ConquestSinceTick { get; set; }

    /// <summary>Tick since which the god has led the cultural share of the villages, its rivals living on, if it is so (ADR-027).</summary>
    public long? CulturalSinceTick { get; set; }

    /// <summary>The god's Creature, if it has one.</summary>
    public required int? CreatureId { get; init; }

    /// <summary>What the god does with migrants arriving where it is followed most (ADR-016).</summary>
    public required RefugeePolicy RefugeePolicy { get; set; }

    /// <summary>Miracles that are cooling down. Entries whose tick has passed may linger until the miracle is cast again.</summary>
    public required List<MiracleCooldown> Cooldowns { get; init; }

    /// <summary>The domains the god has taken up, in the order it chose them (spec §12, ADR-021); never more than its rank allows.</summary>
    public required List<GodDomain> Domains { get; init; }

    /// <summary>What the god has done, as the world remembers it (spec §3.2, §11, ADR-024).</summary>
    public required Culture.GodConduct Conduct { get; init; }

    /// <summary>How the world sees the god now, from its conduct (spec §11, ADR-024).</summary>
    public required List<Culture.GodTrait> Traits { get; init; }

    /// <summary>What its Divine Hand holds now, if anything (spec §55, ADR-030).</summary>
    public Hand.HeldObject? Held { get; set; }

    /// <summary>The gods it knows of (spec §11, ADR-036), by id: it has met their followers, seen their wonders, met their Creature or sat with them in council.</summary>
    public List<int> KnownGods { get; init; } = [];

    /// <summary>The goals an AI god holds now (spec §47, ADR-036).</summary>
    public List<GodGoal> Goals { get; init; } = [];

    /// <summary>When an AI god next makes its major plan (spec §87, ADR-036).</summary>
    public long NextPlanTick { get; set; }

    /// <summary>Who the god is (ADR-065): its epithet, history, voice and temper; none for a player's god or a world made before personas.</summary>
    public GodPersona? Persona { get; set; }
}
