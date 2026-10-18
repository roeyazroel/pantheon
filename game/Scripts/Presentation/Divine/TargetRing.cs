using Godot;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>The circle a selected miracle would cover, drawn on the ground under the cursor: green when it can be cast, red when not.</summary>
internal sealed class TargetRing
{
    // The decal reaches this far above and below the ground point, so it covers slopes and roofs.
    private const float Depth = 80f;

    private static readonly Color ValidColor = new(0.55f, 1f, 0.55f, 0.95f);
    private static readonly Color InvalidColor = new(1f, 0.4f, 0.3f, 0.95f);

    private readonly Decal _decal;

    /// <summary>Creates the hidden ring under a parent.</summary>
    /// <param name="parent">Node that holds it.</param>
    public TargetRing(Node3D parent)
    {
        _decal = new Decal
        {
            Name = "TargetRing",
            TextureAlbedo = RingTexture.Texture,
            Visible = false,
            UpperFade = 0.05f,
            LowerFade = 0.05f,
        };
        parent.AddChild(_decal);
    }

    /// <summary>Shows the ring.</summary>
    /// <param name="center">Ground point at the centre.</param>
    /// <param name="radius">Radius in metres.</param>
    /// <param name="valid">Whether the miracle can be cast there.</param>
    public void Show(Vector3 center, float radius, bool valid)
    {
        _decal.Position = center;
        _decal.Size = new Vector3(radius * 2f, Depth, radius * 2f);
        _decal.Modulate = valid ? ValidColor : InvalidColor;
        _decal.Visible = true;
    }

    /// <summary>Hides the ring.</summary>
    public void Hide() => _decal.Visible = false;
}
