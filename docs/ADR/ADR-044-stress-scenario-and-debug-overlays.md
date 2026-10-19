# ADR-044: The stress scenario and the last debug overlays

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-019](ADR-019-replication-by-section-and-rate.md) (replication by section), [ADR-027](ADR-027-world-sizes-victories-and-large-worlds.md)
  (world sizes) and [ADR-031](ADR-031-book-of-the-world-overlays-overview-and-exports.md) (the map overlays)

## Context

§104 asks for automated or repeatable stress scenarios. Its examples:

- 100,000 conceptual citizens and 5,000 visible;
- 50 settlements, 8 gods and 8 Creatures;
- 20 weather cells and 3 simultaneous disasters.

It asks to measure CPU, memory, frame rate, simulation time, network bandwidth and save time. So far
unattended runs printed frame times, and headless probes measured ticks, but no scenario set such a
load up and nothing measured memory, the simulation's share of a frame or saves in the game.

§106 lists the debug overlays. Three were missing:

- the navmesh;
- network authority (F1 named who plays which god, but nothing showed authority over the land);
- performance metrics beyond the frame rate.

## Decision

### The stress scenario (`--stress=on`, `StressScenario`)

- **The world:** a world size made only for the scenario. It is the largest shipped island with 50
  villages and 8 gods; two names were added to `world_sizes.json` so 50 have names.
- **The load (`Apply`):**
  - 2,000 people in each village (100,000 in all), with houses and stores for them;
  - weather cells added until there are 20;
  - an earthquake, a fire and a flood at three villages.

  Every god has its Creature, so there are 8.
- **The crowd:** in the client, a village shows up to 1,000 figures within 1,300 m, so thousands are
  drawn. The camera starts over the village with the most others near it.
- **Determinism:** the scenario is pure and seeded, so the same seed makes the same stress world.
  A test builds it at full size and runs it.

### Measuring

- **In the client (`PerformanceMonitor`):** the only code that reads the clock. Over its last 240
  frames it measures:
  - frame time, average and worst;
  - the simulation's share of each frame, and the time of a tick;
  - ticks per second;
  - managed and engine memory;
  - the engine's objects, nodes, draw calls and primitives;
  - the citizens drawn.

  It also times writing the world as a save and reading it back.
- **In F1 (`PerformanceReadout`, pure):**
  - the measurements;
  - a census of the world's load: settlements, people, gods, Creatures, weather cells, earthquakes,
    burning and flooded regions, groups on the road, notables and followed villagers.
- **At the end of an unattended run:** the same lines are printed, with a save timed.
- **Network bandwidth:** the host's F1 shows it, and it was measured headless on the stress world
  (below).

### The last overlays

- **Navigation (O, `--overlay=navigation`):**
  - the land the pathfinder may cross, the open roads, and the land buried in rubble it goes round;
  - the ways every migrant group, prophet, followed villager and Creature is on, in their god's
    colour.
- **Authority (O, `--overlay=authority`):** each village's lands show who commands the god most of it
  follows: you in gold, another player in blue, the AI in grey. The host decides the world.
- **F1's network lines:**
  - say who decides: the host simulates, players send intents and receive sections, and a replica
    predicts and is corrected;
  - for a joined player, how old each section is.

### Found on the way

- **F1 rebuilt every frame:** on the stress world the panel cost half the frame rate (43 FPS), as it
  weighs every AI god's options. It now rebuilds four times a second (92 FPS).
- **GameRoot's size:** the debug panel and region map moved to `DebugView`, taking GameRoot from 496
  lines to 413.

## Measured (Apple M3 Pro, 1600 × 900, seed 42)

| What | Measured |
| --- | --- |
| Stress world made, headless | 6.8 s |
| A tick of the stress world | 1.4–1.5 ms (2.2 ms on the fallback renderer's run) |
| Frame rate, 7,000 citizens drawn | 100 FPS average, 95th percentile 11.7 ms, both renderers |
| Memory | about 500 MB managed and 260 MB engine; 1.3 GB resident at most |
| A save | 10.2 MB, written in 129 ms and read back in 151 ms |
| Network, one player | 0.61 MB to join, then 144 KB/s |

The bandwidth is mostly the regions. Each step changes nearly every region's soil water, snow or
vegetation a little, and a changed region is sent whole every 2 s: 1.3 MB raw on the 128 × 128
island. Sending regions only when they change by more than a tolerance would amend ADR-019, and is
left for when a shared game on the largest island needs it.

## Consequences

- **§104:** a repeatable stress scenario exists and the measures it asks for are taken.
- **§106:** every required overlay exists.
- **Balance and saves:** unchanged, as nothing here changes the simulation or the save.
- **Not done:** a CPU budget per frame for the simulation (§105), and measurements on Windows and
  Linux.
