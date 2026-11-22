using System;
using System.Linq;
using System.Text;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking;
using Pantheon.Networking.Discovery;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Multiplayer;

/// <summary>Finding worlds on the local network (ADR-081): the packets, a host's answers and a player's list.</summary>
public sealed class LanDiscoveryTests
{
    private const string Here = "192.168.1.20";
    private const string Friend = "192.168.1.31";
    private const int Port = NetworkProtocol.DefaultPort;

    private static LanAnswer Answer(int protocol = NetworkProtocol.Version, string content = "", string host = "Dana", int open = 3) =>
        new(protocol, content, Port, host, "seed 1234 · Spring, year 1", [host], ["Wynlius", "Belrays"], open);

    [Fact]
    public void Answer_SurvivesTheTrip()
    {
        var answer = Answer(content: "abc123");

        var read = LanCodec.Read(LanCodec.Encode(answer));

        Assert.NotNull(read);
        Assert.Equal(answer.Protocol, read.Protocol);
        Assert.Equal(answer.Content, read.Content);
        Assert.Equal(answer.Host, read.Host);
        Assert.Equal(answer.World, read.World);
        Assert.Equal(answer.Players, read.Players);
        Assert.Equal(answer.FreeGods, read.FreeGods);
        Assert.Equal(answer.Open, read.Open);
        Assert.True(LanCodec.Encode(answer).Length < LanCodec.MaxPacketBytes);
    }

    [Fact]
    public void Query_IsKnownByItsMark_AndNothingElseIs()
    {
        Assert.True(LanCodec.IsQuery(LanCodec.Query));
        Assert.True(LanCodec.IsQuery([.. LanCodec.Query, 1, 2]));
        Assert.False(LanCodec.IsQuery(Encoding.UTF8.GetBytes("M-SEARCH * HTTP/1.1")));
        Assert.False(LanCodec.IsQuery(LanCodec.Encode(Answer())));
        Assert.Null(LanCodec.Read(LanCodec.Query));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("PANTHEON!")]
    [InlineData("PANTHEON!{not json")]
    [InlineData("PANTHEON!{}")]
    [InlineData("PANTHEON!{\"Protocol\":22,\"Content\":\"\",\"Port\":0,\"Host\":\"Dana\",\"World\":\"\",\"Players\":[],\"FreeGods\":[],\"Open\":1}")]
    [InlineData("PANTHEON!{\"Protocol\":22,\"Content\":\"\",\"Port\":7777,\"Host\":\"\",\"World\":\"\",\"Players\":[],\"FreeGods\":[],\"Open\":1}")]
    [InlineData("PANTHEON!{\"Protocol\":22,\"Content\":\"\",\"Port\":7777,\"Host\":\"Dana\",\"World\":\"\",\"Players\":[null],\"FreeGods\":[],\"Open\":1}")]
    [InlineData("PANTHEON!{\"Protocol\":22,\"Content\":\"\",\"Port\":7777,\"Host\":\"Dana\",\"World\":\"\",\"Players\":[],\"Open\":1}")]
    public void Read_RefusesWhatIsNotAnAnswer(string packet)
    {
        Assert.Null(LanCodec.Read(Encoding.UTF8.GetBytes(packet)));
    }

    [Fact]
    public void Read_SkipsMembersItDoesNotKnow_SoALaterVersionStillShows()
    {
        var packet = Encoding.UTF8.GetBytes("PANTHEON!{\"Protocol\":99,\"Content\":\"x\",\"Port\":7777,\"Host\":\"Dana\",\"World\":\"w\",\"Players\":[\"Dana\"],\"FreeGods\":[\"Seren\"],\"Open\":1,\"Lobby\":{\"Chat\":true}}");

        var read = LanCodec.Read(packet);

        Assert.NotNull(read);
        Assert.Equal(99, read.Protocol);
    }

    [Fact]
    public void Read_RefusesAnOversizedPacket()
    {
        var packet = LanCodec.Encode(Answer()).Concat(Enumerable.Repeat((byte)' ', LanCodec.MaxPacketBytes)).ToArray();

        Assert.Null(LanCodec.Read(packet));
    }

    [Fact]
    public void Encode_CutsLongNames_ButNeverTheFingerprint()
    {
        var longName = new string('a', 200);
        var answer = Answer() with { Host = longName, Players = Enumerable.Repeat(longName, 40).ToList() };

        var read = LanCodec.Read(LanCodec.Encode(answer));

        Assert.NotNull(read);
        Assert.InRange(read.Host.Length, 1, LanCodec.MaxText);
        Assert.StartsWith(read.Host, longName, StringComparison.Ordinal);
        Assert.InRange(read.Players.Count, 1, LanCodec.MaxNames);
        Assert.Equal(answer.FreeGods, read.FreeGods);
        var unicode = Answer() with { Players = Enumerable.Repeat(new string('é', 200), 40).ToList() };
        Assert.NotNull(LanCodec.Read(LanCodec.Encode(unicode)));
        Assert.Throws<ArgumentException>(() => LanCodec.Encode(Answer(content: new string('f', LanCodec.MaxFingerprint + 1))));
    }

