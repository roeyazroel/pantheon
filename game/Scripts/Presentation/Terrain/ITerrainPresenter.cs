using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>Renders a <see cref="TerrainField"/>. Implementations are interchangeable (ADR-005).</summary>
public interface ITerrainPresenter
{
    /// <summary>Backend name for logs and the debug overlay.</summary>
    string BackendName { get; }

    /// <summary>Whether this is the fallback renderer, which the UI must flag.</summary>
    bool IsFallback { get; }

    /// <summary>Replaces any previously built terrain with one built from the field.</summary>
    /// <param name="field">Heightfield and biomes.</param>
    /// <param name="palette">Ground colours.</param>
    void Build(TerrainField field, BiomePalette palette);

    /// <summary>Removes the built terrain.</summary>
    void Clear();

    /// <summary>Brings detail to the terrain near the camera and takes it from terrain left behind (spec §84, ADR-050).</summary>
    /// <param name="camera">The camera's place.</param>
    void Stream(Godot.Vector3 camera);

    /// <summary>Chunks in full detail and all chunks, or <c>null</c> when the renderer brings its own detail by distance.</summary>
    (int Detailed, int Chunks)? Detail { get; }
}
