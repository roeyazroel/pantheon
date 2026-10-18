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
    private const float GroundTextureUvScale = 0.1f;

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
        terrain.Call("set_camera", _camera);
        terrain.Set("assets", CreateAssets());
        var material = terrain.Get("material").AsGodotObject();
        material.Set("world_background", WorldBackgroundNone);
        UsePatchedShader(material);
        ImportMaps(terrain, field, palette);

        BackendName = $"{TerrainClass} {terrain.Get("version").AsString()}";
    }

    /// <inheritdoc />
    public void Clear() => _parent.FreeChildren();

    /// <summary>
    /// Replaces Terrain3D's shader with a copy whose region lookups stay in bounds
    /// (<see cref="Terrain3DShaderPatch"/>), or keeps the stock shader when its code is not recognised.
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

    /// <summary>Imports the height map and the biome colour map; there is no control map, so texture 0 covers everything.</summary>
    private static void ImportMaps(Node3D terrain, TerrainField field, BiomePalette palette)
    {
        var size = field.Resolution;
        var heights = Image.CreateFromData(size, size, false, Image.Format.Rf, MemoryMarshal.AsBytes(field.Heights).ToArray());
        var colors = Image.CreateFromData(size, size, false, Image.Format.Rgba8, TerrainColorizer.ComputeRgba(field, palette));
        var images = new Godot.Collections.Array { heights, new Variant(), colors };
        terrain.Get("data").AsGodotObject().Call("import_images", images, Vector3.Zero, 0f, 1f);
    }

    /// <summary>One ground texture asset, tinted per sample by the colour map.</summary>
    private static Resource CreateAssets()
    {
        var texture = ClassDB.Instantiate(TextureAssetClass).As<Resource>();
        texture.Set("name", "Ground");
        texture.Set("albedo_texture", GroundTexture.Create());
        texture.Set("uv_scale", GroundTextureUvScale);

        var assets = ClassDB.Instantiate(AssetsClass).As<Resource>();
        assets.Call("set_texture", 0, texture);
        return assets;
    }
}
