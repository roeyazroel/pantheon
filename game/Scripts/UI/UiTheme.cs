using Godot;

namespace Pantheon.Client.UI;

/// <summary>
/// The game's look for every control (ADR-059): Alegreya Sans for text and Cinzel for titles, deep
/// slate panels with a gilded edge, gold for what matters. <see cref="Apply"/> writes it into the
/// engine's default theme before any interface is built, so every panel, button, slider and
/// tooltip shares it without overrides of its own.
/// </summary>
internal static class UiTheme
{
    /// <summary>Body text, at the design size (the window's stretch scales it).</summary>
    public const int BodySize = 18;

    /// <summary>Secondary text.</summary>
    public const int SmallSize = 16;

    /// <summary>The smallest text: captions and keys.</summary>
    public const int CaptionSize = 14;

    /// <summary>Panel titles.</summary>
    public const int TitleSize = 24;

    /// <summary>Body text.</summary>
    public static readonly Color Ink = new(0.96f, 0.93f, 0.85f);

    /// <summary>Titles and what matters.</summary>
    public static readonly Color Gold = new(1f, 0.83f, 0.42f);

    /// <summary>Secondary text.</summary>
    public static readonly Color Muted = new(0.7f, 0.68f, 0.62f);

    /// <summary>Good news.</summary>
    public static readonly Color Good = new(0.56f, 0.9f, 0.56f);

    /// <summary>Bad news.</summary>
    public static readonly Color Bad = new(1f, 0.52f, 0.43f);

    /// <summary>A warning.</summary>
    public static readonly Color Warning = new(1f, 0.76f, 0.4f);

    /// <summary>Calm information: bonds, water.</summary>
    public static readonly Color Calm = new(0.62f, 0.8f, 1f);

    /// <summary>A panel's background.</summary>
    public static readonly Color PanelBack = new(0.06f, 0.07f, 0.1f, 0.88f);

    /// <summary>A panel's gilded edge.</summary>
    public static readonly Color Edge = new(0.7f, 0.56f, 0.3f, 0.85f);

    private static Font? _body;
    private static Font? _strong;
    private static Font? _italic;
    private static Font? _title;

    /// <summary>The text font.</summary>
    public static Font Body => _body ??= Load("res://Fonts/AlegreyaSans-Medium.ttf");

    /// <summary>The bold text font.</summary>
    public static Font Strong => _strong ??= Load("res://Fonts/AlegreyaSans-Bold.ttf");

    /// <summary>The italic text font.</summary>
    public static Font Italic => _italic ??= Load("res://Fonts/AlegreyaSans-Italic.ttf");

    /// <summary>The titles' font: Cinzel, a Roman capital, at a semibold weight.</summary>
    public static Font Title => _title ??= Weighted(Load("res://Fonts/Cinzel-Variable.ttf"), 650);

    /// <summary>Writes the look into the engine's default theme. Call once, before any interface is built.</summary>
    public static void Apply()
    {
        var theme = ThemeDB.GetDefaultTheme();
        theme.DefaultFont = Body;
        theme.DefaultFontSize = BodySize;
        ThemeDB.FallbackFont = Body;
        ThemeDB.FallbackFontSize = BodySize;

        var panel = Panel(PanelBack, Edge, 1, 10);
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panel);
        theme.SetStylebox("panel", "PopupPanel", Panel(PanelBack with { A = 0.97f }, Edge, 1, 8));
        theme.SetStylebox("panel", "TooltipPanel", Panel(new Color(0.04f, 0.05f, 0.07f, 0.96f), Edge, 1, 6, margin: 10));
        theme.SetFont("font", "TooltipLabel", Body);
        theme.SetFontSize("font_size", "TooltipLabel", SmallSize);
        theme.SetColor("font_color", "TooltipLabel", Ink);
        theme.SetColor("font_color", "Label", Ink);

