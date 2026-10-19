using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Persistence;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.History;

/// <summary>The world's history (spec §50, §92, ADR-020): what it keeps, how it reads, and the game flow around it.</summary>
public sealed class HistoryTests
{
    private static readonly HistoryRules Rules = TestContent.Shipped.History;

    [Fact]
    public void Diplomacy_Dominion_VictoryAndRanks_AreHistory_DecliningIsNot()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);

        Record(
            world,
            Move(1, 2, DiplomaticCommandKind.OfferAlliance, DiplomaticStatus.Neutral),
            Move(2, 1, DiplomaticCommandKind.Accept, DiplomaticStatus.Alliance),
            Move(3, 4, DiplomaticCommandKind.DeclareWar, DiplomaticStatus.War),
            Move(4, 3, DiplomaticCommandKind.Accept, DiplomaticStatus.Neutral),
            Move(1, 2, DiplomaticCommandKind.BreakAlliance, DiplomaticStatus.Neutral, betrayal: true),
            Move(3, 1, DiplomaticCommandKind.Decline, DiplomaticStatus.Neutral),
            new DominanceChanged { Tick = 0, GodId = 2, Started = true, Allies = [3] },
            new VictoryAchieved { Tick = 0, GodIds = [2, 3] },
            new RankAchieved { Tick = 0, GodId = 4, Rank = DivineRank.God });

        Assert.Equal(
            [
                (HistoryKind.Alliance, 1, 2), (HistoryKind.War, 3, 4), (HistoryKind.Peace, 3, 4), (HistoryKind.Betrayal, 1, 2),
                (HistoryKind.Dominion, 2, 3), (HistoryKind.Victory, 2, 3), (HistoryKind.Rank, 4, 0),
            ],
            world.History.Select(r => (r.Kind, r.GodId ?? 0, r.OtherGodId ?? 0)));
        Assert.Equal((int)DivineRank.God, world.History[^1].Count);
    }

    [Fact]
    public void Disasters_Famines_ExodusesAndConversions_CountOnlyWhenTheyAreGreat_AndNotTwiceInAWhile()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var big = world.Settlements[0];
        var small = world.Settlements[1];
        small.Population = Rules.ConversionPopulation - 1;

        Record(
            world,
            new SettlementDamaged { Tick = 0, SettlementId = big.Id, Cause = DisasterKind.Earthquake, HousesLost = 0, Deaths = Rules.EarthquakeDeaths },
            new SettlementDamaged { Tick = 0, SettlementId = big.Id, Cause = DisasterKind.Earthquake, HousesLost = 0, Deaths = Rules.EarthquakeDeaths },
            new SettlementDamaged { Tick = 0, SettlementId = small.Id, Cause = DisasterKind.Earthquake, HousesLost = Rules.HousesLost - 1, Deaths = 1 },
            new SettlementDamaged { Tick = 0, SettlementId = small.Id, Cause = DisasterKind.Fire, HousesLost = Rules.HousesLost, Deaths = 0 },
            new FamineStarted { Tick = 0, SettlementId = big.Id },
            new FamineStarted { Tick = 0, SettlementId = big.Id },
            new MigrationStarted { Tick = 0, GroupId = 1, OriginSettlementId = big.Id, DestinationSettlementId = small.Id, People = Rules.ExodusPeople, Cause = MigrationCause.War, GodId = 1, AbandonedGodId = null },
            new MigrationStarted { Tick = 0, GroupId = 2, OriginSettlementId = small.Id, DestinationSettlementId = big.Id, People = Rules.ExodusPeople - 1, Cause = MigrationCause.War, GodId = 2, AbandonedGodId = null },
            new FollowingChanged { Tick = 0, SettlementId = big.Id, GodId = 3, Gained = true },
            new FollowingChanged { Tick = 0, SettlementId = big.Id, GodId = 4, Gained = true },
            new FollowingChanged { Tick = 0, SettlementId = small.Id, GodId = 3, Gained = true },
            new FollowingChanged { Tick = 0, SettlementId = big.Id, GodId = 1, Gained = false });

        Assert.Equal(
            [(HistoryKind.Earthquake, big.Id), (HistoryKind.Fire, small.Id), (HistoryKind.Famine, big.Id), (HistoryKind.Exodus, big.Id), (HistoryKind.Conversion, big.Id)],
            world.History.Select(r => (r.Kind, r.SettlementId ?? 0)));

        world.Tick = (long)(Rules.RepeatSeconds * 10) + 1;
        Record(world, new FollowingChanged { Tick = world.Tick, SettlementId = big.Id, GodId = 4, Gained = true }, new FamineStarted { Tick = world.Tick, SettlementId = big.Id });
        Assert.Equal([HistoryKind.Conversion, HistoryKind.Famine], world.History.TakeLast(2).Select(r => r.Kind));
    }

    [Fact]
    public void CreaturesBattlesAndLegends_AndSettlementsFallingAndRising_AreHistory()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        mine.Level = Rules.LegendLevels[0];

        Record(
            world,
            new CreatureFightEnded { Tick = 0, CreatureId = mine.Id, OtherCreatureId = theirs.Id, Outcome = FightOutcome.Banished },
            new CreatureFightEnded { Tick = 0, CreatureId = theirs.Id, OtherCreatureId = mine.Id, Outcome = FightOutcome.Banished },
            new CreatureFightEnded { Tick = 0, CreatureId = mine.Id, OtherCreatureId = theirs.Id, Outcome = FightOutcome.Drew },
            new CreatureChanged { Tick = 0, CreatureId = mine.Id, Change = CreatureChange.LevelledUp });
        Assert.Equal([HistoryKind.Battle, HistoryKind.Legend], world.History.Select(r => r.Kind));
        Assert.Equal((mine.Id, theirs.Id, 1, 2), (world.History[0].CreatureId!.Value, world.History[0].OtherCreatureId!.Value, world.History[0].GodId!.Value, world.History[0].OtherGodId!.Value));

        var village = world.Settlements[1];
        village.Population = 0;
        world.Tick = 9;
        Record(world);
        village.Population = 12;
        world.Tick = 19;
        Record(world);
        Assert.Equal([HistoryKind.Fall, HistoryKind.Resettled], world.History.Skip(2).Select(r => r.Kind));
    }

    [Fact]
    public void TheHistory_KeepsItsLatestRecords_UpToItsCapacity()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var recorder = new HistoryRecorder(Rules with { Capacity = 3 });

        recorder.Record(world, Enumerable.Range(1, 5).Select(i => (WorldEvent)new RankAchieved { Tick = 0, GodId = 1, Rank = (DivineRank)i }).ToList(), 0);

        Assert.Equal([3, 4, 5], world.History.Select(r => r.Count));
    }

    [Fact]
    public void ThePlayersOwnMoves_AndTheWorldsTicks_BothWriteHistory()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

        SessionCommands.CommandDiplomacy(session, 2, DiplomaticCommandKind.DeclareWar);
        session.RunTicks(6000);

        Assert.Equal((HistoryKind.War, 1, 2), (session.World.History[0].Kind, session.World.History[0].GodId!.Value, session.World.History[0].OtherGodId!.Value));
        Assert.True(session.World.History.Count > 3);
        Assert.Equal(session.World.History.OrderBy(r => r.Tick).Select(r => r.Tick), session.World.History.Select(r => r.Tick));
    }

    [Fact]
    public void TheChronicle_ReadsLikeAChronicle()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        var village = world.Settlements[0];
        HistoryRecord At(long tick, HistoryKind kind, int? god = null, int? other = null, int? settlement = null, int? creature = null, int? otherCreature = null, int count = 0) =>
            new() { Tick = tick, Kind = kind, GodId = god, OtherGodId = other, SettlementId = settlement, CreatureId = creature, OtherCreatureId = otherCreature, Count = count };

        Assert.Equal("Year 12 — The Pact of Testa and Rivalis", HistoryText.Line(At(12 * 600 + 5, HistoryKind.Alliance, 1, 2), world, Rules));
        Assert.Equal("Year 0 — The Shaking of Town1 (12 dead)", HistoryText.Line(At(0, HistoryKind.Earthquake, settlement: village.Id, count: 12), world, Rules));
        Assert.Equal("Year 0 — The Battle of Testling and Rivaling (Testling prevailed)", HistoryText.Line(At(0, HistoryKind.Battle, creature: mine.Id, otherCreature: theirs.Id), world, Rules));
        Assert.Equal("Year 0 — Rivaling becomes a legend (level 10)", HistoryText.Line(At(0, HistoryKind.Legend, creature: theirs.Id, count: 10), world, Rules));
        Assert.Equal("Year 0 — The Betrayal of Rivalis (broken by Testa)", HistoryText.Line(At(0, HistoryKind.Betrayal, 1, 2), world, Rules));
        Assert.All(Enum.GetValues<HistoryKind>(), kind => Assert.NotEmpty(HistoryText.Title(At(0, kind, 1, 2, village.Id, mine.Id, theirs.Id, 3), world)));
        Assert.True(HistoryText.Concerns(At(0, HistoryKind.Famine, settlement: village.Id), world, 1));
        Assert.False(HistoryText.Concerns(At(0, HistoryKind.Famine, settlement: village.Id), world, 2));
        world.History.Add(At(0, HistoryKind.Alliance, 1, 2));
        world.History.Add(At(700, HistoryKind.War, 2, 1));
        Assert.Equal(["Year 1 — Rivalis goes to war with Testa", "Year 0 — The Pact of Testa and Rivalis"], HistoryReadout.Build(world, Rules, 1).Select(e => e.Text));
    }

    [Fact]
    public void TheEndScreen_SaysWhoWon_AndWhatTheWorldRemembers()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        Assert.Null(EndSummaryBuilder.Build(session));
        session.World.History.Add(new HistoryRecord { Tick = 0, Kind = HistoryKind.Alliance, GodId = 1, OtherGodId = 3, SettlementId = null, CreatureId = null, OtherCreatureId = null, Count = 0 });
        session.World.WinnerGodIds.AddRange([1, 3]);

        var won = EndSummaryBuilder.Build(session)!;
        session.World.WinnerGodIds.Clear();
        session.World.WinnerGodIds.Add(2);
        var lost = EndSummaryBuilder.Build(session)!;

        Assert.Equal((true, "Victory", "You and Seren have won the world's belief together."), (won.Won, won.Title, won.Subtitle));
        Assert.Equal((false, "Defeat", "Vael has won the world's belief."), (lost.Won, lost.Title, lost.Subtitle));
        Assert.Contains(won.Facts, f => f.StartsWith("Bramble reached level 1", StringComparison.Ordinal));
        Assert.Equal(["Year 0 — The Pact of Ilyra and Seren"], won.Highlights);
    }

    [Fact]
    public void TheGuide_MovesOnWhenThePlayerDoesWhatItAsks()
    {
        var guide = new Guide();
        Assert.True(guide.CanSkipStep);
        guide.Next();
        Assert.Equal(GuideStep.Domain, guide.Step);
        guide.Observe([new DomainChosen { Tick = 0, GodId = 2, Domain = Pantheon.Domain.Gods.GodDomain.Storm }], 1, 1, false);
        Assert.Equal(GuideStep.Domain, guide.Step);
        guide.Observe([new DomainChosen { Tick = 0, GodId = 1, Domain = Pantheon.Domain.Gods.GodDomain.Storm }], 1, 1, false);
        Assert.Equal(GuideStep.Cast, guide.Step);
        guide.Next();
        guide.Observe([new MiracleCast { Tick = 0, GodId = 2, MiracleId = "food", Target = default, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 }], 1, 1, false);
        Assert.Equal(GuideStep.Cast, guide.Step);
        guide.Observe([new MiracleCast { Tick = 0, GodId = 1, MiracleId = "food", Target = default, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 }], 1, 1, false);
        guide.Observe([new CreatureChanged { Tick = 0, CreatureId = 1, Change = CreatureChange.Rewarded }], 1, 1, false);
        guide.Observe([], 1, 1, diplomacyOpen: true);
        Assert.Equal(GuideStep.Win, guide.Step);
        Assert.Contains("60%", guide.Text("Ilyra", "Bramble"), StringComparison.Ordinal);
        guide.Next();
        Assert.False(guide.IsVisible);
        guide.Restart();
        Assert.Equal(GuideStep.Welcome, guide.Step);
        guide.Hide();
        Assert.Equal(string.Empty, guide.Text("Ilyra", "Bramble"));
    }

    [Fact]
    public void TheSaveList_ShowsSavesNewestFirst_AndLeavesOtherFilesOut()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pantheon-saves-{Environment.ProcessId}");
        Directory.CreateDirectory(folder);
        try
        {
            var world = GameSession.CreateNew(TestContent.Small, new WorldSeed(42)).World;
            WorldSaveSerializer.SaveToFile(world, Path.Combine(folder, "old.json"));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "old.json"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            world.Tick = 600;
            WorldSaveSerializer.SaveToFile(world, Path.Combine(folder, "new.json"));
            File.WriteAllText(Path.Combine(folder, "notes.json"), "{ \"hello\": 1 }");
            File.WriteAllText(Path.Combine(folder, "broken.json"), "{");

            var saves = SaveCatalog.List(folder);

            Assert.Equal(["new", "old"], saves.Select(s => s.Name));
            Assert.Equal((new WorldSeed(42), 600L, WorldSaveSerializer.CurrentSaveVersion), (saves[0].Seed, saves[0].Tick, saves[0].Version));
            Assert.Empty(SaveCatalog.List(Path.Combine(folder, "missing")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LaunchOptions_TheStartScreen_TheGuideAndThePanels()
    {
        var options = LaunchOptions.Parse(["--menu=on", "--guide=on", "--history-panel=on", "--pause-menu=on"]);

        Assert.Equal((true, true, true, true), (options.Menu, options.Guide, options.HistoryPanel, options.PauseMenu));
        Assert.Null(LaunchOptions.Parse([]).Menu);
        Assert.Equal(false, LaunchOptions.Parse(["--menu=off"]).Menu);
        Assert.Equal(2, LaunchOptions.Parse(["--menu=maybe", "--guide=1"]).Warnings.Count);
    }

    [Fact]
    public void TheShippedHistoryRules_AreValid_AndBrokenOnesAreNot()
    {
        Assert.Equal(60f, Rules.SecondsPerYear);
        var error = Assert.Throws<ArgumentException>(() => HistoryRulesValidator.Validate(Rules with { Capacity = 0, LegendLevels = [1] }));
        Assert.Contains("Capacity", error.Message, StringComparison.Ordinal);
        Assert.Contains("LegendLevels", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Records events in a world as one tick's.</summary>
    private static void Record(WorldState world, params WorldEvent[] events) => new HistoryRecorder(Rules).Record(world, events, 0);

    /// <summary>A diplomatic move.</summary>
    private static DiplomacyChanged Move(int actor, int other, DiplomaticCommandKind kind, DiplomaticStatus status, bool betrayal = false) =>
        new() { Tick = 0, ActorGodId = actor, OtherGodId = other, Kind = kind, Status = status, Betrayal = betrayal };
}
