using System;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Creatures;
using Pantheon.Game.Domains;
using Pantheon.Game.Hand;
using Pantheon.Game.History;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>How the hand, the Creatures' paths and the Divine Acts are told, shown and driven (ADR-030).</summary>
public sealed class PowerPresentationTests
{
    [Fact]
    public void Alerts_TellOfTheHand_ThePaths_AndTheActs()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var (mine, theirs) = (world.Settlements[0], world.Settlements[1]);
        world.Creatures.Add(CreatureWorlds.Create(200f).Creature);
        var creature = world.Creatures[0];
        WorldEvent[] events =
        [
            new VillagersLifted { Tick = 1, GodId = 2, SettlementId = mine.Id, People = 10, FromDanger = false },
            new VillagersSetDown { Tick = 1, GodId = 1, FromSettlementId = theirs.Id, ToSettlementId = mine.Id, People = 10, Rescued = true, Drowned = false },
            new HandStruck { Tick = 1, GodId = 2, Kind = HeldKind.Rock, Position = mine.Position, SettlementId = mine.Id, HousesLost = 3, Deaths = 4 },
            new CreatureSpecialised { Tick = 1, CreatureId = creature.Id, GodId = 1, Specialisation = CreatureSpecialisation.Healer },
            new DivineActDone { Tick = 1, GodId = 2, Act = DivineActKind.Stillness, Position = theirs.Position },
            new DivineActDone { Tick = 1, GodId = 1, Act = DivineActKind.Hallowing, Position = mine.Position },
        ];

