using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Flow;
using Pantheon.Game.History;
using Pantheon.Game.Hud;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>How each kind of victory is told (ADR-027): alerts, history, the end of the game, the HUD and the choices.</summary>
public sealed class VictoryPresentationTests
{
    [Fact]
    public void Alerts_WordEachKindOfVictory()
    {
        var world = PantheonWorlds.Create();

        Assert.Equal(
            [
                ("Rivalis has conquered the world.", AlertSeverity.Danger),
                ("You have won: every rival's following was a remnant for ten minutes.", AlertSeverity.Good),
                ("You and Tertia have won together: every rival's following was a remnant for ten minutes.", AlertSeverity.Good),
                ("Tertia has won the world's culture.", AlertSeverity.Danger),
                ("You have won: most of the world's villages looked to you for ten minutes.", AlertSeverity.Good),
            ],
            new[]
            {
                Victory(VictoryKind.Conquest, 2),
                Victory(VictoryKind.Conquest, 1),
                Victory(VictoryKind.Conquest, 1, 3),
                Victory(VictoryKind.Cultural, 3),
                Victory(VictoryKind.Cultural, 1),
            }.Select(e => Assert.Single(new AlertFeed().Push([e], world, 1, 1.0))).Select(a => (a.Text, a.Severity)));
    }

    [Fact]
    public void History_RecordsHowTheWorldWasWon()
    {
        var world = PantheonWorlds.Create();
        Assert.Equal(VictoryKind.Ascendancy, VictoryText.KindOf(world));

        new HistoryRecorder(TestContent.Shipped.History).Record(world, [Victory(VictoryKind.Conquest, 2)], 0);

        var record = Assert.Single(world.History, r => r.Kind == HistoryKind.Victory);
        Assert.Equal("The Conquest of the World by Rivalis", HistoryText.Title(record, world));
        Assert.Equal(VictoryKind.Conquest, VictoryText.KindOf(world));
    }

    [Fact]
    public void TheEndOfTheGame_TellsHowItWasWon()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        world.WinnerGodIds.Add(2);
        new HistoryRecorder(TestContent.Shipped.History).Record(world, [Victory(VictoryKind.Cultural, 2)], 0);

        var summary = EndSummaryBuilder.Build(session)!;

        Assert.False(summary.Won);
        Assert.Equal($"{world.Gods[1].Name} has won the world's culture.", summary.Subtitle);
    }

    [Fact]
    public void Hud_ShowsTheSandbox_AndConquestsAndCulturesUnderWay()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234), new NewWorldOptions(Victory: VictorySettings.Sandbox));
        var world = session.World;

        Assert.Equal("A sandbox: no god can win.", HudReadout.Build(session, null, null).Contest);

        world.Victory = VictorySettings.All;
        world.Gods[1].ConquestSinceTick = world.Tick;
        var conquest = HudReadout.Build(session, null, null);
        Assert.Equal($"{world.Gods[1].Name} has all but crushed its rivals: it conquers the world in 10:00", conquest.Contest);
        Assert.False(conquest.ContestFavoursPlayer);

        world.Gods[1].ConquestSinceTick = null;
        world.Gods[0].CulturalSinceTick = world.Tick;
        var culture = HudReadout.Build(session, null, null);
        Assert.Matches("^You lead \\d+ of \\d+ villages: you win the world's culture in 10:00$", culture.Contest);
        Assert.True(culture.ContestFavoursPlayer);

        world.Gods[0].CulturalSinceTick = null;
        world.Victory = new VictorySettings { Ascendancy = false, Conquest = true };
        Assert.Equal("Bring every rival below 5% of all belief for 10:00 to conquer the world", HudReadout.Build(session, null, null).Contest);
        world.Victory = new VictorySettings { Ascendancy = false, Cultural = true };
        Assert.Matches("^You lead \\d+ of \\d+ villages; lead \\d+ for 10:00 to win the world's culture$", HudReadout.Build(session, null, null).Contest);
    }

    [Fact]
    public void TheSettings_AreNamed_AndEveryChoiceIsOffered()
    {
        Assert.Equal("Ascendancy", VictoryText.Settings(VictorySettings.Default));
        Assert.Equal("Ascendancy, conquest and cultural victory", VictoryText.Settings(VictorySettings.All));
        Assert.Equal("Conquest and cultural victory", VictoryText.Settings(new VictorySettings { Ascendancy = false, Conquest = true, Cultural = true }));
        Assert.Equal("Sandbox: no victory", VictoryText.Settings(VictorySettings.Sandbox));
        Assert.Equal(["Divine Ascendancy", "Every victory", "Conquest", "Culture", "Sandbox"], VictoryText.Choices.Select(c => c.Name));
        Assert.Equal(VictorySettings.Default, VictoryText.Choices[0].Settings);
        Assert.Equal(VictoryText.Choices.Count, VictoryText.Choices.Select(c => c.Settings).Distinct().Count());
    }

    /// <summary>A victory event.</summary>
    private static VictoryAchieved Victory(VictoryKind kind, params int[] gods) => new() { Tick = 0, GodIds = [.. gods], Kind = kind };
}
