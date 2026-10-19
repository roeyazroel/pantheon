using System;
using Godot;
using Pantheon.Client.UI;
using Pantheon.Game;
using Pantheon.Game.Book;
using Pantheon.Game.Launch;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The Book of the World in the client (ADR-031): Tab opens and closes it, its tabs turn the page,
/// and Aim selects a miracle to cast; <c>--book</c> opens it at the start. It reads the session and
/// sends the player's choices on.
/// </summary>
internal sealed partial class BookController : Node
{
    // The open page is rebuilt this often, in real seconds.
    private const double RefreshSeconds = 0.5;

    private GameSession _session = null!;
    private BookPanel _panel = null!;
    private BookTab _tab = BookTab.World;
    private double _age = RefreshSeconds;

    /// <summary>Raised with a miracle chosen in the book, to aim it like one on the bar.</summary>
    public event Action<string>? MiracleAimed;

    /// <summary>Connects the book to the session and applies the launch options.</summary>
    /// <param name="session">The session.</param>
    /// <param name="options">Launch options.</param>
    public void Setup(GameSession session, LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        _session = session;
        _panel = new BookPanel { Name = "Book" };
        AddChild(_panel);
        _panel.TabChosen += tab =>
        {
            _tab = tab;
            _age = RefreshSeconds;
        };
        _panel.MiracleAimed += miracle =>
        {
            _panel.PanelVisible = false;
            MiracleAimed?.Invoke(miracle);
        };
        if (options.Book is { } page)
        {
            _tab = page;
            CallDeferred(nameof(Open));
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        if (_session is null || !_panel.PanelVisible)
        {
            return;
        }

        _age += delta;
        if (_age >= RefreshSeconds)
        {
            _age = 0.0;
            _panel.ShowPage(BookReadout.Build(_session, _tab));
        }
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session is not null && @event.IsActionPressed(InputActions.ToggleBook))
        {
            _panel.PanelVisible = !_panel.PanelVisible;
            _age = RefreshSeconds;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the book once it is ready.</summary>
    private void Open()
    {
        _panel.PanelVisible = true;
        _age = RefreshSeconds;
    }
}
