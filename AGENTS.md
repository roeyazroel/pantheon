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
- `src/Pantheon.Oracle` holds the language-model adapters (ADR-065). It sits above Game, as the
  client's ENet adapter does: only the client and the tests reference it.
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
- The one exception is the owner's choice of ADR-065 and ADR-067: the player may give the rival
  gods' minds to Claude, GPT or Gemini, with their own key, or to a model on their own computer. It is off by default,
  and every failure falls back to the offline scribes. Keep it that way:
  - a model's answer is an input to the world, taken in between ticks and kept in it;
  - nothing in play waits on a network;
  - the key stays in the profile, and goes only to its provider.
- Add services, CI, package feeds or build tooling only when a task needs them. The tool scripts
  are `tools/vendor-terrain3d.sh`, which fetches Terrain3D's libraries, and `tools/install-godot.sh`,
  which installs Godot and its export templates. The one CI workflow,
  `.github/workflows/build.yml`, tests every push and exports the releases
  ([ADR-075](docs/ADR/ADR-075-builds-in-ci-and-fetched-libraries.md)).
- Commit no build output or native libraries. `build/` and Terrain3D's `bin/` are ignored; the
  libraries come from the pinned, checksummed archive.

### Keep macOS, Windows and Linux working

- Use cross-platform APIs (`Path.Combine`, `user://` for saves) and avoid OS-specific code.
- A native dependency must ship debug and release libraries for Windows, macOS and Linux, and
  must sit behind an adapter with a fallback, as Terrain3D does
  ([ADR-005](docs/ADR/ADR-005-terrain3d-for-terrain.md)) and the player's SQLite profile does
  ([ADR-060](docs/ADR/ADR-060-main-menu-and-local-sqlite-profile.md): `FileProfileStore` when the
  library cannot be loaded). `ArchitectureRulesTests` checks every
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
- Every key goes in the key map, `Pantheon.Game.Controls.KeyGuide` (ADR-068); `ControlsTests`
  fails for an action of `project.godot` it does not name. A key that changes the world outside
  the rules, or a debug view, belongs to its debug group: it runs only while the settings' debug
  tools are on (on, by default, when the game runs from the project), and the god's bar's Debug
  palette gives it a button. A player's command also gets a control in view: a button on the
  god's bar, a panel or a card.
