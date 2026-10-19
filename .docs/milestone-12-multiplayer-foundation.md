# Milestone 12 — Multiplayer Foundation

## Overview

Spec §127 asks for an ENet host, joining, player assignment, action authority and synchronized god
state, for two players first. The decisions are in
[ADR-018](../docs/ADR/ADR-018-multiplayer-host-snapshots-and-intents.md), which builds on ADR-003.

What the players get:

- **Hosting.** `--host=on` (or a port) opens the world on UDP port 7777. `--upnp=on` asks the router
  to forward it. The host plays as usual.
- **Joining.** `--join=<address>[:<port>]` with `--name=Ana` and optionally `--god=Seren` joins a
  host. The player takes over the god it asked for, or the first god the AI plays, and sees the
  host's world.
  - The host refuses a player with other game data, another protocol, or when no god is free.
- **Authority.** Every miracle, Creature command, diplomatic move and refugee policy of a joined
  player goes to the host as an intent. The host checks it as it checks its own player's, as that
  player's god, and answers. The player's status line says "Asking the host..." and then, for
  example, "Food on Larkspur for 45 Divine Power" or "Outside your influence".
- **The shared world.**
  - The host sends its world every second and every event as it happens. A player's alerts and
    miracle effects come from the host.
  - Between snapshots a player's own copy runs the same simulation, so the world moves smoothly,
    and each snapshot corrects it.
  - God state (Divine Power, cooldowns, rank, followers, diplomacy) is the host's, at most a second
    old.
- **Only the host** saves, loads, starts a new world, sets the speed or uses the debug tools.
- **Leaving.** A player who leaves gives its god back to the AI. When the host quits, its players
  are told the session is over, and the host's world is saved to `user://saves/hosted.json` (§71).
- **Seeing it.**
  - The HUD names the players of other gods: "Ilyra (Roey)".
  - F1 shows the session, who plays which god, round trips and traffic.
  - Status lines report joins, departures and verdicts.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Domain` | `GodController.Remote`; `GodState.Controller` can change. |
| `Pantheon.Networking` | Was empty; now references `Pantheon.Domain`. `NetworkProtocol` (version, port, limits); `Protocol/NetMessage` and its records; `MessageCodec` (tagged JSON, gzipped from 1 KB); `EventCodec` (world events by type name); `Transport/INetworkTransport` and `LoopbackNetwork`. |
| `Pantheon.Game` | `Multiplayer/MultiplayerHost` (joining, gods, intents, snapshots, events, leaving) and `MultiplayerClient` (joining, the replica, intents, verdicts). `GameSession`: replicas (`CreateReplica`, `ApplySnapshot`, `ReceiveEvents`), the host's event outbox (`SessionEvents`), players' names, and the issuing god on every command. `SessionCommands`: intents for replicas, `*As` commands with outcomes for the host, and host-only guards. `GameContent.Fingerprint`. `Diagnostics/NetworkReadout`. `--host`, `--join`, `--name`, `--god`, `--upnp`. The Creature debug helpers moved from `GameSession` to `CreatureCommands`. |
| Godot client | `Network/EnetTransport` (ENet behind the transport interface, reliable and unreliable channels) and `Network/NetworkDriver` (joining before the world is shown, hosting, UPnP, polling, saving at the end). |

### One intent

```mermaid
sequenceDiagram
    participant P as Player (replica)
    participant N as ENet
    participant H as Host (MultiplayerHost)
    participant S as Host session and authorities
    P->>N: CastIntent(7, food, Larkspur)
    N->>H: packet
    H->>S: SessionCommands.CastMiracleAs(Seren, food, Larkspur)
    S-->>H: accepted, "Food on Larkspur for 45 Divine Power"
    H->>N: Verdict(7, accepted, text)
    H->>N: EventBatch(MiracleCast ...)
    N->>P: verdict: status line; events: effect and alerts
    H->>N: Snapshot (every second)
    N->>P: the world replaced; prediction carries on from it
