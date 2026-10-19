using Pantheon.Domain.World;

namespace Pantheon.Domain.HolySites;

/// <summary>
/// A sacred place (spec §51, ADR-021): found in the land when the world is made, kept by the god
/// whose influence holds it, and taken by a rival whose influence holds it longer.
/// </summary>
public sealed class HolySite
{
    /// <summary>Unique id within the world, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>What kind of place it is.</summary>
    public required HolySiteKind Kind { get; init; }

    /// <summary>Its name, such as "the Sacred Peak above Elar".</summary>
    public required string Name { get; init; }

    /// <summary>The region it stands in.</summary>
    public required int RegionId { get; init; }

    /// <summary>Where it stands: the centre of its region.</summary>
    public required GroundPosition Position { get; init; }

    /// <summary>The god that keeps it, if any; it keeps it until another god claims it.</summary>
    public required int? KeeperGodId { get; set; }

    /// <summary>The god whose influence is taking it from its keeper, if any.</summary>
    public required int? ClaimantGodId { get; set; }

    /// <summary>Ticks the claimant has held the site's influence so far, without a break.</summary>
    public required long ClaimTicks { get; set; }
}

/// <summary>What kind of sacred place a holy site is (spec §51).</summary>
public enum HolySiteKind
{
    /// <summary>The island's highest summit.</summary>
    SacredPeak,

    /// <summary>The oldest, thickest wood.</summary>
    AncientGrove,

    /// <summary>The wettest land, where water rises.</summary>
    HolySpring,

    /// <summary>A ring of stones on open land, far from any village.</summary>
    StandingStones,
}

/// <summary>What a holy site gives the god that keeps it, besides power and influence.</summary>
public enum HolySiteBoon
{
    /// <summary>The keeper's miracles cost <c>BoonAmount</c> less everywhere.</summary>
    CheaperMiracles,

    /// <summary>Land within <c>BoonReach</c> keeps at least <c>BoonAmount</c> blessing.</summary>
    Blessing,

    /// <summary>The keeper's settlements within <c>BoonReach</c> heal by <c>BoonAmount</c> each step.</summary>
    Healing,

    /// <summary>The keeper's Creature gains <c>BoonAmount</c> experience each step within <c>BoonReach</c>.</summary>
    Wisdom,
}
