using System;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Expressive Creatures and the miracles' signature looks (spec §61, ADR-055).</summary>
public sealed class ExpressionTests
{
    [Fact]
    public void ACreature_ScowlsInAFight_WidensItsEyesInFlight_AndGrinsAtPlay()
    {
        var (world, creature) = Calm();

        var fight = FaceDoing(creature, world.Tick, CreatureAction.Fight);
        var flight = FaceDoing(creature, world.Tick, CreatureAction.Flee);
        var play = FaceDoing(creature, world.Tick, CreatureAction.Play);
        var rest = FaceDoing(creature, world.Tick, CreatureAction.Rest);

        Assert.Equal(CreatureMood.Angry, CreatureExpression.MoodOf(fight));
        Assert.Equal(CreatureMood.Afraid, CreatureExpression.MoodOf(flight));
        Assert.Equal(CreatureMood.Joyful, CreatureExpression.MoodOf(play));
        Assert.Equal(CreatureMood.Sleepy, CreatureExpression.MoodOf(rest));
        Assert.True(play.Wag > 0.9f && fight.Wag == 0f, "it wags its tail at play, never in a fight");
    }

    [Fact]
    public void ItsNeeds_ShowOnItsFace_AHungryBoredCreatureIsGlum_AndATiredOneDrowsy()
    {
        var (world, creature) = Calm();
        Assert.Equal(CreatureMood.Content, CreatureExpression.MoodOf(CreatureExpression.FaceOf(creature, world.Tick)));

        (creature.Needs.Hunger, creature.Needs.Boredom) = (1f, 1f);
        Assert.Equal(CreatureMood.Sad, CreatureExpression.MoodOf(CreatureExpression.FaceOf(creature, world.Tick)));

        (creature.Needs.Hunger, creature.Needs.Boredom, creature.Needs.Fatigue) = (0f, 0f, 1f);
        var tired = CreatureExpression.FaceOf(creature, world.Tick);
        Assert.Equal(CreatureMood.Sleepy, CreatureExpression.MoodOf(tired));
        Assert.True(tired.Sleep < 1f, "only rest shuts its eyes");
    }

    [Fact]
    public void ItsNature_ShowsOnItsFace_AKindCreatureSmiles_AndACruelOneScowls()
    {
        var (world, creature) = Calm();

        creature.Morality = 1f;
        var kind = CreatureExpression.FaceOf(creature, world.Tick);
        creature.Morality = -1f;
        var cruel = CreatureExpression.FaceOf(creature, world.Tick);

        Assert.True(kind.Joy > cruel.Joy);
        Assert.Equal(0f, kind.Anger);
        Assert.Equal(CreatureMood.Angry, CreatureExpression.MoodOf(cruel));
    }

    [Fact]
    public void ItsGodsLastWord_ShowsAWhile_PraiseBringsJoy_AndASlapFear()
    {
        var (world, creature) = Calm();
        var memory = new CreatureMemory { Tick = world.Tick, Action = CreatureAction.Wander, Situation = CreatureSituation.Idle, SettlementId = null, OtherCreatureId = null, Feedback = CreatureFeedback.Rewarded };
        creature.Memories.Add(memory);

        var praised = CreatureExpression.FaceOf(creature, world.Tick);
        memory.Feedback = CreatureFeedback.Punished;
        var slapped = CreatureExpression.FaceOf(creature, world.Tick);
        var later = CreatureExpression.FaceOf(creature, world.Tick + CreatureExpression.FeedbackTicks + 1);

        Assert.Equal(CreatureMood.Joyful, CreatureExpression.MoodOf(praised));
        Assert.Equal(CreatureMood.Afraid, CreatureExpression.MoodOf(slapped));
        Assert.Equal(CreatureMood.Content, CreatureExpression.MoodOf(later));

        // A word on a deed still under way marks no memory; the client passes what it saw given.
        creature.Memories.Clear();
        Assert.Equal(CreatureMood.Joyful, CreatureExpression.MoodOf(CreatureExpression.FaceOf(creature, world.Tick, CreatureFeedback.Rewarded)));
        Assert.Equal(CreatureMood.Afraid, CreatureExpression.MoodOf(CreatureExpression.FaceOf(creature, world.Tick, CreatureFeedback.Punished)));
    }

    [Fact]
    public void TheFace_IsDerivedFromTheState_TheSameEachTime()
    {
        var (world, creature) = Calm();
        creature.Morality = -0.7f;

        Assert.Equal(CreatureExpression.FaceOf(creature, world.Tick), CreatureExpression.FaceOf(creature, world.Tick));
        var face = CreatureExpression.FaceOf(creature, world.Tick);
        Assert.InRange(face.Joy, -1f, 1f);
        Assert.InRange(face.Anger, 0f, 1f);
    }

    [Fact]
    public void TheMiracles_EachHaveASignatureLook_AndTheGreatOnesLookUnlikeTheSmall()
    {
        var view = TestContent.DivineView;
        var styles = TestContent.Shipped.Miracles.Miracles.ToDictionary(m => m.Id, m => view.EffectFor(m.Id).Style, StringComparer.Ordinal);

        Assert.Equal(MiracleEffectStyle.Rain, styles["rain"]);
        Assert.Equal(MiracleEffectStyle.Flames, styles["fire"]);
        Assert.Equal(MiracleEffectStyle.Harvest, styles["food"]);
        Assert.Equal(MiracleEffectStyle.Healing, styles["heal"]);
        Assert.Equal(MiracleEffectStyle.Quake, styles["earthquake"]);
        Assert.Equal(MiracleEffectStyle.Shield, styles["shield"]);
        Assert.Equal(MiracleEffectStyle.Fountain, styles["water"]);
        Assert.Equal(MiracleEffectStyle.Strike, styles["lightning"]);
        Assert.True(styles.Values.Distinct().Count() >= 12, "most looks are in use");
        Assert.All(Enum.GetValues<MiracleEffectStyle>().Where(s => s is not MiracleEffectStyle.Rise and not MiracleEffectStyle.Bolt), s => Assert.Contains(s, styles.Values));
    }

    /// <summary>A Creature at ease: fed, rested, amused, unhurt, neither kind nor cruel, idle.</summary>
    private static (Pantheon.Domain.World.WorldState World, CreatureState Creature) Calm()
    {
        var (world, creature) = CreatureWorlds.Create(0f);
        (creature.Needs.Hunger, creature.Needs.Fatigue, creature.Needs.Boredom) = (0f, 0f, 0f);
        (creature.Health, creature.Morality) = (1f, 0f);
        creature.Memories.Clear();
        (creature.Activity.Action, creature.Activity.Phase) = (CreatureAction.Wander, CreaturePhase.Deciding);
        return (world, creature);
    }

    /// <summary>Its face as it performs an action.</summary>
    private static CreatureFace FaceDoing(CreatureState creature, long tick, CreatureAction action)
    {
        (creature.Activity.Action, creature.Activity.Phase) = (action, CreaturePhase.Performing);
        return CreatureExpression.FaceOf(creature, tick);
    }
}
