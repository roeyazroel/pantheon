using System;

namespace Pantheon.Game.Population;

/// <summary>
/// Chooses a settlement's tier from the camera distance with hysteresis: it enters Tier 2 within
/// <see cref="CitizenSettings.RegionalTierEnterDistance"/> and leaves it only beyond the larger
/// <see cref="CitizenSettings.RegionalTierExitDistance"/>, so the tier does not flicker at the edge.
/// </summary>
public static class TierSelector
{
    /// <summary>The tier for the current camera distance.</summary>
    /// <param name="current">Tier the settlement is in now.</param>
    /// <param name="cameraDistance">Distance from the camera to the settlement centre, in metres.</param>
    /// <param name="settings">Tier distances.</param>
    /// <returns>The new tier.</returns>
    public static SimulationTier Select(SimulationTier current, float cameraDistance, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return current switch
        {
            SimulationTier.Statistical => cameraDistance <= settings.RegionalTierEnterDistance ? SimulationTier.Regional : SimulationTier.Statistical,
            SimulationTier.Regional => cameraDistance > settings.RegionalTierExitDistance ? SimulationTier.Statistical : SimulationTier.Regional,
        };
    }
}
