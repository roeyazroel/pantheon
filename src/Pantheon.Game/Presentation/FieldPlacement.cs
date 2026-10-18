using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>Placement of one field plot.</summary>
/// <param name="Slot">Slot of the field spiral the plot stands on; it identifies the field while the village grows.</param>
/// <param name="Position">Ground position of the field centre.</param>
/// <param name="Yaw">Rotation around the vertical axis in radians.</param>
/// <param name="Normal">Terrain normal at the centre; the plot is tilted to it.</param>
/// <param name="Variant">Value in [0, 1) that picks the crop colour.</param>
public readonly record struct FieldPlacement(int Slot, Vector3 Position, float Yaw, Vector3 Normal, float Variant);
