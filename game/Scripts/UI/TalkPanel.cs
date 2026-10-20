using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Client.UI.Flow;
using Pantheon.Game.Minds;

namespace Pantheon.Client.UI;

/// <summary>What the talk panel shows (ADR-066).</summary>
/// <param name="Title">The god's name and epithet.</param>
/// <param name="Subtitle">How things stand between you, in a few words.</param>
/// <param name="Lines">The conversation, oldest first.</param>
/// <param name="Suggestions">What the player might say next.</param>
/// <param name="Answering">Whether the god is answering now, and its name.</param>
/// <param name="CanType">Whether the player may type their own words (a model is chosen).</param>
/// <param name="Proposal">The god's proposal for the player to answer, in words; <c>null</c> when none.</param>
internal sealed record TalkView(string Title, string Subtitle, IReadOnlyList<TalkLine> Lines, IReadOnlyList<TalkSuggestion> Suggestions, string? Answering, bool CanType, string? Proposal);

/// <summary>
/// Talking to a god (ADR-066): the conversation, the god's proposal with Accept and Decline, what
/// the player might say, and a field for their own words. Esc or Close ends it. The panel reports
/// choices and holds no game state.
/// </summary>
internal sealed partial class TalkPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _column = null!;
    private string _key = string.Empty;

    /// <summary>The player chose a suggestion.</summary>
    public event Action<TalkSuggestion>? SuggestionClicked;

    /// <summary>The player sent their own words.</summary>
    public event Action<string>? Sent;

    /// <summary>The player answered the god's proposal: true to accept.</summary>
    public event Action<bool>? Answered;

    /// <summary>The player ended the conversation.</summary>
    public event Action? Closed;

    /// <summary>Whether the panel is shown.</summary>
    public bool PanelVisible => _panel.Visible;

    /// <inheritdoc />
    public override void _Ready()
    {
        Layer = 5;
        _panel = UiKit.CentredPanel(this, 700, out _column);
        _panel.Visible = false;
    }

    /// <inheritdoc />
    public override void _Input(InputEvent @event)
    {
        if (_panel.Visible && @event.IsActionPressed("ui_cancel"))
        {
            Closed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Shows the conversation, rebuilding only when something shown changed.</summary>
    /// <param name="view">What to show.</param>
    public void Show(TalkView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _panel.Visible = true;
        var key = string.Join("\u001f", [view.Title, view.Subtitle, view.Lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), view.Answering ?? "-", view.CanType ? "t" : "f", view.Proposal ?? "-", .. view.Suggestions.Select(s => s.Text)]);
        if (key != _key)
        {
            _key = key;
            Build(view);
        }
    }

    /// <summary>Hides the panel.</summary>
    public void Close()
    {
        _panel.Visible = false;
        _key = string.Empty;
    }

    /// <summary>Builds the panel's column.</summary>
    private void Build(TalkView view)
    {
        UiKit.Clear(_column);
        var title = UiKit.Heading(view.Title, 26);
        _column.AddChild(title);
        _column.AddChild(UiKit.Text(view.Subtitle, UiTheme.SmallSize, UiTheme.Muted));

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(660, 330), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lines.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(lines);
        _column.AddChild(scroll);
        foreach (var line in view.Lines)
        {
            lines.AddChild(Line(line));
        }

        if (view.Answering is { } who)
        {
            var thinking = UiKit.Text($"{who} is considering your words…", UiTheme.SmallSize, UiTheme.Muted);
            thinking.AddThemeFontOverride("font", UiTheme.Italic);
            lines.AddChild(thinking);
        }

        Callable.From(() =>
        {
            if (IsInstanceValid(scroll) && scroll.IsInsideTree())
            {
                scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            }
        }).CallDeferred();

        if (view.Proposal is { } proposal)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var words = UiKit.Text(proposal, UiTheme.BodySize, UiTheme.Gold);
            words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(words);
            row.AddChild(UiKit.Button("Accept", () => Answered?.Invoke(true)));
            row.AddChild(UiKit.Button("Decline", () => Answered?.Invoke(false)));
            _column.AddChild(row);
        }

        foreach (var suggestion in view.Suggestions)
        {
            var button = UiKit.Button(suggestion.Text, () => SuggestionClicked?.Invoke(suggestion));
            button.Alignment = HorizontalAlignment.Left;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.Disabled = view.Answering is not null;
            _column.AddChild(button);
        }

        var input = new HBoxContainer();
        input.AddThemeConstantOverride("separation", 8);
        var field = UiKit.Field(string.Empty, view.CanType ? "Say something of your own…" : "To speak freely, choose a model in Settings, under Minds");
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        field.Editable = view.CanType && view.Answering is null;
        field.MaxLength = 400;
        void Send()
        {
            if (field.Text.Trim().Length > 0)
            {
                Sent?.Invoke(field.Text.Trim());
            }
        }

        field.TextSubmitted += _ => Send();
        var send = UiKit.Button("Say", Send);
        send.Disabled = !field.Editable;
        input.AddChild(field);
        input.AddChild(send);
        _column.AddChild(input);
        if (field.Editable)
        {
            Callable.From(() =>
            {
                if (IsInstanceValid(field) && field.IsInsideTree())
                {
                    field.GrabFocus();
                }
            }).CallDeferred();
        }

        _column.AddChild(UiKit.Button("Close (Esc)", () => Closed?.Invoke()));
    }

    /// <summary>A line of the conversation: the god's in warm gold, the player's to the right, notes small in the middle.</summary>
    private static Label Line(TalkLine line)
    {
        return line.Speaker switch
        {
            TalkSpeaker.God => UiKit.Text(line.Text, UiTheme.BodySize, new Color(0.96f, 0.86f, 0.62f)),
            TalkSpeaker.Player => Aligned(UiKit.Text(line.Text, UiTheme.BodySize, UiTheme.Ink), HorizontalAlignment.Right),
            TalkSpeaker.Note => Aligned(Italic(UiKit.Text(line.Text, UiTheme.SmallSize, UiTheme.Calm)), HorizontalAlignment.Center),
        };
    }

    /// <summary>A label aligned.</summary>
    private static Label Aligned(Label label, HorizontalAlignment alignment)
    {
        label.HorizontalAlignment = alignment;
        return label;
    }

    /// <summary>A label in italics.</summary>
    private static Label Italic(Label label)
    {
        label.AddThemeFontOverride("font", UiTheme.Italic);
        return label;
    }
}
