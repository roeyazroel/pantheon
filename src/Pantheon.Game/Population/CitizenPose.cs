using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>A representative citizen as drawn this frame.</summary>
/// <param name="Position">World position of its feet.</param>
/// <param name="Yaw">Facing in radians; yaw 0 faces +Z.</param>
/// <param name="Lean">Forward lean in radians.</param>
/// <param name="Activity">What it is doing.</param>
public readonly record struct CitizenPose(Vector3 Position, float Yaw, float Lean, CitizenActivity Activity);
