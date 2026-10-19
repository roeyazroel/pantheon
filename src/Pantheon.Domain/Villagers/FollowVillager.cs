using Pantheon.Domain.World;

namespace Pantheon.Domain.Villagers;

/// <summary>
/// A god follows one of a village's people (spec §80, ADR-043): the Tier 2 figure the player
/// pointed at, with the spots its round visits.
/// </summary>
/// <param name="GodId">The god.</param>
/// <param name="SettlementId">The village.</param>
/// <param name="Slot">The figure's index in the village's roster.</param>
/// <param name="Work">The figure's work.</param>
/// <param name="Home">Where it rests.</param>
/// <param name="WorkPlace">Where it works.</param>
/// <param name="Shrine">Where it gathers by the shrine.</param>
/// <param name="Position">Where it stands now.</param>
public sealed record FollowVillager(int GodId, int SettlementId, int Slot, VillagerWork Work, GroundPosition Home, GroundPosition WorkPlace, GroundPosition Shrine, GroundPosition Position);

/// <summary>Why a god could not follow, or stop following, a villager.</summary>
public enum VillagerRejection
{
    /// <summary>Done.</summary>
    None,

    /// <summary>Villagers cannot be followed in this world.</summary>
    NoVillagers,

    /// <summary>The god is unknown.</summary>
    UnknownGod,

    /// <summary>The village is unknown or empty.</summary>
    UnknownSettlement,

    /// <summary>The villager's places lie outside their village.</summary>
    TooFar,

    /// <summary>The god follows no villager.</summary>
    NotFollowing,
}
