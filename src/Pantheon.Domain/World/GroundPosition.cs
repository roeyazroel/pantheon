namespace Pantheon.Domain.World;

/// <summary>
/// Horizontal position on the world ground plane, in metres. The height is not stored because it
/// is always derived from the terrain.
/// </summary>
/// <param name="X">World X coordinate in metres.</param>
/// <param name="Z">World Z coordinate in metres.</param>
public readonly record struct GroundPosition(float X, float Z);
