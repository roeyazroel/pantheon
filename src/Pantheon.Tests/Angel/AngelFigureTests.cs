using System;
using System.Linq;
using System.Numerics;
using Pantheon.Game.Angel;
using Pantheon.Game.Controls;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation.Angel;
using Pantheon.Game.Settings;
using Pantheon.Tests.Minds.Agents;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Angel;

/// <summary>The angel's figure, its settings, launch options, key and button, and its debug lines (ADR-074).</summary>
public sealed class AngelFigureTests
{
    private static readonly Lazy<AngelMesh> Figure = new(AngelMesh.Build);

    [Fact]
    public void TheFigure_HasEveryPart_AndItsTrianglesAreWhole()
    {
        var mesh = Figure.Value;
        var parts = mesh.Detail.Select(d => (AngelPart)(int)MathF.Round(d.X)).ToHashSet();

        Assert.Equal(Enum.GetValues<AngelPart>().ToHashSet(), parts);
        Assert.Equal(0, mesh.Indices.Length % 3);
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Positions.Length - 1));
        Assert.All(mesh.Normals, n => Assert.InRange(n.Length(), 0.98f, 1.02f));
        Assert.InRange(mesh.Triangles, 2000, 8000);
        Assert.Equal(mesh.Positions.Length, mesh.Uv.Length);
        Assert.Equal(mesh.Positions.Length, mesh.Detail.Length);
    }

    [Fact]
    public void ItsWings_AreMirrorImages_SpreadingEachToItsSide_AboveItsHem()
    {
        var mesh = Figure.Value;
        var wing = Enumerable.Range(0, mesh.Positions.Length).Where(i => (AngelPart)(int)MathF.Round(mesh.Detail[i].X) == AngelPart.Wing).ToList();
        var left = wing.Where(i => mesh.Detail[i].Y < 0f).Select(i => mesh.Positions[i]).ToList();
        var right = wing.Where(i => mesh.Detail[i].Y > 0f).Select(i => mesh.Positions[i]).ToList();

        Assert.Equal(left.Count, right.Count);
        Assert.All(left, p => Assert.True(p.X < 0f));
        Assert.All(right, p => Assert.True(p.X > 0f));
        Assert.Equal(-left.Min(p => p.X), right.Max(p => p.X), 0.001f);
        Assert.InRange(right.Max(p => p.X), 1.2f, 2.2f);
        Assert.InRange(right.Max(p => p.Y), 2.4f, 3.2f);
        Assert.True(right.Min(p => p.Y) > 0.3f);
    }

    [Fact]
    public void ItsHalo_FloatsAboveItsHead_AndItsThornsRiseFromIt()
    {
        var mesh = Figure.Value;
        float Highest(AngelPart part) => Enumerable.Range(0, mesh.Positions.Length).Where(i => (AngelPart)(int)MathF.Round(mesh.Detail[i].X) == part).Max(i => mesh.Positions[i].Y);
        float Lowest(AngelPart part) => Enumerable.Range(0, mesh.Positions.Length).Where(i => (AngelPart)(int)MathF.Round(mesh.Detail[i].X) == part).Min(i => mesh.Positions[i].Y);

        Assert.True(Lowest(AngelPart.Halo) > Highest(AngelPart.Hair));
        Assert.True(Highest(AngelPart.Spike) > Highest(AngelPart.Halo));
        Assert.True(Highest(AngelPart.Skin) < 2.05f);
        Assert.InRange(AngelMesh.Shoulder.Y, 1.3f, 1.7f);
    }

    [Fact]
    public void TheAngelsSettings_DefaultOn_ChatterComesBackIntoRange_AndRoundTrip()
    {
        var settings = new GameSettings();

        Assert.True(settings.Angel && settings.AngelThinks);
        Assert.Equal(1, settings.AngelChatter);
        Assert.Equal(2, (settings with { AngelChatter = 9 }).Clamped().AngelChatter);
        Assert.Equal(0, (settings with { AngelChatter = -3 }).Clamped().AngelChatter);
        var changed = settings with { Angel = false, AngelThinks = false, AngelChatter = 2 };
        Assert.Equal(changed, GameSettings.Parse(changed.Serialize()));
    }

    [Fact]
    public void TheLaunchOptions_KeepOrSendAwayTheAngel_HoldItAtAStage_AndAskIt()
    {
        Assert.True(LaunchOptions.Parse(["--angel=on"]).Angel);
        Assert.False(LaunchOptions.Parse(["--angel=off"]).Angel);
        var held = LaunchOptions.Parse(["--angel=Fallen"]);
        Assert.True(held.Angel);
        Assert.Equal(AngelStage.Fallen, held.AngelLook);
        Assert.Equal("What now?", LaunchOptions.Parse(["--angel-ask=What now?"]).AngelAsk);
        Assert.Null(LaunchOptions.Parse([]).Angel);
        Assert.Contains("--angel", Assert.Single(LaunchOptions.Parse(["--angel=sometimes"]).Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAngel_HasAKey_AndTheFirstButtonOnTheGodsBar()
    {
        Assert.Contains(KeyGuide.Entries, e => e.Actions.Contains("angel_call"));
        Assert.False(KeyGuide.IsDebug("angel_call"));
        var first = CommandBar.Buttons(debugTools: false)[0];
        Assert.Equal(BarCommand.Angel, first.Command);
        Assert.Equal("angel_call", first.Action);
    }

    [Fact]
    public void TheDebugLines_TellItsLook_WhatMadeIt_WhoSpeaksForIt_AndWhatItSaid()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var god = session.World.Gods.Single(g => g.Id == me);
        god.Conduct.Help = 5f;
        var angel = new AngelDirector(TestContent.Angel);
        angel.Update(session, [], 0.1, new AngelTerms(null, true, true, 1));
        angel.Update(session, [], 3, new AngelTerms(null, true, true, 1));

        var lines = AngelReadout.Lines(angel, god, null);

        Assert.Contains(angel.Name, lines[0], StringComparison.Ordinal);
        Assert.Contains("Kind", lines[0], StringComparison.Ordinal);
        Assert.Contains("kindness 5.0, harm 0.0", lines[0], StringComparison.Ordinal);
        Assert.Contains("the scribes", lines[1], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.StartsWith("  said (the scribes, of Welcome)", StringComparison.Ordinal));
        Assert.Empty(AngelReadout.Lines(angel, null, null));
    }
}
