using System;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Creatures;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Simulation.Creatures;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Creatures' dealings with each other as the player sees and drives them (ADR-017): alerts, the card, the panel, the F1 lines, K and <c>--meet</c>.</summary>
public sealed class CreatureRelationsPresentationTests
{
    private static readonly CreatureBondRules Bonds = CreatureBondWorlds.Bonds;

    [Fact]
    public void Alerts_TellThePlayerOfItsCreaturesMeetingsAndFights()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        WorldEvent[] events =
        [
            Met(theirs, mine, MeetingOutcome.Fought, betrayal: true),
            Met(mine, theirs, MeetingOutcome.Played, war: true),
            Met(mine, theirs, MeetingOutcome.Fled),
            Met(mine, theirs, MeetingOutcome.PassedBy),
            Met(mine, theirs, MeetingOutcome.Greeted, war: true, friends: true),
            new CreatureFightEnded { Tick = 1, CreatureId = mine.Id, OtherCreatureId = theirs.Id, Outcome = FightOutcome.Banished },
            new CreatureFightEnded { Tick = 1, CreatureId = theirs.Id, OtherCreatureId = mine.Id, Outcome = FightOutcome.Spared },
        ];

        var alerts = new AlertFeed().Push(events, world, 1, 1.0);

        Assert.Equal(
            [
                ("Rivaling (Rivalis's Creature) turned on Testling, its friend.", AlertSeverity.Danger),
                ("Testling played with Rivaling (Rivalis's Creature) though your gods are at war.", AlertSeverity.Warning),
                ("Testling fled from Rivaling (Rivalis's Creature).", AlertSeverity.Warning),
                ("Testling would not fight its friend Rivaling (Rivalis's Creature), though your gods are at war.", AlertSeverity.Warning),
                ("Testling beat Rivaling (Rivalis's Creature): it is banished for a while.", AlertSeverity.Good),
                ("Rivaling (Rivalis's Creature) beat Testling but let it go.", AlertSeverity.Warning),
            ],
            alerts.Select(a => (a.Text, a.Severity)));
    }

    [Fact]
    public void Alerts_OfFightsBetweenRivalsCreatures_OnlyWhenOneIsBeaten()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();

        var alerts = new AlertFeed().Push(
            [
                new CreatureFightEnded { Tick = 1, CreatureId = theirs.Id, OtherCreatureId = mine.Id, Outcome = FightOutcome.Banished },
                new CreatureFightEnded { Tick = 1, CreatureId = theirs.Id, OtherCreatureId = mine.Id, Outcome = FightOutcome.Drew },
                Met(theirs, mine, MeetingOutcome.Played),
            ],
            world,
            localGodId: 3,
            1.0);

        Assert.Equal(["Rivaling (Rivalis's Creature) beat Testling (Testa's Creature)."], alerts.Select(a => a.Text));
    }

    [Fact]
    public void Wording_NamesTheOtherCreature_AndWhatTheyAreToEachOther()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        (view.Rivalry, view.Fear, view.Trust) = (0.7f, 0.5f, 0.1f);
        CreatureBonds.Fled(Bonds, mine, theirs, 5);

        Assert.Equal("fighting Rivaling", CreatureText.Doing(CreatureAction.Fight, "Rivaling"));
        Assert.Equal("playing with another Creature", CreatureText.Doing(CreatureAction.Befriend, null));
        Assert.Equal(["rival", "fears it", "distrusts it"], CreatureBondText.Labels(Bonds, view));
        Assert.Equal("Rivaling (Rivalis): rival, fears it, distrusts it (fled from it)", CreatureBondText.Describe(Bonds, world, view));
        Assert.Equal("Testling: rival, fears it, distrusts it. Rivaling: chased it off.", CreatureBondText.Between(Bonds, mine, theirs));
        Assert.Equal("Has not met another Creature yet", CreatureBondText.Summary(Bonds, world, CreatureBondWorlds.Create().Mine));
        Assert.All(Enum.GetValues<EncounterKind>(), kind => Assert.NotEmpty(CreatureBondText.Encounter(kind)));

        var brain = CreatureBondWorlds.System().Brain;
        brain.Engage(mine, CreatureAction.Fight, CreatureSituation.MeetsCreature, theirs);
        Assert.Equal("Fighting Rivaling", CreatureText.DescribeActivity(mine, world));
    }

    [Fact]
    public void CardAndPanel_ShowTheCreaturesBondsScarsAndSkill()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var theirs = session.World.Creatures.First(c => c.Id != mine.Id);
        mine.Scars = 2;
        mine.CombatSkill = 0.25f;
        CreatureBonds.Ensure(Bonds, mine, theirs.Id).Friendship = 0.8f;
        CreatureBonds.Played(Bonds, mine, theirs.Id, 1);

        var card = HudReadout.BuildCreatureCard(session)!;
        var entry = DiplomacyReadout.Build(session).Single(e => e.GodId == theirs.GodId);

        Assert.EndsWith("combat skill 25%   2 scars", card.Temperament, StringComparison.Ordinal);
        Assert.StartsWith($"{theirs.Name} (", card.Bonds, StringComparison.Ordinal);
        Assert.Contains("friend", card.Bonds, StringComparison.Ordinal);
        Assert.Equal($"{mine.Name}: friend. {theirs.Name}: met.", entry.Creatures);
        CreatureBonds.Greeted(Bonds, theirs, mine, 2);
        Assert.Equal($"{mine.Name}: friend. {theirs.Name}: greeted.", DiplomacyReadout.Build(session).Single(e => e.GodId == theirs.GodId).Creatures);
    }

    [Fact]
    public void DebugPanel_ListsSkillScarsBonds_AndHowTheCreatureWouldMeetTheNearestOne()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        mine.Scars = 1;
        var view = CreatureBonds.Ensure(Bonds, mine, theirs.Id);
        view.Rivalry = 0.6f;
        CreatureBonds.Drew(Bonds, mine, theirs, 3);
        var responses = CreatureBondWorlds.System().Brain.ListResponses(world, mine, theirs);
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            CreatureRules = CreatureBondWorlds.Rules,
            LocalCreatureId = mine.Id,
            CreatureResponses = responses,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 380f,
            RegionBoundariesVisible = false,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains(lines, l => l.StartsWith("  combat skill 0.00   scars 1   power 1.00   bonds: Rivaling friend 0.00 fear 0.00 rival 0.60 respect 0.10 trust 0.30 [fought to a draw]", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  meeting Rivaling: " + responses[0].Action, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  bonds: none", StringComparison.Ordinal) || l.Contains("bonds: Testling", StringComparison.Ordinal));
    }

    [Fact]
    public void K_AndMeet_BringTheNearestCreature_AndTheyMeet()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var other = session.NearestOtherCreature()!;

        var status = CreatureCommands.BringOther(session);
        session.RunTicks(4);

        Assert.Equal($"{other.Name} comes to {mine.Name}", status);
        Assert.Contains(session.DrainEvents(), e => e is CreaturesMet met && new[] { met.CreatureId, met.OtherCreatureId }.Order().SequenceEqual(new[] { mine.Id, other.Id }.Order()));
        Assert.Equal(string.Empty, LaunchOptions.Parse(["--meet=on"]).Meet);
        Assert.Null(LaunchOptions.Parse(["--meet=off"]).Meet);
        Assert.Equal("Gristle", LaunchOptions.Parse(["--meet=Gristle"]).Meet);
        Assert.Single(LaunchOptions.Parse(["--meet="]).Warnings);
        Assert.Equal("Nobody cannot come to " + mine.Name + " now", CreatureCommands.BringOther(session, "Nobody"));
    }

    [Fact]
    public void Content_TunesBondsAndFights_AndGivesNewbornsTheirFirstAnswers()
    {
        var rules = TestContent.Shipped.Creatures;

        Assert.Contains(rules.StartingPreferences, p => p.Situation == CreatureSituation.MeetsEnemyCreature && p.Action == CreatureAction.Fight);
        Assert.Contains(rules.StartingPreferences, p => p.Situation == CreatureSituation.MeetsCreature && p.Action == CreatureAction.Befriend);
        Assert.Equal(40f, rules.Bonds.MeetRadius);
        Assert.Equal(0.25f, rules.Combat.YieldHealth);
        var broken = rules with { Bonds = rules.Bonds with { SeekRadius = rules.PerceptionRadius + 1f }, Combat = rules.Combat with { YieldHealth = 1f } };
        var error = Assert.Throws<ArgumentException>(() => CreatureRulesValidator.Validate(broken));
        Assert.Contains("SeekRadius", error.Message, StringComparison.Ordinal);
        Assert.Contains("YieldHealth must be below 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A meeting event.</summary>
    private static CreaturesMet Met(CreatureState actor, CreatureState other, MeetingOutcome outcome, bool betrayal = false, bool war = false, bool friends = false) =>
        new() { Tick = 1, CreatureId = actor.Id, OtherCreatureId = other.Id, Outcome = outcome, Betrayal = betrayal, GodsAtWar = war, Friends = friends };
}
