using System.Collections.Generic;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Miracles;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Culture;

/// <summary>What culture adds when people witness miracles, and what the world notes of the caster (spec §3.2, §7, §8, §33, §34, ADR-024).</summary>
public sealed class WitnessCultureTests
{
    private static readonly CultureRules Rules = CultureWorlds.Shipped;

    [Fact]
    public void HarmToAGodsOwnFollowers_BreedsResentment_AndAmongTheFearful_Sacrifice()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.5f, fear: 0.8f);

        WitnessCulture.Witnessed(Rules, village, belief, DivineWorlds.Miracles.Find("lightning")!, need: 0f, closeness: 1f);

        Assert.Equal(Rules.ResentmentFromHarm, belief.Resentment, 4);
        Assert.Equal(Rules.DoctrineFromMiracle * 0.8f, village.Culture.Sacrifice, 4);
    }

    [Fact]
    public void HelpInNeed_BreedsDependencyAndCharity_AndBlessingsOfTheLand_NatureWorship()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.5f);

        WitnessCulture.Witnessed(Rules, village, belief, DivineWorlds.Miracles.Find("food")!, need: 1f, closeness: 1f);
        WitnessCulture.Witnessed(Rules, village, belief, DivineWorlds.Miracles.Find("rain")!, need: 0f, closeness: 1f);

        Assert.Equal(Rules.DependencyFromHelp, belief.Dependency, 4);
        Assert.Equal(Rules.DoctrineFromMiracle, village.Culture.Charity, 4);
        Assert.Equal(Rules.DoctrineFromMiracle, village.Culture.NatureWorship, 4);
        Assert.Equal(0f, belief.Resentment);
    }

    [Fact]
    public void TheWorld_NotesHelpGiftsHarmCrueltyVengeanceAndUpheaval()
    {
        var world = DivineWorlds.Create(0f, 300f, 600f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", Domain.Gods.GodController.Ai, Domain.Gods.GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.6f, faith: 0.5f, godId: 2);
        var conduct = world.Gods[0].Conduct;
        var needs = new Dictionary<int, float> { [1] = 1f, [2] = 0f };

        WitnessCulture.NoteCast(world, 1, DivineWorlds.Miracles.Find("food")!, [1], needs, deaths: 0, isGuilty: _ => false);
        WitnessCulture.NoteCast(world, 1, DivineWorlds.Miracles.Find("food")!, [2], needs, deaths: 0, isGuilty: _ => false);
        WitnessCulture.NoteCast(world, 1, DivineWorlds.Miracles.Find("fire")!, [1], needs, deaths: 4, isGuilty: _ => false);
        WitnessCulture.NoteCast(world, 1, DivineWorlds.Miracles.Find("lightning")!, [2], needs, deaths: 0, isGuilty: god => god == 2);

        Assert.Equal((1f, 0.5f), (conduct.Help, conduct.Gifts));
        Assert.Equal(1.2f + 1f, conduct.Harm, 4);
        Assert.Equal((1f, 1f, 1f), (conduct.Cruelty, conduct.Vengeance, conduct.Upheaval));
    }

    [Fact]
    public void Casting_LightningOnItsOwnPeople_LeavesThemResentful_AndTheWorldCallsTheGodCruel()
    {
        var world = DivineWorlds.Create(10000f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f, fear: 0.5f);
        var caster = new MiracleCaster(DivineWorlds.Rules with { Culture = Rules }, SettlementWorlds.Rules, DivineWorlds.Miracles);

        for (var i = 0; i < 3; i++)
        {
            caster.Cast(world, new CastMiracleRequest(1, "lightning", world.Settlements[0].Position), 1, []);
            world.Tick += 1000;
        }

        Assert.True(belief.Resentment > 0.2f, $"resentment {belief.Resentment}");
        Assert.Contains(GodTrait.Cruel, ConductModel.Traits(Rules, world.Gods[0].Conduct));
    }

    [Fact]
    public void BetrayingLove_ShocksTheMoreWhenTheGodWasKnownForMercy()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var lightning = DivineWorlds.Miracles.Find("lightning")!;
        var before = WitnessCulture.BetrayalFactor(Rules, world, 1, village, lightning);

        world.Gods[0].Traits.Add(GodTrait.Merciful);

        Assert.Equal(1f, before);
        Assert.Equal(1f + Rules.Contradiction, WitnessCulture.BetrayalFactor(Rules, world, 1, village, lightning), 4);
    }
}