- Current tools: the F1 debug panel (the performance metrics and the simulation's CPU budget, the debris, what streams near the camera with the ground cover and the wildlife, and the world's load, gods, the god AI's goal, persona and temper and top candidates with reasons,
  its plans, the goals it is weighing and the gods it knows, and its diplomatic moves with their factors, influence, the Creature's behaviour, its state and behaviour tree (the gods' watch over their villages shows as alerts and remembered trespasses), face, needs,
  options, learned weights and memories, every Creature's combat skill, scars and views of the
  others, and how yours would meet the nearest one, its attributes, paths and callings, the climate (the season and what it and a heat
  or cold wave change), the weather's spell and cells, the land's life at the cursor (snow,
  wildlife, damage, scar, pollution and biodiversity), the rivers'
  levels and the lakes, earthquakes and fires, the volcano, tornadoes, locust swarms, blighted and
  buried land and the marks on it,
  migration groups and refugee policies, a shared session's players, round trips, traffic per
  second, who decides and the sections a player received and how fresh they are, every god's domains, the holy sites with their
  keepers, claimants and who leads their influence, every treaty (secrets included), favour, promise and the council, every war's goal and score, holy war and god's intelligence of the others, the selected settlement's economy, tier, local navigation and its people's clock,
  water, society and technology, culture, dangers and the houses' integrity, pressure to leave, belief, Old Faith,
  sanctuary and religious style, every followed villager with their needs, options and danger, the land's reshapings, every prophet's walk, last deed and time left, with the selected village's elder,
  the forest standing and the roads, with the selected village's stores, land and roads, and the
  great events, the next eclipse, the sick villages, the beast and each faith's doubt, with the
  selected village's plague),
  the H history panel (the world's chronicle), the gods' territories drawn on the land (your border, glow and wash; rivals' fainter borders), the start screen and pause menu (Esc), the god's bar under the season (a button for every panel, the map's chooser of overlays, the overview, the keys and the menu; with the debug tools, the Debug palette with a button for every debug key), the key map (the bar's Keys button and the settings' Keys tab), the settings' Debug tools and Simple terrain, the settings' Minds tab (who writes the gods, each provider's key and models, Test, and the agents: think for themselves, the quick model, questions an hour and voices; and your angel: kept or not, thinking with the model, and how often it speaks), the talk panel (Speak on a god's card: the conversation, the god's acts and why it would not, and the words it sent unasked), the gods' and Creatures' minds in F1 (who thinks, the questions this hour and the tokens, each mind's last turn: what woke it, why, what came of it and what it said, and a god's journal), the Creatures' words above them, the log's "Mind:" lines, the player's angel in F1 (its stage, lean, the kindness and harm behind it, its marks, who speaks for it, the moments waiting and its last words) with its key (') and its button on the god's bar, the log's "Angel:" lines, the P
  diplomacy panel (whether you have met each god, Seek to meet it (and Bring its Creature with the debug tools), status, how the world sees each god, its war's goal and score and what you know of it, treaties with Break, favours, every treaty and request it would accept, each god's alliances, wars and refugee policy, how your Creature
  and its Creature regard each other, attitude, memories, grievances, every diplomatic move and
  your refugee policy), the G Domains panel (slots, every domain's boon and signature miracle, the
  holy sites and who keeps them, and the Divine Acts to aim), the U council panel (the vote in session, decrees, tabled motions and motions to table), the N Settlements panel (a village's faiths, stores, society,
  technology, buildings and work, doctrines, turn from the gods and feelings, holy wars, its priority, the player's word to its holy war and proxy acts), the settings page, the Shift+B region grid, the F3 region
  debug map (including blessing, influence, soil water, fire, scorch, fresh water, flood, snow,
  wildlife, damage, scar, pollution, blight, rubble, timber and stone), the
  O map overlays (influence, religion, faith, food, weather, danger, migration, culture, nature, population, risk, politics, navigation, authority), the `.` single-tick step, the
  `,` settlement step, F4 (+500 Divine Power), F6 (spoil the food stores), F7 (settlers arrive),
  F8 (next weather spell), F10 (a storm at the cursor), F11 (an earthquake), F12 (a fire), L (a
  flood), M
  (people leave the village at the cursor) and K (the nearest Creature comes to yours), J (the council meets), T (veiled casting), Y (the next season), I (the next hazard at the cursor), ; (the next great event), R, Shift+R and Ctrl+R (the hand lifts, sets down, throws and takes offerings), Tab (the Book of the World: world, religion, miracles, Creature, villager), / and Shift+/ (follow the villager at the cursor or look at them, and let them go), ` (the strategic overview), the launch
  options
  `--seed`, `--load`, `--terrain`, `--advance-ticks`, `--camera-distance`, `--divine-power`,
  `--cast`, `--select`, `--overlay`, `--focus` (a settlement, `creature` or `siteN`), `--judge`,
  `--debug-panel`, `--diplomacy-panel`, `--weather`, `--disaster`, `--migrate`, `--refugees`,
  `--meet`, `--hand`, `--book`, `--overview`, `--prophet`, `--villager`, `--stress`, `--reshape`, `--wildlife`, `--roads`, `--stores`, `--great-event`, `--menu`, `--guide`, `--history-panel`, `--pause-menu`, `--speed`, `--species`, `--temperament`,
  `--rank`, `--domains`, `--domains-panel`, `--settings-page`, `--minds`, `--pantheon`, `--mind-check`, `--talk`, `--talk-say`, `--title-page` (`keys` for the settings' Keys tab), `--bar`, `--tutorial`, `--fight`, `--work`, `--visit`, `--music`, `--music-export`, `--agents`, `--wake`, `--angel`, `--angel-ask`, `--priority`, `--settlement-panel`, `--doctrine`, `--treaty`, `--council-panel`, `--summon-council`, `--gods-met`, `--war-goal`, `--holy-war`, `--veil`, `--world-size`, `--victory`, `--seasons`, `--season`, and for shared
  sessions `--host`, `--join`, `--name`, `--god` and `--upnp`,
  and scripted smoke runs with
  `--quit-after-frames` and `--screenshot`, which print frame times, the performance lines with a
  save timed and, in shared sessions, the network lines. The README lists them.

## Where things go

| Change | Place |
| --- | --- |
| State and content contracts | `src/Pantheon.Domain` |
| World generation and tick systems | `src/Pantheon.Simulation`, registered in `SimulationEngine` |
| Saves, content parsing and the player's profile | `src/Pantheon.Persistence` |
| Multiplayer protocol, codecs and transport contract | `src/Pantheon.Networking` (the host and player logic in `src/Pantheon.Game/Multiplayer`, ENet in `game/Scripts/Network`) |
| The gods' minds: the language-model contract, prompts and the scribes, and the adapters | `src/Pantheon.Game/Minds`; Claude, GPT, Gemini and Ollama in `src/Pantheon.Oracle` |
| Session, commands, scheduler, camera maths, debug readouts, presentation data, Tier 2 citizens | `src/Pantheon.Game` |
| Nodes, input, UI | `game/Scripts`, with scenes in `game/Scenes` and actions in `game/project.godot` |
| Content | `game/Data/*.json` |
| Tests | `src/Pantheon.Tests` |
| CI and release builds | `.github/workflows/build.yml`, with its scripts in `tools/` |
| Decisions | `docs/ADR/` |

## Commands

```bash
tools/vendor-terrain3d.sh                # once after cloning: fetches Terrain3D's libraries
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
