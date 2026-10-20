using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Controls;
using Pantheon.Game.Creatures;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>Every command in view (ADR-068): the key map, the god's bar, the map's chooser, seeking a god, and the gods known from the start.</summary>
public sealed class ControlsTests
{
    private static readonly Lazy<IReadOnlySet<string>> GameActions = new(ReadGameActions);

    [Fact]
    public void EveryActionOfTheGame_IsInTheKeyMap_AndTheMapNamesNoOther()
    {
        var mapped = KeyGuide.Entries.SelectMany(e => e.Actions).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(GameActions.Value.Except(mapped));
        Assert.Empty(mapped.Except(GameActions.Value));
        Assert.True(GameActions.Value.Count >= 60, "the actions were read from project.godot");
    }

    [Fact]
    public void EveryLine_HasWords_AndKeysOrAGesture_AndNoLineIsTwice()
    {
        Assert.All(KeyGuide.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Text));
            Assert.True(entry.Actions.Count > 0 || entry.Gesture is not null, entry.Text);
        });
        Assert.Equal(KeyGuide.Entries.Count, KeyGuide.Entries.Select(e => (string.Join(",", e.Actions), e.Modifier, e.Gesture)).Distinct().Count());
        Assert.All(Enum.GetValues<KeyGroup>(), group => Assert.False(string.IsNullOrWhiteSpace(KeyGuide.Heading(group))));
    }

    [Fact]
    public void TheDebugTools_AreTheDebugGroupsActions_EveryDebugKeyAmongThem()
    {
        Assert.All(GameActions.Value.Where(a => a.StartsWith("debug_", StringComparison.Ordinal)), action => Assert.True(KeyGuide.IsDebug(action), action));
        Assert.All(KeyGuide.Entries.Where(e => e.Group != KeyGroup.Debug).SelectMany(e => e.Actions), action => Assert.False(KeyGuide.IsDebug(action), action));
        Assert.True(KeyGuide.IsDebug("world_new"), "F2 replaces the world at a touch");
        Assert.True(KeyGuide.IsDebug("debug_meet") && KeyGuide.IsDebug("debug_council"), "K and J are debug tools");
        Assert.False(KeyGuide.IsDebug("world_quicksave"));
        Assert.Contains(KeyGuide.Shown(debugTools: true), g => g.Group == KeyGroup.Debug);
        Assert.DoesNotContain(KeyGuide.Shown(debugTools: false), g => g.Group == KeyGroup.Debug);
        Assert.All(KeyGuide.Shown(debugTools: false), g => Assert.NotEmpty(g.Entries));
    }

    [Fact]
    public void TheBar_PressesTheKeyMapsActions_OutsideTheDebugGroup_AndOffersTheDebugPaletteOnlyWithTheTools()
    {
        var plain = CommandBar.Buttons(debugTools: false);
        var debug = CommandBar.Buttons(debugTools: true);

        Assert.All(plain.Where(b => b.Action is not null), b => Assert.Contains(b.Action!, GameActions.Value));
        Assert.All(debug.Where(b => b.Action is not null), b => Assert.False(KeyGuide.IsDebug(b.Action!), b.Name));
        Assert.DoesNotContain(plain, b => b.Command == BarCommand.Debug);
        Assert.Contains(debug, b => b.Command == BarCommand.Debug);
        Assert.Equal(Enum.GetValues<BarCommand>().Length, debug.Select(b => b.Command).Distinct().Count());
        Assert.Equal(BarCommand.Menu, debug[^1].Command);
        Assert.All(debug, b => Assert.False(string.IsNullOrWhiteSpace(b.Name)));
    }

    [Fact]
    public void TheDebugOverlays_AreOffered_AndSteppedThrough_OnlyWithTheTools()
    {
        var offered = OverlayChoices.Offered(debugTools: false);
        var stepped = new List<OverlayKind>();
        for (var kind = OverlayMap.Next(null, debugTools: false); kind is { } shown; kind = OverlayMap.Next(shown, debugTools: false))
        {
            stepped.Add(shown);
        }

        Assert.Equal(offered, stepped);
        Assert.DoesNotContain(OverlayKind.Navigation, offered);
        Assert.DoesNotContain(OverlayKind.Authority, offered);
        Assert.Equal(Enum.GetValues<OverlayKind>(), OverlayChoices.Offered(debugTools: true));
        Assert.Equal(OverlayKind.Navigation, OverlayMap.Next(OverlayKind.Politics));
        Assert.Null(OverlayMap.Next(OverlayKind.Politics, debugTools: false));
        Assert.All(Enum.GetValues<OverlayKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(OverlayChoices.Title(kind))));
    }

    [Fact]
    public void Seek_SendsYourCreatureTowardsTheOtherGodsCreature()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var theirs = CreatureCommands.NearestOther(session)!;
        var god = session.World.Gods.Find(g => g.Id == theirs.GodId)!.Name;
        var before = Distance(mine.Position, theirs.Position);

        var status = CreatureCommands.Seek(session, theirs.GodId);
        session.RunTicks(200);

        Assert.Equal($"{mine.Name} sets out to meet {theirs.Name}, the Creature of {god}", status);
        Assert.True(Distance(mine.Position, theirs.Position) < before, "it went towards the other Creature");
    }

    [Fact]
    public void Seek_SaysWhyItCannot()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var theirs = CreatureCommands.NearestOther(session)!;
        var god = session.World.Gods.Find(g => g.Id == theirs.GodId)!.Name;

        Assert.Equal("That god has no Creature to meet", CreatureCommands.Seek(session, 999));
        theirs.Activity.Phase = CreaturePhase.Banished;
        Assert.Equal($"{god} has no Creature to meet", CreatureCommands.Seek(session, theirs.GodId));
        mine.Activity.Phase = CreaturePhase.Banished;
        Assert.Equal($"{mine.Name} is banished and cannot go", CreatureCommands.Seek(session, theirs.GodId));
    }

    [Fact]
    public void ANewWorld_MayBeginWithItsGodsKnownToOneAnother()
    {
        var known = WorldMaker.Generate(TestContent.Small, new WorldSeed(1234), new NewWorldOptions(GodsMet: true)).State;
        var strangers = WorldMaker.Generate(TestContent.Small, new WorldSeed(1234), new NewWorldOptions()).State;

        Assert.All(known.Gods, god => Assert.Equal(known.Gods.Where(g => g.Id != god.Id).Select(g => g.Id).Order(), god.KnownGods.Where(id => id != god.Id).Order()));
        Assert.All(strangers.Gods, god => Assert.DoesNotContain(god.KnownGods, id => id != god.Id));
        Assert.Equal(0, known.Tick);
    }

    [Fact]
    public void Bar_PressesAButtonAtTheStart_AndTheKeysHaveATitlePage()
    {
        Assert.Equal(BarCommand.Keys, Pantheon.Game.Launch.LaunchOptions.Parse(["--bar=keys"]).Bar);
        Assert.Equal(BarCommand.Map, Pantheon.Game.Launch.LaunchOptions.Parse(["--bar=Map"]).Bar);
        Assert.Null(Pantheon.Game.Launch.LaunchOptions.Parse([]).Bar);
        Assert.Single(Pantheon.Game.Launch.LaunchOptions.Parse(["--bar=nothing"]).Warnings);
        Assert.Single(Pantheon.Game.Launch.LaunchOptions.Parse(["--bar=3"]).Warnings);
        Assert.Equal("keys", Pantheon.Game.Launch.LaunchOptions.Parse(["--title-page=keys"]).TitlePage);
    }

    /// <summary>The input actions <c>project.godot</c> defines.</summary>
    private static IReadOnlySet<string> ReadGameActions()
    {
        var text = File.ReadAllText(RepositoryRoot.Combine("game", "project.godot"));
        var start = text.IndexOf("[input]", StringComparison.Ordinal);
        var end = text.IndexOf("\n[", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? text[start..] : text[start..end];
        return Regex.Matches(section, @"^(\w+)=\{", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Distance between two ground positions in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
