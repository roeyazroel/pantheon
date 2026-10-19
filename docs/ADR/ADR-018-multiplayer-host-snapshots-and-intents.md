# ADR-018: Multiplayer sessions: a host's world, players' intents and replicas kept by snapshots

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-003](ADR-003-host-authoritative-enet-networking.md) (host-authoritative ENet, no
  lockstep) and [ADR-006](ADR-006-simulation-state-separate-from-scenes.md) (the world is
  `WorldState`, saved as versioned JSON)

## Context

Milestone 12 (spec §127) asks for an ENet host, joining, player assignment, action authority and
synchronized god state, for two players first. ADR-003 fixed the model:

- one host runs the canonical simulation;
- players send intents;
- the transport is ENet behind an adapter;
- the protocol lives in the engine-agnostic `Pantheon.Networking` project;
- there is no lockstep.

The specification adds:

- **Topology (§65–67, §70):** no server infrastructure. One player's computer is the host and a
  player too, and clients never own canonical state.
- **Networking (§68):** LAN, direct IP and UPnP when available.
- **Host disconnect (§71):** the multiplayer session ends and the host's world is saved.
- **Authority (§73):** clients send intents such as CastMiracleRequest. The host validates them
  (power, location, influence, cooldown, authorisation), executes them and returns the result.
- **Replication (§95–97):** replicate events and changed state, at frequencies that suit each kind
  of data. Predict only where it helps responsiveness: a miracle's result waits for the authority.

Everything a player does already goes through an authority (`MiracleCaster`, `CreatureAuthority`,
`DiplomacyAuthority`, `MigrationAuthority`), with the issuing god checked (ADR-009 to ADR-016).
A world snapshot (the save JSON) is about 390 KB, 42 KB gzipped, and takes about 6 ms to write and
7 ms to read.

## Decision

- **Protocol** (`Pantheon.Networking`, references only `Pantheon.Domain`):
  - Messages are records sent as JSON with a `Type` tag, gzipped from 1 KB on:
    - joining: `Hello`, `Welcome`, `Refused`;
    - state: `Snapshot`, `EventBatch`, `PlayerList`;
    - intents: `CastIntent`, `CreatureIntent`, `DiplomacyIntent`, `RefugeeIntent`;
    - answers and endings: `Verdict`, `Goodbye`.
  - World events travel by type name (`EventCodec`), so new event types travel without protocol
    changes, and a receiver skips kinds it does not know.
  - The transport is `INetworkTransport`: Godot's `ENetMultiplayerPeer` in the client
    (`EnetTransport`), and `LoopbackNetwork` in memory for tests.
  - `NetworkProtocol.Version` is 1, and the default port is 7777.
- **Joining and assignment.**
  - A player sends `Hello` with its protocol version, its content fingerprint, its name and
    optionally the god it wants. The fingerprint is the SHA-256 of every content file, so player
    and host must play by the same rules.
  - The host gives it that god, or else the first god the AI plays (lowest id), and sets the god's
    controller to `Remote`. The AI never acts for a `Remote` god.
  - A different protocol, different game data or no free god is refused with a reason.
  - At most three players join, one per AI god, so the first sessions are for two to four players.
- **Action authority (§73).**
  - A player's commands go to the host as intents. They carry no god: the host acts as the god the
    player was given.
  - The host runs the same issuer-aware commands as for its own player (`SessionCommands.*As`) and
    answers with a `Verdict`: accepted or not, and the status line.
  - The player's own world never applies an intent. Saving, loading, the clock and every debug tool
    are the host's alone (`SessionCommands.HostOnly`).
- **Replication.** The host sends:
  - the whole world when a player joins, and then a snapshot every second;
  - every event its world raises, as it happens, so players see alerts and miracle effects at once;
  - the list of players whenever it changes.
- **Replicas.** A joined player's `GameSession` is a replica:
  - It runs the same simulation between snapshots, at the host's speed and pause. This is only a
    predictor of what it will receive, so creatures walk and clouds drift smoothly.
  - It keeps none of the events it raises.
  - Each snapshot replaces its world whole, and its terrain is kept while the land is the same.

  On one platform the prediction matches the host almost exactly. Elsewhere it drifts slightly for
  at most a second, and the snapshot corrects it. Determinism is never required (§74).
- **Leaving.**
  - A player who leaves or disconnects gives its god back to the AI.
  - A host that ends the session says goodbye; its players' replicas pause with the reason; the
    host saves its world to `user://saves/hosted.json` (§71).
  - A saved world loads with the AI playing every `Remote` god.
  - A host that starts or loads another world keeps its players on their gods, by id.
- **Starting.** `--host=on|<port>` (with `--upnp=on` to ask the router), `--join=<address>[:<port>]`,
  `--name=<player>` and `--god=<name>`. A joining player waits up to ten seconds for the host's world
  before the world is shown, and plays alone if refused or unanswered. There is no menu yet.
- **Seeing it.**
  - The HUD names the players of other gods.
  - The F1 panel shows the session, its players, round trips and traffic.
  - Status lines report joins, departures, refusals and verdicts.

## Consequences

- Positive: one code path checks every command, a player's on the host or the host's own. A
  player cannot set Divine Power, belief or anything else (§73).
- Positive: everything the host simulates reaches players. Gods, settlements, Creatures, weather,
  disasters, migration and diplomacy arrive with each snapshot, and their events at once, with no
  per-system replication code yet.
- Positive: tested headless over the loopback; played over ENet by two instances on one Mac.
- Negative: each player receives the whole world once a second, about 45 KB/s. That is fine on a
  LAN and for direct connections, but §95 asks for interest management and §96 for
  per-kind frequencies. Milestone 13 replaces most snapshots with replication by frequency.
- Negative: a snapshot takes about 7 ms to read on the player's main thread, once a second.
- Negative: joining blocks the start for up to ten seconds, and there is no lobby, chat,
  reconnection to the same god after a drop, or host migration (§72, not for the MVP).

## Alternatives considered

- **Godot's high-level multiplayer (RPCs and `MultiplayerSynchronizer` on nodes).** It ties the
  protocol to the scene tree, which ADR-006 forbids as canonical state, and keeps it out of
  headless tests.
- **Players without a simulation, showing only snapshots.** Creatures and citizens would jump once
  a second. The replica's prediction costs nothing extra and is overwritten anyway.
- **Applying intents on the player's replica at once.** The effect would show before the host
  agreed, and a refused miracle would flicker. §97 keeps miracle results with the authority; the
  host's event arrives within one round trip.
- **Per-system delta replication now.** It is the right end state (§95–96), but it is Milestone
  13's deliverable. Full snapshots make Milestone 12's players see the whole world correctly
  first, and they stay for joining and resynchronising.
