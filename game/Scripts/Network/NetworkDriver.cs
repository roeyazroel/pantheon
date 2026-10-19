using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;

namespace Pantheon.Client.Network;

/// <summary>
/// Wires a shared session into the game (ADR-018): it joins a host before the world is shown
/// (<c>--join</c>), or hosts the local world (<c>--host</c>, with UPnP when asked). It polls the
/// network every frame, and at the end says goodbye; a host also saves its world (spec §71).
/// </summary>
internal sealed class NetworkDriver
{
    // How long a player waits for the host to answer before playing alone.
    private const ulong JoinTimeoutMilliseconds = 10_000;

    private readonly Action<string> _setStatus;
    private MultiplayerHost? _host;
    private MultiplayerClient? _client;
    private int _port;
    private string _address = string.Empty;

    /// <summary>Creates the driver.</summary>
    /// <param name="setStatus">Shows a status line.</param>
    public NetworkDriver(Action<string> setStatus) => _setStatus = setStatus;

    /// <summary>Whether the world is shared, hosted here or joined.</summary>
    public bool IsShared => _host is not null || _client is not null;

    /// <summary>Joins the host the options name and waits for its world.</summary>
    /// <param name="content">The game's content.</param>
    /// <param name="options">Launch options with <see cref="LaunchOptions.Join"/> set.</param>
    /// <returns>The replica session, or <c>null</c> after a status line saying why not.</returns>
    public GameSession? Join(GameContent content, LaunchOptions options)
    {
        if (options.Join is not { } target)
        {
            return null;
        }

        (_address, _port) = (target.Address, target.Port);
        var (transport, failure) = EnetTransport.Join(target.Address, target.Port);
        if (transport is null)
        {
            _setStatus(failure ?? "Could not connect");
            return null;
        }

        var client = new MultiplayerClient(content, transport, options.PlayerName ?? "Player", options.WantedGod);
        var deadline = Time.GetTicksMsec() + JoinTimeoutMilliseconds;
        while (client.Phase == ClientPhase.Connecting && Time.GetTicksMsec() < deadline)
        {
            Show(client.Update());
            OS.DelayMsec(20);
        }

        if (client.Phase != ClientPhase.Joined || client.Session is null)
        {
            var reason = client.Reason ?? "the host did not answer";
            client.Dispose();
            _setStatus($"Could not join {Address}: {reason}. Playing alone.");
            return null;
        }

        _client = client;
        return client.Session;
    }

    /// <summary>Hosts the session's world on the port the options name.</summary>
    /// <param name="session">The host's session.</param>
    /// <param name="options">Launch options with <see cref="LaunchOptions.HostPort"/> set.</param>
    public void Host(GameSession session, LaunchOptions options)
    {
        if (options.HostPort is not { } port || session.IsReplica)
        {
            return;
        }

        var (transport, failure) = EnetTransport.Host(port);
        if (transport is null)
        {
            _setStatus(failure ?? "Could not host");
            return;
        }

        _port = port;
        _host = new MultiplayerHost(session, transport, options.PlayerName ?? "Host");
        if (options.Upnp)
        {
            _setStatus(OpenPort(port));
        }

        _setStatus($"Hosting on port {port}: other players start with --join=<this computer's address>:{port}");
    }

    /// <summary>Handles the network for this frame.</summary>
    /// <param name="delta">Real seconds since the last frame.</param>
    public void Update(double delta)
    {
        if (_host is not null)
        {
            Show(_host.Update(delta));
        }

        if (_client is not null)
        {
            Show(_client.Update());
        }
    }

    /// <summary>The F1 panel's network lines.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The lines; none when playing alone.</returns>
    public IReadOnlyList<string> Lines(GameSession session) =>
        _host is not null ? NetworkReadout.Describe(_host, _port, session)
        : _client is not null ? NetworkReadout.Describe(_client, Address, session)
        : [];

    /// <summary>Ends the shared session: a player leaves; a host tells its players and saves its world (spec §71).</summary>
    /// <param name="session">The session.</param>
    /// <param name="savePath">Where a host saves its world.</param>
    public void Shutdown(GameSession session, string savePath)
    {
        if (_host is not null)
        {
            _host.Dispose();
            _host = null;
            GD.Print(SessionCommands.QuickSave(session, savePath));
        }

        _client?.Dispose();
        _client = null;
    }

    /// <summary>The host's address and port.</summary>
    private string Address => $"{_address}:{_port}";

    /// <summary>Asks the router to forward the port (spec §68: UPnP when available).</summary>
    private static string OpenPort(int port)
    {
        var upnp = new Upnp();
        var found = (Upnp.UpnpResult)upnp.Discover();
        if (found != Upnp.UpnpResult.Success || upnp.GetGateway() is not { } gateway || !gateway.IsValidGateway())
        {
            return $"UPnP found no router ({found}); forward UDP port {port} by hand to play over the internet";
        }

        var mapped = (Upnp.UpnpResult)upnp.AddPortMapping(port, port, "Pantheon", "UDP");
        return mapped == Upnp.UpnpResult.Success
            ? $"UPnP: the router forwards UDP port {port}; players join with --join={upnp.QueryExternalAddress()}:{port}"
            : $"UPnP could not forward port {port}: {mapped}";
    }

    /// <summary>Shows each status line.</summary>
    private void Show(IReadOnlyList<string> statuses)
    {
        foreach (var status in statuses)
        {
            _setStatus(status);
        }
    }
}
