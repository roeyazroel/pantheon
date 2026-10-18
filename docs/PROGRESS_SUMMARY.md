# PANTHEON — Progress Summary

This document sums up the work done so far, Milestones 1 and 2, against the owner's complete
specification, [PANTHEON_SPEC.md](PANTHEON_SPEC.md), which is the source of truth. Section numbers
(§) refer to it. It describes the branch `cursor/milestone-2-living-settlement-651f` on
2026-10-18.

Milestone 1 is in [pull request #1](https://github.com/roeyazroel/pantheon/pull/1), merged into
`main` on 2026-10-18. Milestone 2 is in [pull request #2](https://github.com/roeyazroel/pantheon/pull/2),
which builds on it and is open for review. Pull request #2 still targets the Milestone 1 branch,
`cursor/milestone-1-foundation-e33e`, which holds exactly what `main` merged.

Status words used below:

- **Implemented**: built as the section asks, and verified.
- **Partial**: some of it is built; the notes say what is missing.
- **Not started**: nothing of it is built yet.

Sections that set a rule or a target rather than a feature say instead whether the rule is
followed or the target has been measured.

## In short

- Two of the specification's fifteen milestones are implemented: 1 Foundation (§116) and
  2 Living Settlement (§117). Their playable outcomes hold: the player can explore a seeded
  island, and its settlement visibly lives and changes.
- The world is one 2 km island with one settlement. The settlement is simulated statistically
  (Tier 1) and, near the camera, shown by up to 300 representative citizens (Tier 2).
- The god game itself does not exist yet: there are no gods, belief, Divine Power, influence,
  miracles, Divine Hand, Creature, rival gods, diplomacy, weather, disasters or multiplayer.
  Milestone 3, First God, starts it.
- 331 xUnit tests pass headless; Milestone 1 ended with 169. The solution builds with no
  warnings.
- Everything was built and checked on one Linux x64 virtual machine without a GPU, which renders
  with Mesa's software Vulkan driver at a few frames per second. The game has not been run on
  Windows, macOS or a real GPU.

## Milestones (§116–130)

| # | Milestone | Status |
| --- | --- | --- |
| 1 | Foundation (§116) | Implemented; merged in [#1](https://github.com/roeyazroel/pantheon/pull/1) |
| 2 | Living Settlement (§117) | Implemented; in review in [#2](https://github.com/roeyazroel/pantheon/pull/2) |
| 3 | First God (§118) | Not started; next |
| 4 | Creature (§119) | Not started |
| 5 | Rival God (§120) | Not started |
| 6 | Diplomacy (§121) | Not started |
| 7 | Weather (§122) | Not started |
| 8 | Disaster Simulation (§123) | Not started |
| 9 | Four Gods (§124) | Not started |
| 10 | Population Movement (§125) | Not started |
| 11 | Creature Relationships (§126) | Not started |
| 12 | Multiplayer Foundation (§127) | Not started; decided in ADR-003, and `Pantheon.Networking` is an empty project |
| 13 | Multiplayer Simulation (§128) | Not started |
| 14 | Complete Vertical Slice (§129) | Not started |
| 15 | Polish and Expansion (§130) | Not started |

Milestones 1 and 2 were planned before the complete specification was in the repository, against
an earlier working roadmap with different numbers. ADR-004 and ADR-008 still cite that roadmap:
their "Milestone 12" is its Tiers at Scale, which the specification does not schedule, and
ADR-008's "Milestone 4" is its Influence & the Hand. Every other document now uses the
specification's numbers, and [GAME_SPEC.md](GAME_SPEC.md#earlier-working-roadmap-superseded) maps
the two.

### Milestone 1: Foundation (§116), pull request #1

| §116 deliverable | Status | What exists |
| --- | --- | --- |
| Godot project | Implemented | `game/`: Godot 4.7.2-stable .NET, pinned by ADR-001, with the Forward+ renderer. |
| C# solution | Implemented | `Pantheon.sln` with Domain, Simulation, Persistence, Game, Networking (reserved, empty) and Tests, plus the Godot client project `game/Pantheon.csproj` (ADR-002). Warnings are errors. |
| Camera | Implemented | A free god camera with Close, Regional and Strategic zoom bands (§56). |
| Terrain | Implemented | A seeded island rendered with Terrain3D 1.0.2 (ADR-005), patched at runtime (ADR-007), with a fallback mesh renderer. |
| One settlement | Implemented | Founded on fertile land near the island centre, with a simple per-tick food and population model. |
| Saveable WorldState | Implemented | `WorldState` is the only canonical state (ADR-006) and is saved as versioned JSON. |
| Debug UI | Implemented | The F1 debug panel, the B region grid, the F3 region debug map and the `.` single-tick step. |

Playable outcome, "Player can explore the world": met. The plan, with its verification results,
is [.docs/milestone-1-foundation.md](../.docs/milestone-1-foundation.md).

Also built in Milestone 1:

- World generation from a `WorldSeed` with FastNoiseLite: 1024 × 1024 height samples 2 m apart
  (a 2048 m island), moisture and temperature fields, and the biomes Sea, Grassland, Forest and
  Highland. A 32 × 32 grid of 64 m regions gives each region a biome, elevation, temperature,
  moisture, fertility and vegetation.
- A fixed 10 Hz simulation tick, independent of the frame rate, with pause and ×1, ×4 and ×16
  speeds. Simulation randomness comes from a seeded xoshiro256\*\* generator whose state is saved.
- Quicksave and quickload (F5, F9): indented JSON with a `SaveVersion`, written atomically and
  checked on load. The terrain is regenerated from the seed, and loading rebuilds every visual.
- Launch options that reproduce a world and script smoke runs that save a capture.
- `AGENTS.md`, which turns the coding agent rules (§134) into repository conventions, and
  `ArchitectureRulesTests`, which enforces the ones a test can check.
- Fixes for problems found while verifying: a crash of Mesa's software Vulkan driver in
  Terrain3D's shader (ADR-007), flickering sun glints on distant water, and a pale line along the
  world's east and south edges.

### Milestone 2: Living Settlement (§117), pull request #2

| §117 deliverable | Status | What exists |
| --- | --- | --- |
| Aggregate population | Implemented | A statistical (Tier 1) settlement step once per simulated second, on every tenth tick (ADR-008). Births and deaths are Poisson draws from the saved random stream. Births need free housing, a food surplus and stocked stores; deaths have a baseline plus an ill-health term. |
| Food | Implemented | 60% of the inhabitants work. Farmers farm the land regions within two regions of the settlement, with diminishing returns. Everyone eats; the stores are capped by housing and the rest spoils. |
| Housing | Implemented | When the settlement is crowded and has food in reserve, up to a fifth of the workers (at most 10) build one house at a time, and each house holds 5 people. The player never places houses. |
| Health | Implemented | Falls with the share of people left hungry, recovers once everyone is fed, and changes births and deaths. |
| Visible representative citizens | Implemented | When the camera comes within 450 m of a settlement (until it is more than 550 m away), up to 300 figures appear, drawn with one MultiMesh, each standing for population ÷ figures people. Farmers, builders and villagers follow the labour split and a repeating round: rest by the door, work, gather at the shrine, go home. They are derived from the settlement's numbers and never saved (ADR-008). |

Playable outcome, "Settlement visibly lives and changes": met. With seed 1234 the settlement grows
from 40 people in 12 houses to about 540 people in 129 houses in about 25 simulated minutes (about
90 seconds at ×16). New houses, the house being built and the fields appear as it grows. A famine
started with the F7 and F6 debug keys lowers health to about two thirds and raises deaths until
the land feeds everyone again; then the settlement recovers. The plan, with its verification
results, is [.docs/milestone-2-living-settlement.md](../.docs/milestone-2-living-settlement.md).

Also built in Milestone 2:

- `SaveVersion` 2 with a migration from version 1, tested against a save written by the
  Milestone 1 build.
- Balance values in `game/Data/settlement_rules.json`; layout, colours, tier distances and
  routines in `game/Data/settlement_view.json`.
- Debug tools: settlement and tier readouts in the F1 panel, the `,` settlement step, F6 (spoil
  the food stores), F7 (settlers arrive), and the `--advance-ticks` and `--camera-distance`
  launch options.
- Checks in Godot, with captures: the settlement and its citizens on both terrain renderers, the
  building site, tier switching, citizens standing still while paused, a famine and its recovery,
  and a version 1 save loading through the migration. The citizen update costs 0.42 ms per frame
  for 300 citizens (Debug build, software renderer).

## Coverage by specification section

Sections that only state intent are summarised at the end of this part.

### World and generation

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 5 | The world | Partial | One continuous world, divided into simulation regions. Regions have a biome, elevation, temperature, moisture (standing in for rainfall), fertility and vegetation, and a settlement belongs to a region. Missing: wildlife, water, resources, religious influence, ecological health and disaster risk. Of the ten listed biomes, grassland, forest and mountain (as Highland) exist, plus the sea. |
| 6 | World scale | Partial | A 2 km island against 10–20 km, 1 settlement against 8 in the MVP, no gods, and about 540 people against 10,000–100,000+. Hundreds of visible citizens, and no heavyweight object per citizen. |
| 84 | World streaming | Not started | The whole island is loaded. Terrain3D gives terrain LOD; only the citizens depend on distance. |
| 93 | Procedural generation | Partial | Elevation, moisture, temperature, fertility, vegetation, biome and the settlement's site come from the seed, and the same seed and settings give the same world (tested). Missing: trees (forest is a biome, drawn as a ground colour), resources, river candidates, sacred sites, and a list of settlement candidates (the founder scores every habitable region and keeps only the best). |

### Settlements and population

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 29 | Settlements | Partial | Housing, job allocation (farm or build) and construction are autonomous. Missing: roads, gathering (food comes only from farming), expansion, and priorities set by a god. |
| 30, 90 | Settlement state | Partial | `SettlementState` has Id, Name, RegionId, Position, Population, Food, Housing, Health and ConstructionProgress. Missing: Water, Safety, Happiness, Education, Wealth, PoliticalStability, and belief, fear, love, doubt and resentment per god. |
| 31–36 | Migration, conversion, culture, schisms, atheism, technology | Not started | |
| 80 | Simulation tiering | Partial | Tier 1 simulates a simple economy (labour, food and housing), demographics and health, but not belief or migration. Tier 2 shows walking, working and gathering figures; they are presentation only and do not worship or flee. Tier 3 is not started. |
| 81 | Population representation | Implemented | Up to 300 figures stand for the population: one for every 1.8 people at 540. |
| 82 | Migration representation | Not started | |
| 83 | Rendering large crowds | Implemented | Each settlement draws all its citizens with one MultiMesh, and its house walls, roofs, doors and fields with one MultiMesh each. The citizens share one mesh and vary by role colour and routine phase. Each is a tapered cylinder with a sphere for a head that bobs and leans; there are no animation sets, clothing or accessories. |
| 112 | MVP settlements | Partial | Settlements gather food (by farming), build housing and grow on their own, and the player places no houses. Missing: worship, reacting to danger and responding to miracles. |

### Gods, belief and miracles

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 7–8 | Belief, love versus fear | Not started | `src/Pantheon.Domain/Belief` is reserved. |
| 9–10 | Divine Power, influence | Not started | |
| 11–13 | God attributes, domains, rank | Not started | `src/Pantheon.Domain/Gods` is reserved. |
| 14–15 | Miracles, systemic consequences | Not started | |
| 47–49, 87 | AI gods | Not started | |
| 109, 110, 115 | MVP belief, miracles and victory | Not started | |

### The Creature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 22–28, 86, 111 | Creature, progression, learning, personality, relationships, AI, MVP | Not started | `src/Pantheon.Domain/Creatures` is reserved. |

### Nature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 16–17 | Weather, seasons | Not started | `src/Pantheon.Domain/Weather` is reserved. |
| 18 | Ecology | Partial (data only) | Regions carry temperature, moisture, fertility and vegetation from generation, and farming reads fertility, but nothing changes them over time. Missing: wildlife, water availability, pollution, biodiversity, fire damage and every ecological process. |
| 19–21, 94, 114 | Disasters, preparation, physics and disasters, MVP nature | Not started | |

### Politics and history

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 37–46, 113 | Divine politics, diplomacy, betrayal, war, MVP diplomacy | Not started | `src/Pantheon.Domain/Diplomacy` is reserved. |
| 50–54 | History, sacred locations, world events, victory, alliance endgame | Not started | |
| 91–92 | Domain events, history system | Not started | Systems do not publish events yet. |

### Player experience

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 55 | Player interaction | Not started | There is no Divine Hand. The player moves the camera and uses debug keys. |
| 56 | Camera | Partial | WASD, edge movement, drag, zoom, rotate and focus settlement, with Close, Regional and Strategic bands and seamless zoom. Missing: focus Creature, a strategic overview, and strategic content (influence, weather, diplomacy). |
| 57 | User interface | Not started | No HUD or panels; the F1 debug panel is the only interface. |
| 58 | Map overlays | Partial (debug only) | The F3 debug map shows biome, elevation, temperature, moisture, fertility and vegetation, and F1 shows population and food as text. There are no overlays for players. |
| 59 | Alert system | Not started | The debug panel's warnings (starving, construction waiting for food, births blocked) are not player alerts. |
| 60 | Audio | Not started | |
| 61 | Visual direction | Partial | Stylised and readable from above: terrain coloured by biome, a sea shader with waves, depth colours and shoreline foam, and huts, fields and figures built from Godot primitive meshes. No original art assets yet, and religion changes nothing visible. |

### Platforms, technology and performance

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 62 | Supported platforms | Partial | The code is cross-platform, saves use `user://`, and the one native dependency ships Windows x64, macOS and Linux x64 libraries (a test checks the files). Built and run only on Linux x64; no export presets. |
| 63 | Core technology stack | Partial | In use: Godot .NET pinned to 4.7.2, C#, Terrain3D, FastNoiseLite and MultiMesh; Flecs is rightly not used. Not used yet: Jolt Physics (nothing is physical, and `project.godot` does not choose a 3D physics engine), Godot Navigation and Recast, LimboAI, a utility AI, and Blender assets. |
| 64 | Terrain destruction | Not started | Terrain3D is used as §64 asks; nothing damages the terrain yet. |
| 88 | Threading model | Followed | Everything runs on the main thread, as §88 asks to begin with. Nothing has moved to a worker thread, and world generation, the first candidate §88 names, has not been profiled. |
| 89 | Update frequencies | Partial | The settlement steps at 1 Hz. Citizens' roles and homes change at most once per settlement step, and their poses are evaluated every frame from simulation time; there is no separate 2–5 Hz behaviour update, because citizens make no decisions. No other systems exist yet. |
| 104 | Performance testing | Not started | No stress scenarios. Only the citizen update, the roster rebuild and the layout have been timed. |
| 105 | Target performance | Not measured | 1080p at 60 FPS has not been measured, and there are no graphics options. |

### Architecture, saves and process

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 65 | Local-first architecture | Implemented | Fully offline: no account, backend, external API or database. |
| 66 | Single-player architecture | Partial | `GameSession` is the local authority over the world and its settlements. There are no AI gods yet. |
| 67–69, 71–74, 95–98 | Multiplayer, ENet, Steam, host disconnect and migration, authority, replication, prediction, cheating | Not started | ADR-003 decides host-authoritative ENet without lockstep. `Pantheon.Networking` is empty. |
| 70 | No required server infrastructure | Implemented | There is none. |
| 75 | Save architecture | Partial | A save holds the seed, the generation settings, the tick, the random state, the regions and the settlements, in one quicksave file, `user://saves/quicksave.json`. There is no GameTime (time is derived from the tick), and the other listed state does not exist yet. |
| 76, 99 | Save principle, persistence format | Implemented | `WorldState` is serialized, never the scene tree. Saves are human-readable JSON with a `SaveVersion` (2) and migrations. |
| 77–78 | Core rule, layered architecture | Implemented | Simulation, then presentation adapters, then Godot. The core projects never reference Godot (tested). |
| 79 | Simulation systems | Partial | `SimulationEngine` runs the world tick and `SettlementSystem` the settlements and their population. The other systems do not exist. |
| 90 | Core data models | Partial | Subsets of `SettlementState` (above) and `RegionState` (Wildlife, Pollution and Weather missing). No GodState, CreatureState, DiplomacyRelation or WorldEvent. |
| 100 | Repository structure | Implemented, with differences | As recommended, plus `World/` in `Pantheon.Domain`, `game/Data/` for content, `game/addons/` for Terrain3D, `.docs/` for the milestone plans and `AGENTS.md`. `game/Assets/`, `game/Audio/` and `build/` do not exist yet. |
| 101 | Architecture decision records | Implemented | ADR-001 to ADR-006 are the six examples; ADR-007 and ADR-008 amend ADR-005 and ADR-004. |
| 102–103 | Testing, deterministic testing | Implemented | Every rule that exists has tests, including §102's "Starving population loses health". Randomness is seeded, and a test checks that the core uses no unseeded randomness or clock. |
| 106 | Debug tools | Partial | Population, region boundaries, simulation tier and the frame rate. Missing: influence, belief, AI intentions, navmesh, weather cells, migration routes, network authority, other performance metrics, and the god AI and Creature views. |
| 107 | Content data | Partial | World generation, settlement balance and presentation are data files, parsed strictly. There are no miracles, abilities or domains yet. |
| 108 | MVP world | Partial | One map with three land biomes. One of eight settlements, and no gods, Creatures or AI players. |
| 131 | Non-goals | Respected | None of them has been built. |
| 132 | Validation priorities | Not yet testable | Each question needs conversion, the Creature, rival gods, diplomacy or disasters. |
| 134 | Coding agent rules | Followed | `AGENTS.md`; `ArchitectureRulesTests` checks the mechanical ones. |

### Sections that state intent

- §1–4 (vision, originality, pillars, core loop): names, art and the interface are original, and
  the only third-party code in the repository is Terrain3D and FastNoiseLite, both MIT. The pillars
  need gods, belief and the Creature, and of the core loop only "Observe world" is possible.
- §133, §135 and §136 (an example story and the north stars): §135's split between simulation and
  presentation is in place for settlements and population, with `GameSession` as the local
  authority. Belief, changing ecology, weather, diplomacy, gods, Creatures and the multiplayer
  authority are still to come. §133's story needs several gods, and §136's goals all start from
  what the player does; the player cannot act on the world yet.

## Architecture decision records

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR/ADR-001-godot-dotnet-pinned-version.md) | Godot 4.x .NET, pinned to 4.7.2-stable | Accepted |
| [ADR-002](ADR/ADR-002-csharp-primary-language.md) | C# as the primary language | Accepted |
| [ADR-003](ADR/ADR-003-host-authoritative-enet-networking.md) | Host-authoritative networking over ENet, no lockstep | Accepted; implementation deferred to Milestones 12 and 13 |
| [ADR-004](ADR/ADR-004-statistical-population-simulation-with-tiering.md) | Statistical population simulation with tiering | Accepted; amended by ADR-008 |
| [ADR-005](ADR/ADR-005-terrain3d-for-terrain.md) | Terrain3D for terrain rendering | Accepted; amended by ADR-007 |
| [ADR-006](ADR/ADR-006-simulation-state-separate-from-scenes.md) | Simulation state is separate from scenes | Accepted |
| [ADR-007](ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md) | Patch Terrain3D's generated shader and report why the fallback is used (Milestone 1) | Accepted; amends ADR-005 |
| [ADR-008](ADR/ADR-008-settlement-step-and-representative-citizens.md) | Settlement step cadence and representative citizens (Milestone 2) | Accepted; amends ADR-004 |

ADR-001 to ADR-006 are the six decisions §101 names. No decision has been reversed. Early in
Milestone 1, ADR-005 was edited in place; it was restored, and the change was recorded as ADR-007.
ADR-003's status line now gives the specification's milestone numbers.

## Tests

The xUnit suite in `src/Pantheon.Tests` has 331 tests, and they all pass headless in about five
seconds, without Godot (`dotnet test Pantheon.sln`). Milestone 1 ended with 169 (re-run on its
branch for this summary), so Milestone 2 added 162.

| Area | Test classes | M1 | Now |
| --- | --- | --- | --- |
| World generation and determinism | `WorldGenerationTests`, `BiomeClassifierTests`, `WorldSeedTests`, `DeterminismTests`, `SimulationRandomTests`, `TerrainRaycasterTests` | 47 | 47 |
| Settlement rules and step (Tier 1) | `SettlementEconomyTests`, `SettlementSystemTests`, `IntervalSystemTests` | 6 | 43 |
| Representative citizens (Tier 2) | `RepresentativeRosterTests`, `CitizenRoutineTests`, `SettlementCrowdTests`, `TierSelectorTests` | 0 | 68 |
| Saves and migration | `SaveLoadTests`, `SaveMigrationTests` | 17 | 34 |
| Session, scheduler, commands, launch options | `GameSessionTests`, `SimulationSchedulerTests`, `SessionCommandsTests`, `LaunchOptionsTests` | 27 | 38 |
| Camera | `GodCameraModelTests`, `EdgeScrollTests` | 27 | 27 |
| Presentation and settlement layout | `PresentationTests`, `SettlementLayoutTests`, `Terrain3DShaderPatchTests` | 16 | 30 |
| Debug readouts and region map | `DebugReadoutTests`, `RegionDebugMapTests` | 12 | 22 |
| Content parsing | `ContentTests` | 6 | 11 |
| Architecture rules | `ArchitectureRulesTests` | 11 | 11 |
| **Total** | | **169** | **331** |

Not covered by automated tests: the Godot client (`game/Scripts`), which is checked by building it
and by headless, windowed and scripted runs; any world with more than one settlement; and Windows
and macOS.

## How to run

You need Godot 4.7.2-stable, .NET edition, the .NET SDK 8.0 or newer, and a GPU with Vulkan
support. Terrain3D is vendored in the repository.

```bash
godot --headless --path game --import    # once after cloning: registers Terrain3D
dotnet build Pantheon.sln                # warnings are errors
dotnet test Pantheon.sln                 # headless, no Godot needed
godot --path game                        # play
```

`godot` stands for the Godot 4.7.2 .NET executable. Other useful runs:

```bash
godot --path game -- --seed=42                                    # another world
godot --path game -- --advance-ticks=9000 --camera-distance=110   # a grown settlement, up close
godot --path game -- --terrain=fallback                           # the fallback terrain renderer
godot --path game -- --seed=1234 --quit-after-frames=40 --screenshot=/tmp/pantheon.png  # smoke run
```

Main controls: W A S D, the arrow keys or the screen edge to pan, the right mouse button to grab
and drag the ground, the mouse wheel to zoom, Q and E to rotate, and F to focus the settlement. F1
shows the debug panel, B the region grid and F3 the region debug map. Space pauses; 1, 2 and 3 set
×1, ×4 and ×16; `.` steps one tick and `,` one settlement step. F5 and F9 quicksave and quickload,
F2 makes a new world, F6 spoils the food stores and F7 brings settlers. The
[README](../README.md) lists every control and option.

To watch the settlement live, press 3 and let it grow for about 90 seconds. The citizens are
shown within 450 m of the settlement, which includes the starting view at 380 m; zoom in to see
them clearly. Then press F7 and F6 to start a famine, and follow health in the F1 panel.

## Known gaps and deviations from the specification

### Scope and scale

- **One settlement.** The MVP world has 8 (§108) and the full game 20–50 (§6). World generation
  founds exactly one. The settlement step, layout, tiers and views loop over every settlement, but
  nothing has run with two: there is no founding, no test uses more than one settlement, and the
  F key focuses the first one.
- **A small world.** One 2 km island instead of 10–20 km (§6), with the biomes Sea, Grassland,
  Forest and Highland. No trees or other vegetation are drawn.
- **A small population.** The seed 1234 settlement levels off at about 540 people (a poor site,
  seed 2, near 210), against 10,000–100,000+ conceptual citizens in the world (§6).

### The settlement model

- The only jobs are farming and building: no gathering, worship or other work. There is no water,
  and food is one abstract quantity (§30, §90).
- Health depends only on food. There is no safety, happiness, wealth, education or stability.
- Houses never decay, and housing never shrinks.
- The model farms the land regions within two regions of the settlement. The fields drawn around
  the village are presentation and do not change production.
- Settlements do not worship, react to danger or respond to miracles (§112).

### The population tiers

- Tier 2 citizens are presentation only (ADR-008). They have no effects, nobody can select or
  carry one, and they are not saved. They gather at the shrine but do not worship, because belief
  does not exist. Tier 3 does not exist.
- Citizens walk in straight lines between spots over the terrain; there is no navigation (§85).
- Citizen poses are evaluated every frame rather than updated at the 2–5 Hz of §89.

### Saves

- One quicksave slot: no named saves and no save menu (§75's example is `world_001.save`). There
  is no GameTime; simulated time is derived from the tick count.

### Platforms, performance and tooling

- **Linux only, no GPU.** The game was built, tested and run only on a Linux x64 virtual machine
  without a GPU, rendering with Mesa's software Vulkan driver (lavapipe) at a few frames per
  second. It has not been run on Windows, macOS (Apple Silicon or Intel) or any real GPU, so the
  1080p, 60 FPS target (§105) is unmeasured.
- macOS may quarantine the unsigned Terrain3D libraries.
- There are no export presets, so there are no packaged builds; an export must include
  `Data/*.json`.
- There are no stress scenarios (§104) and no graphics options (§105).
- Terrain3D 1.0.2's shader is patched at runtime against the software driver's crash and an edge
  line (ADR-007). Both defects are still to be reported upstream. Every start logs a harmless
  Terrain3D deprecation warning.
- Jolt Physics, Godot Navigation and Recast, LimboAI, a utility AI and Blender assets (§63) are
  not used yet, and all geometry is built from Godot primitive meshes.

### Interface

- There is no HUD, panel, player overlay, alert, audio, menu or settings screen (§57–60). The
  player cannot act on the world except through the debug keys.

### Documents written before the specification

- [GAME_SPEC.md](GAME_SPEC.md), the vision summary from Milestone 1, differs from the
  specification in places, and the specification wins:
  - Its earlier roadmap numbers the milestones differently. It is mapped in GAME_SPEC.md, and
    ADR-004 and ADR-008 cite it.
  - It caps Divine Power by total Faith. §9 gives generation as Followers × Faith ×
    WorshipIntensity × ReligiousInfrastructure × situational modifiers, with no cap.
  - It says contested influence weakens every god's miracles. §10 says every god may act there,
    but weaker gods pay more.
  - Its example miracles (Rainfall, Wildfire, Harvest Blessing, Mending Light, Storm Lance) are
    its own. The MVP list is §110: Heal, Food, Water, Rain, Lightning, Fire, Bless Crops,
    Earthquake and Shield.
  - Its roadmap's Campaign & Content milestone conflicts with §131, which rules out a scripted
    campaign for initial development.

## Recommended next steps

### Milestone 3: First God (§118)

Deliverables: Divine Power, influence, belief and basic miracles. Playable outcome: the player can
gain and spend belief. A possible order, building on what exists:

1. **Plan and decide.** Write `.docs/milestone-3-first-god.md`, and add ADRs for what the
   specification leaves open: how belief is stored per god and settlement, how influence is
   computed (the region grid is the natural carrier), how miracles are defined and validated, and
   how the player casts. The Divine Hand (§55) is in no milestone's list, so decide whether
   casting starts with the hand or with a selected miracle and a click. Cursor picking already
   exists.
2. **State.** Add a `GodState` with what Milestone 3 needs from §11 and §90 (Id, Name,
   DivinePower, Followers), and belief per god per settlement with at least Faith, Love, Fear and
   Doubt (§109; Dependency and Resentment may wait). Bump `SaveVersion` to 3 with a migration from
   version 2, tested against a save written by the Milestone 2 build.
3. **Simulation.** A belief system at 0.5–1 Hz (§89), wrapped in `IntervalSystem` like the
   settlement step. Divine Power generation as in §9. Worship as a settlement job (§112), so belief
   competes with farming and building for labour. Influence from followers and the shrine (§10).
4. **Miracles.** Data-driven miracle definitions (§107) in `game/Data`, starting with the §110
   miracles that act through systems that exist: Food (the stores), Heal (health) and Bless Crops
   (the yield). Rain first needs moisture to matter to farming, and Water a water supply, which
   settlements do not have yet. Validate each cast as a host would (§73: enough power, a valid
   target inside influence, no cooldown), and test that a god cannot cast a miracle without
   enough power (§102). Miracles change the belief of the settlements that see them (§15).
5. **Interface and tools.** A HUD with Divine Power, followers, worship generation and the
   selected miracle (§57), influence and belief overlays (§106), and an influence layer on the F3
   map.
6. **More settlements.** §118 does not require them, but belief and influence mean more with
   several settlements, and Milestone 5's competing belief and conversion needs them. Founding
   several early (the MVP has 8, and `worldgen.json` already lists eight names) would also run
   the multi-settlement code paths for the first time.

### Alongside Milestone 3 (not features)

- Run the game on Windows, macOS and a real GPU, and measure the frame rate at 1080p (§62, §105).
- Add export presets for the three platforms (a Milestone 1 follow-up).
- Pull request #1 is merged. Pull request #2 still targets the Milestone 1 branch, so merging it
  there would not update `main`. Retargeting it to `main` leaves its changes the same, because
  `main` merged exactly that branch. Retargeting and merging are the owner's call, and
  Milestone 3 would build on #2.
- Report the two Terrain3D shader defects upstream, and drop the patch once a fixed release is
  vendored.
- Choose Jolt explicitly in `project.godot` with the first physics feature (§63).

### Later milestones

- **4 Creature (§119):** the first Tier 3 entity (§80). It needs Jolt physics, local navigation
  (§85), behaviour trees with LimboAI (§86), needs, and reward and punishment, which need a way to
  interact with it (the Divine Hand, §55). Physics runs inside Godot while ADR-006 keeps canonical
  state out of nodes, so the Creature needs a decision on how physics results reach `WorldState`.
- **5 Rival God (§120):** a second god with utility AI (§47–48, §87) under the same rules,
  competing belief and conversion (§32).
- **6 Diplomacy (§121):** relationships, alliance, war and betrayal memory (§38–40, and
  `DiplomacyRelation` in §90).
- **7 Weather (§122):** weather cells moving across regions (§16), rain and drought. Region state
  starts to change over time and to feed farming.
- **8 Disaster Simulation (§123):** earthquake, wildfire, destruction and the settlements'
  response, with structural abstractions for buildings (§94) and no terrain remeshing (§64).
- **9 Four Gods (§124):** one player and three AI gods, with diplomacy between all of them and
  several strategic personalities.
- **10 Population Movement (§125):** migration groups (§82), refugees and conversion through
  migration.
- **11 Creature Relationships (§126):** rival Creatures, friendship, rivalry, combat and memories.
- **12 and 13 Multiplayer (§127–128):** `Pantheon.Networking` as ADR-003 decides. The session
  commands become intents that the host validates (§73). The F7 debug command sets a
  settlement's population directly, which §73 forbids clients to do, so F7, like F6, must stay a
  host-side debug tool.
- **14 Complete Vertical Slice (§129):** 4 gods, 8 settlements, Creatures, belief, politics,
  weather, disasters, local saves, multiplayer and victory (§115). It is the first build that can
  answer §132's questions.
- **15 Polish and Expansion (§130):** only after the vertical slice is fun.
