# ADR-056: The simulation's CPU budget, graphics scaling and regions sent within a tolerance

- Status: Accepted (amends ADR-019: regions are sent within a tolerance)
- Date: 2026-10-20
- Builds on: [ADR-019](ADR-019-replication-by-section-and-rate.md) (replication by section and
  rate), [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (the player's
  settings) and [ADR-044](ADR-044-stress-scenario-and-debug-overlays.md) (the stress scenario)

## Context

§105 asks that the simulation "attempt to remain below an allocated CPU budget". It also asks that
graphics options scale shadows, vegetation, particles, visible population and terrain quality.

The scheduler only capped the ticks of one update, at 32, and dropped the rest. At ×16 the stress
world spent 30 ms of each frame on ticks. The settings offered shadows, lighting effects, render
scale and citizens shown, but nothing for vegetation, particles or the terrain.

Measuring the stress world at ×16 also showed the client doing a frame's worth of work for changes
that only matter now and then:

- the flood sheet's check built a text key over the island's 16,384 regions every frame, and each
  rebuild of the sheet took a tenth of a second;
- a village's citizens and its walkers' grid were rebuilt whenever its roles or buildings changed,
  which happens every few frames at ×16. A grid rebuild sampled the terrain under every cell, and
  for a big village it took half a second;
- the forests redrew every planted chunk whenever any village gained a house.

ADR-044 found that in a shared game on the largest island, regions are nearly all the traffic
(144 KB/s to one player). The region section was sent whenever any value changed, and the land's
values creep a little every tick.

## Decision

### The simulation's CPU budget (`SimulationScheduler`)

- **The budget:** `BudgetSeconds` is the most real time the ticks of one update may take. Once it
  is spent, the ticks still due wait for the next update. At least one due tick always runs.
- **The backlog:** at most `MaxBacklogTicks` ticks may wait (twice the per-update cap); more are
  dropped and counted. So a world too heavy for its speed runs a little slower than asked, and
  never steals the frame or spirals.
- **The clock:** the client measures the elapsed time and passes it to `GameSession.Update`. The
  core still never reads the clock.
- **The setting:** "Simulation budget" (2–16 ms a frame, 6 by default), on the settings page.
- **Tools:** F1's performance lines show the budget, how often it cut a frame short, the ticks
  waiting and the ticks dropped.

### Graphics scaling (`GameSettings`, `GraphicsScaling`, `GraphicsScale`)

Three settings join the others:

- **Vegetation** (0.25–1): how far the ground cover reaches (100 m at full) and how far the forests
  are planted, at least half their full reach, so the land still reads as wooded from on high.
- **Particles** (0.25–1): the share of particles drawn by rain, fire, smoke, the volcano and the
  miracles' looks.
- **Terrain quality** (0.5–1): the size of Terrain3D's finest mesh (48 vertices a side at full, down
  to 24), or how far the fallback draws the land in full detail.

Particles apply at once. Vegetation and terrain quality apply when the game next starts, as citizens
shown already did. `GraphicsScaling.Apply` scales the presentation content, and it is pure and
tested.

### Regions sent within a tolerance (amends ADR-019)

- **When:** a region is sent to a player again only when one of its values has moved more than
  `RegionTolerance` (0.01) from what that player has, or has reached or left nothing. A fire going
  out or a flood starting is always sent.
- **What:** the values go out rounded to thousandths. A value above nothing stays above it.
- **Correction:** the occasional whole-world correction (ADR-019) still brings a replica exactly in
  line.

### The client's reactions, at a pace

- **Citizens:** a village's citizens, and its walkers' grid, are rebuilt at most once every
  `RosterRefreshSeconds` (2 s of the crowds' time, in `settlement_view.json`). A village newly
  shown still builds at once.
- **The walkers' grid:** it keeps the land under it (`LocalNavLand`) while its frame holds. Its
  reach is rounded up to 16 m, so its frame holds as a village grows a few houses. It is built
  once, not twice.
- **Floods:** the sheet is looked at about once a second, with a hash for its key. Its mesh
  skips dry rows and regions instead of testing every cell against its neighbours.
- **Forests:** when a village's clearing changes or a road is built, only the chunks around it are
  redrawn.

## Consequences

- **§105:** the simulation keeps to a CPU budget the player can set, and every graphics option it
  lists (shadows, vegetation, particles, visible population and terrain quality) scales.
- **Measured** on the stress world at ×16, per frame:
  - the simulation fell from 30 ms to the 6 ms budget (more ticks wait or drop instead);
  - the world's visuals fell from 31 ms to 4 ms;
  - the citizens fell from 42 ms to 11 ms;
  - with the lowest settings, the grown game draws 739 calls instead of 856.
- **Traffic:** a player of the stress world receives 60 KB/s instead of 144 KB/s. The regions'
  share fell from 660 KB/s raw to 117 KB/s.
- **Measuring on the Mac:** frame rates measured while the Mac's screen is locked are not
  trustworthy. macOS stops taking the window's frames, and each frame then waits up to a second
  for a drawable. Check `ioreg -n Root -d1 | grep CGSSessionScreenIsLocked` before measuring.
- **Not done:** a budget for the client's own work, and measurements on Windows and Linux.
