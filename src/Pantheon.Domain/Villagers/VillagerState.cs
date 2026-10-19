using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Villagers;

/// <summary>The work a followed villager does, as the figure the player chose did (spec §80, ADR-043).</summary>
public enum VillagerWork
{
    /// <summary>Works in the fields.</summary>
    Farming,

    /// <summary>Works on the village's building.</summary>
    Building,

    /// <summary>Does chores about the house.</summary>
    Chores,
}

/// <summary>What a followed villager is about.</summary>
public enum VillagerActivity
{
    /// <summary>Resting at home.</summary>
    Resting,

    /// <summary>Eating at home, from the village's stores.</summary>
    Eating,

    /// <summary>At work.</summary>
    Working,

    /// <summary>Praying at the shrine.</summary>
    Worshipping,

    /// <summary>Fleeing danger.</summary>
    Fleeing,

    /// <summary>Waiting out danger away from home.</summary>
    Sheltering,

    /// <summary>On the way to a new home, the old one lost.</summary>
    Seeking,
}

/// <summary>What a followed villager remembers.</summary>
public enum VillagerDeed
{
    /// <summary>A god first took notice of them.</summary>
    Followed,

    /// <summary>They saw a miracle.</summary>
    SawMiracle,

    /// <summary>They lived through a disaster at home.</summary>
    Survived,

    /// <summary>They went hungry, or thirsty, with their village.</summary>
    Hungered,

    /// <summary>They fled danger.</summary>
    Fled,

    /// <summary>They came home after danger.</summary>
    Returned,

    /// <summary>They found a new home, the old one lost.</summary>
    Moved,

    /// <summary>They turned to another god, or came to believe.</summary>
    Turned,

    /// <summary>They saw a Creature act in their village.</summary>
    SawCreature,

    /// <summary>Their village raised a building.</summary>
    Built,

    /// <summary>They heard a prophet preach.</summary>
    HeardPreaching,

    /// <summary>Their village gave or received an offering.</summary>
    Offering,

    /// <summary>They fell ill with the plague.</summary>
    FellIll,
}

/// <summary>How a followed villager's life ended, or what danger they fled.</summary>
public enum VillagerFate
{
    /// <summary>Old age.</summary>
    OldAge,

    /// <summary>Hunger.</summary>
    Hunger,

    /// <summary>Illness.</summary>
    Illness,

    /// <summary>Fire.</summary>
    Fire,

    /// <summary>Flood.</summary>
    Flood,

    /// <summary>An earthquake.</summary>
    Earthquake,
}

/// <summary>One thing a followed villager remembers (spec §80's individual memory).</summary>
public sealed class VillagerMemory
{
    /// <summary>When.</summary>
    public required long Tick { get; init; }

    /// <summary>What.</summary>
    public required VillagerDeed Deed { get; init; }

    /// <summary>Where, if in a village.</summary>
    public int? SettlementId { get; init; }

    /// <summary>The god it concerned, if any.</summary>
    public int? GodId { get; init; }

    /// <summary>The Creature, prophet or other god it concerned, if any.</summary>
    public int? OtherId { get; init; }

    /// <summary>A detail: the miracle, the disaster, the building or the Creature's deed.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>How many people it concerned, if any; a hardship lived through again soon after adds to it.</summary>
    public int Count { get; set; }
}

/// <summary>
/// One villager a god follows (spec §80's "selected villager", ADR-043): simulated in full in Tier 3,
/// with a name and an age, a home, work and a god, needs, a way across the land and a memory of
/// their own, until death.
/// </summary>
public sealed class VillagerState
{
    /// <summary>Unique id, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god who follows them.</summary>
    public required int FollowerGodId { get; init; }

    /// <summary>The village they live in.</summary>
    public required int SettlementId { get; set; }

    /// <summary>The Tier 2 figure they were chosen as, while they live in that village; <c>null</c> after they move.</summary>
    public int? Slot { get; set; }

    /// <summary>The work they do.</summary>
    public required VillagerWork Work { get; init; }

    /// <summary>The god they follow, or <c>null</c> for none.</summary>
    public int? GodId { get; set; }

    /// <summary>Their faith in that god (0..1).</summary>
    public float Faith { get; set; }

    /// <summary>Where they rest and eat: in front of their door.</summary>
    public required GroundPosition Home { get; set; }

    /// <summary>Where they work.</summary>
    public required GroundPosition WorkPlace { get; set; }

    /// <summary>Where they pray: by the shrine.</summary>
    public required GroundPosition Shrine { get; set; }

    /// <summary>Where they stand.</summary>
    public required GroundPosition Position { get; set; }

    /// <summary>The way still to walk.</summary>
    public List<GroundPosition> Path { get; init; } = [];

    /// <summary>What they are about.</summary>
    public VillagerActivity Activity { get; set; }

    /// <summary>Ticks left of it once there; for <see cref="VillagerActivity.Sheltering"/>, unused.</summary>
    public int TicksLeft { get; set; }

    /// <summary>The village they flee or move to, if any.</summary>
    public int? RefugeId { get; set; }

    /// <summary>Hunger (0..1): at 1 they starve.</summary>
    public float Hunger { get; set; }

    /// <summary>Weariness (0..1).</summary>
    public float Fatigue { get; set; }

    /// <summary>Health (0..1): at 0 they die.</summary>
    public float Health { get; set; } = 1f;

    /// <summary>Their age, in years, when their god first followed them.</summary>
    public required float AgeYears { get; init; }

    /// <summary>The age at which age takes them.</summary>
    public required float LifeYears { get; init; }

    /// <summary>When their god first followed them.</summary>
    public required long FollowedTick { get; init; }

    /// <summary>When they last prayed.</summary>
    public long LastWorshipTick { get; set; }

    /// <summary>What they remember, oldest first.</summary>
    public List<VillagerMemory> Memories { get; init; } = [];
}
