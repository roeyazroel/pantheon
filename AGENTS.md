# Working on PANTHEON

Read this before changing the repository, whether you are a person or a coding agent.

The owner's complete specification, [docs/PANTHEON_SPEC.md](docs/PANTHEON_SPEC.md), is the source
of truth for what to build and how. Where this file, `docs/GAME_SPEC.md`, `docs/ARCHITECTURE.md`,
a milestone plan or the code disagrees with it, the specification wins. An accepted ADR still
changes only through a new ADR, as below. The specification is the owner's document: keep it
verbatim and do not edit it. What is built so far, section by section against the
specification, is in [docs/PROGRESS_SUMMARY.md](docs/PROGRESS_SUMMARY.md).

The rules below are the specification's coding agent rules (section 134) and are binding. Each
rule is followed by the conventions that meet it in this repository.

## Rules

### Build incrementally; every milestone leaves the game playable

- The milestones, with their deliverables and playable outcomes, are sections 116–130 of the
  specification. Cite milestones by those numbers.
- Each milestone has a plan in `.docs/`, for example `.docs/milestone-8-disasters.md`, with its
  architecture, diagrams, code plan, tasks and test plan. The plan is the contract for the
  milestone. Update its tasks and results in the same change as the code.
- Work in small steps that each build, pass the tests and still launch into an explorable world.

### Simulation and presentation are separate; the core simulation is testable without rendering

- The core projects (`src/Pantheon.Domain`, `Simulation`, `Persistence`, `Game` and
  `Networking`) never reference Godot. Godot code lives only in `game/Scripts`
  (namespace `Pantheon.Client`).
- References point down the layers only: Domain has no references; Simulation, Persistence and
  Networking use Domain; and Game uses all four. `ArchitectureRulesTests` enforces this.
- Simulation behaviour is tested headless with xUnit in `src/Pantheon.Tests`.

### `WorldState` is authoritative; SceneTree nodes are never canonical domain objects

- Game state lives in `WorldState` (`Pantheon.Domain`). Simulation systems change it during
  ticks, and `GameSession` and `SessionCommands` change it for player commands. Presenters only
  read it and build nodes from it.
- Never keep gameplay state in nodes, and never serialize the SceneTree
  ([ADR-006](docs/ADR/ADR-006-simulation-state-separate-from-scenes.md)). Saves are versioned
  JSON of `WorldState`. A change to the save shape bumps `WorldSaveSerializer.CurrentSaveVersion`
  and adds a `SaveMigrations` step from the previous version, tested against a save written by
  the previous build (`src/Pantheon.Tests/Persistence/Fixtures/`).
- Derived presentation data, such as the settlement layout and the Tier 2 representative
  citizens (`Pantheon.Game.Presentation` and `Pantheon.Game.Population`), is computed from
  `WorldState`, never written back and never saved
  ([ADR-008](docs/ADR/ADR-008-settlement-step-and-representative-citizens.md)).

### Prefer data-driven content

- Tunable numbers, names and colours go in `game/Data/*.json`. Content is parsed strictly:
  unknown members and out-of-range values are errors, and `ContentTests` covers the shipped
  files.

### Don't optimize before profiling

- Write the simple version first. Optimise only after a measurement (the Godot profiler, frame
  times, a benchmark) shows the cost, and record the numbers in the plan or the pull request.

### No infrastructure without need; no cloud dependency

- The game runs fully offline: no backend, accounts, telemetry or downloads at runtime.
- Add services, CI, package feeds or build tooling only when a task needs them. The one tool
  script, `tools/vendor-terrain3d.sh`, reproduces vendored code.

### Keep macOS, Windows and Linux working

- Use cross-platform APIs (`Path.Combine`, `user://` for saves) and avoid OS-specific code.
- A native dependency must ship debug and release libraries for Windows, macOS and Linux, and
  must sit behind an adapter with a fallback, as Terrain3D does
  ([ADR-005](docs/ADR/ADR-005-terrain3d-for-terrain.md)). `ArchitectureRulesTests` checks every
  `.gdextension` manifest under `game/addons`.
