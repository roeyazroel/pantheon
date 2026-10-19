using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>The Creature behaves on its own (spec §119): it chooses from its needs and preferences, walks, acts and changes the world.</summary>
public sealed class CreatureBehaviourTests
{
    private static readonly SimulationEngine Engine = new(SettlementWorlds.NoBirthsOrDeaths, DivineWorlds.Rules, CreatureWorlds.Rules);
    private static readonly CreatureBrain Brain = new(CreatureWorlds.Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths);

    [Fact]
    public void HungryCreature_GoesForFood()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        creature.Needs.Hunger = 0.95f;
        creature.Needs.Boredom = 0f;

        Brain.Decide(world, creature, new SimulationRandom(5));

        Assert.Contains(creature.Activity.Action, new[] { CreatureAction.Forage, CreatureAction.EatFromStores });
        Assert.Equal(CreatureSituation.Hungry, creature.Activity.Situation);
    }

    [Fact]
    public void TiredCreature_Rests()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        creature.Needs.Fatigue = 1f;
        creature.Needs.Hunger = 0f;
        creature.Needs.Boredom = 0f;

        Brain.Decide(world, creature, new SimulationRandom(5));

        Assert.Equal(CreatureAction.Rest, creature.Activity.Action);
        Assert.Equal(CreaturePhase.Performing, creature.Activity.Phase);
    }

    [Fact]
    public void WhatItLearned_DecidesWhatItDoesForAVillageInNeed()
    {
        var (world, kind) = CreatureWorlds.Create(200f);
        var (cruelWorld, cruel) = CreatureWorlds.Create(200f);
        foreach (var w in new[] { world, cruelWorld })
        {
            w.Settlements[0].Food = 0f;
        }

        foreach (var c in new[] { kind, cruel })
        {
            c.Needs.Hunger = 0f;
            c.Needs.Fatigue = 0f;
            c.Needs.Boredom = 0f;
        }

        for (var i = 0; i < 6; i++)
        {
            CreatureModel.Learn(CreatureWorlds.Rules, kind, CreatureSituation.VillageInNeed, CreatureAction.Feed, CreatureFeedback.Rewarded);
            CreatureModel.Learn(CreatureWorlds.Rules, cruel, CreatureSituation.VillageInNeed, CreatureAction.Attack, CreatureFeedback.Rewarded);
            CreatureModel.Learn(CreatureWorlds.Rules, cruel, CreatureSituation.VillageInNeed, CreatureAction.Feed, CreatureFeedback.Punished);
        }

        Assert.Equal(CreatureAction.Feed, Brain.ListOptions(world, kind)[0].Action);
        Assert.Equal(CreatureAction.Attack, Brain.ListOptions(cruelWorld, cruel)[0].Action);
    }

    [Fact]
    public void Options_ConsiderOnlySettlementsWithinPerception()
    {
        var (world, creature) = CreatureWorlds.Create(200f, 900f);
        var far = world.Settlements[1].Id;

        var options = Brain.ListOptions(world, creature);

        Assert.DoesNotContain(options, option => option.SettlementId == far);
        Assert.Contains(options, option => option.SettlementId == world.Settlements[0].Id);
    }

    [Fact]
    public void Creature_WalksToItsTarget_AtItsSpeed()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var start = creature.Position;
        var speed = CreatureModel.Speed(CreatureWorlds.Rules, creature);
        Assert.True(Brain.Command(world, creature, new GroundPosition(start.X + (2f * speed), start.Z)));

        Engine.Run(world, SimulationTime.TicksPerSecond);

        Assert.Equal(start.X + speed, creature.Position.X, 2);
        Assert.Equal(CreaturePhase.Travelling, creature.Activity.Phase);

        Engine.Run(world, SimulationTime.TicksPerSecond);
        Assert.Equal(CreaturePhase.Performing, creature.Activity.Phase);
        Assert.Equal(start.X + (2f * speed), creature.Position.X, 3);
    }

    [Fact]
    public void Feeding_FillsTheStores_AndWinsLoveForItsGod()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        village.Food = 0f;
        var love = BeliefModel.Find(village, creature.GodId)!.Love;
        var events = Perform(world, creature, CreatureAction.Feed, CreatureSituation.VillageInNeed, village.Id);

        var acted = Assert.Single(events.OfType<CreatureActed>());
        Assert.Equal(CreatureModel.RuleFor(CreatureWorlds.Rules, CreatureAction.Feed).Amount, acted.Food, 3);
        Assert.True(village.Food >= acted.Food);
        Assert.True(BeliefModel.Find(village, creature.GodId)!.Love > love);
        Assert.True(creature.Morality > 0f);
        Assert.Equal(CreatureAction.Feed, creature.Memories[^1].Action);
    }

    [Fact]
    public void Attacking_KillsVillagers_AndSpreadsFearOfItsGod()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        var fear = BeliefModel.Find(village, creature.GodId)!.Fear;

        var events = Perform(world, creature, CreatureAction.Attack, CreatureSituation.VillageThriving, village.Id);

        Assert.Equal(97, village.Population);
        Assert.Equal(3, events.OfType<CreatureActed>().Single().Deaths);
        Assert.True(BeliefModel.Find(village, creature.GodId)!.Fear > fear);
        Assert.True(creature.Morality < 0f);
    }

    [Fact]
    public void EatingFromStores_TakesFood_AndHelpingBuild_AddsWork()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var village = world.Settlements[0];
        creature.Needs.Hunger = 0.9f;

        var eaten = Perform(world, creature, CreatureAction.EatFromStores, CreatureSituation.Hungry, village.Id).OfType<CreatureActed>().Single();
        Assert.Equal(-CreatureModel.RuleFor(CreatureWorlds.Rules, CreatureAction.EatFromStores).Amount, eaten.Food, 2);
        Assert.True(creature.Needs.Hunger < 0.2f);

        Perform(world, creature, CreatureAction.HelpBuild, CreatureSituation.VillageBuilding, village.Id);
        Assert.Equal(CreatureModel.RuleFor(CreatureWorlds.Rules, CreatureAction.HelpBuild).Amount, village.ConstructionProgress, 2);
    }

    [Fact]
    public void Experience_RaisesItsLevel()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        creature.Experience = CreatureWorlds.Rules.LevelExperience[0] - 0.5f;

        var events = Perform(world, creature, CreatureAction.Feed, CreatureSituation.VillageInNeed, world.Settlements[0].Id);

        Assert.Equal(2, creature.Level);
        Assert.Contains(events, e => e is CreatureChanged { Change: CreatureChange.LevelledUp });
    }

    [Fact]
    public void StarvingCreature_IsBanished_ThenReturnsHome()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        creature.Needs.Hunger = 1f;
        creature.Health = 0.005f;
        creature.Position = new GroundPosition(700f, 700f);
        var events = new List<WorldEvent>();

        for (var i = 0; i < 100 && creature.Activity.Phase != CreaturePhase.Banished; i++)
        {
            Engine.Step(world, events);
        }

        Assert.Equal(CreaturePhase.Banished, creature.Activity.Phase);
        Assert.Contains(events, e => e is CreatureChanged { Change: CreatureChange.Banished });

        var banished = (int)(CreatureWorlds.Rules.BanishedSeconds * SimulationTime.TicksPerSecond);
        for (var i = 0; i < banished; i++)
        {
            Engine.Step(world, events);
        }

        Assert.Equal(CreaturePhase.Deciding, creature.Activity.Phase);
        Assert.Equal(CreatureFounder.SpawnPoint(world.Regions, world.Settlements[0].Position), creature.Position);
        Assert.Contains(events, e => e is CreatureChanged { Change: CreatureChange.Returned });
    }

    [Fact]
    public void CreaturePresence_SpreadsItsGodsInfluence()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        var far = new GroundPosition(900f, 900f);
        var before = InfluenceField.Build(DivineWorlds.Rules, world, creature.GodId).At(far);

        creature.Position = far;

        Assert.Equal(0f, before);
        Assert.Equal(DivineWorlds.Rules.CreatureInfluenceStrength, InfluenceField.Build(DivineWorlds.Rules, world, creature.GodId).At(far), 4);
    }

    [Fact]
    public void SameWorld_LivesTheSameWay()
    {
        var (first, _) = CreatureWorlds.Create(200f, 600f);
        var (second, _) = CreatureWorlds.Create(200f, 600f);

        Engine.Run(first, 3000);
        Engine.Run(second, 3000);

        WorldAssert.Equal(first, second);
        Assert.NotEmpty(first.Creatures[0].Memories);
    }

    [Fact]
    public void DesperateCreature_StopsWaitingWhereItWasSent()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        Assert.True(Brain.Command(world, creature, creature.Position with { X = creature.Position.X + 1f }));
        Engine.Run(world, 5);
        Assert.Equal(CreaturePhase.Performing, creature.Activity.Phase);

        creature.Needs.Hunger = 0.95f;
        Engine.Step(world);

        Assert.Equal(CreaturePhase.Deciding, creature.Activity.Phase);
    }

    [Fact]
    public void AiGod_DoesNotSendAHungryCreature()
    {
        var (world, creature) = CreatureWorlds.Create(200f, 900f);
        var god = world.Gods[0];
        var ai = new Pantheon.Simulation.Gods.GodAi(
            RivalWorlds.AiRules,
            DivineWorlds.Rules,
            DivineWorlds.Miracles,
            new Pantheon.Simulation.Miracles.MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles),
            new CreatureAuthority(CreatureWorlds.Rules, Brain),
            CreatureWorlds.Rules);
        var zealot = DivineWorlds.God(god.Id, god.Name, Pantheon.Domain.Gods.GodController.Ai, Pantheon.Domain.Gods.GodPersonality.Zealot, 0f);
        world.Gods[0] = new Pantheon.Domain.Gods.GodState
        {
            Id = zealot.Id,
            Name = zealot.Name,
            Controller = zealot.Controller,
            Personality = zealot.Personality,
            DivinePower = 0f,
            Rank = zealot.Rank,
            DominantSinceTick = null,
            CreatureId = creature.Id,
            RefugeePolicy = RefugeePolicy.Welcome,
            Cooldowns = [],
            Domains = [],
        };
        creature.Needs.Hunger = 0f;
        creature.Needs.Fatigue = 0f;

        Assert.Contains(ai.Evaluate(world, world.Gods[0]), c => c.Kind == Pantheon.Simulation.Gods.AiActionKind.SendCreature);

        creature.Needs.Hunger = 0.9f;
        Assert.DoesNotContain(ai.Evaluate(world, world.Gods[0]), c => c.Kind == Pantheon.Simulation.Gods.AiActionKind.SendCreature);
    }

    /// <summary>Starts an action at the Creature's position and runs it to completion.</summary>
    private static List<WorldEvent> Perform(WorldState world, CreatureState creature, CreatureAction action, CreatureSituation situation, int? settlementId)
    {
        var activity = creature.Activity;
        activity.Action = action;
        activity.Situation = situation;
        activity.TargetSettlementId = settlementId;
        activity.Target = creature.Position;
        activity.Path.Clear();
        activity.Phase = CreaturePhase.Performing;
        activity.TicksLeft = Brain.PerformTicks(creature, action);
        var events = new List<WorldEvent>();
        while (activity.Phase == CreaturePhase.Performing)
        {
            Engine.Step(world, events);
        }

        return events;
    }
}
