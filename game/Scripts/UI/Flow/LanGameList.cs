using System;
using Godot;
using Pantheon.Client.Network;
using Pantheon.Game.Multiplayer;

namespace Pantheon.Client.UI.Flow;

/// <summary>
/// The worlds hosted on the local network (ADR-081), on the Play Together page. It looks while it
/// is shown and stops when the page closes; each world that can be joined has its Join button.
/// </summary>
internal sealed partial class LanGameList : VBoxContainer
{
    private LanBrowser? _browser;
    private string? _failure;
    private bool _waitedLong;

    /// <summary>The fingerprint of this game's data, to tell which worlds it could join.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Joins a world chosen from the list.</summary>
    public Action<LanGame>? Joining { get; set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        Rebuild();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            Stop();
            return;
        }

        if (_browser is null && _failure is null)
        {
            var (channel, failure) = UdpLanChannel.Ask();
            _failure = failure;
            _browser = channel is null ? null : new LanBrowser(channel, Content);
        }

        var changed = _browser?.Update(delta) ?? false;
        var waitedLong = _browser is { Seconds: >= 3.0 };
        if (changed || waitedLong != _waitedLong)
        {
            _waitedLong = waitedLong;
            Rebuild();
        }
    }

    /// <inheritdoc />
    public override void _ExitTree() => Stop();

    /// <summary>Stops looking, closing the port.</summary>
    private void Stop()
    {
        _browser?.Dispose();
        _browser = null;
    }

    /// <summary>Lists the worlds found, or says why there are none.</summary>
    private void Rebuild()
    {
        UiKit.Clear(this);
        if (_failure is not null)
        {
            AddChild(UiKit.Text($"Cannot look for worlds on your network ({_failure}). Join by address below.", UiTheme.SmallSize, UiTheme.Muted));
            return;
        }

        var games = _browser?.Games ?? [];
        if (games.Count == 0)
        {
            AddChild(UiKit.Text(LanText.Looking(_browser?.Seconds ?? 0.0), UiTheme.SmallSize, UiTheme.Muted));
            return;
        }

        foreach (var game in games)
        {
            AddChild(Row(game));
        }
    }

    /// <summary>One world: its host and details, and Join when it can be joined.</summary>
    private Control Row(LanGame game)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        words.AddChild(UiKit.Text(LanText.Title(game), UiTheme.BodySize, game.Fit == LanFit.Joinable ? UiTheme.Ink : UiTheme.Muted));
        words.AddChild(UiKit.Text(LanText.Details(game), UiTheme.CaptionSize, game.Fit == LanFit.Joinable ? UiTheme.Muted : UiTheme.Warning));
        row.AddChild(words);
        var join = UiKit.Button("Join", () => Joining?.Invoke(game));
        join.CustomMinimumSize = new Vector2(96, 40);
        join.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        join.Disabled = game.Fit != LanFit.Joinable;
        join.TooltipText = LanText.Where(game);
        row.AddChild(join);
        return row;
    }
}
