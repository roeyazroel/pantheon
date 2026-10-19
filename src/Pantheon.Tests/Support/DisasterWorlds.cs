using Pantheon.Domain.Disasters;
using Pantheon.Domain.World;

namespace Pantheon.Tests.Support;

/// <summary>Pinned disaster rules and helpers for disaster tests.</summary>
internal static class DisasterWorlds
{
    /// <summary>
    /// Rules with fixed values (the balance shipped with Milestone 8), so disaster tests keep their
    /// meaning when <c>disasters.json</c> is retuned.
    /// </summary>
    public static DisasterRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        LightningIgnitionPerSecond = 0.004f,
        HearthFirePerSecond = 0.001f,
        FuelHeat = 2f,
        IgnitionIntensity = 0.4f,
        MinFlammability = 0.04f,
        WetSoil = 0.6f,
        FireGrowthPerSecond = 0.08f,
        BurnPerSecond = 0.06f,
        SpreadPerSecond = 0.12f,
        WindSpread = 2f,
        RainQuenchPerSecond = 0.03f,
        FirefightingPerSecond = 0.01f,
        ScorchRecoveryPerSecond = 0.004f,
        FireHousingLossPerSecond = 0.02f,
        FireDeathsPerSecond = 0.004f,
        FireFoodLossPerSecond = 0.03f,
        FireDreadPerSecond = 0.03f,
        EarthquakesPerHour = 2f,
        MinMagnitude = 4.5f,
        MaxMagnitude = 7.5f,
        FullMagnitude = 7.5f,
        HarmlessMagnitude = 4.5f,
        RadiusPerMagnitude = 250f,
        ShakingSeconds = 12f,
        QuakeHousingLoss = 0.6f,
        QuakeDeaths = 0.06f,
        QuakeFoodLoss = 0.3f,
        QuakeFireChance = 0.5f,
        HouseQuakeResistance = 0.3f,
        HouseFireResistance = 0.5f,
        ShieldedDamage = 0.1f,
        Preparedness = Preparedness.None,
    };

    /// <summary>Makes every region parched and fully vegetated, so it burns as readily as land can.</summary>
    /// <param name="world">The world.</param>
    public static void Parch(WorldState world)
    {
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0f;
            region.Vegetation = 1f;
        }
    }
}
