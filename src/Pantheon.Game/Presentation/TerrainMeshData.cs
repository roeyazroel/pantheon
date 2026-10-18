using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>Engine-agnostic triangle mesh for one fallback terrain chunk.</summary>
/// <param name="Positions">Vertex positions in world metres.</param>
/// <param name="Normals">Unit vertex normals.</param>
/// <param name="Colors">sRGB vertex colours (RGBA, 0..1).</param>
/// <param name="Indices">Triangle list. Front faces wind clockwise when seen from above, as Godot expects.</param>
public sealed record TerrainMeshData(Vector3[] Positions, Vector3[] Normals, Vector4[] Colors, int[] Indices);
