# ADR-003: Host-authoritative networking over ENet (later milestone), no lockstep

- Status: Accepted (implementation deferred to Milestones 12 and 13)
- Date: 2026-10-18

## Context

Multiplayer god games are often built on deterministic lockstep. Lockstep needs bit-exact
determinism across machines, compilers and CPUs (floating point, iteration order, library
math), and one desync ruins a session. PANTHEON's simulation is large, mixes statistical and
agent tiers, and will evolve quickly.

## Decision

- Multiplayer will be **host-authoritative**: one peer (the host) runs the only authoritative
  `WorldState`. Clients send intent *commands* (cast miracle at X, move hand, give creature an
  order). The host validates and applies them, then replicates state snapshots and deltas.
- Transport: **ENet** (reliable and unreliable channels over UDP), accessed through Godot's
  `ENetMultiplayerPeer` behind an adapter. The protocol and message types will live in the
  engine-agnostic `Pantheon.Networking` project.
- **No lockstep.** Cross-machine bit-exact determinism is not required. Determinism is still
  valued per platform for tests, replays and debugging (seeded RNG, ordered iteration).
- Sessions are LAN or direct-IP; no backend services (the game is fully offline-capable).

## Consequences

- Positive: tolerant of floating-point differences and platform libraries; late join and
  reconnect come from snapshots.
- Positive: the M1 rules already fit this model: all mutation goes through the simulation, and
  `WorldState` is serializable and separate from scenes (ADR-006).
- Negative: higher bandwidth than lockstep, and the host must handle interest management.
  The simulation tiers (ADR-004) and region grid give natural relevance boundaries.
- Negative: clients see the world with latency. Hand and camera input will use local
  prediction in presentation only.

## Alternatives considered

- Deterministic lockstep: lowest bandwidth, but fragile, and it constrains every system to
  fixed-point or strict float discipline.
- Dedicated or cloud servers: conflicts with "fully offline, no backend".
