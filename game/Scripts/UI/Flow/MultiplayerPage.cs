using System.Globalization;
using Godot;
using Pantheon.Networking;

namespace Pantheon.Client.UI.Flow;

/// <summary>Playing together (ADR-018, ADR-081): join a world on your network or by address, or set up a new shared world on the New World page (ADR-082).</summary>
internal static class MultiplayerPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, to report choices, for the content its worlds must share, and to open the hosted world's page (ADR-082).</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(UiKit.Heading("Play Together"));
        column.AddChild(UiKit.Text("Friends join your world, each playing one of the rival gods. No server or account: your computer runs the world.", UiTheme.SmallSize, UiKit.Muted));

        column.AddChild(UiKit.Text("Join a world", UiTheme.BodySize, UiTheme.Gold));
        var joinName = UiKit.Field("Player", "your name");
        var god = UiKit.Field(string.Empty, "any");
        column.AddChild(UiKit.Row("Your name", joinName));
        column.AddChild(UiKit.Row("God to play", god));
        string? Wanted() => god.Text.Trim().Length > 0 ? god.Text.Trim() : null;
        column.AddChild(UiKit.Text("On your network", UiTheme.SmallSize, UiTheme.Ink));
        column.AddChild(new LanGameList
        {
            Content = menu.ContentFingerprint,
            Joining = game => menu.ChooseJoin(game.Address, game.Answer.Port, joinName.Text.Trim(), Wanted()),
        });

        column.AddChild(UiKit.Text("Or by address", UiTheme.SmallSize, UiTheme.Ink));
        var address = UiKit.Field("127.0.0.1", "the host's address");
        var joinPort = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        column.AddChild(UiKit.Row("Host's address", address));
        column.AddChild(UiKit.Row("Port", joinPort));
        column.AddChild(UiKit.Button("Join", () => menu.ChooseJoin(address.Text.Trim(), Port(joinPort.Text), joinName.Text.Trim(), Wanted())));

        column.AddChild(UiKit.Text("Host a world", UiTheme.BodySize, UiTheme.Gold));
        column.AddChild(UiKit.Text("Make a new world with every choice of a new one, its gods and rivals among them; each rival god is a seat a friend may take.", UiTheme.CaptionSize, UiKit.Muted));
        column.AddChild(UiKit.Button("Set up a shared world…", menu.OpenHostPage));
        column.AddChild(UiKit.Text("Friends on your network see your world here; others join with your address.", UiTheme.CaptionSize, UiKit.Muted));
        return column;
    }

    /// <summary>A port typed by the player, or the default.</summary>
    private static int Port(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 ? port : NetworkProtocol.DefaultPort;
}
