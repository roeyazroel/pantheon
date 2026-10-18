using System.Collections.Generic;
using Pantheon.Game.Population;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>A settlement enters Tier 2 close to the camera and leaves it only well beyond, so the tier does not flicker.</summary>
public sealed class TierSelectorTests
{
    private static readonly CitizenSettings Settings = TestContent.SettlementView.Citizens with
    {
        RegionalTierEnterDistance = 450f,
        RegionalTierExitDistance = 550f,
    };

    [Theory]
    [InlineData(SimulationTier.Statistical, 900f, SimulationTier.Statistical)]
    [InlineData(SimulationTier.Statistical, 500f, SimulationTier.Statistical)]
    [InlineData(SimulationTier.Statistical, 450f, SimulationTier.Regional)]
    [InlineData(SimulationTier.Statistical, 20f, SimulationTier.Regional)]
    [InlineData(SimulationTier.Regional, 20f, SimulationTier.Regional)]
    [InlineData(SimulationTier.Regional, 500f, SimulationTier.Regional)]
    [InlineData(SimulationTier.Regional, 550f, SimulationTier.Regional)]
    [InlineData(SimulationTier.Regional, 551f, SimulationTier.Statistical)]
    public void Select_EntersTier2WithinTheEnterDistance_AndLeavesBeyondTheExitDistance(SimulationTier current, float distance, SimulationTier expected)
    {
        Assert.Equal(expected, TierSelector.Select(current, distance, Settings));
    }

    [Fact]
    public void Select_KeepsTheTierWhileTheCameraHoversBetweenTheDistances()
    {
        const SimulationTier Statistical = SimulationTier.Statistical;
        const SimulationTier Regional = SimulationTier.Regional;
        var tier = Statistical;
        var tiers = new List<SimulationTier>();

        foreach (var distance in new[] { 600f, 500f, 449f, 500f, 549f, 500f, 551f, 500f, 449f })
        {
            tier = TierSelector.Select(tier, distance, Settings);
            tiers.Add(tier);
        }

        Assert.Equal(new[] { Statistical, Statistical, Regional, Regional, Regional, Regional, Statistical, Statistical, Regional }, tiers);
    }
}
