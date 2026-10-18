using System;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Water;

/// <summary>
/// Sea surface at height 0, plus a dark sea floor beyond the terrain, both reaching the horizon.
/// The water shader tints the surface from a baked depth map (<see cref="WaterDepthMap"/>).
/// </summary>
public sealed class WaterPresenter
{
    private const string WaterMaterialPath = "res://Materials/water.tres";
    private const string SeaFloorMaterialPath = "res://Materials/sea_floor.tres";
    private const int DepthMapResolution = 512;

    // The planes reach far past the island so the sea meets the sky at the horizon.
    private const float HorizonMargin = 7000f;

    // The floor sits below the deepest terrain sample so it never shows through the sea bed.
    private const float SeaFloorClearance = 2f;

    private readonly Node3D _parent;
    private readonly ShaderMaterial _water;
    private readonly Material _seaFloor;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the water planes.</param>
    public WaterPresenter(Node3D parent)
    {
        _parent = parent;
        _water = GD.Load<ShaderMaterial>(WaterMaterialPath);
        _seaFloor = GD.Load<Material>(SeaFloorMaterialPath);
    }

    /// <summary>Replaces the water with planes and a depth map for the given terrain.</summary>
    /// <param name="field">Terrain the sea surrounds.</param>
    /// <param name="palette">Water colours and depth scale.</param>
    public void Build(TerrainField field, BiomePalette palette)
    {
        _parent.FreeChildren();
        ConfigureWaterMaterial(field, palette);

        var size = field.WorldSize + (2f * HorizonMargin);
        var center = new Vector3(field.WorldSize * 0.5f, 0f, field.WorldSize * 0.5f);
        var floorHeight = LowestHeight(field) - SeaFloorClearance;
        _parent.AddChild(CreatePlane("SeaSurface", size, center, _water));
        _parent.AddChild(CreatePlane("SeaFloor", size, center with { Y = floorHeight }, _seaFloor));
    }

    /// <summary>Uploads the depth map and copies the palette's water colours into the shader.</summary>
    private void ConfigureWaterMaterial(TerrainField field, BiomePalette palette)
    {
        var depths = WaterDepthMap.Compute(field, DepthMapResolution, palette.DeepWaterDepth);
        var image = Image.CreateFromData(DepthMapResolution, DepthMapResolution, false, Image.Format.L8, depths);
        _water.SetShaderParameter("depth_map", ImageTexture.CreateFromImage(image));
        _water.SetShaderParameter("map_size", field.WorldSize);
        _water.SetShaderParameter("shallow_color", palette.ShallowWater.ToGodot());
        _water.SetShaderParameter("deep_color", palette.DeepWater.ToGodot());
    }

    /// <summary>Square horizontal plane that casts no shadow.</summary>
    private static MeshInstance3D CreatePlane(string name, float size, Vector3 center, Material material) => new()
    {
        Name = name,
        Mesh = new PlaneMesh { Size = new Vector2(size, size) },
        MaterialOverride = material,
        Position = center,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    /// <summary>Lowest terrain height in metres.</summary>
    private static float LowestHeight(TerrainField field)
    {
        var lowest = float.MaxValue;
        foreach (var height in field.Heights)
        {
            lowest = MathF.Min(lowest, height);
        }

        return lowest;
    }
}
