using System;

namespace Pantheon.Game.Presentation;

/// <summary>World streaming (spec §84, ADR-050), from <c>settlement_view.json</c>: how near the camera the land carries its detail. Presentation only.</summary>
public sealed record StreamingSettings
{
    /// <summary>How near the camera a chunk of forest is planted, in metres.</summary>
    public required float ForestLoadRadius { get; init; }

    /// <summary>How far from the camera a chunk of forest is cleared again.</summary>
    public required float ForestUnloadRadius { get; init; }

    /// <summary>The most chunks of forest planted a frame once the world is shown.</summary>
    public required int ForestChunksPerFrame { get; init; }

    /// <summary>How near the camera the fallback terrain is drawn in full detail, in metres.</summary>
    public required float TerrainDetailRadius { get; init; }

    /// <summary>How far from the camera detailed terrain goes back to the coarse.</summary>
    public required float TerrainCoarseRadius { get; init; }

    /// <summary>The most terrain chunks made detailed a frame.</summary>
    public required int TerrainChunksPerFrame { get; init; }

    /// <summary>How near the camera fires are drawn, in metres; beyond it they are only simulation data.</summary>
    public required float EffectRadius { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(ForestLoadRadius > 0f) || !(ForestUnloadRadius >= ForestLoadRadius) || ForestChunksPerFrame < 1
            || !(TerrainDetailRadius >= 0f) || !(TerrainCoarseRadius >= TerrainDetailRadius) || TerrainChunksPerFrame < 1 || !(EffectRadius > 0f))
        {
            throw new ArgumentException("Streaming needs positive radii with each unload radius at least its load radius, at least one chunk a frame, and a positive effect radius.");
        }
    }
}
