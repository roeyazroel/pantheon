using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>Where a citizen's routine puts it at a moment, before it is placed on the terrain.</summary>
/// <param name="Ground">World (X, Z) position.</param>
/// <param name="Yaw">Facing in radians; yaw 0 faces +Z.</param>
/// <param name="Lean">Forward lean in radians while working.</param>
/// <param name="Bounce">Height of the walking bob above the ground, in metres.</param>
/// <param name="Activity">What it is doing.</param>
public readonly record struct RoutinePose(Vector2 Ground, float Yaw, float Lean, float Bounce, CitizenActivity Activity);
