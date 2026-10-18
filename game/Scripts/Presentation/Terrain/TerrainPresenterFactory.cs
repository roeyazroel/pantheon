using Godot;
using Pantheon.Game.Launch;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>Chooses the terrain renderer: Terrain3D when it is loaded, otherwise the clearly flagged fallback.</summary>
public static class TerrainPresenterFactory
{
    /// <summary>Creates the terrain presenter for a backend preference.</summary>
    /// <param name="preference">Preference from <c>--terrain</c>.</param>
    /// <param name="parent">Node that will hold the terrain.</param>
    /// <param name="camera">Rendering camera (Terrain3D centres its level of detail on it).</param>
    /// <returns>The presenter.</returns>
    public static ITerrainPresenter Create(TerrainBackendPreference preference, Node3D parent, Camera3D camera)
    {
        var available = Terrain3DTerrainPresenter.IsAvailable;
        var useTerrain3D = preference switch
        {
            TerrainBackendPreference.Auto => available,
            TerrainBackendPreference.Terrain3D => available,
            TerrainBackendPreference.Fallback => false,
        };

        if (useTerrain3D)
        {
            return new Terrain3DTerrainPresenter(parent, camera);
        }

        if (preference == TerrainBackendPreference.Fallback)
        {
            GD.Print("Terrain: using the fallback mesh renderer (--terrain=fallback).");
            return new FallbackMeshTerrainPresenter(parent, "forced by --terrain=fallback");
        }

        GD.PushWarning(
            "Terrain: the Terrain3D GDExtension is not loaded, so the FALLBACK mesh renderer is used. " +
            "Open the project in the Godot editor once, or run 'godot --headless --path game --import', to register the extension.");
        return new FallbackMeshTerrainPresenter(parent, "Terrain3D unavailable");
    }
}
