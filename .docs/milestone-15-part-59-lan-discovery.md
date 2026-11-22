# Milestone 15, part 59 — Finding worlds on the local network

## Overview

The owner asked whether LAN play worked, and then: "add LAN discovery so joiners see hosts in the
menu". The decisions are in [ADR-081](../docs/ADR/ADR-081-finding-worlds-on-the-local-network.md),
which amends ADR-018's joining (§68: LAN, direct IP and UPnP).

- **Players ask:** while Play Together is open, a player broadcasts a question on UDP 7778.
- **Hosts answer:** each host replies with a card of its world.
- **The page lists the worlds:**
  - joinable ones first, each with a **Join** button;
  - the others greyed, with the reason (another version, different data, no god free).
- **`--join=lan`** joins the first world found.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Networking` | `Discovery/`: `LanCodec`, `LanAnswer`, `LanAddress`, `ILanChannel`, `LanEndpoint`, `LanPacket`, `LoopbackLan` |
| `Pantheon.Game` | • `Multiplayer/LanResponder` (and its `Advert`), `LanBrowser`, `LanFit`, `LanGame`, `LanText`<br>• `NetworkReadout`: the host's local-network line<br>• `SeasonText.Calendar`, shared with the HUD |
| client | • `Network/UdpLanChannel`, `NetworkDriver` (answering, `--join=lan`)<br>• `UI/Flow/LanGameList`, `MultiplayerPage`, `MenuScreen.ContentFingerprint` |
| export | macOS `Info.plist`: `NSLocalNetworkUsageDescription` |

```mermaid
sequenceDiagram
    participant P as Player (LanBrowser)
    participant H as Host (LanResponder)
    P->>H: PANTHEON? broadcast to UDP 7778, every second
    H-->>P: PANTHEON! {protocol, data, port, host, seed and season, players, free gods}
    Note over P: lists the joinable first; forgets a host silent for 3.5 s
    P->>H: Hello over ENet to that address and port
```

## Tasks

- [x] The question and the answer, read tolerantly, always one datagram
- [x] A host answers only local addresses, at most 20 a second
- [x] The list: fit, order, forgetting, changes only when it shows something new
- [x] Play Together lists the worlds while it is open; Join uses the name and god above it
- [x] `--join=lan`, with `--god`
- [x] F1's local-network line on the host; status lines
- [x] macOS local-network usage text
- [x] Tests, ADR-081, docs

## Test plan

- `LanDiscoveryTests`:
  - the codec: a round trip, foreign and malformed packets, unknown members, cutting to fit;
  - local addresses;
  - a hosted world found with its seed, host and free gods, as players join until it is full;
  - worlds that cannot be joined, and why;
  - picking by god;
  - forgetting a host that went quiet;
  - which senders a host answers, and how often;
  - `--join=lan`;
  - the F1 line.
- On one Mac, rendering:
  - a host;
  - the Play Together page listing it (`--title-page=together`);
  - a player joining with `--join=lan`.
- The fallback renderer: a host, and two headless players looking at the same moment.

## Results

2038 tests pass (2000 before). On the M3 Pro, with the Mac's address 10.100.102.135:

- **Terrain3D:** the page listed "Dana's world" as "seed 1234 · Spring, year 1 · Dana plays · free:
  Wynlius, Ashsaeth, Belrays". `--join=lan` printed "Found Dana's world on your network at
  10.100.102.135:7777" and joined as Wynlius. The host answered 33 questions.
- **Fallback renderer:** the host answered two headless players asking at the same moment. One joined;
  the other asked for a god that seed 77 does not have, and said so.

A broadcast from a socket bound to 0.0.0.0 reaches a listener on the same Mac, with the Mac's LAN
address as the sender. A Python probe checked this first.

Not yet tried:

- two computers on one network;
- Windows or Linux;
- a guest Wi-Fi that keeps devices apart;
- clicking the page's Join button (it runs the same join as Join by address, which was tested).

Not part of this change: the joiner's log once showed "Invalid polygon data, triangulation failed"
from `MiracleGlyphs.Fill` while the HUD's miracle icons drew.
