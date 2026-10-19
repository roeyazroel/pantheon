using System;
using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Culture;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.History;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Society;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Culture;

/// <summary>How culture is shown and driven (ADR-024): the Settlements panel, the HUD and diplomacy panel, alerts, history, the overlay and the launch option.</summary>
public sealed class CulturePresentationTests
{
    [Fact]
    public void ThePanel_ShowsTheDoctrinesHeld_TheTurnFromTheGods_AndThePeoplesFeelings()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var home = session.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence >= 0.5f));
        (home.Culture.Charity, home.Culture.Pacifism, home.Culture.Secularism) = (0.8f, 0.3f, 0.35f);
        var mine = home.Beliefs.Single(b => b.GodId == session.LocalGodId);
        (mine.Dependency, mine.Resentment) = (0.4f, 0.1f);

        var lines = SettlementPanelReadout.Build(session, home.Id)!.Lines;

        Assert.Contains("Holds: charity (strongly), pacifism (somewhat). Towards the gods: agnostic (35% turned away).", lines);
        Assert.Contains(lines, line => line.StartsWith("Feelings: Ilyra: love", StringComparison.Ordinal) && line.Contains("need 40%, resentment 10%", StringComparison.Ordinal));
    }

    [Fact]
    public void TheHud_AndTheDiplomacyPanel_SayHowTheWorldSeesTheGods()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var me = session.World.Gods.Single(g => g.Id == session.LocalGodId);
        var rival = session.World.Gods.First(g => g.Id != session.LocalGodId);
        me.Traits.Add(GodTrait.Compassionate);
        rival.Traits.AddRange([GodTrait.Cruel, GodTrait.Vengeful]);

        var hud = HudReadout.Build(session, null, null);
        var entry = DiplomacyReadout.Build(session).Single(e => e.GodId == rival.Id);

        Assert.EndsWith(" - seen as compassionate", hud.GodLine, StringComparison.Ordinal);
        Assert.EndsWith(" (seen as cruel, vengeful)", entry.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Alerts_TellOfRevoltsGodlessVillagesDoctrinesAndTraits()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", Domain.Gods.GodController.Ai, Domain.Gods.GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        WorldEvent[] events =
        [
            new Rebellion { Tick = 1, SettlementId = 1, GodId = DivineWorlds.GodId, People = 24 },
            new SecularismRose { Tick = 1, SettlementId = 1, Stage = SecularStage.Atheist, Previous = SecularStage.Agnostic },
            new SecularismRose { Tick = 1, SettlementId = 1, Stage = SecularStage.Agnostic, Previous = SecularStage.Devout },
            new DoctrineEmbraced { Tick = 1, SettlementId = 1, Doctrine = Doctrine.Charity },
            new DoctrineEmbraced { Tick = 1, SettlementId = 2, Doctrine = Doctrine.Militarism },
            new TraitEarned { Tick = 1, GodId = DivineWorlds.GodId, Trait = GodTrait.Merciful },
            new TraitEarned { Tick = 1, GodId = 2, Trait = GodTrait.Cruel },
        ];

        var raised = new AlertFeed().Push(events, world, DivineWorlds.GodId, 10.0);

        Assert.Equal(
            [
                "Town1 revolts against you: 24 renounce you and tear down your temple.",
                "Town1 no longer believes in the gods: it yields little power and resists miracles.",
                "Town1 embraces charity.",
                "Your people now speak of you as merciful.",
                "The world now speaks of Rivalis as cruel.",
            ],
            raised.Select(a => a.Text));
        Assert.Equal(AlertSeverity.Danger, raised[0].Severity);
    }

    [Fact]
    public void Revolts_Renunciations_AndDoctrinesOfLargeVillages_EnterTheHistory()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        world.Settlements[1].Population = 5;
        var recorder = new HistoryRecorder(TestContent.Shipped.History);

        recorder.Record(
            world,
            [
                new Rebellion { Tick = 0, SettlementId = 1, GodId = 1, People = 30 },
                new SecularismRose { Tick = 0, SettlementId = 1, Stage = SecularStage.AntiDivine, Previous = SecularStage.Atheist },
                new SecularismRose { Tick = 0, SettlementId = 2, Stage = SecularStage.Atheist, Previous = SecularStage.Agnostic },
                new DoctrineEmbraced { Tick = 0, SettlementId = 1, Doctrine = Doctrine.NatureWorship },
                new DoctrineEmbraced { Tick = 0, SettlementId = 1, Doctrine = Doctrine.NatureWorship },
                new DoctrineEmbraced { Tick = 0, SettlementId = 2, Doctrine = Doctrine.Charity },
            ],
            0);

        Assert.Equal([HistoryKind.Revolt, HistoryKind.Renunciation, HistoryKind.Doctrine], world.History.Select(r => r.Kind));
        Assert.Equal("The Revolt of Town1 against Testa", HistoryText.Title(world.History[0], world));
        Assert.Equal("30 renounced their god", HistoryText.Detail(world.History[0], world));
        Assert.Equal("Town1 renounces the gods", HistoryText.Title(world.History[1], world));
        Assert.Equal("Town1 embraces nature worship", HistoryText.Title(world.History[2], world));
    }

    [Fact]
    public void TheCultureOverlay_ColoursLandsByTheirDoctrine_AndGreyWhereTheGodsAreForsaken()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        world.Settlements[0].Culture.Militarism = 0.9f;
        world.Settlements[1].Culture.Secularism = 0.9f;
        var view = TestContent.DivineView;

        var image = OverlayMap.Build(world, OverlayKind.Culture, 1, DivineWorlds.Rules, SettlementWorlds.Rules, view);

        Assert.StartsWith("Culture overlay", image.Legend, StringComparison.Ordinal);
        Assert.Equal(OverlayKind.Nature, OverlayMap.Next(OverlayKind.Culture));
        var warlike = world.Settlements[0].RegionId * 4;
        var godless = world.Settlements[1].RegionId * 4;
        Assert.True(image.Rgba[warlike] > 2 * image.Rgba[warlike + 2], "militarist lands are red");
        Assert.Equal(image.Rgba[godless], image.Rgba[godless + 2]);
        Assert.True(image.Rgba[godless + 3] > 0);
    }

    [Fact]
    public void TheDoctrineOption_ShapesAVillagesCulture()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var village = session.World.Settlements[0];

        var options = LaunchOptions.Parse(["--doctrine=nature-worship", "--doctrine=Militarism"]);
        var embraced = CultureCommands.Impose(session, village.Id, CultureAtStart.Militarism);
        var forsaken = CultureCommands.Impose(session, village.Id, CultureAtStart.Secular);

        Assert.Equal(CultureAtStart.Militarism, options.Doctrine);
        Assert.Single(options.Warnings);
        Assert.Equal(village.Name + " embraces militarism", embraced);
        Assert.Equal(village.Name + " turns against the gods", forsaken);
        Assert.Equal((1f, 0.9f), (village.Culture.Militarism, village.Culture.Secularism));
    }
}