    [Theory]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.2", true)]
    [InlineData("169.254.3.4", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("203.0.113.5", false)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd12:3456::1", true)]
    [InlineData("2001:db8::1", false)]
    [InlineData("::ffff:192.168.1.2", true)]
    [InlineData("::ffff:8.8.8.8", false)]
    [InlineData("not an address", false)]
    public void LocalAddresses_AreThisComputerAndPrivateRanges(string address, bool local)
    {
        Assert.Equal(local, LanAddress.IsLocal(address));
    }

    [Fact]
    public void APlayer_FindsAHostedWorld_WithItsSeedHostAndFreeGods()
    {
        var shared = SharedWorlds.Start();
        var lan = new LoopbackLan();
        using var responder = new LanResponder(lan.Listen(Here), () => LanResponder.Advert(shared.Host, shared.Server.Seats, Port));
        using var browser = new LanBrowser(lan.Open(Friend), shared.Host.Content.Fingerprint);

        browser.Update(0.0);
        responder.Update(0.0);
        var changed = browser.Update(0.1);

        Assert.True(changed);
        var game = Assert.Single(browser.Games);
        Assert.Equal(Here, game.Address);
        Assert.Equal(Port, game.Answer.Port);
        Assert.Equal(LanFit.Joinable, game.Fit);
        Assert.Equal("Hostess", game.Answer.Host);
        Assert.StartsWith("seed 1234", game.Answer.World, StringComparison.Ordinal);
        var aiGods = shared.Host.World.Gods.Where(g => g.Controller == Pantheon.Domain.Gods.GodController.Ai).OrderBy(g => g.Id).Select(g => g.Name);
        Assert.Equal(aiGods, game.Answer.FreeGods);
        Assert.Equal(1, responder.Answered);
        Assert.Equal("Hostess's world", LanText.Title(game));
        Assert.Equal("192.168.1.20:7777", LanText.Where(game));
    }

    [Fact]
    public void TheAnswer_FollowsWhoJoins_UntilNoGodIsFree()
    {
        var shared = SharedWorlds.Start();
        var free = LanResponder.Advert(shared.Host, shared.Server.Seats, Port).FreeGods.Count;
        var players = Enumerable.Range(0, NetworkProtocol.MaxClients).Select(i => shared.Join($"Player {i}")).ToList();

        var answer = LanResponder.Advert(shared.Host, shared.Server.Seats, Port);

        Assert.Equal(free - NetworkProtocol.MaxClients, answer.FreeGods.Count);
        Assert.Equal(0, answer.Open);
        Assert.Equal(["Hostess", "Player 0", "Player 1", "Player 2"], answer.Players);
        var lan = new LoopbackLan();
        using var responder = new LanResponder(lan.Listen(Here), () => answer);
        using var browser = new LanBrowser(lan.Open(Friend), shared.Host.Content.Fingerprint);
        Find(browser, responder);
        Assert.Equal(LanFit.Full, Assert.Single(browser.Games).Fit);
        Assert.Null(browser.Pick(null));
        Assert.EndsWith("no god is free", LanText.Details(browser.Games[0]), StringComparison.Ordinal);
    }

    [Fact]
    public void WorldsAPlayerCannotJoin_AreListedWithTheReason_AfterTheJoinable()
    {
        var lan = new LoopbackLan();
        using var other = new LanResponder(lan.Listen("10.0.0.2"), () => Answer(protocol: NetworkProtocol.Version + 1, host: "Ana"));
        using var data = new LanResponder(lan.Listen("10.0.0.3"), () => Answer(content: "modded", host: "Bo"));
        using var good = new LanResponder(lan.Listen("10.0.0.4"), () => Answer(host: "Cy"));
        using var browser = new LanBrowser(lan.Open("10.0.0.9"), string.Empty);

        Find(browser, other, data, good);

        Assert.Equal([LanFit.Joinable, LanFit.OtherVersion, LanFit.OtherData], browser.Games.Select(g => g.Fit));
        Assert.Equal("Cy", browser.Games[0].Answer.Host);
        Assert.EndsWith("another version of the game", LanText.Details(browser.Games[1]), StringComparison.Ordinal);
        Assert.EndsWith("different game data", LanText.Details(browser.Games[2]), StringComparison.Ordinal);
        Assert.Equal("Cy", browser.Pick(null)?.Answer.Host);
    }

    [Fact]
    public void Pick_TakesAWorldWhereTheGodAskedForIsFree()
    {
        var lan = new LoopbackLan();
        using var dana = new LanResponder(lan.Listen("10.0.0.2"), () => Answer(host: "Dana") with { FreeGods = ["Seren"] });
        using var eli = new LanResponder(lan.Listen("10.0.0.3"), () => Answer(host: "Eli") with { FreeGods = ["Wynlius"] });
        using var browser = new LanBrowser(lan.Open("10.0.0.9"), string.Empty);

        Find(browser, dana, eli);

        Assert.Equal("Eli", browser.Pick("wynlius")?.Answer.Host);
        Assert.Equal("Dana", browser.Pick(null)?.Answer.Host);
        Assert.Null(browser.Pick("Belrays"));
    }

    [Fact]
    public void AHostThatStopsAnswering_IsForgotten()
    {
        var lan = new LoopbackLan();
        var responder = new LanResponder(lan.Listen(Here), () => Answer());
        using var browser = new LanBrowser(lan.Open(Friend), string.Empty);
        Find(browser, responder);
        Assert.Single(browser.Games);

        responder.Dispose();
        var changed = false;
        for (var second = 0.0; second <= LanBrowser.ForgetAfterSeconds + 1.0; second += 0.5)
        {
            changed |= browser.Update(0.5);
        }

        Assert.True(changed);
        Assert.Empty(browser.Games);
    }

    [Fact]
    public void TheList_ChangesOnlyWhenWhatItShowsChanges()
    {
        var lan = new LoopbackLan();
        using var responder = new LanResponder(lan.Listen(Here), () => Answer());
        using var browser = new LanBrowser(lan.Open(Friend), string.Empty);
        Find(browser, responder);

        var changes = 0;
        for (var i = 0; i < 10; i++)
        {
            responder.Update(0.5);
            changes += browser.Update(0.5) ? 1 : 0;
        }

        Assert.Equal(0, changes);
        Assert.True(browser.Asked >= 5);
    }

    [Fact]
    public void AHost_AnswersOnlyTheLocalNetwork_AndOnlySoOften()
    {
        var lan = new LoopbackLan();
        using var responder = new LanResponder(lan.Listen(Here), () => Answer());
        var outsider = lan.Open("203.0.113.5");
        outsider.Broadcast(LanCodec.Query);
        responder.Update(0.0);
        Assert.Empty(outsider.Poll());
        Assert.Equal(1, responder.Ignored);

        var neighbour = lan.Open(Friend);
        neighbour.Broadcast(Encoding.UTF8.GetBytes("something else"));
        for (var i = 0; i < LanResponder.AnswersPerSecond + 5; i++)
        {
            neighbour.Broadcast(LanCodec.Query);
        }

        responder.Update(0.0);
        Assert.Equal(LanResponder.AnswersPerSecond, neighbour.Poll().Count);
        Assert.Equal(6, responder.Ignored);

        responder.Update(1.0);
        neighbour.Broadcast(LanCodec.Query);
        responder.Update(0.0);
        Assert.Single(neighbour.Poll());
    }

    [Fact]
    public void JoinLan_MeansTheFirstWorldFound()
    {
        Assert.True(LanBrowser.IsAnyHost("lan"));
        Assert.True(LanBrowser.IsAnyHost("LAN"));
        Assert.False(LanBrowser.IsAnyHost("192.168.1.20"));
        var options = Pantheon.Game.Launch.LaunchOptions.Parse(["--join=lan"]);
        Assert.Empty(options.Warnings);
        Assert.Equal(("lan", NetworkProtocol.DefaultPort), options.Join);
    }

    [Fact]
    public void TheHostsF1Lines_SayWhetherItIsListed()
    {
        var shared = SharedWorlds.Start();
        var lan = new LoopbackLan();
        using var responder = new LanResponder(lan.Listen(Here), () => LanResponder.Advert(shared.Host, shared.Server.Seats, Port));
        var player = lan.Open(Friend);
        player.Broadcast(LanCodec.Query);
        responder.Update(0.0);

        var listed = NetworkReadout.Describe(shared.Server, Port, shared.Host, responder);
        var unlisted = NetworkReadout.Describe(shared.Server, Port, shared.Host, null);

        Assert.Contains("  local network: listed on UDP 7778, 1 answered, 0 ignored", listed);
        Assert.Contains(unlisted, line => line.Contains("not listed", StringComparison.Ordinal));
    }

    /// <summary>A player asks and the hosts answer, until the answers are in.</summary>
    private static void Find(LanBrowser browser, params LanResponder[] responders)
    {
        browser.Update(0.0);
        foreach (var responder in responders)
        {
            responder.Update(0.0);
        }

        browser.Update(0.1);
    }
}
