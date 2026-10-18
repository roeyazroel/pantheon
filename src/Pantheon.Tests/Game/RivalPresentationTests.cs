using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Divine;
using Pantheon.Game.Hud;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The rival on the HUD, in alerts, on the overlays and in the AI debug view.</summary>
public sealed class RivalPresentationTests
{
    private readonly GameSession _session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

    [Fact]
    public void Hud_ShowsTheRivalAndTheRaceForVictory()
    {
        var hud = HudReadout.Build(_session, null, null);
        var rival = _session.World.Gods[1];

        Assert.Equal(_session.World.Gods.Count - 1, hud.Rivals.Count);
        var line = hud.Rivals[0];
        Assert.StartsWith($"{rival.Name} the {DivineText.Personality(rival.Personality)}: ", line, StringComparison.Ordinal);
        Assert.EndsWith("; 60% for 10:00 wins", hud.Contest, StringComparison.Ordinal);

        rival.DominantSinceTick = _session.World.Tick;
        var dominated = HudReadout.Build(_session, null, null);
        Assert.Contains($"{rival.Name} holds", dominated.Contest, StringComparison.Ordinal);
        Assert.EndsWith("it wins in 10:00", dominated.Contest, StringComparison.Ordinal);
        Assert.False(dominated.ContestFavoursPlayer);

        _session.World.WinnerGodIds.Add(_session.LocalGodId!.Value);
        Assert.Equal("You have won. The world goes on.", HudReadout.Build(_session, null, null).Contest);
    }

    [Fact]
    public void Alerts_WarnOfTheRivalsMoves()
    {
        var world = RivalWorlds.Create(GodPersonality.Tyrant, 0f, 200f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.5f);
        var feed = new AlertFeed();

        var alerts = feed.Push(
            [
                new MiracleCast { Tick = 1, GodId = RivalWorlds.RivalId, MiracleId = "lightning", Target = world.Settlements[0].Position, Cost = 70f, AffectedSettlementIds = [1], Deaths = 3 },
                new MiracleCast { Tick = 1, GodId = RivalWorlds.RivalId, MiracleId = "food", Target = world.Settlements[1].Position, Cost = 40f, AffectedSettlementIds = [2], Deaths = 0 },
                new DominanceChanged { Tick = 1, GodId = RivalWorlds.RivalId, Started = true, Allies = [] },
                new VictoryAchieved { Tick = 2, GodIds = [RivalWorlds.RivalId] },
            ],
            world,
            1,
            1.0);

        Assert.Equal(
            [
                "Rivalis worked a miracle (lightning) on Town1: 3 died.",
                "Rivalis holds most of the world's belief: it wins in ten minutes unless you stop it.",
                "Rivalis has won the world's belief.",
            ],
            alerts.Select(a => a.Text));
        Assert.All(alerts, a => Assert.Equal(AlertSeverity.Danger, a.Severity));
    }

    [Fact]
    public void Overlays_ShowTheStrongestGodAndEachSettlementsMainGod()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 0f, 200f, 800f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 1f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 1f, godId: RivalWorlds.RivalId);
        var view = TestContent.DivineView;

        var influence = OverlayMap.Build(world, OverlayKind.Influence, 1, DivineWorlds.Rules, SettlementWorlds.Rules, view);
        var religion = OverlayMap.Build(world, OverlayKind.Religion, 1, DivineWorlds.Rules, SettlementWorlds.Rules, view);

        foreach (var image in new[] { influence, religion })
        {
            Assert.Equal(RgbColor.ToByte(view.GodColor(1).R), image.Rgba[world.Settlements[0].RegionId * 4]);
            Assert.Equal(RgbColor.ToByte(view.GodColor(RivalWorlds.RivalId).R), image.Rgba[world.Settlements[1].RegionId * 4]);
        }
    }

    [Fact]
    public void DebugPanel_ShowsWhatTheAiIsWeighing()
    {
        var rival = _session.World.Gods[1];
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = _session.World,
            SettlementRules = _session.Content.SettlementRules,
            DivineRules = _session.Content.DivineRules,
            GodAiCandidates = [(rival.Id, _session.PreviewGodAi(rival))],
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

        Assert.Contains(lines, line => line.StartsWith($"AI {rival.Name} ({DivineText.Personality(rival.Personality)}): goal now: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[save power]", StringComparison.Ordinal) || line.Contains("cost ", StringComparison.Ordinal));
        Assert.Empty(_session.PreviewGodAi(_session.LocalGod!));
    }

    [Fact]
    public void PantheonSetup_NeedsAPersonalityExactlyForAiGods()
    {
        var setup = TestContent.Shipped.Pantheon;
        var humanTyrant = setup with { Gods = [setup.Gods[0] with { Personality = GodPersonality.Tyrant }] };
        var aiWithout = setup with { Gods = [setup.Gods[1] with { Personality = GodPersonality.None }] };

        Assert.Throws<ArgumentException>(() => PantheonFounder.Validate(humanTyrant));
        Assert.Throws<ArgumentException>(() => PantheonFounder.Validate(aiWithout));
        PantheonFounder.Validate(setup);
    }

    [Fact]
    public void GodAiRules_NeedAProfileForEveryPersonality()
    {
        var text = TestContent.ReadText(ContentJson.GodAiFileName);
        var broken = text.Replace("\"Personality\": \"Zealot\"", "\"Personality\": \"Guardian\"", StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == ContentJson.GodAiFileName ? broken : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains("no profile for Zealot", exception.Message, StringComparison.Ordinal);
    }
}