- Development scripts are bash. On Windows, run them from Git Bash or WSL.

### Seeded randomness

- Simulation randomness comes from `SimulationRandom` streams derived from the `WorldSeed` with
  `SeedMixer`, and the RNG state is saved in `WorldState.Random`. A system may instead draw a
  fresh stream each step from the seed and the tick (`SeedMixer.Derive(seed, purpose, tick)`), as
  the weather and disasters do, so it needs no saved state and never shifts the shared stream
  ([ADR-013](docs/ADR/ADR-013-weather-cells-and-soil-water.md)).
- Core code never uses `System.Random`, the clock, tick counts or GUIDs. `ArchitectureRulesTests`
  scans the compiled core assemblies for them.
- The one exception is the Godot client picking a fresh seed for a new world (F2). The seed is
  shown in the overlay and saved, so the world can be reproduced.
- Presentation variation (house sizes, field crops, citizens' homes and routines) comes from
  `PresentationHash` of the seed, ids and slots, never from a simulation stream, so showing
  something never changes what the simulation does next.

### Automated tests for important simulation rules

- Every simulation rule (production, consumption, storage, births, deaths) and every guarantee
  about determinism or saves has an xUnit test. The same seed and the same inputs give the same
  state, including across a save and load.

### Document architecture changes; never silently replace an architecture decision

- `docs/ARCHITECTURE.md` (including its Mermaid diagrams) and the milestone plan change in the
  same pull request as the code they describe.
- Accepted ADRs in `docs/ADR/` are never edited, except for their status line. To change a
  decision, add a new ADR that supersedes or amends it, as
  [docs/ADR/README.md](docs/ADR/README.md) describes. For example,
  [ADR-007](docs/ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md) amends ADR-005.

### Gameplay systems over graphical polish; emergent interactions over scripted content

- Simulation systems and their rules come before visual effects. Polish waits until the gameplay
  it decorates exists.
- Behaviour comes from systems interacting through shared state (food, population, fertility,
  and later belief), not from scripted events or hand-placed outcomes.

### Debug tools early

- Every new system ships with a way to see and drive it: a debug overlay readout, a layer on the
  F3 region debug map, a hotkey or a launch option. Build readouts as pure functions in
  `Pantheon.Game.Diagnostics`, with tests, and keep the client to displaying them.
- Current tools: the F1 debug panel (gods, the god AI's goal and top candidates with reasons
  and its diplomatic moves with their factors, influence, the Creature's behaviour, needs,
  options, learned weights and memories, every Creature's combat skill, scars and views of the
  others, and how yours would meet the nearest one, the weather's spell and cells, the rivers'
  levels and the lakes, earthquakes and fires,
  migration groups and refugee policies, a shared session's players, round trips, traffic per
  second and the sections a player received, every god's domains, the holy sites with their
  keepers, claimants and who leads their influence, every treaty (secrets included), favour, promise and the council, the selected settlement's economy, tier,
  water, society and technology, culture, dangers, pressure to leave, belief, Old Faith and
  sanctuary),
  the H history panel (the world's chronicle), the start screen and pause menu (Esc), the P
  diplomacy panel (status, how the world sees each god, treaties with Break, favours, every treaty and request it would accept, each god's alliances, wars and refugee policy, how your Creature
  and its Creature regard each other, attitude, memories, grievances, every diplomatic move and
  your refugee policy), the G Domains panel (slots, every domain's boon and signature miracle, the
  holy sites and who keeps them), the U council panel (the vote in session, decrees, tabled motions and motions to table), the N Settlements panel (a village's faiths, stores, society,
  technology, buildings and work, doctrines, turn from the gods and feelings, and its priority), the settings page, the B region grid, the F3 region
  debug map (including blessing, influence, soil water, fire, scorch, fresh water and flood), the
  O map overlays (influence, religion, faith, food, weather, danger, migration, culture), the `.` single-tick step, the
  `,` settlement step, F4 (+500 Divine Power), F6 (spoil the food stores), F7 (settlers arrive),
  F8 (next weather spell), F10 (a storm at the cursor), F11 (an earthquake), F12 (a fire), L (a
  flood), M
  (people leave the village at the cursor) and K (the nearest Creature comes to yours), J (the council meets), the launch
  options
  `--seed`, `--load`, `--terrain`, `--advance-ticks`, `--camera-distance`, `--divine-power`,
  `--cast`, `--select`, `--overlay`, `--focus` (a settlement, `creature` or `siteN`), `--judge`,
  `--debug-panel`, `--diplomacy-panel`, `--weather`, `--disaster`, `--migrate`, `--refugees`,
  `--meet`, `--menu`, `--guide`, `--history-panel`, `--pause-menu`, `--speed`, `--species`,
  `--rank`, `--domains`, `--domains-panel`, `--settings-page`, `--priority`, `--settlement-panel`, `--doctrine`, `--treaty`, `--council-panel`, `--summon-council`, and for shared
  sessions `--host`, `--join`, `--name`, `--god` and `--upnp`,
  and scripted smoke runs with
  `--quit-after-frames` and `--screenshot`, which print frame times and, in shared sessions, the
  network lines. The README lists them.

## Where things go

| Change | Place |
| --- | --- |
| State and content contracts | `src/Pantheon.Domain` |
| World generation and tick systems | `src/Pantheon.Simulation`, registered in `SimulationEngine` |
| Saves and content parsing | `src/Pantheon.Persistence` |
| Multiplayer protocol, codecs and transport contract | `src/Pantheon.Networking` (the host and player logic in `src/Pantheon.Game/Multiplayer`, ENet in `game/Scripts/Network`) |
| Session, commands, scheduler, camera maths, debug readouts, presentation data, Tier 2 citizens | `src/Pantheon.Game` |
| Nodes, input, UI | `game/Scripts`, with scenes in `game/Scenes` and actions in `game/project.godot` |
| Content | `game/Data/*.json` |
| Tests | `src/Pantheon.Tests` |
| Decisions | `docs/ADR/` |

## Commands

```bash
godot --headless --path game --import    # once after cloning: registers Terrain3D
dotnet build Pantheon.sln                # warnings are errors
dotnet test Pantheon.sln                 # headless, no Godot needed
godot --path game                        # play
godot --path game -- --seed=1234 --quit-after-frames=40 --screenshot=/tmp/pantheon.png
godot --path game -- --seed=1234 --advance-ticks=9000 --camera-distance=110 --quit-after-frames=30 --screenshot=/tmp/grown.png
godot --path game -- --terrain=fallback  # check the fallback renderer
```

`godot` stands for the Godot 4.7.2 .NET executable.

## Code conventions

- C# on .NET 8 with nullable reference types and warnings as errors. Core public members have
  XML documentation comments.
- Switch expressions over enums list every member and have no discard arm, so adding a member
  fails the build until every switch handles it.
- Keep classes under 500 lines and functions small. Vendored code is exempt.
- Compare strings ordinally and format numbers with the invariant culture.
- Bash scripts start with `set -euo pipefail`, quote their variables, support `--help` and pass
  `shellcheck`.

## Definition of done

- `dotnet build Pantheon.sln` reports no warnings and `dotnet test Pantheon.sln` passes.
- The game starts and stays playable with both terrain renderers.
- New simulation rules have tests, and new systems have debug tooling.
- The README (controls and options), `docs/ARCHITECTURE.md`, the milestone plan and, for
  decisions, a new ADR are updated. When a milestone is finished, so is
  `docs/PROGRESS_SUMMARY.md`.
