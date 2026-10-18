using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Creatures;

/// <summary>
/// A god's Creature (spec §22–23, §90): a Tier 3 entity, fully simulated in the core and saved
/// whole. Presentation only reads it (ADR-010).
/// </summary>
public sealed class CreatureState
{
    /// <summary>Unique Creature id within the world, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>The god it belongs to.</summary>
    public required int GodId { get; init; }

    /// <summary>Its name.</summary>
    public required string Name { get; init; }

    /// <summary>Its species.</summary>
    public required string Species { get; init; }

    /// <summary>Where it stands.</summary>
    public required GroundPosition Position { get; set; }

    /// <summary>Direction it faces, in radians about the vertical axis (0 faces +Z).</summary>
    public required float Heading { get; set; }

    /// <summary>Level, from 1 (spec §23; 10 in the MVP).</summary>
    public required int Level { get; set; }

    /// <summary>Experience gathered so far.</summary>
    public required float Experience { get; set; }

    /// <summary>Health, from 0 (defeated) to 1.</summary>
    public required float Health { get; set; }

    /// <summary>How readily it obeys and stays near its god's people (0..1).</summary>
    public required float Loyalty { get; set; }

    /// <summary>What its deeds add up to, from -1 (cruel) to 1 (kind). It emerges from what it does.</summary>
    public required float Morality { get; set; }

    /// <summary>Its needs.</summary>
    public required CreatureNeeds Needs { get; init; }

    /// <summary>Its personality.</summary>
    public required CreaturePersonality Personality { get; init; }

    /// <summary>What it is doing.</summary>
    public required CreatureActivity Activity { get; init; }

    /// <summary>Its learned preferences, one per situation and action it knows.</summary>
    public required List<LearnedPreference> Preferences { get; init; }

    /// <summary>Its recent deeds, oldest first.</summary>
    public required List<CreatureMemory> Memories { get; init; }

    /// <summary>The settlement it calls home: it returns there after a banishment.</summary>
    public required int HomeSettlementId { get; set; }
}
