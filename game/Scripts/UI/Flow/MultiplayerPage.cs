using System.Globalization;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Networking;

namespace Pantheon.Client.UI.Flow;

/// <summary>Playing together (ADR-018): host a new shared world, or join a friend's.</summary>
internal static class MultiplayerPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="menu">The menu, to report choices.</param>
    /// <param name="seed">The seed offered for a hosted world.</param>
    /// <returns>The page.</returns>
    public static Control Build(MenuScreen menu, WorldSeed seed)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(UiKit.Heading("Play Together"));
        column.AddChild(UiKit.Text("Up to three friends join your world, each playing one of the rival gods. No server or account: your computer runs the world.", UiTheme.SmallSize, UiKit.Muted));

        column.AddChild(UiKit.Text("Host a world", UiTheme.BodySize, UiTheme.Gold));
        var hostName = UiKit.Field("Host", "your name");
        var hostPort = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        var hostSeed = UiKit.Field(seed.Value.ToString(CultureInfo.InvariantCulture), "seed");
        column.AddChild(UiKit.Row("Your name", hostName));
        column.AddChild(UiKit.Row("Port", hostPort));
        column.AddChild(UiKit.Row("Seed", hostSeed));
        column.AddChild(UiKit.Button("Host", () => menu.ChooseHost(hostSeed.Text.Trim().Length > 0 ? WorldSeed.Parse(hostSeed.Text.Trim()) : seed, hostName.Text.Trim(), Port(hostPort.Text))));

        column.AddChild(UiKit.Text("Join a world", UiTheme.BodySize, UiTheme.Gold));
        var address = UiKit.Field("127.0.0.1", "the host's address");
        var joinPort = UiKit.Field(NetworkProtocol.DefaultPort.ToString(CultureInfo.InvariantCulture), "port");
        var joinName = UiKit.Field("Player", "your name");
        var god = UiKit.Field(string.Empty, "any");
        column.AddChild(UiKit.Row("Host's address", address));
        column.AddChild(UiKit.Row("Port", joinPort));
        column.AddChild(UiKit.Row("Your name", joinName));
        column.AddChild(UiKit.Row("God to play", god));
        column.AddChild(UiKit.Button("Join", () => menu.ChooseJoin(address.Text.Trim(), Port(joinPort.Text), joinName.Text.Trim(), god.Text.Trim().Length > 0 ? god.Text.Trim() : null)));
        return column;
    }

    /// <summary>A port typed by the player, or the default.</summary>
    private static int Port(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535 ? port : NetworkProtocol.DefaultPort;
}
