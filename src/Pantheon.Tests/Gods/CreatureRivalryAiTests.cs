using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>AI gods and their Creatures' dealings with other Creatures (ADR-017).</summary>
public sealed class CreatureRivalryAiTests
{
    private static readonly CreatureRules Rules = CreatureBondWorlds.Rules;

    [Theory]
    [InlineData(DiplomaticStatus.War, CreatureAction.Fight, CreatureFeedback.Rewarded)]
    [InlineData(DiplomaticStatus.War, CreatureAction.Befriend, CreatureFeedback.Punished)]
    [InlineData(DiplomaticStatus.Alliance, CreatureAction.Fight, CreatureFeedback.Punished)]
    [InlineData(DiplomaticStatus.Alliance, CreatureAction.Befriend, CreatureFeedback.Rewarded)]
    public void AnyGod_TeachesItsCreatureToFightEnemiesAndBefriendAllies(DiplomaticStatus status, CreatureAction deed, CreatureFeedback expected)
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, status);
        var situation = CreatureBonds.SituationFor(world, theirs, mine);
        CreatureModel.Remember(Rules, theirs, world.Tick, deed, situation, null, mine.Id);
        var before = CreatureModel.Preference(theirs, situation, deed);

        Ai().Act(world, world.Gods.Single(g => g.Id == 2), new SimulationRandom(3), new List<WorldEvent>());

        Assert.Equal(expected, theirs.Memories[^1].Feedback);
        var after = CreatureModel.Preference(theirs, situation, deed);
        Assert.True(expected == CreatureFeedback.Rewarded ? after > before : after < before);
    }

    [Fact]
    public void AGodAtWar_SendsItsCreatureAgainstTheEnemysCreatureNearItsPeople()
    {
        var (world, mine, _) = CreatureBondWorlds.Create(gap: 300f);
        var ai = Ai();
        var tyrant = world.Gods.Single(g => g.Id == 2);
        Assert.DoesNotContain(ai.Evaluate(world, tyrant), c => c.Reason.StartsWith("confront", System.StringComparison.Ordinal));

        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        var confront = ai.Evaluate(world, tyrant).Single(c => c.Reason.StartsWith("confront", System.StringComparison.Ordinal));

        Assert.Equal(AiActionKind.SetCreatureOn, confront.Kind);
        Assert.Equal(mine.Position, confront.Target);
        var profile = PantheonWorlds.Profile(Domain.Gods.GodPersonality.Tyrant);
        Assert.Equal(profile.SendCreature * ((profile.Protect * 0.6f) + (0.5f * profile.Aggression)), confront.Value, 3);

        mine.Level = 9;
        Assert.DoesNotContain(ai.Evaluate(world, tyrant), c => c.Reason.StartsWith("confront", System.StringComparison.Ordinal));
    }

    [Fact]
    public void AnAiGod_DoesNotPullItsCreatureOutOfAFight()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 300f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        var brain = CreatureBondWorlds.System().Brain;
        brain.Engage(theirs, CreatureAction.Fight, CreatureSituation.MeetsEnemyCreature, mine);
        brain.Engage(mine, CreatureAction.Fight, CreatureSituation.MeetsEnemyCreature, theirs);

        Assert.DoesNotContain(Ai().Evaluate(world, world.Gods.Single(g => g.Id == 2)), c => c.Kind == AiActionKind.SendCreature);
    }

    /// <summary>A god AI with Creatures over the pinned rules.</summary>
    private static GodAi Ai()
    {
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);
        var authority = new CreatureAuthority(Rules, CreatureBondWorlds.System().Brain);
        return new GodAi(RivalWorlds.AiRules, DivineWorlds.Rules, DivineWorlds.Miracles, caster, authority, Rules);
    }
}
