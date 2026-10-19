using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Disasters;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>The paths Creatures take up and the attributes that grow with level (spec §23–24, ADR-030).</summary>
public sealed class CallingTests
{
    private static readonly CreatureRules Rules = CreatureWorlds.Rules;
    private static readonly CallingRules Callings = Rules.Callings;

    [Fact]
    public void Deeds_CallTheCreatureDownTheirPaths_AndRewardsCallItFurther()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var village = world.Settlements[0];

        Do(world, creature, CreatureAction.Feed, village.Id);
        Do(world, creature, CreatureAction.Forage, null);

        Assert.Equal(1f, creature.Callings.Healer, 4);
        Assert.Equal(0.3f, creature.Callings.Guardian, 4);
        Assert.Equal(1f, creature.Callings.NatureKeeper, 4);

        CreatureCalling.Deed(Callings, creature, CreatureAction.Feed, Callings.RewardCalling);
        Assert.Equal(2f, creature.Callings.Healer, 4);
        Assert.Equal("Healer 2.0, Nature Keeper 1.0, Guardian 0.6", Pantheon.Game.Creatures.CreatureGrowthText.Callings(creature));
    }

    [Fact]
    public void AtEachCallingLevel_ItTakesUpThePathItWalkedMost_NeverTwice_AndNeverAll()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        creature.Callings.Healer = 5f;
        creature.Callings.Warrior = 3f;
        var events = new List<WorldEvent>();

        creature.Level = 2;
        CreatureCalling.Grow(Callings, creature, world.Tick, events);
        Assert.Empty(creature.Specialisations);

        creature.Level = 6;
        CreatureCalling.Grow(Callings, creature, world.Tick, events);
        Assert.Equal([CreatureSpecialisation.Healer, CreatureSpecialisation.Warrior], creature.Specialisations);

        creature.Level = 10;
        CreatureCalling.Grow(Callings, creature, world.Tick, events);
        CreatureCalling.Grow(Callings, creature, world.Tick, events);

        Assert.Equal(3, creature.Specialisations.Count);
        Assert.Equal(3, creature.Specialisations.Distinct().Count());
        Assert.Equal(creature.Specialisations, events.OfType<CreatureSpecialised>().Select(e => e.Specialisation));
    }

    [Fact]
    public void WithNoCalling_ItFollowsItsStrongestTrait()
    {
        var (_, creature) = CreatureWorlds.Create(300f);
        (creature.Personality.Kindness, creature.Personality.Aggression, creature.Personality.Curiosity, creature.Personality.Laziness) = (0.1f, 0.9f, 0.5f, 0.2f);

        Assert.Equal(CreatureSpecialisation.Warrior, CreatureCalling.Next(creature));
        creature.Specialisations.Add(CreatureSpecialisation.Warrior);
        Assert.Equal(CreatureSpecialisation.NatureKeeper, CreatureCalling.Next(creature));
    }

    [Fact]
    public void AWonFight_CallsTowardsWarrior_AndMercy_TowardsGuardian()
    {
        var (_, creature) = CreatureWorlds.Create(300f);

        CreatureCalling.Add(creature.Callings, CreatureSpecialisation.Warrior, Callings.VictoryCalling);
        CreatureCalling.Add(creature.Callings, CreatureSpecialisation.Guardian, Callings.MercyCalling);

        Assert.Equal((1.5f, 1f), (creature.Callings.Warrior, creature.Callings.Guardian));
    }

    [Fact]
    public void AHealer_HealsTheVillagesItHelps_AndAMissionary_WinsTheirGodless()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var village = world.Settlements[0];
        village.Health = 0.5f;
        var before = BeliefModel.Find(village, creature.GodId)!.Adherence;
        creature.Specialisations.AddRange([CreatureSpecialisation.Healer, CreatureSpecialisation.Missionary]);

        Do(world, creature, CreatureAction.Play, village.Id);

        Assert.Equal(0.5f + Callings.HealerHealth, village.Health, 4);
        Assert.Equal(before + (Callings.MissionaryConversion * (1f - before)), BeliefModel.Find(village, creature.GodId)!.Adherence, 4);
    }

    [Fact]
    public void ATrickster_SowsDoubtInTheRivalGodOfTheVillagesItVisits()
    {
        var (world, creature) = CreatureWorlds.Create(300f, 600f);
        var rivalVillage = world.Settlements[1];
        var rival = DivineWorlds.Believe(rivalVillage, adherence: 0.7f, faith: 0.6f, godId: 9);
        creature.Specialisations.Add(CreatureSpecialisation.Trickster);

        Do(world, creature, CreatureAction.Play, rivalVillage.Id);

        Assert.Equal(Callings.TricksterDoubt, rival.Doubt, 4);
    }

    [Fact]
    public void ANatureKeeper_HealsTheLandWhereItGoes()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        world.Regions.TryGetRegionIdAt(creature.Position.X, creature.Position.Z, out var regionId);
        var region = world.Regions.Cells[regionId];
        (region.Damage, region.Scar, region.Blight) = (0.5f, 0.1f, 0.3f);
        creature.Specialisations.Add(CreatureSpecialisation.NatureKeeper);

        Do(world, creature, CreatureAction.Wander, null);

        Assert.Equal(0.5f - Callings.KeeperHealing, region.Damage, 4);
        Assert.Equal(0.3f - Callings.KeeperHealing, region.Blight, 4);
    }

    [Fact]
    public void ADestroyer_KillsMore_AndACommanderNearAVillage_DefendsIt()
    {
        int Killed(CreatureSpecialisation? attacker, bool commanded)
        {
            var (world, creature) = CreatureWorlds.Create(300f, 600f);
            var village = world.Settlements[1];
            (village.Population, village.Housing) = (400, 400);
            if (attacker is { } path)
            {
                creature.Specialisations.Add(path);
            }

            if (commanded)
            {
                DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.6f, godId: 9);
                world.Creatures.Add(Rival(world, village.Position, CreatureSpecialisation.Commander));
            }

            var events = Do(world, creature, CreatureAction.Attack, village.Id);
            return events.OfType<CreatureActed>().Single().Deaths;
        }

        var plain = Killed(null, commanded: false);
        Assert.True(plain > 0);
        Assert.True(Killed(CreatureSpecialisation.Destroyer, commanded: false) > plain);
        Assert.True(Killed(null, commanded: true) < plain);
    }

    [Fact]
    public void AWarrior_FightsHarder()
    {
        var (_, creature) = CreatureWorlds.Create(300f);
        var plain = CreatureBonds.Power(Rules, creature);

        creature.Specialisations.Add(CreatureSpecialisation.Warrior);

        Assert.Equal(plain * (1f + Callings.WarriorPower), CreatureBonds.Power(Rules, creature), 4);
    }

    [Fact]
    public void AGuardian_SparesItsGodsVillagesNearItMostOfAQuake()
    {
        int Ruins(bool guarded)
        {
            var (world, creature) = CreatureWorlds.Create(300f);
            var village = world.Settlements[0];
            village.Housing = 2000;
            if (guarded)
            {
                creature.Specialisations.Add(CreatureSpecialisation.Guardian);
            }

            DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, village.Position, 7.5f, world.Tick, null);
            var system = new DisasterSystem(DisasterWorlds.Rules with { EarthquakesPerHour = 0f, HearthFirePerSecond = 0f }, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules, callings: Callings);
            for (var i = 0; i < 13; i++)
            {
                system.Update(new Pantheon.Simulation.Systems.TickContext(world, new Pantheon.Simulation.Randomness.SimulationRandom(1), []));
                world.Tick += 10;
            }

            return village.Ruins;
        }

        var bare = Ruins(guarded: false);
        Assert.True(bare > 50);
        Assert.InRange(Ruins(guarded: true), bare * 0.55, bare * 0.85);
    }

    [Fact]
    public void Intelligence_Resilience_AndAffinity_GrowWithLevel_AndAffinityWidensTheGodsInfluence()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var divine = DivineWorlds.Rules with { CreatureAffinityPerLevel = 0.1f, CreatureInfluenceStrength = 0.5f };
        var species = CreatureModel.SpeciesOf(Rules, creature);

        creature.Level = 5;

        Assert.Equal(species.Learning * (1f + (4f * Callings.IntelligencePerLevel)), CreatureAttributes.Intelligence(Rules, creature), 4);
        Assert.Equal(species.Toughness * (1f + (4f * Callings.ResiliencePerLevel)), CreatureAttributes.Resilience(Rules, creature), 4);
        Assert.Equal(1.4f, CreatureAttributes.Affinity(divine, creature), 4);
        Assert.Contains(InfluenceField.Build(divine, world, creature.GodId).Sources, s => s.Position == creature.Position && System.Math.Abs(s.Reach - (divine.CreatureInfluenceReach * 1.4f)) < 0.01f);
    }

    [Fact]
    public void InTheEngine_ACreatureThatLevels_TakesUpAPath_AndItIsTold()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        creature.Callings.Healer = 2f;
        CreatureModel.AddExperience(Rules, creature, Rules.LevelExperience[1]);
        var events = new List<WorldEvent>();

        new CreatureSystem(Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths).Update(new Pantheon.Simulation.Systems.TickContext(world, new Pantheon.Simulation.Randomness.SimulationRandom(1), events));

        Assert.Equal(3, creature.Level);
        Assert.Equal(CreatureSpecialisation.Healer, Assert.Single(creature.Specialisations));
        Assert.Equal(CreatureSpecialisation.Healer, Assert.Single(events.OfType<CreatureSpecialised>()).Specialisation);
    }

    /// <summary>A rival god's Creature standing at a point, walking a path.</summary>
    private static CreatureState Rival(WorldState world, GroundPosition position, CreatureSpecialisation path)
    {
        var rival = CreatureFounder.Found(Rules, world.Seed, DivineWorlds.God(9, "Rivalis", Pantheon.Domain.Gods.GodController.Ai, Pantheon.Domain.Gods.GodPersonality.Tyrant, 0f), "Rivalling", world.Settlements[0], world.Regions);
        var placed = new CreatureState
        {
            Id = 9, GodId = 9, Name = rival.Name, Species = rival.Species, Needs = rival.Needs, Personality = rival.Personality, Activity = rival.Activity,
            Preferences = rival.Preferences, Memories = rival.Memories, Position = position, Heading = 0f, Level = 1, Experience = 0f, Health = 1f,
            Loyalty = 0.7f, Morality = 0f, HomeSettlementId = world.Settlements[0].Id, CombatSkill = 0f, Scars = 0, Relations = [],
        };
        placed.Specialisations.Add(path);
        return placed;
    }

    /// <summary>Completes a deed at once.</summary>
    private static List<WorldEvent> Do(WorldState world, CreatureState creature, CreatureAction action, int? settlementId)
    {
        var activity = creature.Activity;
        (activity.Action, activity.Situation, activity.TargetSettlementId, activity.Phase) = (action, CreatureSituation.Idle, settlementId, CreaturePhase.Performing);
        var events = new List<WorldEvent>();
        new CreatureDeeds(Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths).Complete(world, creature, events);
        return events;
    }
}