        var texts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()?.Text).ToList();

        Assert.Equal($"Rivalis's hand lifts 10 people out of {mine.Name}.", texts[0]);
        Assert.StartsWith($"You carry 10 people to safety in {mine.Name}", texts[1], StringComparison.Ordinal);
        Assert.Equal($"Rivalis hurls a rock into {mine.Name}: 3 houses fall, 4 dead.", texts[2]);
        Assert.Equal($"{creature.Name} becomes a Healer: it heals the people it helps.", texts[3]);
        Assert.StartsWith("Rivalis stills the world", texts[4], StringComparison.Ordinal);
        Assert.StartsWith($"You hallow the land around {mine.Name}", texts[5], StringComparison.Ordinal);
    }

    [Fact]
    public void TheHistory_RemembersPaths_Rescues_Stonings_AndActs()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        var village = world.Settlements[0];
        HazardWorlds.Mountain(world, 100f);
        Pantheon.Simulation.Disasters.HazardModel.Find(HazardWorlds.Hazards.Volcano, world);
        WorldEvent[] events =
        [
            new CreatureSpecialised { Tick = 1, CreatureId = creature.Id, GodId = 1, Specialisation = CreatureSpecialisation.NatureKeeper },
            new VillagersSetDown { Tick = 1, GodId = 1, FromSettlementId = village.Id, ToSettlementId = village.Id, People = 10, Rescued = true, Drowned = false },
            new HandStruck { Tick = 1, GodId = 1, Kind = HeldKind.Rock, Position = village.Position, SettlementId = village.Id, HousesLost = 4, Deaths = 6 },
            new DivineActDone { Tick = 1, GodId = 1, Act = DivineActKind.Hallowing, Position = village.Position },
            new DivineActDone { Tick = 1, GodId = 1, Act = DivineActKind.Awakening, Position = village.Position },
            new DivineActDone { Tick = 1, GodId = 1, Act = DivineActKind.Stillness, Position = village.Position },
        ];

        new HistoryRecorder(TestContent.Shipped.History).Record(world, events, 0);

        var god = world.Gods[0].Name;
        Assert.Equal(
            [
                $"{creature.Name} becomes a Nature Keeper", $"The Rescue of {village.Name} by {god}", $"The Stoning of {village.Name}",
                $"{god} hallows the land of {village.Name}", $"{god} wakes Mount {village.Name}", $"The Great Stillness of {god}",
            ],
            world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal(["10 carried to safety", "6 dead"], world.History.Where(r => r.Kind is HistoryKind.Rescue or HistoryKind.Stoning).Select(r => HistoryText.Detail(r, world)));
    }

    [Fact]
    public void TheCreatureCard_AndTheDebugPanel_ShowItsPaths_AndAttributes()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var creature = session.LocalCreature!;
        creature.Level = 4;
        creature.Specialisations.Add(CreatureSpecialisation.Healer);
        creature.Callings.Warrior = 2f;

        var card = HudReadout.BuildCreatureCard(session)!;

        Assert.Equal("Healer - at level 6 it becomes a Warrior", card.Paths);
        Assert.Matches(@"^strength \d+\.\d   speed \d+\.\d   intelligence \d\.\d\d   resilience \d\.\d\d   affinity 1\.15$", card.Attributes);
        Assert.Equal("Warrior 2.0", CreatureGrowthText.Callings(creature));
        Assert.StartsWith("No path yet - at level 3 it becomes a ", CreatureGrowthText.Paths(session.Content.Creatures, CreatureWorlds.Create(300f).Creature), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHandCommands_PickUp_SetDown_AndThrow_WithWordsForEach()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var home = session.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence > 0.5f));

        Assert.Equal(HandAction.PickUp, HandCommands.ActionFor(session, shift: true));
        Assert.StartsWith($"You lift 10 people of {home.Name}", HandCommands.Use(session, HandAction.PickUp, home.Position), StringComparison.Ordinal);
        Assert.Equal((HandAction.SetDown, HandAction.Throw), (HandCommands.ActionFor(session, false), HandCommands.ActionFor(session, true)));
        Assert.Equal($"You set 10 people of {home.Name} down", HandCommands.Use(session, HandAction.SetDown, home.Position));
        Assert.Equal("Your hand is empty", HandCommands.Use(session, HandAction.SetDown, home.Position));
        Assert.Equal("Your hand cannot reach where your influence does not", HandText.Describe(HandRejection.NoInfluence));
        Assert.All(Enum.GetValues<HandRejection>(), r => Assert.NotEmpty(HandText.Describe(r)));
    }

    [Fact]
    public void TheDomainsPanel_OffersTheActs_ToAGreaterGodWithThePower()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));

        var acts = DomainReadout.Build(session).Acts;
        Assert.Equal(["hallow", "awaken", "still"], acts.Select(a => a.MiracleId));
        Assert.All(acts, a => Assert.False(a.CanChoose));
        Assert.StartsWith("Greater God, 1500 power", acts[0].Requirement, StringComparison.Ordinal);

        (session.LocalGod!.Rank, session.LocalGod.DivinePower) = (DivineRank.GreaterGod, 3000f);
        Assert.True(DomainReadout.Build(session).Acts[0].CanChoose);
        Assert.DoesNotContain(HudReadout.AvailableMiracles(session), m => m.Id == "hallow");
        Assert.StartsWith("Hallow the Land on", HudReadout.Build(session, "hallow", session.World.Settlements[0].Position).TargetHint, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHandOption_AndTheRegionsSection_CarryTheNewState()
    {
        Assert.Equal(HandAtStart.Creature, LaunchOptions.Parse(["--hand=creature"]).Hand);
        Assert.Single(LaunchOptions.Parse(["--hand=boulder"]).Warnings);

        var region = SettlementWorlds.Create(100, 200, 100f, 0.5f).Regions.Cells[3];
        (region.Blessing, region.Hallowed) = (0.7f, 0.6f);
        var replica = SettlementWorlds.Create(100, 200, 100f, 0.5f).Regions.Cells[3];
        RegionChange.Of(region).ApplyTo(replica);
        Assert.Equal(0.6f, replica.Hallowed);
    }

    [Fact]
    public void TheShippedPowers_Parse_AndBrokenOnesAreRejected()
    {
        Assert.True(TestContent.Shipped.DivineRules.Hand.Enabled);
        Assert.Equal([3, 6, 9], TestContent.Shipped.Creatures.Callings.Levels);

        AssertRejected(Pantheon.Persistence.ContentJson.DivineRulesFileName, "\"MaxShare\": 0.25", "\"MaxShare\": 1.5", "Hand.MaxShare");
        AssertRejected(Pantheon.Persistence.ContentJson.CreaturesFileName, "\"Levels\": [3, 6, 9]", "\"Levels\": [3, 3]", "Callings.Levels");
        AssertRejected(Pantheon.Persistence.ContentJson.CreaturesFileName, "\"HealerHealth\": 0.05", "\"HealerHealth\": 0.05, \"Necromancer\": 1", "Necromancer");
    }

    [Fact]
    public void AJoinedPlayersHandIntent_LiftsOnTheHost()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var home = shared.Host.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == 2 && b.Adherence >= 0.5f));
        var people = home.Population;

        var asking = HandCommands.Use(ana.Session!, HandAction.PickUp, home.Position);
        var statuses = shared.Exchange(ana);

        Assert.Equal("Asking the host...", asking);
        Assert.Contains(statuses, s => s.StartsWith("You lift", StringComparison.Ordinal));
        Assert.True(home.Population < people);
        Assert.Equal(HeldKind.Villagers, shared.Host.World.Gods.First(g => g.Id == 2).Held!.Kind);
    }

    /// <summary>Breaks a shipped content file one way and checks the error names the member.</summary>
    private static void AssertRejected(string file, string shipped, string broken, string expected)
    {
        var text = TestContent.ReadText(file);
        Assert.Contains(shipped, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == file ? text.Replace(shipped, broken, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<Pantheon.Persistence.ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }
}
