using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Book;
using Pantheon.Game.Camera;
using Pantheon.Game.Launch;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>The Book of the World (spec §57) and the strategic overview (spec §56), ADR-031.</summary>
public sealed class BookTests
{
    [Fact]
    public void TheWorldPage_NamesTheWorld_EveryGod_TheRace_TheDangers_AndTheHolySites()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        session.RunTicks(100);
        world.Gods[1].DominantSinceTick = world.Tick - 100;

        var page = BookReadout.Build(session, BookTab.World);
        var text = page.Lines.Select(l => l.Text).ToList();

        Assert.Equal(["The world", "The gods", "The race", "Prophets and elders", "Dangers", "Holy sites"], page.Lines.Where(l => l.Tone == BookTone.Heading).Select(l => l.Text));
        Assert.StartsWith("Seed 77, an island", text[1], StringComparison.Ordinal);
        Assert.All(world.Gods, god => Assert.Contains(text, t => t.StartsWith(god.Name, StringComparison.Ordinal) && t.Contains(" follower", StringComparison.Ordinal)));
        Assert.Contains(text, t => t.StartsWith(session.LocalGod!.Name + " (you): ", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Tone == BookTone.Good && l.Text.Contains("(you)", StringComparison.Ordinal));
        Assert.Contains(text, t => t.StartsWith(world.Gods[1].Name + " has held its dominance for 0:10", StringComparison.Ordinal));
        Assert.Contains(text, t => t.EndsWith("locust swarms.", StringComparison.Ordinal));
        Assert.Equal(world.HolySites.Count, text.SkipWhile(t => t != "Holy sites").Skip(1).Count());
    }

    [Fact]
    public void TheReligionPage_ShowsYourVillages_EveryGodsFaith_AndTheOldFaiths()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var mine = world.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence > 0.5f));
        mine.Beliefs.First(b => b.GodId == session.LocalGodId).Doubt = 0.6f;
        var other = world.Settlements.First(s => s.Id != mine.Id);
        other.OldFaith = new OldFaith { GodId = world.Gods[2].Id, Share = 0.2f, Returned = 0f, Faded = 0f, SinceTick = 0 };

        var page = BookReadout.Build(session, BookTab.Religion);

        Assert.Equal(["Your faith", "Your villages", "Your prophets and elders", "Every god's faith", "Old Faiths and the godless"], page.Lines.Where(l => l.Tone == BookTone.Heading).Select(l => l.Text));
        Assert.Contains(page.Lines, l => l.Tone == BookTone.Warning && l.Text.StartsWith(mine.Name + ":", StringComparison.Ordinal) && l.Text.Contains("doubt 0.60", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text == $"{other.Name}: 20% keep the Old Faith of {world.Gods[2].Name}");
        Assert.Equal(world.Gods.Count, page.Lines.SkipWhile(l => l.Text != "Every god's faith").Skip(1).TakeWhile(l => l.Tone != BookTone.Heading).Count());
    }

    [Fact]
    public void TheMiraclesPage_ListsEveryMiracle_ByKind_AndOffersThoseThatMayBeAimed()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var miracles = TestContent.Shipped.Miracles.Miracles;

        var page = BookReadout.Build(session, BookTab.Miracles);

        Assert.Equal(["Miracles", "Signature miracles of the domains", "Divine Acts"], page.Lines.Where(l => l.Tone == BookTone.Heading).Select(l => l.Text));
        Assert.Equal(miracles.Count, page.Lines.Count(l => l.MiracleId is not null));
        var food = page.Lines.Single(l => l.MiracleId == "food");
        Assert.True(food.CanAim);
        Assert.StartsWith("Food (minor, 40 power", food.Text, StringComparison.Ordinal);
        Assert.EndsWith("[needs a Lesser God]", page.Lines.Single(l => l.MiracleId == "mass_healing").Text, StringComparison.Ordinal);
        Assert.EndsWith("[needs a God]", page.Lines.Single(l => l.MiracleId == "firestorm").Text, StringComparison.Ordinal);
        Assert.EndsWith($"[needs {Pantheon.Game.Domains.DomainText.InSentence(GodDomain.Nature)}]", page.Lines.Single(l => l.MiracleId == "regrowth").Text, StringComparison.Ordinal);
        Assert.False(page.Lines.Single(l => l.MiracleId == "still").CanAim);
    }

    [Fact]
    public void TheCreaturePage_ShowsItsGrowth_Paths_Learning_Memories_AndBonds()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var creature = session.LocalCreature!;
        creature.Specialisations.Add(CreatureSpecialisation.Healer);

        var page = BookReadout.Build(session, BookTab.Creature);

        Assert.Equal([$"{creature.Name} the {creature.Species}", "Paths", "What it has learned", "What it remembers", "Other Creatures"], page.Lines.Where(l => l.Tone == BookTone.Heading).Select(l => l.Text));
        Assert.Contains(page.Lines, l => l.Text == "Healer: it heals the people it helps.");
        Assert.Contains(page.Lines, l => l.Text.StartsWith("By a village in need: ", StringComparison.Ordinal));
        Assert.Contains(page.Lines, l => l.Text == "It has met no other Creature.");
        Assert.All(Enum.GetValues<BookTab>(), tab => Assert.NotEmpty(BookReadout.Name(tab)));
    }

    [Fact]
    public void TheOverview_ShowsTheWholeIslandFromAfar_AndGoesBack()
    {
        var camera = new GodCameraModel(GodCameraSettings.Default);
        camera.SetBounds(Vector2.Zero, new Vector2(2048f, 2048f));
        camera.FocusOn(new Vector2(300f, 400f), 150f);
        camera.SnapToTarget();

        camera.ToggleOverview();
        camera.SnapToTarget();
        Assert.True(camera.InOverview);
        Assert.Equal((new Vector2(1024f, 1024f), camera.MaxDistance), (camera.Focus, camera.Distance));

        camera.ToggleOverview();
        camera.SnapToTarget();
        Assert.False(camera.InOverview);
        Assert.Equal((new Vector2(300f, 400f), 150f), (camera.Focus, camera.Distance));
    }

    [Fact]
    public void LaunchOptions_OpenTheBook_AndTheOverview()
    {
        var options = LaunchOptions.Parse(["--book=religion", "--overview=on", "--overlay=politics"]);

        Assert.Empty(options.Warnings);
        Assert.Equal((BookTab.Religion, true), (options.Book!.Value, options.Overview));
        Assert.Single(LaunchOptions.Parse(["--book=bestiary"]).Warnings);
    }
}