```

| Quantity | Value |
| --- | --- |
| Port | 7777 by default |
| Players | the host and up to 3 more, one per god the AI plays |
| Snapshot | every real second, the whole world in the save format; about 390 KB of JSON, 42 KB gzipped |
| Events | every event the host's world raises, sent the frame it is drained |
| Join wait | up to 10 s before playing alone |

## Test Plan

| Area | Tests |
| --- | --- |
| Protocol | `ProtocolTests`: every message survives encoding; a snapshot is compressed and restores the world; events travel with their fields, and unknown kinds are skipped; packets that are not messages are refused; the loopback connects, delivers in order and disconnects. |
| Sessions | `MultiplayerSessionTests`: joining and the first free god; asking for a god; two players; refusals (game data, protocol, no free god); a player's miracle checked and cast as its god, and refused without power; diplomacy, Creature and refugee intents for its god only; host-only commands; events and snapshots reaching the player, prediction in between; no events of a replica's own and the host's clock; leaving; the host ending the session; the AI leaving a player's god alone; a saved shared world loading with the AI; a new host world keeping the players. |
| Presentation | `MultiplayerPresentationTests`: players' names on the HUD; the F1 lines; the launch options. |
| Architecture | `ArchitectureRulesTests`: `Pantheon.Networking` references only `Pantheon.Domain`, and `Pantheon.Game` may reference it; neither uses Godot, a clock or unseeded randomness. |

### Verification results

- `dotnet build Pantheon.sln`: no warnings. `dotnet test Pantheon.sln`: 756 tests pass (724 at the
  end of Milestone 11).
- Two instances on one Mac (Apple M3 Pro, Metal), over ENet on 127.0.0.1:7777, seed 1234:
  - The host printed "Ana joined and plays Seren". The joining instance printed "You joined and play
    Seren" and built the world's visuals in about 0.3 s.
  - Ana's Food on Seren's home: "Asking the host..." then "Food on Larkspur for 45 Divine Power".
    Her HUD showed Seren's power drop from about 111 to 66 and Food's cooldown, and the rivals line
    read "Ilyra (Roey): 20 followers (25%)".
  - Ana's Food on the host's home was refused: "Outside your influence: 0% here, Food needs 10%".
  - Her F1 line read "Network: joined 127.0.0.1:7777, host tick 133, received 450 KB, round trip
    28 ms", about 45 KB/s of snapshots.
  - When Ana quit, the host printed "Ana left; the AI plays Seren again". When the host quit, it
    saved "tick 354 to hosted.json".
  - A first run in which the host quit early showed ENet errors from polling a closed connection.
    The adapter now stops polling once closed.

## Tasks

### Completed

- [x] ADR-018; ADR-003's status.
- [x] The protocol, its codec, events by name, the transport contract and the loopback.
- [x] The host: joining, assignment, refusals, intents with verdicts, snapshots, events, players,
  leaving, ending, a new world.
- [x] The player: joining, the replica and its prediction, intents, verdicts, the end of a session.
- [x] Host-only commands; a saved shared world loading with the AI.
- [x] ENet in the client, the network driver, UPnP, the launch options, the HUD's names, F1 lines.
- [x] Tests and a two-instance run.

### Future (Milestone 13 and later)

- Replication by frequency and interest (§95–96): Creatures often, god state at medium rate,
  settlements rarely, diplomacy and history by event. Full snapshots kept for joining and
  resynchronising.
- Four players and the sessions' pacing; reconnecting to the same god after a drop; a lobby or menu
  instead of launch options; chat.
- Host migration (§72) is not for the MVP; Steam lobbies and relays (§69) belong to Milestone 15.

## Relevant Files

- `src/Pantheon.Networking/`
- `src/Pantheon.Game/Multiplayer/`, `src/Pantheon.Game/{GameSession,SessionEvents,SessionCommands,GameContent}.cs`, `src/Pantheon.Game/Diagnostics/NetworkReadout.cs`
- `game/Scripts/Network/`, `game/Scripts/Bootstrap/GameRoot.cs`
- `src/Pantheon.Tests/Multiplayer/`, `src/Pantheon.Tests/Support/SharedWorlds.cs`
