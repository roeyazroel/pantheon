using Pantheon.Domain.Creatures;
using Pantheon.Simulation.Creatures;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>A Creature's growth and learning (spec §23, §25–26).</summary>
public sealed class CreatureModelTests
{
    private static readonly CreatureRules Rules = CreatureWorlds.Rules;

    [Fact]
    public void RewardingFeeding_RaisesItsPreference_AsTheSpecExampleDoes()
    {
        var (_, creature) = CreatureWorlds.Create(200f);
        Assert.Equal(0.4f, CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Feed));

        for (var i = 0; i < 5; i++)
        {
            CreatureModel.Learn(Rules, creature, CreatureSituation.VillageInNeed, CreatureAction.Feed, CreatureFeedback.Rewarded);
        }

        // Spec §25: after repeated rewards for feeding, "Feed: 0.85".
        Assert.InRange(CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Feed), 0.85f, 0.9f);
    }

    [Fact]
    public void PunishingAttacks_DrivesThemTowardsNothing()
    {
        var (_, creature) = CreatureWorlds.Create(200f);

        for (var i = 0; i < 5; i++)
        {
            CreatureModel.Learn(Rules, creature, CreatureSituation.VillageInNeed, CreatureAction.Attack, CreatureFeedback.Punished);
        }

        Assert.InRange(CreatureModel.Preference(creature, CreatureSituation.VillageInNeed, CreatureAction.Attack), 0f, 0.01f);
    }

    [Fact]
    public void Learning_AddsAPreferenceItDidNotHave()
    {
        var (_, creature) = CreatureWorlds.Create(200f);
        Assert.Null(CreatureModel.FindPreference(creature, CreatureSituation.Bored, CreatureAction.Attack));

        var weight = CreatureModel.Learn(Rules, creature, CreatureSituation.Bored, CreatureAction.Attack, CreatureFeedback.Rewarded);

        Assert.True(weight > CreatureModel.UnknownPreference);
        Assert.NotNull(CreatureModel.FindPreference(creature, CreatureSituation.Bored, CreatureAction.Attack));
    }

    [Fact]
    public void Feedback_NudgesTheTraitBehindTheAction()
    {
        var personality = new CreaturePersonality { Kindness = 0.5f, Aggression = 0.5f, Laziness = 0.5f, Curiosity = 0.5f };

        CreatureModel.Nudge(Rules, personality, CreatureAction.Feed, CreatureFeedback.Rewarded);
        CreatureModel.Nudge(Rules, personality, CreatureAction.Attack, CreatureFeedback.Punished);
        CreatureModel.Nudge(Rules, personality, CreatureAction.Forage, CreatureFeedback.Rewarded);

        Assert.Equal(0.5f + Rules.PersonalityNudge, personality.Kindness, 5);
        Assert.Equal(0.5f - Rules.PersonalityNudge, personality.Aggression, 5);
        Assert.Equal(0.5f, personality.Laziness);
    }

    [Fact]
    public void Levels_FollowExperience_UpToTheMvpMaximum()
    {
        Assert.Equal(1, CreatureModel.LevelFor(Rules, 0f));
        Assert.Equal(2, CreatureModel.LevelFor(Rules, Rules.LevelExperience[0]));
        Assert.Equal(CreatureModel.MaxLevel, CreatureModel.LevelFor(Rules, 1_000_000f));
    }

    [Fact]
    public void StrengthAndSpeed_GrowWithLevel()
    {
        var (_, creature) = CreatureWorlds.Create(200f);
        var strength = CreatureModel.Strength(Rules, creature);
        var speed = CreatureModel.Speed(Rules, creature);

        creature.Level = 4;

        Assert.Equal(strength + (3 * Rules.StrengthPerLevel), CreatureModel.Strength(Rules, creature), 5);
        Assert.Equal(speed + (3 * Rules.SpeedPerLevel), CreatureModel.Speed(Rules, creature), 5);
    }

    [Fact]
    public void Needs_GrowOverTime_AndActionsEaseThem()
    {
        var needs = new CreatureNeeds { Hunger = 0.1f, Fatigue = 0.1f, Boredom = 0.1f };

        CreatureModel.GrowNeeds(Rules, needs, seconds: 10f);
        Assert.Equal(0.1f + (10f * Rules.HungerPerSecond), needs.Hunger, 5);

        CreatureModel.Relieve(CreatureModel.RuleFor(Rules, CreatureAction.Forage), needs, share: 1f);
        Assert.Equal(0f, needs.Hunger);
    }
}
