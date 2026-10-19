using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Culture;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Culture;

/// <summary>The culture step (spec §8, §11, §32–35, ADR-024): doctrines, secularism, missionaries, charity, revolts and traits.</summary>
public sealed class CultureSystemTests
{
    private static readonly CultureRules Fast = CultureWorlds.Shipped with { DoctrineDriftPerSecond = 0.2f, SecularDriftPerSecond = 0.2f };

    [Fact]
    public void Doctrines_DriftTowardsWhatTheVillageLivesThrough_AndAnEmbracedOneIsReportedOnce()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.9f, faith: 0.9f, love: 1f);
        var events = new List<WorldEvent>();

        Step(world, Fast, steps: 40, events);

        Assert.True(village.Culture.Charity > Fast.Embraced);
        Assert.True(village.Culture.Militarism < 0.05f);
        Assert.Single(events.OfType<DoctrineEmbraced>(), e => e.Doctrine == Doctrine.Charity);
    }

    [Fact]
    public void Secularism_RisesWhereFewFollowAndManyDoubt_ThroughEachStage()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.6f, faith: 0f, doubt: 1f);
        belief.Resentment = 1f;
        var events = new List<WorldEvent>();

        Step(world, Fast, steps: 30, events);

        Assert.Equal(SecularStage.Atheist, CultureModel.Stage(Fast, village.Culture.Secularism));
        Assert.Equal([SecularStage.Agnostic, SecularStage.Atheist], events.OfType<SecularismRose>().Select(e => e.Stage));
    }

    [Fact]
    public void AnAntiDivineVillage_TurnsItsPeopleFromTheGods_AndSpreadsItsReasonToItsNeighbours()
    {
        var world = DivineWorlds.Create(0f, 300f, 500f);
        var (godless, neighbour) = (world.Settlements[0], world.Settlements[1]);
        var belief = DivineWorlds.Believe(godless, adherence: 0.5f, faith: 0f, doubt: 1f);
        godless.Culture.Secularism = 1f;
        DivineWorlds.Believe(neighbour, adherence: 1f, faith: 1f);

        Step(world, CultureWorlds.Shipped, steps: 10);

        Assert.True(belief.Adherence < 0.5f * System.MathF.Pow(1f - CultureWorlds.Shipped.AntiDivineApostasy, 9));
        Assert.True(neighbour.Culture.Secularism > 0f);
    }

    [Fact]
    public void ATemple_SendsMissionariesToWinTheGodlessOfNeighbouringVillages()
    {
        var world = DivineWorlds.Create(0f, 300f, 500f);
        var home = world.Settlements[0];
        DivineWorlds.Believe(home, adherence: 0.9f, faith: 0.9f);
        home.Society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = DivineWorlds.GodId });

        Step(world, CultureWorlds.Shipped, steps: 60);

        var won = world.Settlements[1].Beliefs.Single(b => b.GodId == DivineWorlds.GodId);
        Assert.InRange(won.Adherence, 0.01f, 0.5f);
    }

    [Fact]
    public void ACharitableVillage_WithFullStores_FeedsAStarvingNeighbour()
    {
        var world = DivineWorlds.Create(0f, 300f, 500f);
        var (giver, hungry) = (world.Settlements[0], world.Settlements[1]);
        giver.Culture.Charity = 1f;
        giver.Food = 900f;
        hungry.Food = 0f;

        Step(world, CultureWorlds.Shipped with { DoctrineDriftPerSecond = 0f }, steps: 5);

        Assert.True(hungry.Food > 0f);
        Assert.Equal(900f, giver.Food + hungry.Food, 2);
    }

    [Fact]
    public void FearedAndResentedGods_FaceRevoltInAnUnsettledVillage_AndLoseTheirTemple()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var belief = DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.5f, fear: 0.8f);
        belief.Resentment = 0.9f;
        village.Society.Stability = 0.3f;
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = DivineWorlds.GodId });
        (village.Society.Priority, village.Society.PriorityGodId) = (SettlementPriority.Worship, DivineWorlds.GodId);
        var events = new List<WorldEvent>();

        Step(world, CultureWorlds.Shipped, steps: 1, events);

        var revolt = events.OfType<Rebellion>().Single();
        Assert.Equal((village.Id, DivineWorlds.GodId, 32), (revolt.SettlementId, revolt.GodId, revolt.People));
        Assert.Equal(0.8f * (1f - CultureWorlds.Shipped.RebellionShare), belief.Adherence, 4);
        Assert.Empty(village.Society.Buildings);
        Assert.Equal(SettlementPriority.None, village.Society.Priority);
        Assert.True(belief.Resentment < CultureWorlds.Shipped.RebellionResentment);
    }

    [Fact]
    public void ASettledVillage_DoesNotRevolt()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f, fear: 0.8f);
        belief.Resentment = 0.9f;
        var events = new List<WorldEvent>();

        Step(world, CultureWorlds.Shipped, steps: 1, events);

        Assert.Empty(events.OfType<Rebellion>());
    }

    [Fact]
    public void Traits_AreEarnedFromConduct_AndReportedOnce()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Gods[0].Conduct.Help = 2f * CultureWorlds.Shipped.TraitThreshold;
        var events = new List<WorldEvent>();

        Step(world, CultureWorlds.Shipped, steps: 3, events);

        Assert.Equal([GodTrait.Compassionate], world.Gods[0].Traits);
        Assert.Single(events.OfType<TraitEarned>());
    }

    private static void Step(WorldState world, CultureRules culture, int steps, List<WorldEvent>? events = null)
    {
        var rules = DivineWorlds.Rules with { Culture = culture };
        var system = new CultureSystem(rules, SettlementWorlds.Rules, SocietyWorlds.Rules, rules.StepIntervalTicks);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += rules.StepIntervalTicks;
        }
    }
}
