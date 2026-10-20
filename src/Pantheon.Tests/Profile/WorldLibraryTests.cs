using System;
using System.IO;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Persistence;
using Pantheon.Persistence.Profile;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Profile;

/// <summary>The worlds in the profile (ADR-060): starting, saving, loading, continuing and the saves from before it.</summary>
public sealed class WorldLibraryTests : IDisposable
{
    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pantheon-library-" + Guid.NewGuid().ToString("N"));
    private readonly IProfileStore _store;

    public WorldLibraryTests() => _store = SqliteProfileStore.Open(Path.Combine(Directory.CreateDirectory(_directory).FullName, "test.db"));

    [Fact]
    public void ANewWorld_IsNamedAfterItsFirstVillage_AndRecordsItsGodAndSize()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var library = new WorldLibrary(_store, session.Content);

        var world = library.Begin(session, "  ", Noon);

        Assert.Equal($"The isle of {session.World.Settlements[0].Name}", world.Name);
        Assert.Equal(session.LocalGod!.Name, world.God);
        Assert.Equal(1234UL, world.Seed);
        Assert.Equal(session.Content.Sizes.Sizes[0].Name, world.Size);
        Assert.Equal(world, library.Current);
        Assert.Equal("Islay", library.Begin(session, "Islay", Noon).Name);
    }

    [Fact]
    public void ASave_LoadsBackTheSameWorld_AndCountsTheTimePlayed()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var library = new WorldLibrary(_store, session.Content);
        library.Begin(session, "Home", Noon);
        SessionCommands.AdvanceTicks(session, 300);
        library.AddPlayTime(90);
        var saved = WorldSaveSerializer.Serialize(session.World);

        Assert.StartsWith("Saved Home", library.Save(session, SaveKind.Manual, Noon.AddMinutes(5)), StringComparison.Ordinal);
        SessionCommands.AdvanceTicks(session, 300);
        var save = _store.Saves(library.Current!.Id).Single();
        Assert.StartsWith("Loaded Home", library.Load(session, save.Id), StringComparison.Ordinal);

        Assert.Equal(saved, WorldSaveSerializer.Serialize(session.World));
        var record = _store.Worlds().Single();
        Assert.Equal((300L, 90.0, Noon.AddMinutes(5)), (record.Tick, record.PlaySeconds, record.PlayedUtc));
        Assert.Equal(record.Id, library.Latest()!.Value.World.Id);
    }

    [Fact]
    public void SavingAWorldNotInTheLibrary_RecordsItFirst_AndAQuicksaveReplacesTheLast()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(5));
        var library = new WorldLibrary(_store, session.Content);
        Assert.Null(library.Current);

        library.Save(session, SaveKind.Quick, Noon);
        SessionCommands.AdvanceTicks(session, 50);
        library.Save(session, SaveKind.Quick, Noon.AddMinutes(1));

        var world = Assert.Single(_store.Worlds());
        Assert.Equal(50L, Assert.Single(_store.Saves(world.Id)).Tick);
        Assert.Equal(50L, library.LatestQuick()!.Tick);
    }

    [Fact]
    public void TheSavesFromBefore_AreBroughtIn_Once()
    {
        var saves = Directory.CreateDirectory(Path.Combine(_directory, "saves")).FullName;
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(9));
        SessionCommands.AdvanceTicks(session, 20);
        WorldSaveSerializer.SaveToFile(session.World, Path.Combine(saves, "Ilyra-year1-seed9.json"));
        File.WriteAllText(Path.Combine(saves, "broken.json"), "{ not a save");
        var library = new WorldLibrary(_store, session.Content);

        Assert.Equal(1, library.ImportFiles(saves, Noon));
        Assert.Equal(0, library.ImportFiles(saves, Noon));

        var card = Assert.Single(library.Cards());
        Assert.Equal("Ilyra-year1-seed9", card.World.Name);
        Assert.Equal(20L, Assert.Single(card.Saves).Tick);
        Assert.Contains("seed 9", card.Details, StringComparison.Ordinal);
        Assert.Contains("followers", card.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletingTheCurrentWorld_LeavesItUnrecorded()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(3));
        var library = new WorldLibrary(_store, session.Content);
        var world = library.Begin(session, "Doomed", Noon);
        library.Save(session, SaveKind.Manual, Noon);

        library.Delete(world.Id);

        Assert.Null(library.Current);
        Assert.Empty(library.Cards());
        Assert.Null(library.Latest());
    }

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}

/// <summary>The title screen's launch option (ADR-060).</summary>
public sealed class TitlePageOptionTests
{
    [Fact]
    public void ATitlePage_OpensTheTitleScreen_AndAnUnknownOneWarns()
    {
        var worlds = Pantheon.Game.Launch.LaunchOptions.Parse(["--title-page=worlds"]);
        Assert.Equal(("worlds", true), (worlds.TitlePage, worlds.Menu));

        var unknown = Pantheon.Game.Launch.LaunchOptions.Parse(["--title-page=credits"]);
        Assert.Null(unknown.TitlePage);
        Assert.Contains(unknown.Warnings, warning => warning.Contains("title page", StringComparison.Ordinal));
    }
}
