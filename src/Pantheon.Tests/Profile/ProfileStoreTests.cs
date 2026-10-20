using System;
using System.IO;
using System.Linq;
using Pantheon.Persistence;
using Pantheon.Persistence.Profile;
using Xunit;

namespace Pantheon.Tests.Profile;

/// <summary>The player's profile (ADR-060): SQLite, and the plain files it falls back to, behave alike.</summary>
public sealed class ProfileStoreTests : IDisposable
{
    private static readonly DateTime Monday = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pantheon-profile-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<string> Kinds => ["sqlite", "files"];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Values_AreKept_AcrossOpenings(string kind)
    {
        using (var store = Open(kind))
        {
            Assert.Null(store.ReadValue("tutorial"));
            store.WriteValue("tutorial", "3");
            store.WriteValue("tutorial", "4");
        }

        using var again = Open(kind);
        Assert.Equal("4", again.ReadValue("tutorial"));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Worlds_AreListed_TheMostRecentlyPlayedFirst(string kind)
    {
        using var store = Open(kind);
        var older = store.AddWorld(World("Older", Monday));
        var newer = store.AddWorld(World("Newer", Monday.AddHours(1)));
        Assert.NotEqual(older.Id, newer.Id);
        Assert.Equal(["Newer", "Older"], store.Worlds().Select(w => w.Name));

        store.UpdateWorld(older with { PlayedUtc = Monday.AddHours(2), Followers = 42, Rank = "God" });
        var first = store.Worlds()[0];
        Assert.Equal(("Older", 42, "God", 18_446_744_073_709_551_615UL), (first.Name, first.Followers, first.Rank, first.Seed));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AWorld_KeepsEveryManualSave_ButOnlyItsNewestAutosaveAndQuicksave(string kind)
    {
        using var store = Open(kind);
        var world = store.AddWorld(World("World", Monday));
        store.WriteSave(world.Id, SaveKind.Manual, "one", 10, Monday, 33, [1]);
        store.WriteSave(world.Id, SaveKind.Manual, "two", 20, Monday.AddMinutes(1), 33, [2]);
        store.WriteSave(world.Id, SaveKind.Auto, "auto", 30, Monday.AddMinutes(2), 33, [3]);
        var auto = store.WriteSave(world.Id, SaveKind.Auto, "auto", 40, Monday.AddMinutes(3), 33, [4, 4]);
        store.WriteSave(world.Id, SaveKind.Quick, "quick", 50, Monday.AddMinutes(4), 33, [5]);

        var saves = store.Saves(world.Id);
        Assert.Equal([50L, 40, 20, 10], saves.Select(s => s.Tick));
        Assert.Equal(new byte[] { 4, 4 }, store.ReadSave(auto.Id));
        Assert.Equal(2, saves.Single(s => s.Id == auto.Id).Bytes);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void DeletingAWorld_DeletesItsSaves(string kind)
    {
        using var store = Open(kind);
        var gone = store.AddWorld(World("Gone", Monday));
        var kept = store.AddWorld(World("Kept", Monday));
        var save = store.WriteSave(gone.Id, SaveKind.Manual, "save", 1, Monday, 33, [9]);
        store.WriteSave(kept.Id, SaveKind.Manual, "save", 1, Monday, 33, [8]);

        store.DeleteWorld(gone.Id);

        Assert.Equal(["Kept"], store.Worlds().Select(w => w.Name));
        Assert.Null(store.ReadSave(save.Id));
        Assert.Single(store.Saves(kept.Id));
    }

    [Fact]
    public void TheProfile_IsSqlite_WhenItsLibraryLoads()
    {
        using var store = ProfileStores.Open(_directory, out var problem);
        Assert.Null(problem);
        Assert.StartsWith("SQLite 3.", store.Kind, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_directory, ProfileStores.DatabaseName)));
    }

    [Fact]
    public void ASave_IsPackedSmall_AndUnpackedWhole_AndDamageIsASaveError()
    {
        var json = "{\"SaveVersion\":33," + string.Concat(Enumerable.Repeat("\"padding\":\"aaaaaaaaaaaaaaaa\",", 200)) + "\"end\":1}";
        var packed = ProfileStores.Pack(json);

        Assert.True(packed.Length < json.Length / 10, "a save's JSON compresses well");
        Assert.Equal(json, ProfileStores.Unpack(packed));
        Assert.Throws<SaveFormatException>(() => ProfileStores.Unpack([1, 2, 3, 4]));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private IProfileStore Open(string kind) => kind == "sqlite"
        ? SqliteProfileStore.Open(Path.Combine(Directory.CreateDirectory(_directory).FullName, "test.db"))
        : FileProfileStore.Open(Path.Combine(_directory, "files"));

    private static WorldRecord World(string name, DateTime played) => new()
    {
        Name = name,
        God = "Ilyra",
        Seed = ulong.MaxValue,
        Size = "Small",
        Victory = "ascendancy",
        Seasons = true,
        Species = "Mossback",
        CreatedUtc = played,
        PlayedUtc = played,
    };
}
