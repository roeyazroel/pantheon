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
}
