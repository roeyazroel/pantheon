using System;
using System.IO;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The session ties world, terrain, clock and persistence together.</summary>
public sealed class GameSessionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pantheon-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GameSession_UpdateRunsTicksAtTheFixedRate()
    {
        var session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(3));

        for (var frame = 0; frame < 60; frame++)
        {
            session.Update(1.0 / 60.0);
        }

        Assert.InRange(session.World.Tick, 9, 10);
    }

    [Fact]
    public void GameSession_SaveAndLoadRestoresWorldAndTerrain()
    {
        var session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(3));
        session.Update(3.0);
        var path = Path.Combine(_directory, "quicksave.json");
        session.SaveTo(path);
        var saved = WorldSaveSerializer.Serialize(session.World);
        var expectedTerrain = session.Terrain;

        session.StartNewWorld(new WorldSeed(4));
        var replacements = 0;
        session.WorldReplaced += () => replacements++;
        session.LoadFrom(path);

        Assert.Equal(1, replacements);
        Assert.Equal(saved, WorldSaveSerializer.Serialize(session.World));
        WorldAssert.Equal(expectedTerrain, session.Terrain);
    }

    [Fact]
    public void GameSession_LoadUsesSavedGenerationSettings()
    {
        var session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(8));
        var path = Path.Combine(_directory, "small.json");
        session.SaveTo(path);

        var loaded = GameSession.Load(TestContent.Shipped, path);

        Assert.Equal(TestContent.SmallWorldGen.HeightmapResolution, loaded.Terrain.Resolution);
        WorldAssert.Equal(WorldGenerator.Generate(new WorldSeed(8), TestContent.SmallWorldGen).Terrain, loaded.Terrain);
    }

    [Fact]
    public void GameSession_FailedLoadKeepsCurrentWorld()
    {
        var session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(3));
        var path = Path.Combine(_directory, "broken.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"SaveVersion\": 99}");
        var world = session.World;

        Assert.Throws<SaveFormatException>(() => session.LoadFrom(path));
        Assert.Same(world, session.World);
    }

    [Fact]
    public void GameSession_StartNewWorldReplacesWorldAndResetsClock()
    {
        var session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(3));
        session.Update(0.05);
        var replaced = false;
        session.WorldReplaced += () => replaced = true;

        session.StartNewWorld(new WorldSeed(11));

        Assert.True(replaced);
        Assert.Equal(new WorldSeed(11), session.World.Seed);
        Assert.Equal(0, session.World.Tick);
        Assert.Equal(0.0, session.Scheduler.Alpha);
    }

    /// <summary>Deletes the temporary save directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
