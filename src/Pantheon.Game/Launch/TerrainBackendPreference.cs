namespace Pantheon.Game.Launch;

/// <summary>Which terrain renderer the client should use.</summary>
public enum TerrainBackendPreference
{
    /// <summary>Terrain3D when its GDExtension is loaded, otherwise the fallback mesh.</summary>
    Auto,

    /// <summary>Terrain3D. If it is unavailable the client falls back and reports it.</summary>
    Terrain3D,

    /// <summary>Always the fallback mesh renderer.</summary>
    Fallback,
}
