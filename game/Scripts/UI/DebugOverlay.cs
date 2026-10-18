using Godot;
using Pantheon.Game.Diagnostics;

namespace Pantheon.Client.UI;

/// <summary>
/// Debug panel (F1) showing the lines built by <see cref="DebugReadout"/>, the region debug map
/// (F3) built by <see cref="RegionDebugMap"/>, and a badge that stays visible whenever the
/// fallback terrain renderer is active, even with the panel hidden.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Control _panel = null!;
    private Label _text = null!;
    private Label _fallbackBadge = null!;
    private Control _regionMap = null!;
    private Label _regionMapLegend = null!;
    private TextureRect _regionMapView = null!;
    private ImageTexture? _regionMapTexture;

    /// <summary>Whether the debug panel is shown.</summary>
    public bool PanelVisible
    {
        get => _panel.Visible;
        set => _panel.Visible = value;
    }

    /// <summary>Finds the panel, map and badge nodes.</summary>
    public override void _Ready()
    {
        _panel = GetNode<Control>("Panel");
        _text = GetNode<Label>("Panel/Text");
        _fallbackBadge = GetNode<Label>("FallbackBadge");
        _regionMap = GetNode<Control>("RegionMap");
        _regionMapLegend = GetNode<Label>("RegionMap/Layout/Legend");
        _regionMapView = GetNode<TextureRect>("RegionMap/Layout/Map");
    }

    /// <summary>Shows new panel content; the label is only touched when the text changed.</summary>
    /// <param name="model">Formatted overlay lines.</param>
    public void ShowModel(DebugPanelModel model)
    {
        var text = model.ToText();
        if (_text.Text != text)
        {
            _text.Text = text;
        }
    }

    /// <summary>Shows or hides the fallback renderer badge.</summary>
    /// <param name="visible">Whether the fallback renderer is active.</param>
    public void SetFallbackBadgeVisible(bool visible) => _fallbackBadge.Visible = visible;

    /// <summary>Shows the region debug map, or hides it.</summary>
    /// <param name="map">Map pixels and legend, or <c>null</c> to hide the map.</param>
    public void ShowRegionMap(RegionDebugMapImage? map)
    {
        _regionMap.Visible = map is not null;
        if (map is null)
        {
            return;
        }

        var image = Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, map.Rgba);

        // ImageTexture.Update only accepts an image of the texture's size and format.
        if (_regionMapTexture is not null && _regionMapTexture.GetWidth() == map.Width && _regionMapTexture.GetHeight() == map.Height)
        {
            _regionMapTexture.Update(image);
        }
        else
        {
            _regionMapTexture = ImageTexture.CreateFromImage(image);
            _regionMapView.Texture = _regionMapTexture;
        }

        _regionMapLegend.Text = map.Legend;
    }
}
