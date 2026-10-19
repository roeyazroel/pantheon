# ADR-019: Replication by section and rate, with interest and event-based diplomacy

- Status: Accepted (amends ADR-018's replication)
- Date: 2026-10-19
- Builds on: [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) (a host's world, players'
  intents, replicas that predict)

## Context

Milestone 13 (spec §128) asks to synchronize miracles, Creatures, settlements, disasters and
diplomacy, for four players. ADR-018 (Milestone 12) sent every player the whole world every second.
That is correct, but:

- **Bandwidth.** About 36 KB of compressed JSON per player per second on the shipped world, and
  §95 asks not to send the whole world continuously.
- **Freshness.** A player's own miracle showed its cost, cooldown and effect on settlements up to
  a second late.
- **Cost.** Reading a whole snapshot takes about 7 ms on a player's main thread every second.

The specification asks to replicate relevant events, nearby entity state, important strategic
state, player actions and changed values, with interest management (§95), at rates by kind (§96):

| Kind of data | Rate |
| --- | --- |
| Creature movement | high |
| God resources | medium |
| Settlement food | low |
| Diplomatic state and world history | by event |

## Decision

- **The world in sections** (`WorldSection`). Each section travels in the save format as compact
  JSON (`PantheonJson.WireOptions`), gzipped from 512 bytes, in a `SectionUpdate` carrying the
  host's tick:

  | Section | Contents |
  | --- | --- |
  | Gods | the gods, with the world's tick, random state and winners |
  | CreatureMotion | position, heading, health and activity |
  | Creatures | every Creature whole |
  | Settlements | the settlements |
  | Regions | only the changing values of regions that changed: soil water, fire, scorch, blessing, vegetation, fertility |
  | Weather, Disasters, Migration, Diplomacy | that part of the world |

  A replica applies a section in place (`WorldSections.Apply`), so presenters keep reading the same
  world.
- **Rates** (`Replicator.Intervals`), per player:

  | Section | Interval |
  | --- | --- |
  | Creature motion | 0.2 s |
  | Gods | 0.25 s |
  | Migration | 0.5 s |
  | Settlements, weather, disasters | 1 s |
  | Creatures whole, regions | 2 s |

  A section goes out only if it differs from what that player last received. Each keeps its rate
  whatever the frame time.
- **Interest** (§95). A player tells the host where its camera looks (`ViewFocus`, when it moves
  50 m or every 2 s). Creatures within 600 m of it move at 5 Hz, the others once a second.
- **Diplomacy by event** (§96). Diplomacy goes out when a status, a proposal or a remembered act
  changes. The relations' fading values are otherwise refreshed only every five seconds.
- **Intents at once.** After a player's intent the gods, settlements, diplomacy, weather, disasters
  and migration go out with the verdict, so a player's own miracle shows its cost, cooldown and
  effect within one round trip. Miracle events reach every player as they happen (ADR-018).
- **Whole snapshots** stay for joining, for a new host world, and as a correction every 20 s.
- **The replica's clock.** It keeps predicting between updates (ADR-018). When the gods' section
  shows it more than five ticks from the host's tick, it takes the host's tick, so cooldowns and
  timers read as the host's.
- **Coming back.** A player who leaves keeps a claim on its god for the session: a player joining
  without asking for a god gets another free god first. The same name gets its god back.
- **Four players**: the host and three more, one per god the AI plays (as in ADR-018).
- **Seeing it.** The F1 panel shows traffic per second, and on a player the sections it has
  received; an unattended run prints the network lines at the end.

## Consequences

- Positive: on the shipped world after 5 minutes, with three players, each received 12.5 KB/s
  against about 36 KB/s for a whole snapshot a second, a third. Players' own actions show up
  faster, Creatures near them move five times as often, and settlements still refresh every
  second. The host spent about 1.5 ms per second of play replicating; a player about 0.7 ms per
  second applying.
- Positive: a section is generic. A new field in a domain type travels without protocol changes,
  and the save format's strict reading protects replicas from malformed parts.
- Positive: a host and three headless players on one Mac exchanged as designed: about 14 KB/s
  each, 13 ms round trips, and joining and leaving announced to all. Four instances that all
  render on one Mac starved each other of GPU and frames; that is not a network limit.
- Negative: sections are whole lists, not field deltas. A settlement's whole entry travels when
  one number changes. Field deltas would cut traffic further but need per-field code.
- Negative: interest covers only Creature motion; settlements and migration groups travel whatever
  the view.
- Negative: there is no delta compression against the previous packet, and there are no
  per-player bandwidth limits.

## Alternatives considered

- **Generic JSON diffs of the whole world each update.** Each update would mean serializing
  everything and diffing trees for every player, for little saving over sections.
- **Hand-written per-field replication messages.** They are the smallest on the wire, but every
  system change would need protocol changes, and the protocol would grow with each milestone.
- **Unreliable delivery for everything frequent.** Only Creature motion is unreliable, since the
  next update replaces it. Gods and settlements carry intents' results, so they must arrive.
