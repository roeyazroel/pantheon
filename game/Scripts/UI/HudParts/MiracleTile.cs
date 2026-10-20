using System;
using System.Globalization;
using Godot;
using Pantheon.Game.Hud;

namespace Pantheon.Client.UI;

/// <summary>
/// One miracle on the bar (ADR-059): its glyph on a tile tinted by its colour, the key that selects
/// it, its cost, a sweep while it cools down, a lock while the god's rank is too low, and a gilded
/// ring when it is aimed. Its name is beneath, and its description in the tooltip.
/// </summary>
internal sealed partial class MiracleTile : VBoxContainer
{
    /// <summary>The tile's side, at the design size.</summary>
    public const float Side = 64f;

    private readonly Face _face = new();
    private readonly Label _name;

    /// <summary>Builds an empty tile.</summary>
    public MiracleTile()
    {
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeConstantOverride("separation", 2);
        _face.CustomMinimumSize = new Vector2(Side, Side);
        _face.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(_face);
        _name = UiTheme.Label(this, UiTheme.CaptionSize, UiTheme.Ink);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.CustomMinimumSize = new Vector2(Side + 16f, 0f);
        _name.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _name.AddThemeConstantOverride("outline_size", 4);
        _face.GuiInput += OnInput;
    }

    /// <summary>The player clicked the tile.</summary>
    public event Action? Clicked;

    /// <summary>Shows a slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="glyph">Its picture.</param>
    /// <param name="tint">Its colour.</param>
    /// <param name="cooldownTotal">Its whole cooldown in seconds, for the sweep.</param>
    public void Show(MiracleSlot slot, MiracleGlyph glyph, Color tint, double cooldownTotal)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (_name.Text != slot.Name)
        {
            _name.Text = slot.Name;
        }

        if (_face.TooltipText != slot.Tooltip)
        {
            _face.TooltipText = slot.Tooltip;
        }

        var key = slot.Hotkey switch
        {
            0 => string.Empty,
            > 10 => "⇧" + (slot.Hotkey - 10).ToString(CultureInfo.InvariantCulture),
            _ => (slot.Hotkey % 10).ToString(CultureInfo.InvariantCulture),
        };
        var cost = slot.CooldownSeconds > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{slot.CooldownSeconds:0}s")
            : string.Create(CultureInfo.InvariantCulture, $"{slot.BaseCost:0}");
        var usable = !slot.Locked && slot.CooldownSeconds <= 0 && slot.Affordable;
        var cooling = cooldownTotal > 0 ? (float)Math.Clamp(slot.CooldownSeconds / cooldownTotal, 0, 1) : slot.CooldownSeconds > 0 ? 1f : 0f;
        var state = new FaceState(glyph, tint, key, cost, slot.Selected, usable, slot.Locked, slot.Affordable, cooling);
        if (state != _face.State)
        {
            _face.State = state;
            _face.QueueRedraw();
            _name.Modulate = usable || slot.Selected ? Colors.White : new Color(1f, 1f, 1f, 0.6f);
        }
    }

    /// <summary>A left click selects the miracle, or puts it down again.</summary>
    private void OnInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Clicked?.Invoke();
            _face.AcceptEvent();
        }
    }

    /// <summary>What the tile shows; it is drawn again only when this changes.</summary>
    private sealed record FaceState(MiracleGlyph Glyph, Color Tint, string Key, string Cost, bool Selected, bool Usable, bool Locked, bool Affordable, float Cooling);

    /// <summary>The square face, drawn.</summary>
    private sealed partial class Face : Control
    {
        public FaceState? State { get; set; }

        public Face() => MouseFilter = MouseFilterEnum.Stop;

        public override void _Draw()
        {
            if (State is not { } state)
            {
                return;
            }

            var rect = new Rect2(Vector2.Zero, Size);
            var back = state.Selected ? state.Tint.Darkened(0.55f) with { A = 0.95f } : new Color(0.08f, 0.09f, 0.12f, 0.9f);
            var edge = state.Selected ? UiTheme.Gold : state.Usable ? state.Tint.Darkened(0.2f) : UiTheme.Edge with { A = 0.4f };
            DrawStyleBox(UiTheme.Panel(back, edge, state.Selected ? 3 : 1, 12, margin: 0), rect);

            // A soft glow of the miracle's colour behind its glyph.
            var centre = rect.GetCenter() + new Vector2(0f, -3f);
            DrawCircle(centre, Size.X * 0.34f, state.Tint with { A = state.Usable ? 0.22f : 0.08f });
            var glyphColor = state.Usable || state.Selected ? state.Tint.Lightened(0.15f) : state.Tint.Lerp(new Color(0.5f, 0.5f, 0.5f), 0.65f);
            MiracleGlyphs.Draw(this, state.Glyph, centre, Size.X * 0.5f, glyphColor);

            if (state.Cooling > 0f)
            {
                DrawSweep(rect.GetCenter(), Size.X * 0.48f, state.Cooling);
            }

            if (state.Locked)
            {
                DrawRect(rect, new Color(0f, 0f, 0f, 0.45f));
                DrawLock(rect.GetCenter(), Size.X * 0.2f);
            }

            var font = UiTheme.Strong;
            if (state.Key.Length > 0)
            {
                var badge = new Vector2(11f, 11f);
                DrawCircle(badge, 9.5f, new Color(0f, 0f, 0f, 0.7f));
                DrawArc(badge, 9.5f, 0f, MathF.Tau, 20, UiTheme.Edge, 1f, true);
                DrawString(font, badge + new Vector2(-9.5f, 5f), state.Key, HorizontalAlignment.Center, 19f, 13, UiTheme.Gold);
            }

            var costColor = state.Locked ? UiTheme.Muted : state.Affordable ? UiTheme.Ink : UiTheme.Bad;
            DrawString(font, new Vector2(0f, Size.Y - 4f), state.Cost, HorizontalAlignment.Right, Size.X - 5f, 13, costColor);
        }

        /// <summary>A dark sweep over the share still to cool, clockwise from the top.</summary>
        private void DrawSweep(Vector2 centre, float radius, float share)
        {
            var points = new System.Collections.Generic.List<Vector2> { centre };
            var steps = Math.Max(3, (int)(share * 32));
            for (var i = 0; i <= steps; i++)
            {
                var a = (-MathF.PI / 2f) + (i * share * MathF.Tau / steps);
                points.Add(centre + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius));
            }

            DrawColoredPolygon([.. points], new Color(0f, 0f, 0f, 0.55f));
        }

        /// <summary>A padlock.</summary>
        private void DrawLock(Vector2 centre, float s)
        {
            DrawArc(centre + new Vector2(0, -s * 0.3f), s * 0.6f, MathF.PI, MathF.Tau, 12, UiTheme.Muted, s * 0.22f, true);
            DrawRect(new Rect2(centre + new Vector2(-s, -s * 0.2f), new Vector2(s * 2f, s * 1.5f)), UiTheme.Muted);
            DrawCircle(centre + new Vector2(0, s * 0.45f), s * 0.2f, new Color(0.1f, 0.1f, 0.1f));
        }
    }
}
