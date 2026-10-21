using System;
using Godot;
using Pantheon.Client.Presentation.Angel;
using Pantheon.Client.UI.Flow;
using Pantheon.Game.Angel;

namespace Pantheon.Client.UI;

/// <summary>
/// The angel beside the god's hand (ADR-074): its figure, and a card with its name and what its god's
/// deeds have made it, its words told out letter by letter, the deeds it urges (each shows the god
/// where), Thank you, and Ask, to ask it something. It follows the hand loosely, holds still while the
/// pointer is on it, and leaves when its words have been read. Beside the tutorial's card it stands
/// alone, the tutorial speaking for it.
/// </summary>
internal sealed partial class AngelCard : CanvasLayer
{
    // The figure's size on the screen, and the gap between it and the hand.
    private const float FigureSize = 290f;
    private const float Gap = 26f;

    // Letters told a second, and how fast it comes and goes.
    private const float Letters = 75f;
    private const float Fade = 2.6f;

    private AngelFigure _figure = null!;
    private TextureRect _picture = null!;
    private PanelContainer _card = null!;
    private StyleBoxFlat _style = null!;
    private Label _name = null!;
    private Label _stage = null!;
    private Label _words = null!;
    private HFlowContainer _chips = null!;
    private HBoxContainer _askRow = null!;
    private LineEdit _ask = null!;
    private AngelWords? _shown;
    private Vector2 _at;
    private bool _placed;
    private bool _thinking;
    private double _left;
    private float _reveal;
    private float _appear;

    /// <summary>The player chose a deed the angel urged: show the god where.</summary>
    public event Action<AngelSuggestion>? SuggestionChosen;

    /// <summary>The player asked the angel something.</summary>
    public event Action<string>? Asked;

    /// <summary>A rectangle on the screen the figure stands beside without its card (the tutorial's), or none.</summary>
    public Rect2? Beside { get; set; }

