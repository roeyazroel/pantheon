using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>Reward, punishment and commands are checked by the authority (spec §73) and teach the Creature (spec §25).</summary>
public sealed class CreatureAuthorityTests
{
    private static readonly CreatureBrain Brain = new(CreatureWorlds.Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths);
    private static readonly CreatureAuthority Authority = new(CreatureWorlds.Rules, Brain);

    [Fact]
    public void Reward_JudgesWhatItIsDoing_RaisingThePreferenceAndLoyalty()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        Doing(creature, CreatureAction.Feed, CreatureSituation.VillageInNeed);
        var events = new List<WorldEvent>();

        var result = Authority.Execute(world, Command(creature, CreatureCommandKind.Reward), creature.GodId, events);

        Assert.True(result.IsAccepted);
        Assert.Equal(CreatureAction.Feed, result.JudgedAction);
        Assert.True(result.Preference > 0.4f);
        Assert.Equal(CreatureWorlds.Rules.StartingLoyalty + CreatureWorlds.Rules.LoyaltyPerReward, creature.Loyalty, 5);
        Assert.Equal(CreaturePhase.Performing, creature.Activity.Phase);
        Assert.Contains(events, e => e is CreatureChanged { Change: CreatureChange.Rewarded });
    }

    [Fact]
    public void Punishment_StopsWhatItIsDoing()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        Doing(creature, CreatureAction.Attack, CreatureSituation.VillageThriving);

        var result = Authority.Execute(world, Command(creature, CreatureCommandKind.Punish), creature.GodId, new List<WorldEvent>());

        Assert.True(result.IsAccepted);
        Assert.True(result.Preference < 0.05f);
        Assert.Equal(CreaturePhase.Deciding, creature.Activity.Phase);
        Assert.True(creature.Loyalty < CreatureWorlds.Rules.StartingLoyalty);
    }

    [Fact]
    public void Feedback_JudgesTheLastDeed_OnlyWithinTheWindow()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        creature.Memories.Add(new CreatureMemory { Tick = 100, Action = CreatureAction.Play, Situation = CreatureSituation.VillageThriving, SettlementId = 1, OtherCreatureId = null, Feedback = CreatureFeedback.None });
        world.Tick = 100 + (long)(CreatureWorlds.Rules.FeedbackWindowSeconds * SimulationTime.TicksPerSecond);

        var inWindow = Authority.Execute(world, Command(creature, CreatureCommandKind.Reward), creature.GodId, new List<WorldEvent>());
        world.Tick++;
        var tooLate = Authority.Execute(world, Command(creature, CreatureCommandKind.Punish), creature.GodId, new List<WorldEvent>());

        Assert.Equal(CreatureAction.Play, inWindow.JudgedAction);
        Assert.Equal(CreatureFeedback.Rewarded, creature.Memories[0].Feedback);
        Assert.Equal(CreatureCommandRejection.NothingToJudge, tooLate.Rejection);
    }

    [Fact]
    public void Commands_AreCheckedBeforeAnythingChanges()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        Doing(creature, CreatureAction.Feed, CreatureSituation.VillageInNeed);
        var events = new List<WorldEvent>();

        Assert.Equal(CreatureCommandRejection.NotAuthorized, Authority.Execute(world, Command(creature, CreatureCommandKind.Reward), 9, events).Rejection);
        Assert.Equal(CreatureCommandRejection.UnknownGod, Authority.Execute(world, new CreatureCommand(9, creature.Id, CreatureCommandKind.Reward), null, events).Rejection);
        Assert.Equal(CreatureCommandRejection.UnknownCreature, Authority.Execute(world, new CreatureCommand(creature.GodId, 9, CreatureCommandKind.Reward), null, events).Rejection);
        creature.Activity.Phase = CreaturePhase.Banished;
        Assert.Equal(CreatureCommandRejection.Banished, Authority.Execute(world, Command(creature, CreatureCommandKind.Reward), creature.GodId, events).Rejection);
        Assert.Empty(events);
        Assert.Equal(0.4f, CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Feed));
    }

    [Fact]
    public void GoTo_SendsItOverLand_OrSaysWhyNot()
    {
        var (world, creature) = CreatureWorlds.Create(200f);
        foreach (var cell in world.Regions.Cells)
        {
            if (world.Regions.GetCoordinates(cell.Id).Column == 12)
            {
                cell.Biome = Biome.Sea;
            }
        }

        var near = Authority.Execute(world, Command(creature, CreatureCommandKind.GoTo, new GroundPosition(500f, 500f)), creature.GodId, new List<WorldEvent>());
        Assert.True(near.IsAccepted);
        Assert.Equal(CreatureAction.GoTo, creature.Activity.Action);
        Assert.Equal(CreatureSituation.Commanded, creature.Activity.Situation);

        Assert.Equal(CreatureCommandRejection.Unreachable, Authority.Execute(world, Command(creature, CreatureCommandKind.GoTo, new GroundPosition(950f, 500f)), creature.GodId, new List<WorldEvent>()).Rejection);
        Assert.Equal(CreatureCommandRejection.OutsideWorld, Authority.Execute(world, Command(creature, CreatureCommandKind.GoTo, new GroundPosition(-50f, 500f)), creature.GodId, new List<WorldEvent>()).Rejection);
    }

    /// <summary>A command from the Creature's god.</summary>
    private static CreatureCommand Command(CreatureState creature, CreatureCommandKind kind, GroundPosition target = default) =>
        new(creature.GodId, creature.Id, kind, target);

    /// <summary>Puts the Creature in the middle of an action.</summary>
    private static void Doing(CreatureState creature, CreatureAction action, CreatureSituation situation)
    {
        creature.Activity.Action = action;
        creature.Activity.Situation = situation;
        creature.Activity.Phase = CreaturePhase.Performing;
        creature.Activity.TargetSettlementId = 1;
        creature.Activity.TicksLeft = 20;
    }
}
