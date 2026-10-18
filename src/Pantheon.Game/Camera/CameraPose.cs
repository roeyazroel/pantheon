using System.Numerics;

namespace Pantheon.Game.Camera;

/// <summary>Camera placement in world space (Y up).</summary>
/// <param name="Position">Camera position.</param>
/// <param name="Target">Point the camera looks at.</param>
public readonly record struct CameraPose(Vector3 Position, Vector3 Target);
