using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>
/// A map overlay (spec §58) stretched over the whole world as one decal, so it drapes over the
/// terrain of either renderer. The image has one pixel per region and is filtered smoothly.
/// </summary>
internal sealed class MapOverlay
{
    private const float Depth = 600f;

    private readonly Decal _decal;
    private ImageTexture? _texture;

    /// <summary>Creates the hidden overlay under a parent.</summary>
    /// <param name="parent">Node that holds it.</param>
    public MapOverlay(Node3D parent)
    {
        _decal = new Decal { Name = "MapOverlay", Visible = false, UpperFade = 0f, LowerFade = 0f, CullMask = DeclutteredLabel.GroundOnly };
        parent.AddChild(_decal);
    }

    /// <summary>Sets the world's size, which the overlay covers.</summary>
    /// <param name="worldSize">Side of the square world in metres.</param>
    public void Fit(float worldSize)
    {
        _decal.Position = new Vector3(worldSize / 2f, Depth / 4f, worldSize / 2f);
        _decal.Size = new Vector3(worldSize, Depth, worldSize);
    }

    /// <summary>Shows an overlay image, or hides the overlay.</summary>
    /// <param name="image">Overlay pixels, or <c>null</c> to hide it.</param>
    public void Show(OverlayImage? image)
    {
        _decal.Visible = image is not null;
        if (image is null)
        {
            return;
        }

        var pixels = Image.CreateFromData(image.Width, image.Height, false, Image.Format.Rgba8, image.Rgba);
        if (_texture is not null && _texture.GetWidth() == image.Width && _texture.GetHeight() == image.Height)
        {
            _texture.Update(pixels);
        }
        else
        {
            _texture = ImageTexture.CreateFromImage(pixels);
            _decal.TextureAlbedo = _texture;
        }
    }
}
