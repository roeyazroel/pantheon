using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Hud;
using Pantheon.Game.Settings;
using Pantheon.Simulation.Belief;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The HUD's gauges (ADR-059): the numbers behind the plaque, the meters, the rival chips and the time controls.</summary>
public sealed class HudGaugeTests
{
    [Fact]
    public void ThePlaque_ShowsPowerFollowersAndTheNextRank_AsNumbers()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var god = session.LocalGod!;
        god.DivinePower = 412.7f;

        var hud = HudReadout.Build(session, null, null);

        Assert.Equal(god.Name, hud.GodName);
        Assert.Equal(412.7f, hud.Power, 2);
        Assert.Equal(GodStanding.CountFollowers(session.World, god.Id), hud.Followers);
        Assert.NotNull(hud.NextRankName);
        Assert.True(hud.NextRankFollowers > hud.Followers, "the next rank needs more followers than the god has");
        Assert.InRange(hud.BeliefShare, 0f, 1f);
        Assert.Equal(session.Content.DivineRules.VictoryShare, hud.WinningShare);
    }

    [Fact]
    public void TheRivalChips_NameEveryOtherGod_UnmetUntilTheyMeet()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var hud = HudReadout.Build(session, null, null);

        Assert.Equal(session.World.Gods.Count - 1, hud.RivalChips.Count);
        Assert.DoesNotContain(hud.RivalChips, chip => chip.GodId == session.LocalGodId);
        Assert.All(hud.RivalChips, chip => Assert.False(string.IsNullOrEmpty(chip.Detail)));

        var gods = session.World.Gods.Select(g => g.Id).ToList();
        foreach (var other in gods.Where(id => id != session.LocalGodId))
        {
            Pantheon.Simulation.Diplomacy.Acquaintance.Introduce(session.World, session.LocalGodId!.Value, other, Pantheon.Domain.Events.MeetingWay.Neighbours, null, new System.Collections.Generic.List<Pantheon.Domain.Events.WorldEvent>());
        }

        Assert.All(HudReadout.Build(session, null, null).RivalChips, chip => Assert.NotNull(chip.Status));
    }

    [Fact]
    public void TheTimeControls_KnowThePauseTheSpeedAndTheSeason()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        Assert.Equal(0, HudReadout.Build(session, null, null).SpeedPreset);

        SessionCommands.SetSpeedPreset(session, 2);
        SessionCommands.TogglePause(session);
        var hud = HudReadout.Build(session, null, null);

        Assert.Equal(2, hud.SpeedPreset);
        Assert.True(hud.Paused);
        Assert.Matches("^(Spring|Summer|Autumn|Winter), year \\d+$", hud.Calendar);
    }

    [Fact]
    public void TheCreatureCard_CarriesItsNeedsAndNature_AsNumbers()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var creature = session.LocalCreature!;
        (creature.Health, creature.Needs.Hunger, creature.Needs.Fatigue, creature.Needs.Boredom) = (0.9f, 0.3f, 0.2f, 0.1f);
        (creature.Loyalty, creature.Morality) = (0.7f, -0.4f);

        var card = HudReadout.Build(session, null, null).Creature!;

        Assert.Equal(creature.Name, card.Name);
        Assert.Equal(creature.Level, card.Level);
        Assert.Equal((0.9f, 0.3f, 0.2f, 0.1f), (card.Health, card.Hunger, card.Fatigue, card.Boredom));
        Assert.Equal((0.7f, -0.4f), (card.Loyalty, card.Morality));
    }

    [Fact]
    public void TheInterfaceSize_IsASetting_KeptInRange()
    {
        Assert.Equal(1f, new GameSettings().InterfaceScale);
        Assert.Equal(GameSettings.MaxInterfaceScale, GameSettings.Parse("""{ "InterfaceScale": 4 }""").InterfaceScale);
        Assert.Equal(GameSettings.MinInterfaceScale, GameSettings.Parse("""{ "InterfaceScale": 0.1 }""").InterfaceScale);
    }
}
