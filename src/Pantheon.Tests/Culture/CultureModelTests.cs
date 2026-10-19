using Pantheon.Domain.Belief;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Culture;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Culture;

/// <summary>The rules of culture as pure functions (spec §7, §8, §33, §35, ADR-024).</summary>
public sealed class CultureModelTests
{
    private static readonly CultureRules Rules = CultureWorlds.Shipped;

    [Fact]
    public void Secularism_PassesThroughItsStages()
    {
        Assert.Equal(SecularStage.Devout, CultureModel.Stage(Rules, Rules.Agnostic - 0.01f));
        Assert.Equal(SecularStage.Agnostic, CultureModel.Stage(Rules, Rules.Agnostic));
        Assert.Equal(SecularStage.Atheist, CultureModel.Stage(Rules, Rules.Atheist));
        Assert.Equal(SecularStage.AntiDivine, CultureModel.Stage(Rules, Rules.AntiDivine));
    }

    [Fact]
    public void Fear_BreedsResentment_LoveDissolvesIt_AndSacrificeSoftensIt()
    {
        var feared = Belief(fear: 1f);
        var loved = Belief(fear: 1f, love: 1f, resentment: 0.5f);
        var appeased = Belief(fear: 1f);
        var sacrificing = Culture(c => c.Sacrifice = 1f);

        CultureModel.DriftFeelings(Rules, feared, CultureState.Founded());
        CultureModel.DriftFeelings(Rules, loved, CultureState.Founded());
        CultureModel.DriftFeelings(Rules, appeased, sacrificing);

        Assert.Equal(Rules.ResentmentFromFear, feared.Resentment, 5);
        Assert.True(loved.Resentment < 0.5f);
        Assert.True(appeased.Resentment < feared.Resentment);
    }

    [Fact]
    public void Dependency_FadesInComfort_AndTheDependentDoubtInHardship()
    {
        var belief = Belief(dependency: 0.8f);

        CultureModel.DriftFeelings(Rules, belief, CultureState.Founded());

        Assert.Equal(0.8f * (1f - Rules.DependencyFade), belief.Dependency, 5);
        Assert.Equal(0f, CultureModel.Expectation(Rules, belief, hardship: 0f));
        Assert.True(CultureModel.Expectation(Rules, belief, hardship: 1f) > CultureModel.Expectation(Rules, Belief(dependency: 0.1f), hardship: 1f));
    }

    [Fact]
    public void Worship_IsHarderAmongTheDependentAndFearfulSacrificers_AndWeakWhereTheGodsAreDoubted()
    {
        var devout = CultureState.Founded();
        var sacrificing = Culture(c => c.Sacrifice = 1f);
        var secular = Culture(c => c.Secularism = 1f);

        Assert.Equal(1f, CultureModel.WorshipFactor(Rules, Belief(), devout));
        Assert.Equal(1f + Rules.DependencyWorship, CultureModel.WorshipFactor(Rules, Belief(dependency: 1f), devout), 5);
        Assert.Equal(1f + Rules.SacrificeWorship, CultureModel.WorshipFactor(Rules, Belief(fear: 1f), sacrificing), 5);
        Assert.Equal(1f - Rules.SecularPowerLoss, CultureModel.WorshipFactor(Rules, Belief(), secular), 5);
    }

    [Fact]
    public void SecularPeople_ResistEveryGod_AndIsolationists_EveryGodButTheirOwn()
    {
        var secular = Culture(c => c.Secularism = 1f);
        var closed = Culture(c => c.Isolationism = 1f);

        Assert.Equal(1f - Rules.SecularResistance, CultureModel.PullFactor(Rules, secular, isMain: true), 5);
        Assert.Equal(1f, CultureModel.PullFactor(Rules, closed, isMain: true));
        Assert.Equal(1f - Rules.DoctrineEffect, CultureModel.PullFactor(Rules, closed, isMain: false), 5);
        Assert.Equal(1f - Rules.DoctrineEffect, CultureModel.WelcomeFactor(Rules, closed), 5);
    }

