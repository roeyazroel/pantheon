using System;
using System.Runtime.InteropServices;
using Godot;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>
/// Terrain3D adapter (ADR-005). Terrain3D is a GDExtension without C# bindings, so it is driven
/// through <see cref="ClassDB"/> and dynamic <c>Set</c>/<c>Call</c>; this is the only class that
/// knows its API. The heightfield and biome colour map are imported at runtime and Terrain3D never
/// saves anything.
/// </summary>
public sealed class Terrain3DTerrainPresenter : ITerrainPresenter
{
    private const string TerrainClass = "Terrain3D";
    private const string AssetsClass = "Terrain3DAssets";
    private const string TextureAssetClass = "Terrain3DTextureAsset";

    // Terrain3D 1.0 enum values.
    private const int CollisionModeDisabled = 0;
    private const int WorldBackgroundNone = 0;

    // One region covers the default 1024-sample world; larger heightmaps are split across regions on import.
    private const int RegionSize = 1024;

    private readonly Node3D _parent;
    private readonly Camera3D _camera;

    /// <summary>Creates the presenter; nothing is built until <see cref="Build"/>.</summary>
    /// <param name="parent">Node that will hold the Terrain3D node.</param>
    /// <param name="camera">Camera that Terrain3D centres its level-of-detail rings on.</param>
    public Terrain3DTerrainPresenter(Node3D parent, Camera3D camera)
    {
        _parent = parent;
        _camera = camera;
    }

    /// <summary>Whether the Terrain3D GDExtension is loaded.</summary>
    public static bool IsAvailable => ClassDB.ClassExists(TerrainClass);

    /// <inheritdoc />
    public string BackendName { get; private set; } = TerrainClass;

    /// <inheritdoc />
    public bool IsFallback => false;

    /// <inheritdoc />
    public void Build(TerrainField field, BiomePalette palette)
    {
        Clear();
        var terrain = ClassDB.Instantiate(TerrainClass).As<Node3D>();
        terrain.Name = TerrainClass;

        // Terrain3D creates its data, material and collision objects when it enters the tree, so
        // it is configured after AddChild.
        _parent.AddChild(terrain);
        terrain.Set("collision_mode", CollisionModeDisabled);
        terrain.Call("change_region_size", RegionSize);
        terrain.Set("vertex_spacing", field.Spacing);
        terrain.Set("mesh_size", Pantheon.Game.Settings.GraphicsScaling.TerrainMeshSize(GraphicsScale.Terrain));
        terrain.Call("set_camera", _camera);
        terrain.Set("assets", CreateAssets(palette.Ground));
        var material = terrain.Get("material").AsGodotObject();
        material.Set("world_background", WorldBackgroundNone);

        // Textures meet in softer edges than Terrain3D's default (ADR-051).
        material.Call("set_shader_param", "blend_sharpness", 0.2f);
        UsePatchedShader(material);
        ImportMaps(terrain, field, palette);

        BackendName = $"{TerrainClass} {terrain.Get("version").AsString()}";
    }

    /// <inheritdoc />
    public void Clear() => _parent.FreeChildren();

    /// <inheritdoc />
    /// <remarks>Terrain3D's clipmap already draws the land near the camera in full detail and far land coarser.</remarks>
    public void Stream(Vector3 camera)
    {
    }

    /// <inheritdoc />
    public (int Detailed, int Chunks)? Detail => null;

    /// <summary>
    /// Replaces Terrain3D's shader with a copy that fixes two Terrain3D 1.0 defects
    /// (<see cref="Terrain3DShaderPatch"/>, ADR-007), or keeps the stock shader when its code is not recognised.
    /// </summary>
    /// <param name="material">The terrain's <c>Terrain3DMaterial</c>.</param>
    private static void UsePatchedShader(GodotObject material)
    {
        // Enabling the override copies the code generated for the current material settings, so
        // this must run after every setting that changes it.
        material.Call("enable_shader_override", true);
        if (material.Call("get_shader_override").As<Shader>() is not { } shader
            || Terrain3DShaderPatch.Apply(shader.Code) is not { } patched)
        {
            material.Call("enable_shader_override", false);
            GD.PushWarning("Terrain3D shader not recognised; using it unpatched, which can crash software Vulkan drivers.");
            return;
        }

        shader.Code = patched;
        material.Call("set_shader_override", shader);
    }

    /// <summary>
    /// Imports the height map, the control map that picks each sample's textures, and the biome
    /// colours as a gentle tint over them (ADR-051).
    /// </summary>
    private static void ImportMaps(Node3D terrain, TerrainField field, BiomePalette palette)
    {
        var size = field.Resolution;
        var heights = Image.CreateFromData(size, size, false, Image.Format.Rf, MemoryMarshal.AsBytes(field.Heights).ToArray());
        var control = Image.CreateFromData(size, size, false, Image.Format.Rf, MemoryMarshal.AsBytes(TerrainControl.Compute(field, palette).AsSpan()).ToArray());
        var rgba = TerrainColorizer.ComputeRgba(field, palette);
        TerrainControl.Tint(rgba, palette.Ground);
        var colors = Image.CreateFromData(size, size, false, Image.Format.Rgba8, rgba);
        var images = new Godot.Collections.Array { heights, control, colors };
        terrain.Get("data").AsGodotObject().Call("import_images", images, Vector3.Zero, 0f, 1f);
    }

    /// <summary>The ground's textures, one asset for each kind, in the order of their ids.</summary>
    private static Resource CreateAssets(GroundSettings ground)
    {
        var assets = ClassDB.Instantiate(AssetsClass).As<Resource>();
        foreach (var kind in Enum.GetValues<GroundKind>())
        {
            var maps = GroundTextures.Create(kind, ground);
            var texture = ClassDB.Instantiate(TextureAssetClass).As<Resource>();
            texture.Set("name", kind.ToString());
            texture.Set("albedo_texture", maps.Albedo);
            texture.Set("normal_texture", maps.Normal);
            texture.Set("uv_scale", ground.UvScale);
            texture.Set("detiling_rotation", kind == GroundKind.Rock ? 0f : 0.25f);
            assets.Call("set_texture", (int)kind, texture);
        }

        return assets;
    }
}
