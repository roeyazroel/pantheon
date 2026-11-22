# ADR-081: Finding worlds on the local network

- Status: Accepted
- Date: 2026-11-22
- Amends: [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) (joining: a player no longer
  needs the host's address on the same network)
- Builds on: [ADR-003](ADR-003-host-authoritative-enet-networking.md) (LAN or direct IP, no backend)

## Context

Spec §68 asks for LAN, direct IP and UPnP. Until now, LAN play meant direct IP:

- every joining player typed the host's address;
- the host was told only "--join=<this computer's address>", so someone had to find that address in
  the system's settings first.

The owner asked that joiners see hosts in the menu. §70 still rules out any server, so the computers
on the network have to find each other themselves.

## Decision

### Players ask, hosts answer

A player looking for worlds broadcasts a short question on UDP port **7778**
(`LanCodec.Port`, beside the game's 7777). Each host there answers that player alone, with a card of
its world (`LanAnswer`):

- the protocol version and the fingerprint of its game data;
- the port its world listens on;
- the host's name;
- the world in a few words: its seed and the season and year;
- who plays;
- the gods still free, and how many more may join.

The questions and answers are short JSON datagrams after a mark (`PANTHEON?` and `PANTHEON!`). An
answer always fits one datagram of 1200 bytes, its names cut shorter until it does.

### Why the player asks

Hosts could announce themselves instead, with players listening on a fixed port. The question is
better here:

- **Only the host listens.** It already needs an open port for ENet, so its firewall question
  (macOS, Windows) covers discovery too. A player only asks, and its replies come back to the
  port it asked from.
- **Several players on one computer** can look at once, since each asks from a port of its own.
  That is how a host and two players are tested on one Mac.
- **The list fills at once** when the page opens, rather than at the next announcement.
- **No traffic when nobody looks.** Hosts never broadcast.

The price is one listed host per computer: a second host there cannot listen on 7778. It still
hosts, says it is not listed, and is joined by address.

### What the player sees

**Play Together** lists the worlds on the network while the page is open (`LanGameList`). It asks
every second and forgets a host that has not answered for 3.5 seconds.

- **Joinable worlds** come first, each with a **Join** button. It joins with the name and god typed
  above the list, through the same path as joining by address.
- **Worlds this game cannot join** are listed too, greyed, with the reason: another version of the
  game, different game data, or no god free. This saves guessing why a friend's world is missing.

`--join=lan` joins the first joinable world found, waiting up to ten seconds as joining does. With
`--god`, the world must have that god free.

### Reading answers

Content files and saves are read strictly, but a discovery answer skips members it does not know.
A later version's host then still shows as "another version of the game" rather than not at all.
Everything else is checked: the mark, the size, a port in range, names present and short.

### Who is answered

A host answers only this computer, private IPv4 ranges (10/8, 172.16/12, 192.168/16), link-local
addresses (169.254/16, fe80::/10) and unique local IPv6 (fc00::/7). It answers at most 20 questions
a second (`LanResponder.AnswersPerSecond`).

UPnP forwards only the game's port, so the question cannot normally arrive from the internet
anyway. The limit keeps a forged question from turning the host into a reflector.

### Without a network

A broadcast that cannot leave the computer, with no network at all, is sent to 127.0.0.1 instead.
A host on the same computer is still found.

### On macOS

macOS asks before an app talks to the local network. The macOS export's `Info.plist` gains
`NSLocalNetworkUsageDescription`, so the question says why: to find and join worlds on the network.

### Seeing it

- **F1 on the host:** "local network: listed on UDP 7778, n answered, m ignored", or "not listed"
  when the port was taken. An unattended run prints it at the end.
- **The host's status line** says friends on the network find the world in Play Together.
- **`--join=lan`** prints "Found Dana's world on your network at 10.0.0.5:7777", or why nothing was
  joined.
- **`--title-page=together`** opens the page for captures.

## Consequences

- Positive:
  - nobody types an address on a LAN;
  - a player sees why a world on the network cannot be joined;
  - the session protocol is unchanged, so `NetworkProtocol.Version` stays 22;
  - tested headless over an in-memory network (`LoopbackLan`), and played on one Mac with a host,
    a page that listed it and players that joined it with `--join=lan`.
- Negative:
  - Broadcasts do not cross routers. Players on another subnet, a VPN or the internet still join
    by address, and so does a host whose Wi-Fi keeps its devices apart (guest networks often do).
  - A broadcast to 255.255.255.255 leaves by one network interface. On a computer with two, such
    as Wi-Fi and Ethernet, only one network hears it.
  - One listed host per computer.
  - Not yet played between two computers, or on Windows or Linux.

## Alternatives considered

- **Hosts announce, players listen** (as many LAN games do): see "Why the player asks".
- **Multicast or mDNS/DNS-SD** (Bonjour): it would cross more home routers' interface rules, but
  needs a group join and, for DNS-SD, a responder library on each platform. Broadcast is enough for
  one network, and the channel is behind `ILanChannel`, so multicast can replace it later.
- **Godot's high-level multiplayer or a library:** neither finds hosts. Godot's `PacketPeerUdp` is
  enough, and it is the only native piece (`UdpLanChannel`).
- **A lobby server or Steam lobbies** (§69): §70 rules out a server. Steam stays optional and would
  be a second way to find worlds, not a replacement.
