namespace Pantheon.Game.Population;

/// <summary>A settlement's simulation tier and representatives, for the debug overlay.</summary>
/// <param name="SettlementId">Settlement the status belongs to.</param>
/// <param name="Tier">Current tier.</param>
/// <param name="Representatives">Representative citizens shown (0 in Tier 1).</param>
/// <param name="PeoplePerRepresentative">Inhabitants each representative stands for (0 when none are shown).</param>
/// <param name="CameraDistance">Distance from the camera to the settlement centre, in metres.</param>
/// <param name="RegionalTierEnterDistance">The settlement enters Tier 2 within this distance.</param>
/// <param name="RegionalTierExitDistance">The settlement leaves Tier 2 beyond this distance.</param>
public sealed record CrowdStatus(
    int SettlementId,
    SimulationTier Tier,
    int Representatives,
    float PeoplePerRepresentative,
    float CameraDistance,
    float RegionalTierEnterDistance,
    float RegionalTierExitDistance);