    [Fact]
    public void Miracles_MoveSecularWitnessesLess_AndNatureWorshippersMoreWhenTheLandIsBlessed()
    {
        var rain = DivineWorlds.Miracles.Find("rain")!;
        var lightning = DivineWorlds.Miracles.Find("lightning")!;
        var fire = DivineWorlds.Miracles.Find("fire")!;
        var nature = Culture(c => c.NatureWorship = 1f);

        Assert.True(CultureModel.Blesses(rain) && !CultureModel.Harms(rain));
        Assert.True(CultureModel.Harms(lightning) && !CultureModel.Ravages(lightning));
        Assert.True(CultureModel.Ravages(fire));
        Assert.Equal(1f - Rules.MiracleResistance, CultureModel.WitnessFactor(Rules, Culture(c => c.Secularism = 1f), lightning), 5);
        Assert.Equal(1f + Rules.DoctrineEffect, CultureModel.WitnessFactor(Rules, nature, rain), 5);
        Assert.Equal(1f, CultureModel.WitnessFactor(Rules, nature, lightning));
        Assert.True(CultureModel.OutrageFactor(Rules, nature, fire) > CultureModel.OutrageFactor(Rules, nature, lightning));
        Assert.True(CultureModel.OutrageFactor(Rules, Culture(c => c.Militarism = 1f), lightning) < 1f);
    }

    [Fact]
    public void Doctrines_FollowWhatAVillageLivesThrough()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.6f, love: 0.9f, fear: 0.1f);
        float Target(Doctrine doctrine, float war = 0f, int neighbours = 4, float blessing = 0f) => CultureModel.DoctrineTarget(world, village, war, neighbours, blessing, doctrine);

        Assert.True(Target(Doctrine.Pacifism) > Target(Doctrine.Militarism));
        Assert.True(Target(Doctrine.Militarism, war: 1f) > Target(Doctrine.Pacifism, war: 1f));
        Assert.True(Target(Doctrine.Charity) > Target(Doctrine.Sacrifice));
        belief.Fear = 0.9f;
        Assert.True(Target(Doctrine.Sacrifice) > 0.7f);
        Assert.True(Target(Doctrine.NatureWorship, blessing: 1f) > Target(Doctrine.NatureWorship));
        Assert.True(Target(Doctrine.Isolationism, neighbours: 0) > Target(Doctrine.Isolationism));
        village.Society.Education = 0.9f;
        village.Society.Priority = SettlementPriority.Expansion;
        Assert.True(Target(Doctrine.KnowledgeSeeking) > 0.7f);
        Assert.True(Target(Doctrine.Expansionism) >= 0.5f);
        world.Gods[0].Domains.Add(GodDomain.Nature);
        world.Gods[0].Traits.Add(GodTrait.Merciful);
        Assert.True(Target(Doctrine.NatureWorship) >= 0.4f);
        Assert.True(Target(Doctrine.Pacifism) > 0.7f);
    }

    [Fact]
    public void Secularism_TendsUpWithDoubtTheGodlessAndResentment_AndDownWithFaith()
    {
        var world = DivineWorlds.Create(0f, 300f, 600f);
        var faithful = world.Settlements[0];
        var doubting = world.Settlements[1];
        DivineWorlds.Believe(faithful, adherence: 0.9f, faith: 0.9f);
        var belief = DivineWorlds.Believe(doubting, adherence: 0.4f, faith: 0.1f, doubt: 0.9f);
        belief.Resentment = 0.8f;

        Assert.Equal(0f, CultureModel.SecularTarget(Rules, faithful));
        Assert.True(CultureModel.SecularTarget(Rules, doubting) > Rules.Agnostic);
    }

    private static GodBelief Belief(float fear = 0f, float love = 0f, float dependency = 0f, float resentment = 0f) =>
        new() { GodId = 1, Adherence = 0.5f, Faith = 0.5f, Love = love, Fear = fear, Doubt = 0f, Dependency = dependency, Resentment = resentment };

    private static CultureState Culture(System.Action<CultureState> set)
    {
        var culture = CultureState.Founded();
        set(culture);
        return culture;
    }
}
