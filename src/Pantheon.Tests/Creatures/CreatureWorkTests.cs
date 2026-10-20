using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Resources;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>A Creature working for the villages (ADR-063): farming their fields, fetching their wood, and building with it.</summary>
public sealed class CreatureWorkTests
{
    private static readonly SettlementRules Settlements = TestContent.Shipped.SettlementRules;
    private static readonly SimulationEngine Engine = new(Settlements with { BirthChancePerPersonPerStep = 0.0, DeathChancePerPersonPerStep = 0.0, IllHealthDeathChancePerPersonPerStep = 0.0 }, DivineWorlds.Rules, CreatureWorlds.Rules);
    private static readonly CreatureBrain Brain = new(CreatureWorlds.Rules, DivineWorlds.Rules, Settlements);

    [Fact]
    public void Farming_TillsWatersAndWeedsTheFields_AndBringsInAHarvest()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        village.Food = 0f;
        var field = world.Regions.GetRegion(village.RegionId);
        field.Blight = 0.4f;

        var events = Perform(world, creature, CreatureAction.Farm, CreatureSituation.VillageInNeed, village.Id);

        var acted = Assert.Single(events.OfType<CreatureActed>());
        Assert.True(acted.Food > 0f && village.Food >= acted.Food, "a harvest came in");
        Assert.True(field.SoilMoisture > 0.5f && field.Blight < 0.4f && field.Fertility > 0.6f, "the fields were watered, weeded and tilled");
        Assert.Equal(CreatureAction.Farm, creature.Memories[^1].Action);
    }

    [Fact]
    public void FetchingWood_ThinsTheForestWhereItStands_AndFillsTheStores()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        village.Wood = 0f;
        world.Regions.TryGetRegionIdAt(creature.Position.X, creature.Position.Z, out var standing);
        var forest = world.Regions.GetRegion(standing);
        (forest.Forest, forest.Timber) = (1f, 0.8f);

        var events = Complete(world, creature, CreatureAction.GatherWood, CreatureSituation.VillageBuilding, village.Id);

        var wood = Assert.Single(events.OfType<CreatureActed>()).Wood;
        Assert.True(wood > 5f, "it brought an armful of timber");
        Assert.Equal(wood, village.Wood, 2);
        Assert.Equal(0.8f - (wood * Settlements.Resources.TimberPerWood), forest.Timber, 3);
    }

    [Fact]
    public void Building_SpendsTheVillagesWood_AndWithoutWoodNothingGoesUp()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        (village.Wood, village.ConstructionProgress, village.Housing) = (0f, 0f, 10);

        Complete(world, creature, CreatureAction.HelpBuild, CreatureSituation.VillageBuilding, village.Id);
        Assert.Equal((0f, 10), (village.ConstructionProgress, village.Housing));

        village.Wood = 100f;
        Complete(world, creature, CreatureAction.HelpBuild, CreatureSituation.VillageBuilding, village.Id);
        Assert.True(village.ConstructionProgress > 0f || village.Housing > 10);
        Assert.True(village.Wood < 100f, "the work spent wood");
    }

    [Fact]
    public void ACreature_OffersToFarmForAHungryVillage_AndToFetchWoodForOneShortOfIt()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        village.Food = 0f;
        Assert.Contains(Brain.ListOptions(world, creature), o => o.Action == CreatureAction.Farm && o.Situation == CreatureSituation.VillageInNeed);

        village.Food = 1000f;
        village.Wood = 0f;
        var forest = world.Regions.GetRegion(village.RegionId + 2);
        (forest.Forest, forest.Timber) = (1f, 0.9f);
        var fetch = Assert.Single(Brain.ListOptions(world, creature), o => o.Action == CreatureAction.GatherWood);
        Assert.Equal(world.Regions.GetRegionCenter(forest.Id), fetch.Target);
    }

    [Fact]
    public void TheWorkCommand_DoesWhatTheVillageNeeds_OrFetchesFromTheWoodPointedAt()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        var authority = new CreatureAuthority(CreatureWorlds.Rules, Brain);
        village.Food = 0f;

        var farm = authority.Execute(world, Work(creature, village.Position), creature.GodId, new List<WorldEvent>());
        Assert.Equal((true, CreatureAction.Farm), (farm.IsAccepted, creature.Activity.Action));
        Assert.Equal(CreatureSituation.VillageInNeed, creature.Activity.Situation);

        var forest = world.Regions.GetRegion(world.Regions.Cells.Count - 1);
        (forest.Forest, forest.Timber) = (1f, 0.9f);
        var wood = authority.Execute(world, Work(creature, world.Regions.GetRegionCenter(forest.Id)), creature.GodId, new List<WorldEvent>());
        Assert.Equal((true, CreatureAction.GatherWood, (int?)village.Id), (wood.IsAccepted, creature.Activity.Action, creature.Activity.TargetSettlementId));
        Assert.Equal(world.Regions.GetRegionCenter(forest.Id), creature.Activity.Target);

        var bare = world.Regions.GetRegion(0);
        bare.Timber = 0f;
        Assert.Equal(CreatureCommandRejection.NoWorkThere, authority.Execute(world, Work(creature, world.Regions.GetRegionCenter(0)), creature.GodId, new List<WorldEvent>()).Rejection);
    }

    [Fact]
    public void RewardingCommandedWork_TeachesItToDoSoUnasked()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        world.Settlements[0].Food = 0f;
        var authority = new CreatureAuthority(CreatureWorlds.Rules, Brain);
        authority.Execute(world, Work(creature, world.Settlements[0].Position), creature.GodId, new List<WorldEvent>());
        var before = CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Farm);

        var reward = authority.Execute(world, new CreatureCommand(creature.GodId, creature.Id, CreatureCommandKind.Reward), creature.GodId, new List<WorldEvent>());

        Assert.Equal(CreatureAction.Farm, reward.JudgedAction);
        Assert.True(CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Farm) > before);
    }

    /// <summary>Finishes an action at once, with nothing else of the world running.</summary>
    private static List<WorldEvent> Complete(WorldState world, CreatureState creature, CreatureAction action, CreatureSituation situation, int? settlementId)
    {
        var activity = creature.Activity;
        (activity.Action, activity.Situation, activity.TargetSettlementId, activity.Target, activity.Phase) = (action, situation, settlementId, creature.Position, CreaturePhase.Performing);
        var events = new List<WorldEvent>();
        new CreatureDeeds(CreatureWorlds.Rules, DivineWorlds.Rules, Settlements).Complete(world, creature, events);
        return events;
    }

    private static CreatureCommand Work(CreatureState creature, GroundPosition at) => new(creature.GodId, creature.Id, CreatureCommandKind.Work, at);

    private static List<WorldEvent> Perform(WorldState world, CreatureState creature, CreatureAction action, CreatureSituation situation, int? settlementId)
    {
        var activity = creature.Activity;
        (activity.Action, activity.Situation, activity.TargetSettlementId, activity.Target) = (action, situation, settlementId, creature.Position);
        activity.Path.Clear();
        activity.Phase = CreaturePhase.Performing;
        activity.TicksLeft = Brain.PerformTicks(creature, action);
        var events = new List<WorldEvent>();
        while (activity.Phase == CreaturePhase.Performing && activity.Action == action)
        {
            Engine.Step(world, events);
        }

        return events;
    }
}
