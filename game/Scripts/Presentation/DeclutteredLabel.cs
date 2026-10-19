using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation;

/// <summary>
/// A floating label that gives way to more important ones (spec §84, ADR-027): each frame
/// <see cref="LabelDeclutterer"/> shows it in full, by its short text, or not at all.
/// </summary>
internal sealed partial class DeclutteredLabel : Label3D
{
    /// <summary>The group every such label joins.</summary>
    public const string Group = "decluttered_labels";

    /// <summary>The render layer labels are drawn on, which decals on the ground leave alone.</summary>
    public const uint Layer = 1u << 1;

    /// <summary>The cull mask of a decal on the ground: every layer but the labels'.</summary>
    public const uint GroundOnly = ~Layer;

    private string _full = string.Empty;
    private string _short = string.Empty;
    private LabelFit _fit = LabelFit.Full;
    private Vector2? _fullSize;
    private Vector2? _shortSize;

    /// <summary>Creates a label that draws over clouds and smoke.</summary>
    public DeclutteredLabel()
    {
        RenderPriority = 20;
        OutlineRenderPriority = 19;
        Layers = Layer;
    }

    /// <summary>The larger, the sooner it takes its place on screen.</summary>
    public float Priority { get; set; }

    /// <summary>Its size in full, in the font's pixels.</summary>
    public Vector2 FullSize => _fullSize ??= Measure(_full);

    /// <summary>Its size by its short text, in the font's pixels.</summary>
    public Vector2 ShortSize => _shortSize ??= Measure(_short);

    /// <summary>Sets what it says in full and in short.</summary>
    /// <param name="full">All of it.</param>
    /// <param name="brief">Its short text.</param>
    public void SetTexts(string full, string brief)
    {
        if (full != _full)
        {
            (_full, _fullSize) = (full, null);
        }

        if (brief != _short)
        {
            (_short, _shortSize) = (brief, null);
        }

        Apply();
    }

    /// <summary>Shows as much of it as fits.</summary>
    /// <param name="fit">How much.</param>
    public void ShowFit(LabelFit fit)
    {
        if (fit != _fit)
        {
            _fit = fit;
            Apply();
        }
    }

    /// <inheritdoc />
    public override void _EnterTree() => AddToGroup(Group);

    /// <summary>Shows the text of its fit.</summary>
    private void Apply()
    {
        Visible = _fit != LabelFit.Hidden;
        var text = _fit == LabelFit.Short ? _short : _full;
        if (Text != text)
        {
            Text = text;
        }
    }

    /// <summary>A text's size in the font's pixels, with its outline.</summary>
    private Vector2 Measure(string text) =>
        (Font ?? ThemeDB.FallbackFont).GetMultilineStringSize(text, HorizontalAlignment.Center, -1f, FontSize) + new Vector2(OutlineSize, OutlineSize);
}