        StyleButtons(theme, "Button");
        StyleButtons(theme, "OptionButton");
        StyleButtons(theme, "MenuButton");
        theme.SetStylebox("normal", "LineEdit", Panel(new Color(0.03f, 0.04f, 0.06f, 0.9f), Edge with { A = 0.5f }, 1, 6, margin: 8));
        theme.SetStylebox("focus", "LineEdit", Panel(new Color(0f, 0f, 0f, 0f), Gold, 2, 6, margin: 8));
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetStylebox("panel", "PopupMenu", Panel(PanelBack with { A = 0.97f }, Edge, 1, 6, margin: 6));
        theme.SetStylebox("hover", "PopupMenu", Panel(new Color(0.3f, 0.24f, 0.1f, 0.9f), Gold, 0, 4, margin: 6));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Gold);
        theme.SetStylebox("slider", "HSlider", Panel(new Color(0.02f, 0.02f, 0.03f, 0.9f), Edge with { A = 0.4f }, 1, 3, margin: 3));
        theme.SetStylebox("grabber_area", "HSlider", Panel(Gold with { A = 0.75f }, Gold, 0, 3, margin: 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Panel(Gold, Gold, 0, 3, margin: 3));
        theme.SetStylebox("background", "ProgressBar", Panel(new Color(0f, 0f, 0f, 0.55f), Edge with { A = 0.35f }, 1, 4, margin: 0));
        theme.SetStylebox("fill", "ProgressBar", Panel(Gold, Gold, 0, 4, margin: 0));
        theme.SetColor("font_color", "CheckBox", Ink);
        theme.SetColor("font_hover_color", "CheckBox", Gold);
        theme.SetStylebox("panel", "TabContainer", Panel(new Color(0.04f, 0.05f, 0.07f, 0.7f), Edge with { A = 0.4f }, 1, 8, margin: 6));
        theme.SetStylebox("tab_selected", "TabContainer", Panel(new Color(0.3f, 0.23f, 0.09f, 0.95f), Gold, 1, 6, margin: 8));
        theme.SetStylebox("tab_unselected", "TabContainer", Panel(new Color(0.08f, 0.09f, 0.12f, 0.9f), Edge with { A = 0.35f }, 1, 6, margin: 8));
        theme.SetStylebox("tab_hovered", "TabContainer", Panel(new Color(0.14f, 0.13f, 0.12f, 0.95f), Gold with { A = 0.7f }, 1, 6, margin: 8));
        theme.SetColor("font_selected_color", "TabContainer", Gold);
        theme.SetColor("font_unselected_color", "TabContainer", Muted);
        theme.SetColor("font_hovered_color", "TabContainer", Ink);
        theme.SetConstant("separation", "VBoxContainer", 6);
        theme.SetConstant("separation", "HBoxContainer", 8);
    }

    /// <summary>A rounded panel style.</summary>
    /// <param name="background">The fill.</param>
    /// <param name="border">The edge's colour.</param>
    /// <param name="borderWidth">The edge's width.</param>
    /// <param name="radius">The corners' radius.</param>
    /// <param name="margin">The inner margin on every side; by default wider than tall.</param>
    /// <returns>The style.</returns>
    public static StyleBoxFlat Panel(Color background, Color border, int borderWidth, int radius, float? margin = null)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            ContentMarginLeft = margin ?? 16,
            ContentMarginRight = margin ?? 16,
            ContentMarginTop = margin ?? 10,
            ContentMarginBottom = margin ?? 10,
            AntiAliasing = true,
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    /// <summary>A label in the theme's fonts.</summary>
    /// <param name="parent">Where it goes, if anywhere.</param>
    /// <param name="size">Its font size.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="font">Its font, or the body font.</param>
    /// <returns>The label, ignoring the mouse.</returns>
    public static Label Label(Control? parent, int size, Color color, Font? font = null)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (font is not null)
        {
            label.AddThemeFontOverride("font", font);
        }

        parent?.AddChild(label);
        return label;
    }

    /// <summary>The buttons' states: slate, a gilded edge when hovered, gold when pressed.</summary>
    private static void StyleButtons(Theme theme, string type)
    {
        theme.SetStylebox("normal", type, Panel(new Color(0.1f, 0.11f, 0.15f, 0.92f), Edge with { A = 0.55f }, 1, 8, margin: 10));
        theme.SetStylebox("hover", type, Panel(new Color(0.16f, 0.15f, 0.14f, 0.95f), Gold, 1, 8, margin: 10));
        theme.SetStylebox("pressed", type, Panel(new Color(0.38f, 0.29f, 0.1f, 0.95f), Gold, 2, 8, margin: 10));
        theme.SetStylebox("disabled", type, Panel(new Color(0.08f, 0.08f, 0.1f, 0.7f), Edge with { A = 0.2f }, 1, 8, margin: 10));
        theme.SetStylebox("focus", type, new StyleBoxEmpty());
        theme.SetColor("font_color", type, Ink);
        theme.SetColor("font_hover_color", type, Gold);
        theme.SetColor("font_pressed_color", type, new Color(1f, 0.95f, 0.8f));
        theme.SetColor("font_disabled_color", type, Muted with { A = 0.6f });
        theme.SetFontSize("font_size", type, BodySize);
    }

    /// <summary>Loads a font, falling back to the engine's when it is missing.</summary>
    private static Font Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Font>(path) : ThemeDB.FallbackFont;

    /// <summary>A variable font at a weight.</summary>
    private static Font Weighted(Font font, int weight)
    {
        var variation = new FontVariation { BaseFont = font };
        var tags = new Godot.Collections.Dictionary { [TextServerManager.GetPrimaryInterface().NameToTag("weight")] = weight };
        variation.VariationOpentype = tags;
        return variation;
    }
}