    /// <summary>Whether the card is up: words, thinking, or a question being written.</summary>
    public bool Open => _shown is not null || _thinking || _askRow.Visible;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 6;
        _figure = AngelFigure.Create((int)(FigureSize * Mathf.Max(1f, DisplayServer.ScreenGetScale())));
        AddChild(_figure);
        _picture = new TextureRect
        {
            Texture = _figure.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(FigureSize, FigureSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        AddChild(_picture);
        _style = UiTheme.Panel(new Color(0.04f, 0.05f, 0.08f, 0.92f), UiTheme.Gold, 2, 14, margin: 16);
        _card = new PanelContainer { CustomMinimumSize = new Vector2(380f, 0f), Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _card.AddThemeStyleboxOverride("panel", _style);
        AddChild(_card);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        _card.AddChild(column);
        _name = UiTheme.Label(column, 22, UiTheme.Gold, UiTheme.Title);
        _stage = UiTheme.Label(column, UiTheme.CaptionSize, UiTheme.Muted, UiTheme.Italic);
        _words = UiTheme.Label(column, UiTheme.BodySize, UiTheme.Ink);
        _words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _words.CustomMinimumSize = new Vector2(350f, 0f);
        _chips = new HFlowContainer();
        _chips.AddThemeConstantOverride("h_separation", 6);
        _chips.AddThemeConstantOverride("v_separation", 6);
        column.AddChild(_chips);
        var buttons = new HBoxContainer();
        column.AddChild(buttons);
        var thanks = UiKit.Button("Thank you", Close);
        thanks.Flat = true;
        buttons.AddChild(thanks);
        var ask = UiKit.Button("Ask…", ToggleAsk);
        ask.Flat = true;
        buttons.AddChild(ask);
        _askRow = new HBoxContainer { Visible = false };
        column.AddChild(_askRow);
        _ask = UiKit.Field(string.Empty, "Ask your angel…");
        _ask.CustomMinimumSize = new Vector2(270f, 36f);
        _ask.TextSubmitted += Send;
        _askRow.AddChild(_ask);
        _askRow.AddChild(UiKit.Button("Ask", () => Send(_ask.Text)));
    }

    /// <summary>Shows the angel's words.</summary>
    /// <param name="words">Its words.</param>
    /// <param name="stage">What its god's deeds have made it.</param>
    public void Say(AngelWords words, AngelStageText stage)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(stage);
        (_shown, _thinking, _left, _reveal) = (words, false, words.ShowSeconds, 0f);
        Header(words.Name, stage, words.Look);
        _words.Text = "“" + words.Text + "”";
        _words.VisibleCharacters = 0;
        UiKit.Clear(_chips);
        foreach (var suggestion in words.Suggestions)
        {
            var chip = UiKit.Button("▸ " + suggestion.Text, () => SuggestionChosen?.Invoke(suggestion));
            chip.TooltipText = "Show me";
            chip.CustomMinimumSize = new Vector2(0f, 34f);
            _chips.AddChild(chip);
        }
    }

    /// <summary>Shows the angel listening while its model thinks of an answer.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="stage">What its god's deeds have made it.</param>
    /// <param name="look">How it looks.</param>
    public void Listen(string name, AngelStageText stage, AngelLook look)
    {
        ArgumentNullException.ThrowIfNull(stage);
        (_thinking, _shown, _reveal) = (true, null, 0f);
        Header(name, stage, look);
        _words.Text = "…";
        _words.VisibleCharacters = -1;
        UiKit.Clear(_chips);
    }

    /// <summary>Sends the angel away.</summary>
    public void Close()
    {
        (_shown, _thinking) = (null, false);
        _askRow.Visible = false;
        _ask.ReleaseFocus();
    }

    /// <summary>Moves the figure and its card beside the hand, tells out its words, and lets it come and go.</summary>
    /// <param name="hand">The god's hand on the screen.</param>
    /// <param name="look">How the angel looks now.</param>
    /// <param name="delta">Real seconds since the last frame.</param>
    public void Frame(Vector2 hand, AngelLook look, double delta)
    {
        var seconds = (float)delta;
        var screen = _picture.GetViewportRect().Size;
        var held = _card.Visible && _card.GetGlobalRect().Grow(12f).HasPoint(_card.GetGlobalMousePosition());
        if (_shown is not null && !held && !_askRow.Visible && (_left -= delta) <= 0)
        {
            Close();
        }

        var wanted = Open || Beside is not null;
        _appear = Mathf.MoveToward(_appear, wanted ? 1f : 0f, Fade * seconds);
        _figure.Appear = Mathf.SmoothStep(0f, 1f, _appear);
        _figure.Show(look, seconds);
        _picture.Visible = _appear > 0.01f;
        _figure.RenderTargetUpdateMode = _picture.Visible ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _card.Visible = Open && Beside is null;
        _card.Modulate = Colors.White with { A = Mathf.Clamp(_appear * 1.4f, 0f, 1f) };
        if (_shown is not null && _reveal < _words.Text.Length)
        {
            _reveal += Letters * seconds;
            _words.VisibleCharacters = _reveal >= _words.Text.Length ? -1 : (int)_reveal;
        }

        if (!held)
        {
            Place(hand, screen, seconds);
        }
    }

    /// <summary>Sets the card's heading and its edge in the angel's colour.</summary>
    private void Header(string name, AngelStageText stage, AngelLook look)
    {
        var light = AngelFigure.Light(look.Lean);
        _name.Text = name + ", your angel";
        _name.AddThemeColorOverride("font_color", light.Lerp(UiTheme.Gold, 0.35f));
        _stage.Text = stage.Name + " · " + stage.Note;
        _style.BorderColor = light with { A = 0.85f };
    }

    /// <summary>Eases the figure beside the hand (or the tutorial's card), and the card beside it, on the screen.</summary>
    private void Place(Vector2 hand, Vector2 screen, float seconds)
    {
        Vector2 target;
        if (Beside is { } other)
        {
            target = new Vector2(other.Position.X - (FigureSize * 0.92f), other.Position.Y + (other.Size.Y * 0.5f) - (FigureSize * 0.55f));
            _figure.Facing = 1f;
        }
        else
        {
            var right = hand.X + Gap + FigureSize < screen.X - 16f;
            target = new Vector2(right ? hand.X + Gap : hand.X - Gap - FigureSize, hand.Y - (FigureSize * 0.78f));
            _figure.Facing = right ? -1f : 1f;
        }

        target = new Vector2(Mathf.Clamp(target.X, 8f, screen.X - FigureSize - 8f), Mathf.Clamp(target.Y, 70f, screen.Y - FigureSize - 90f));
        _at = _placed ? _at.Lerp(target, 1f - MathF.Exp(-4f * seconds)) : target + new Vector2(0f, -60f);
        _placed = _appear > 0.01f;
        _picture.Position = _at;
        _picture.Size = new Vector2(FigureSize, FigureSize);
        var size = _card.GetCombinedMinimumSize();
        var onLeft = _figure.Facing < 0f;
        var x = onLeft ? _at.X - size.X + (FigureSize * 0.12f) : _at.X + (FigureSize * 0.88f);
        var y = Mathf.Min(_at.Y + 14f, hand.Y - size.Y - 34f);
        _card.Position = new Vector2(Mathf.Clamp(x, 8f, screen.X - size.X - 8f), Mathf.Clamp(y, 64f, screen.Y - size.Y - 80f));
        _card.Size = size;
    }

    /// <summary>Opens or closes the question's field.</summary>
    private void ToggleAsk()
    {
        _askRow.Visible = !_askRow.Visible;
        if (_askRow.Visible)
        {
            _ask.GrabFocus();
        }
    }

    /// <summary>Sends a question.</summary>
    private void Send(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _ask.Text = string.Empty;
        _askRow.Visible = false;
        _ask.ReleaseFocus();
        Asked?.Invoke(text.Trim());
    }
}
