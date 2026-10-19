using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking.Transport;

namespace Pantheon.Tests.Support;

/// <summary>A host and its players over the in-memory loopback, for multiplayer tests (ADR-018).</summary>
internal sealed class SharedWorlds
{
    private SharedWorlds(GameSession host, MultiplayerHost server, LoopbackNetwork network)
    {
        Host = host;
        Server = server;
        Network = network;
    }

    /// <summary>The host's session: the small shipped world, seed 1234, four gods.</summary>
    public GameSession Host { get; }

    /// <summary>The host's multiplayer side.</summary>
    public MultiplayerHost Server { get; }

    /// <summary>The loopback network.</summary>
    public LoopbackNetwork Network { get; }

    /// <summary>Status lines the host reported, oldest first.</summary>
    public List<string> HostStatuses { get; } = [];

    /// <summary>Starts hosting a fresh small world.</summary>
    /// <param name="seed">The world's seed.</param>
    /// <returns>The host.</returns>
    public static SharedWorlds Start(ulong seed = 1234)
    {
        var network = new LoopbackNetwork();
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(seed));
        return new SharedWorlds(session, new MultiplayerHost(session, network.CreateHost(), "Hostess"), network);
    }

    /// <summary>Connects a player and exchanges messages until it is answered.</summary>
    /// <param name="name">The player's name.</param>
    /// <param name="wantedGod">The god it asks for, if any.</param>
    /// <param name="content">Its content; the host's by default.</param>
    /// <returns>The player.</returns>
    public MultiplayerClient Join(string name, string? wantedGod = null, GameContent? content = null)
    {
        var client = new MultiplayerClient(content ?? TestContent.Small, Network.Connect(), name, wantedGod);
        Exchange(client);
        return client;
    }

    /// <summary>Lets the host and the players handle what arrived, a few rounds, without time passing.</summary>
    /// <param name="clients">The players.</param>
    /// <returns>The players' status lines, oldest first.</returns>
    public List<string> Exchange(params MultiplayerClient[] clients) => Pass(0.0, clients);

    /// <summary>Lets real time pass on the host (for its snapshots) and exchanges messages.</summary>
    /// <param name="seconds">Real seconds.</param>
    /// <param name="clients">The players.</param>
    /// <returns>The players' status lines.</returns>
    public List<string> Pass(double seconds, params MultiplayerClient[] clients)
    {
        var statuses = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            foreach (var client in clients)
            {
                statuses.AddRange(client.Update());
            }

            HostStatuses.AddRange(Server.Update(round == 0 ? seconds : 0.0));
        }

        foreach (var client in clients)
        {
            statuses.AddRange(client.Update());
        }

        return statuses;
    }

    /// <summary>The name of a god in the host's world.</summary>
    /// <param name="godId">The god.</param>
    /// <returns>Its name.</returns>
    public string GodName(int godId) => Host.World.Gods.Single(g => g.Id == godId).Name;
}
