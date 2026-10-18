using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Creatures;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Hud;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The player's Creature through the session: commands, the HUD card, alerts and the debug view.</summary>
public sealed class CreatureSessionTests
{
    private readonly GameSession _session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

    [Fact]
    public void NewWorld_GivesThePlayersGodItsCreatureAtHome()
    {
        var creature = Assert.IsType<CreatureState>(_session.LocalCreature);

        Assert.Equal(TestContent.Shipped.Pantheon.Gods[0].CreatureName, creature.Name);
        Assert.Equal(_session.World.Settlements[0].Id, creature.HomeSettlementId);
        Assert.Equal(creature.Id, _session.LocalGod!.CreatureId);
    }

    [Fact]
    public void Creature_ActsOnItsOwn()
    {
        _session.RunTicks(1200);

        Assert.Contains(_session.DrainEvents(), e => e is CreatureActed);
        Assert.NotEmpty(_session.LocalCreature!.Memories);
    }

    [Fact]
    public void RewardAndPunish_ReportWhatTheyTaught()
    {
        var creature = _session.LocalCreature!;
        creature.Activity.Action = CreatureAction.Play;
        creature.Activity.Situation = CreatureSituation.VillageThriving;
        creature.Activity.Phase = CreaturePhase.Performing;
        creature.Activity.TicksLeft = 50;

        var reward = SessionCommands.CommandCreature(_session, CreatureCommandKind.Reward);
        var punish = SessionCommands.CommandCreature(_session, CreatureCommandKind.Punish);
        var nothing = SessionCommands.CommandCreature(_session, CreatureCommandKind.Punish);

        Assert.StartsWith($"You reward {creature.Name} for playing in a settlement: it likes that more now", reward, StringComparison.Ordinal);
        Assert.StartsWith($"You punish {creature.Name} for playing in a settlement: it likes that less now", punish, StringComparison.Ordinal);
        Assert.Equal($"{creature.Name} has done nothing lately to reward or punish", nothing);
        Assert.Equal(2, _session.DrainEvents().OfType<CreatureChanged>().Count());
    }

    [Fact]
    public void GoTo_SendsTheCreature()
    {
        var home = _session.World.Settlements[0].Position;

        var status = SessionCommands.CommandCreature(_session, CreatureCommandKind.GoTo, new GroundPosition(home.X + 50f, home.Z));

        Assert.Equal($"{_session.LocalCreature!.Name} heads where you pointed", status);
        Assert.Equal(CreatureAction.GoTo, _session.LocalCreature!.Activity.Action);
    }

    [Fact]
    public void HudCard_ShowsLevelActivityNeedsAndWhatFeedbackWouldJudge()
    {
        var creature = _session.LocalCreature!;
        creature.Activity.Action = CreatureAction.Feed;
        creature.Activity.Phase = CreaturePhase.Performing;
        creature.Activity.TargetSettlementId = _session.World.Settlements[0].Id;

        var card = HudReadout.Build(_session, null, null).Creature!;

        Assert.Equal($"{creature.Name} the Mossback - level 1", card.Title);
        Assert.Equal($"Feeding {_session.World.Settlements[0].Name}", card.Doing);
        Assert.StartsWith("health 100%", card.Condition, StringComparison.Ordinal);
        Assert.StartsWith("loyalty 70%", card.Temperament, StringComparison.Ordinal);
        Assert.Equal($"Reward or punish: feeding {_session.World.Settlements[0].Name}", card.JudgeHint);
        Assert.True(card.CanJudge);
        Assert.False(card.Banished);
    }

    [Fact]
    public void Alerts_ReportTheCreaturesNotableDeeds()
    {
        var world = _session.World;
        var creature = _session.LocalCreature!;
        var village = world.Settlements[0];
        var feed = new AlertFeed();

        var alerts = feed.Push(
            [
                new CreatureActed { Tick = 1, CreatureId = creature.Id, Action = CreatureAction.Attack, SettlementId = village.Id, Deaths = 3, Food = 0f },
                new CreatureActed { Tick = 1, CreatureId = creature.Id, Action = CreatureAction.Wander, SettlementId = null, Deaths = 0, Food = 0f },
                new CreatureChanged { Tick = 1, CreatureId = creature.Id, Change = CreatureChange.LevelledUp },
            ],
            world,
            _session.LocalGodId,
            1.0);

        Assert.Equal([$"{creature.Name} attacked {village.Name}: 3 died.", $"{creature.Name} reached level 1."], alerts.Select(a => a.Text));
        Assert.Equal(AlertSeverity.Danger, alerts[0].Severity);
    }

    [Fact]
    public void DebugPanel_ShowsTheCreatureView()
    {
        _session.RunTicks(600);
        var creature = _session.LocalCreature!;
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = _session.World,
            SettlementRules = _session.Content.SettlementRules,
            DivineRules = _session.Content.DivineRules,
            CreatureRules = _session.Content.Creatures,
            LocalGodId = _session.LocalGodId,
            LocalCreatureId = creature.Id,
            CreatureOptions = _session.PreviewCreatureOptions(),
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "Terrain3D",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 300f,
            RegionBoundariesVisible = false,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains(lines, line => line.StartsWith($"Creature {creature.Name} (Mossback) Lv ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  doing: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  needs: hunger ", StringComparison.Ordinal) && line.Contains("personality: kind", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  options: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  learned: VillageInNeed: Feed 0.40", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  remembers: ", StringComparison.Ordinal));
        Assert.Contains(DebugReadout.CreatureHints, lines);
    }

    [Fact]
    public void ActivityWording_CoversEveryPhase()
    {
        var creature = _session.LocalCreature!;
        var world = _session.World;
        creature.Activity.Action = CreatureAction.Rest;
        creature.Activity.TargetSettlementId = null;

        creature.Activity.Phase = CreaturePhase.Deciding;
        Assert.Equal("Thinking what to do", CreatureText.DescribeActivity(creature, world));
        creature.Activity.Phase = CreaturePhase.Travelling;
        Assert.Equal("On the way: resting", CreatureText.DescribeActivity(creature, world));
        creature.Activity.Phase = CreaturePhase.Performing;
        Assert.Equal("Resting", CreatureText.DescribeActivity(creature, world));
        creature.Activity.Phase = CreaturePhase.Banished;
        creature.Activity.TicksLeft = 420;
        Assert.Equal("Banished: back in 42 s", CreatureText.DescribeActivity(creature, world));
    }

    [Fact]
    public void Creature_SurvivesASaveAndLoad()
    {
        _session.RunTicks(300);
        var json = WorldSaveSerializer.Serialize(_session.World);

        var loaded = WorldSaveSerializer.Deserialize(json);

        WorldAssert.Equal(_session.World, loaded);
        Assert.Single(loaded.Creatures);
    }
}
