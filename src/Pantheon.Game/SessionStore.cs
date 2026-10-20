using System;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game;

/// <summary>Reads saved worlds for <see cref="GameSession"/>, with the terrain they were played on.</summary>
internal static class SessionStore
{
    /// <summary>
    /// Reads a save and regenerates its terrain from the saved seed and generation settings. Gods that
    /// connected players played go back to the AI, and a world from before holy sites finds its own (ADR-021).
    /// </summary>
    internal static (WorldState World, TerrainField Terrain) LoadWorld(GameContent content, string path) =>
        Prepare(content, WorldSaveSerializer.LoadFromFile(path));

    /// <summary>
    /// Readies a world read from a save, wherever it was kept (a file or the profile, ADR-060): its
    /// terrain is made again from its seed and generation settings, and remote gods go back to the AI.
    /// </summary>
    internal static (WorldState World, TerrainField Terrain) Prepare(GameContent content, WorldState world)
    {
        Simulation.HolySites.HolySitePlacer.EnsurePlaced(content.HolySites, world);
        foreach (var god in world.Gods)
        {
            if (god.Controller == GodController.Remote)
            {
                god.Controller = GodController.Ai;
            }
        }

        try
        {
            // The saved settings, not the current content files, define the terrain, so a save
            // always reproduces the terrain it was played on.
            return (world, TerrainGenerator.Generate(world.Seed, world.Generation, world.Land.Reshapes));
        }
        catch (ArgumentException exception)
        {
            throw new SaveFormatException($"The save's generation settings are invalid: {exception.Message}", exception);
        }
    }
}
