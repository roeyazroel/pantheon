using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>Placement of one hut.</summary>
/// <param name="Position">Ground position of the hut centre.</param>
/// <param name="Yaw">Rotation around the vertical axis in radians. The hut's local +Z axis (its door side) faces the shrine.</param>
/// <param name="Scale">Uniform size multiplier.</param>
public readonly record struct HutPlacement(Vector3 Position, float Yaw, float Scale);
