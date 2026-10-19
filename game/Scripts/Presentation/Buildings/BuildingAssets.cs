using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Buildings;

/// <summary>
/// The buildings made in code (spec §61, ADR-052): houses of every style, temples, stakes, stone
/// walls and towers from <see cref="HouseMesh"/> and <see cref="CivicMesh"/>, built on first use,
/// with the building shader that paints them. Shared by every village.
/// </summary>
internal sealed class BuildingAssets
{
    private static BuildingAssets? _shared;
    private readonly Dictionary<(HouseStyle, bool), ArrayMesh> _houses = [];
    private readonly Dictionary<StyleMotif, ArrayMesh> _temples = [];
    private ArrayMesh? _plainTemple;
    private ArrayMesh? _stake;
    private ArrayMesh? _wallSegment;
    private ArrayMesh? _timberTower;
    private ArrayMesh? _stoneTower;

    private BuildingAssets(ArchitectureSettings settings)
    {
        Settings = settings;
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/building.gdshader") };
        Material.SetShaderParameter("trim", Linear(settings.Trim));
        Material.SetShaderParameter("door", Linear(settings.Door));
        Material.SetShaderParameter("window_color", Linear(settings.Window));
        Material.SetShaderParameter("stone", Linear(settings.Stone));
    }

    /// <summary>The settings the buildings were made with.</summary>
    public ArchitectureSettings Settings { get; }

    /// <summary>The building shader's material.</summary>
    public ShaderMaterial Material { get; }

    /// <summary>The shared buildings, made on first use.</summary>
    /// <param name="settings">Architecture settings.</param>
    /// <returns>The assets.</returns>
    public static BuildingAssets For(ArchitectureSettings settings) =>
        _shared is { } shared && ReferenceEquals(shared.Settings, settings) ? shared : _shared = new BuildingAssets(settings);

    /// <summary>A house.</summary>
    /// <param name="style">Its style.</param>
    /// <param name="twoStoreys">Whether it has an upper storey.</param>
    /// <returns>The mesh.</returns>
    public ArrayMesh House(HouseStyle style, bool twoStoreys) =>
        _houses.TryGetValue((style, twoStoreys), out var mesh) ? mesh : _houses[(style, twoStoreys)] = Build(HouseMesh.Build(style, twoStoreys, detailed: true));

    /// <summary>A temple for a god's style, or a plain one.</summary>
    /// <param name="motif">The style, if strongly felt.</param>
    /// <returns>The mesh.</returns>
    public ArrayMesh Temple(StyleMotif? motif) => motif is { } style
        ? _temples.TryGetValue(style, out var mesh) ? mesh : _temples[style] = Build(CivicMesh.Temple(style, detailed: true))
        : _plainTemple ??= Build(CivicMesh.Temple(null, detailed: true));

    /// <summary>A palisade's stake.</summary>
    public ArrayMesh Stake => _stake ??= Build(CivicMesh.Stake(detailed: true));

    /// <summary>A length of stone wall.</summary>
    public ArrayMesh WallSegment => _wallSegment ??= Build(CivicMesh.WallSegment(detailed: true));

    /// <summary>A watchtower, of timber or stone.</summary>
    /// <param name="stone">Whether of stone.</param>
    /// <returns>The mesh.</returns>
    public ArrayMesh Tower(bool stone) => stone ? _stoneTower ??= Build(CivicMesh.Tower(true, detailed: true)) : _timberTower ??= Build(CivicMesh.Tower(false, detailed: true));

    /// <summary>A multimesh of buildings, each with a colour and custom data.</summary>
    /// <param name="mesh">The building.</param>
    /// <returns>The multimesh.</returns>
    public static MultiMesh NewMultiMesh(Mesh? mesh = null) => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true,
        UseCustomData = true,
        Mesh = mesh,
    };

    /// <summary>The colour an instance carries: its walls, linear.</summary>
    /// <param name="wall">The walls' colour.</param>
    /// <param name="shade">Its brightness.</param>
    /// <returns>The instance colour.</returns>
    public static Color InstanceColor(RgbColor wall, float shade = 1f) => (wall.ToGodot() * shade).SrgbToLinear() with { A = 1f };

    /// <summary>The custom data an instance carries: its roof's colour, linear, and its patterns.</summary>
    /// <param name="roof">The roof's colour.</param>
    /// <param name="walls">The walls' pattern.</param>
    /// <param name="roofPattern">The roof's pattern.</param>
    /// <param name="shade">Its brightness.</param>
    /// <returns>The custom data.</returns>
    public static Color InstanceCustom(RgbColor roof, SurfacePattern walls, SurfacePattern roofPattern, float shade = 1f)
    {
        var linear = (roof.ToGodot() * shade).SrgbToLinear();
        return new Color(linear.R, linear.G, linear.B, (int)walls + (8 * (int)roofPattern));
    }

    private static Vector3 Linear(RgbColor color)
    {
        var linear = color.ToGodot().SrgbToLinear();
        return new Vector3(linear.R, linear.G, linear.B);
    }

    /// <summary>A building's triangles as a Godot mesh.</summary>
    private ArrayMesh Build(TaggedMesh data)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Positions.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals.Select(n => new Vector3(n.X, n.Y, n.Z)).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = data.Tags.Select(t => new Vector2(t.X, t.Y)).ToArray();
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material);
        return mesh;
    }
}
