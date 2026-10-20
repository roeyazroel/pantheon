using Godot;
using Pantheon.Game.Presentation.Nature;

namespace Pantheon.Client.Presentation.Land;

/// <summary>
/// The leaf atlas and the bark as Godot textures (ADR-073), made once from <see cref="LeafAtlas"/>'s
/// pixels with their own mipmaps, and the plant shader's material for them.
/// </summary>
public static class PlantTextures
{
    private const string ShaderPath = "res://Shaders/plant.gdshader";
    private static ImageTexture? _leaves;
    private static ImageTexture? _bark;

    /// <summary>The leaf atlas.</summary>
    public static ImageTexture Leaves => _leaves ??= Texture(LeafAtlas.Leaves(), LeafAtlas.Width, LeafAtlas.Height);

    /// <summary>The bark's patterns.</summary>
    public static ImageTexture Bark => _bark ??= Texture(LeafAtlas.Bark(), LeafAtlas.BarkSize, LeafAtlas.BarkSize);

    /// <summary>Ground noise, for tracks.</summary>
    public static ImageTexture Noise => _noise ??= Texture(LeafAtlas.Noise(), LeafAtlas.BarkSize, LeafAtlas.BarkSize);

    private static ImageTexture? _noise;

    /// <summary>A new material of the plant shader, with the atlas and the bark set.</summary>
    /// <returns>The material.</returns>
    public static ShaderMaterial Material()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("leaf_atlas", Leaves);
        material.SetShaderParameter("bark", Bark);
        return material;
    }

    /// <summary>A texture from pixels with their mipmaps.</summary>
    private static ImageTexture Texture((byte[] Rgba, int Mipmaps) pixels, int width, int height) =>
        ImageTexture.CreateFromImage(Image.CreateFromData(width, height, pixels.Mipmaps > 0, Image.Format.Rgba8, pixels.Rgba));
}
