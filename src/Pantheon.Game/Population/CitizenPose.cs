using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>A representative citizen as drawn this frame.</summary>
/// <param name="Position">World position of its feet.</param>
/// <param name="Yaw">Facing in radians; yaw 0 faces +Z.</param>
/// <param name="Lean">Forward lean in radians.</param>
/// <param name="Activity">What it is doing.</param>
/// <param name="Cycle">How far its motion has gone, in cycles (ADR-049).</param>
public readonly record struct CitizenPose(Vector3 Position, float Yaw, float Lean, CitizenActivity Activity, float Cycle = 0f);
