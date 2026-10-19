using System;
using System.Linq;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Hud;
using Pantheon.Game.Launch;
using Pantheon.Networking;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Multiplayer;

/// <summary>A shared session as the players see and start it (ADR-018): the HUD's names, the F1 lines and the launch options.</summary>
public sealed class MultiplayerPresentationTests
{
    [Fact]
    public void TheHud_NamesThePlayersOfOtherGods()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        var hostHud = HudReadout.Build(shared.Host, null, null);
        var anaHud = HudReadout.Build(ana.Session!, null, null);

        Assert.StartsWith("Vael (Ana): ", hostHud.Rivals[0], StringComparison.Ordinal);
        Assert.StartsWith("Seren the Guardian: ", hostHud.Rivals[1], StringComparison.Ordinal);
        Assert.StartsWith("Ilyra (Hostess): ", anaHud.Rivals[0], StringComparison.Ordinal);
        Assert.StartsWith("Vael", anaHud.GodLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDebugPanel_ShowsWhoPlaysWhichGod_AndTheTraffic()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");

        var host = NetworkReadout.Describe(shared.Server, 7777, shared.Host);
        var player = NetworkReadout.Describe(ana, "127.0.0.1:7777", ana.Session!);

        Assert.StartsWith("Network: hosting on port 7777, 1 of 3 players joined; sent ", host[0], StringComparison.Ordinal);
        Assert.Equal(["  Hostess plays Ilyra (host)", "  Ana plays Vael, round trip 0 ms"], host.Skip(1));
        Assert.StartsWith("Network: joined 127.0.0.1:7777, host tick 0, sent 0 KB, received ", player[0], StringComparison.Ordinal);
        Assert.Equal(["  Hostess plays Ilyra (host)", "  Ana plays Vael (you)"], player.Skip(1));
    }

    [Fact]
    public void LaunchOptions_HostJoinNameAGodAndUpnp()
    {
        var host = LaunchOptions.Parse(["--host=on", "--name=Roey", "--upnp=on"]);
        Assert.Equal((NetworkProtocol.DefaultPort, "Roey", true), (host.HostPort, host.PlayerName, host.Upnp));
        Assert.Equal(9000, LaunchOptions.Parse(["--host=9000"]).HostPort);

        var join = LaunchOptions.Parse(["--join=192.168.1.20:9000", "--god=Seren"]);
        Assert.Equal(("192.168.1.20", 9000), join.Join);
        Assert.Equal("Seren", join.WantedGod);
        Assert.Equal(("pantheon.local", NetworkProtocol.DefaultPort), LaunchOptions.Parse(["--join=pantheon.local"]).Join);

        var bad = LaunchOptions.Parse(["--host=99999", "--join=a b", "--name=", "--upnp=maybe"]);
        Assert.Null(bad.HostPort);
        Assert.Null(bad.Join);
        Assert.Equal(4, bad.Warnings.Count);
    }
}
