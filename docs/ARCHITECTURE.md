# PANTHEON — Architecture

This document describes how the code is organised and the rules that keep the simulation
independent of the engine. The owner's complete specification,
[PANTHEON_SPEC.md](PANTHEON_SPEC.md), is the source of truth for the architecture it describes,
and [PROGRESS_SUMMARY.md](PROGRESS_SUMMARY.md) maps what is built so far against it. Decisions
are recorded in [ADR/](ADR/README.md), and the coding rules every change follows are in
[AGENTS.md](../AGENTS.md).

## Guiding rules

1. **`WorldState` is the source of truth** (ADR-006). It is plain C# data in `Pantheon.Domain`.
2. **Godot nodes are presentation only.** Presentation adapters build and update nodes from
   `WorldState` and derived data. Nodes never hold authoritative state.
3. **Never serialize the SceneTree.** Saves are versioned JSON of `WorldState`. Loading rebuilds
   visuals.
4. **Core code has no Godot dependency** and is tested headless (`dotnet test`).
5. **Seeded randomness only.** Core systems use `SimulationRandom` (xoshiro256\*\*) derived from
   the `WorldSeed`. RNG state lives in `WorldState.Random`, so saves resume the same sequence.
   No global or ambient randomness in core systems: `ArchitectureRulesTests` scans the core
   assemblies for `System.Random`, clock and tick-count reads and GUIDs.
6. **Data-driven content** where practical (`game/Data/*.json`), parsed strictly (unknown or
   missing members are errors).
7. **No speculative infrastructure or optimisation.** Reserved areas (weather, networking)
   contain only a README until their milestone.
8. **Commands go through an authority.** A player's intent (casting a miracle, rewarding,
   punishing or sending the Creature, a diplomatic move) is a request that `MiracleCaster`,
   `CreatureAuthority` or `DiplomacyAuthority` checks against the world before anything changes
   (spec §73, ADR-009, ADR-010, ADR-012). The engine owns them as one `Authorities` bundle.
   `GameSession` is the local authority; a multiplayer host will run the same checks. AI gods
   send the same requests through the same authorities (ADR-011).

## Repository layout

```text
Pantheon.sln                  Root solution (Godot editor uses it via dotnet/project/solution_directory)
Directory.Build.props         Shared C# settings (nullable, warnings as errors)
AGENTS.md                     The owner's coding rules and the conventions that meet them
game/                         Godot 4.7.2 project (res://)
  project.godot               Project settings, input map, .NET settings
  Pantheon.csproj             Godot.NET.Sdk/4.7.2 project: assembly "Pantheon", namespace Pantheon.Client
  Scenes/                     Main.tscn, Camera/GodCamera.tscn, UI/DebugOverlay.tscn
  Scripts/                    Bootstrap, Camera, Presentation (adapters), UI
  Data/                       worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                              creatures.json, god_ai.json, diplomacy.json (simulation); settlement_view.json,
                              biome_palette.json, divine_view.json (presentation)
  Materials/ Shaders/         Presentation resources (.tres, .gdshader)
  Assets/ Audio/              Original art and audio (none yet; created with the first asset)
  addons/terrain_3d/          Vendored Terrain3D 1.0.2 runtime subset (ADR-005)
src/
  Pantheon.Domain/            State and content contracts: World, Ecology, Settlements, Gods, Belief, Miracles,
                              Creatures, Diplomacy, Events (+ reserved folders)
  Pantheon.Simulation/        Randomness, Noise (FastNoiseLite), Terrain, WorldGen, Settlements, Belief, Miracles,
                              Creatures, Navigation, Gods (AI), Diplomacy, Systems, SimulationEngine
  Pantheon.Persistence/       Versioned JSON saves with migrations, content parsing
  Pantheon.Game/              Engine-agnostic application layer: GameSession, scheduler, camera model,
                              diagnostics, HUD model, alerts, overlays, settlement layout,
                              Tier 2 representative citizens (Population)
  Pantheon.Networking/        Reserved for host-authoritative ENet (ADR-003)
  Pantheon.Tests/             xUnit tests (headless)
tools/                        Maintenance scripts (vendor-terrain3d.sh)
docs/                         PANTHEON_SPEC.md (the owner's specification), PROGRESS_SUMMARY.md,
                              GAME_SPEC.md, ARCHITECTURE.md, ADR/
.docs/                        Milestone task files (plan / progress contracts)
```

### Why the Godot code is in `game/Scripts` rather than `src/Pantheon.Game`

Godot requires the C# project file next to `project.godot`, and node scripts must be inside
`res://`. The Godot-facing code (bootstrap, input, presentation adapters, UI) therefore lives in
`game/Scripts` and compiles into the `Pantheon` assembly. `src/Pantheon.Game` keeps the
engine-agnostic application logic: the session lifecycle, the fixed-step scheduler, camera math
and debug readouts. That logic stays testable, and the separation between simulation and
presentation is preserved.

## Dependency graph

```mermaid
graph TD
    Client["game/Pantheon.csproj<br/>(Godot client: Pantheon.Client)"]
    Game["Pantheon.Game"]
    Sim["Pantheon.Simulation"]
    Persist["Pantheon.Persistence"]
    Domain["Pantheon.Domain"]
    Net["Pantheon.Networking (reserved)"]
    Tests["Pantheon.Tests"]
    T3D["Terrain3D GDExtension (optional at runtime)"]

    Client --> Game
    Client --> Sim
    Client --> Persist
    Client --> Domain
    Client -. "ClassDB dynamic calls" .-> T3D
    Game --> Sim
    Game --> Persist
    Game --> Domain
    Sim --> Domain
    Persist --> Domain
    Tests --> Game
    Tests --> Sim
    Tests --> Persist
    Tests --> Domain
```

Allowed references point down the graph only. `Pantheon.Domain` references nothing.
Only `game/Pantheon.csproj` references Godot. `ArchitectureRulesTests` reads the core project
files and compiled assemblies and fails when either rule is broken.

## Layers

### Domain (`Pantheon.Domain`)

- `WorldState`: `Seed`, `Generation` (a copy of the `WorldGenSettings` used), `Tick`, `Random`
  (RNG state), `Regions` (`RegionGrid`), `Settlements`, `Gods`, `Creatures`, `Diplomacy` and
  `WinnerGodId` (once a god has won, spec §115).
- `RegionGrid`: `Columns × Rows` cells of `RegionState`, each `RegionSize` metres square, with a
  row-major `Id = row * Columns + column`.
- `RegionState`: `Id`, `Biome`, `Elevation` (mean metres, sea level = 0), `Temperature`,
  `Moisture`, `Fertility`, `Vegetation` (0..1) and `Blessing` (0..1, from miracles; it fades).
- `SettlementState`: `Id`, `Name`, `RegionId`, `Position`, `Population`, `Food`, `Housing`
  (capacity in people), `Health` (0..1) and `ConstructionProgress` (work done on the one house
  being built; 0 when none is under way) and `Beliefs`. These aggregates are the canonical
  population (Tier 1, ADR-004 and ADR-008).
- `GodBelief` (one per god a settlement knows): `GodId`, `Adherence` (the share of the people who
  follow the god; the shares add up to at most 1) and `Faith`, `Love`, `Fear`, `Doubt` (0..1).
  Followers are `Population × Adherence` and are never stored (ADR-009).
- `GodState`: `Id`, `Name`, `Controller` (Human or Ai), `Personality` (`None` for players;
  Guardian, Tyrant, Opportunist, Zealot or NatureKeeper for AI gods), `DivinePower`, `Rank`
  (never falls), `DominantSinceTick` (while it holds the victory share), `CreatureId` and
  `Cooldowns`. Followers and influence are derived from the beliefs.
- `CreatureState` (ADR-010): a Tier 3 entity saved whole: position and heading, level, experience,
  health, loyalty, morality, `Needs` (hunger, fatigue, boredom), `Personality` (kindness,
  aggression, laziness, curiosity), `Activity` (action, situation, phase, target, path, ticks
  left), `Preferences` (learned weight per situation and action) and `Memories` (recent deeds with
  the god's feedback). `CreatureCommand` (reward, punish, go to) is the intent the authority
  checks. `World/SimulationTime` fixes 10 ticks per simulated second.
- `CastMiracleRequest` (god, miracle id, target): the intent that the authority checks (§73).
- `DiplomacyState` (ADR-012): `Relations` (one `GodRelation` per observer and subject: trust,
  fear, respect, rivalry, gratitude, suspicion and the `DiplomaticMemory` of acts), `Stances`
  (one `GodStance` per pair: `DiplomaticStatus` Alliance, Friendly, Neutral, Hostile or War, since
  when, and the last proposal) and `Proposals` (alliance or peace, waiting for an answer).
  `DiplomaticCommand` (offer alliance, break alliance, declare war, request peace, accept,
  decline) is the intent the diplomacy authority checks.
- `Events/`: transient `WorldEvent` records (`MiracleCast`, `FollowingChanged`, `RankAchieved`,
  `FamineStarted`, `DiplomacyChanged` and others) raised during a tick or a command and never
  saved.
- Content contracts: `WorldGenSettings` (and its nested settings records), `SettlementRules`
  (the per-step balance of the settlement model, `game/Data/settlement_rules.json`),
  `DivineRules` (belief, worship, power, influence and ranks, `divine_rules.json`),
  `MiracleCatalog` and `MiracleDefinition` (`miracles.json`), `PantheonSetup` (the gods of a
  new world, their personalities and their Creatures' names, `gods.json`), `CreatureRules`
  (`creatures.json`), `GodAiRules` with one `GodProfile` per personality, including its
  diplomacy weights (`god_ai.json`), and `DiplomacyRules` (`diplomacy.json`).

### Simulation (`Pantheon.Simulation`)

- `Randomness/SimulationRandom`: xoshiro256\*\* PRNG with explicit, serializable state.
  `SeedMixer` derives independent streams (`"terrain.elevation"`, `"simulation"`,
  `"settlement.names"`) from one seed.
- `Noise/FastNoiseLite`: vendored C# FastNoiseLite 1.1.1 (MIT), kept unmodified apart from a
  namespace and `internal` visibility. As third-party code it is exempt from the project's
  class-size rule.
- `Terrain/TerrainGenerator` produces a `TerrainField` (derived data, never saved):
  1. Continent fBm plus ridged mountain noise, shaped by a radial island falloff.
  2. Normalised elevation is remapped to metres (land exponent for flatter lowlands).
  3. Moisture (noise plus coastal bonus) and temperature (noise minus elevation lapse rate).
  4. Each sample is classified by data-driven `BiomeRule`s (first match wins).
- `WorldGen/RegionBuilder` aggregates samples per region (means plus majority biome) and derives
  fertility and vegetation. `SettlementFounder.FoundAll` places up to `Count` settlements (8) on
  the best habitable regions, at least `MinimumSpacing` regions apart so their farmland never
  overlaps, with names and populations drawn from seeded streams. `PantheonFounder` creates the
  gods of `gods.json`, each followed in its own home settlement, best site first.
- `Settlements/SettlementEconomy`: the settlement rules as pure functions: the farmland survey,
  the labour split into farmers and builders, production with diminishing returns,
  consumption, storage, health, birth and death chances, and `Assess`, which previews the next
  step for the debug panel. `Settlements/SettlementSystem` applies one Tier 1 step to every
  settlement (see [Settlement simulation and population tiers](#settlement-simulation-and-population-tiers)).
  `SettlementRulesValidator` lists every invalid rule.
- `Belief/BeliefModel`: the belief rules as pure functions (conditions, drift, pull and push,
  conversion within the free share, worshippers, worship intensity, Divine Power, impulses).
  `BeliefSystem` applies one belief step (see [Gods, belief and miracles](#gods-belief-and-miracles)).
  `InfluenceField` derives a god's influence from its followers. `GodStanding` counts followers,
  sums power and picks ranks.
- `Miracles/MiracleBook` validates and indexes the miracles; `MiracleCaster` checks requests
  (§73), computes costs and applies accepted casts.
- `Creatures/`: `CreatureModel` (growth, needs and learning as pure functions), `CreatureBrain`
  (options and utilities, decisions, planned routes), `CreatureDeeds` (what a finished action does
  to the world), `CreatureSystem` (the per-tick state machine), `CreatureAuthority` (rewards,
  punishments and go-to, checked) and `CreatureFounder` (see [The Creature](#the-creature)).
  `Navigation/RegionPathfinder` runs A* over land regions and smooths the path.
- `Gods/GodAi` is the strategic utility AI of AI gods (see [Rival gods](#rival-gods)), run by
  `GodAiSystem`; `Gods/GodDiplomat` adds its diplomatic moves. `Belief/VictorySystem` tracks
  dominance and victory.
- `Diplomacy/`: `DiplomacyModel` (finding and creating relations and stances, grudge, warmth),
  `DiplomacyLedger` (the `IActRecorder` that miracles, Creature deeds and conversions report
  acts to), `DiplomacySystem` (the diplomacy step) and `DiplomacyAuthority` (diplomatic commands,
  checked). See [Diplomacy](#diplomacy).
- `Systems/IntervalSystem` runs a wrapped system on the last tick of every interval of N ticks.
  `Systems/Authorities` bundles the miracle, Creature and diplomacy authorities, which share one
  ledger.
- `SimulationEngine.Step(WorldState, events)` runs the systems in order (the settlement step, the
  belief step, the victory check and the diplomacy step, each in an `IntervalSystem`, then the
  Creature system every tick, then the AI gods every `EvaluationIntervalTicks`) with a
  `SimulationRandom` restored from `WorldState.Random`, collects the events they raise, writes
  the RNG state back and increments `Tick`.
- `Terrain/TerrainRaycaster`: ray against the heightfield (used for cursor picking).

### Persistence (`Pantheon.Persistence`)

- Save envelope: `{ "SaveVersion": 6, "World": { ... } }`, indented, enums as strings,
  property names exactly as in C#, strict members.
- Loading reads `SaveVersion` first. A current save deserializes directly. An older save, back
  to `MinimumSupportedSaveVersion` (1), is parsed into a `JsonObject`, upgraded one version at
  a time by `SaveMigrations`, then deserialized with the same strict options, so a migrated
  save gets exactly the checks of a current one. Version 1 → 2 gives every settlement
  `"Health": 1` and `"ConstructionProgress": 0`. Version 2 → 3 adds the Milestone 3 player god
  (Ilyra, 100 Divine Power) followed by 60% of the first settlement, empty beliefs elsewhere, no
  blessing on any region and founding settings for one settlement. Version 3 → 4 gives every god
  the Milestone 4 newborn Creature, Bramble, at the settlement where most of the people follow
  it. Version 4 → 5 gives every god no personality and no dominance, and the world no winner.
  Version 5 → 6 adds empty diplomacy: no relations, no stances (every pair neutral) and no
  proposals. A missing, unsupported or newer version raises `SaveFormatException` with a clear
  message.
- `SaveIntegrity` then checks the world's structure and lists every inconsistency: the grid
  against the generation settings, settlement ids and regions, non-negative quantities, health
  and blessing in 0..1, god ids and Divine Power, and beliefs (known gods, one each, values in
  0..1, shares adding up to at most 1), Creatures (`CreatureIntegrity`: owners, home and
  target settlements, levels, values in range) and diplomacy (`DiplomacyIntegrity`: known gods,
  one relation per observer and subject and one stance per pair, values in range).
- Saves are written atomically (temporary file, then rename).
- `ContentJson` parses the content files with the same strict options (comments and trailing
  commas allowed).

### Application (`Pantheon.Game`)

- `GameSession`: creates or loads worlds, owns `WorldState` + `TerrainField`, runs the
  scheduler, saves and loads, and raises `WorldReplaced` so presentation rebuilds. It is the local
  authority: `LocalGodId` is the first human god, `CheckCast` and `FindReached` preview a cast,
  `CastMiracle` sends the request through `MiracleCaster` with the local god as issuer, and
  `DrainEvents` hands the tick and command events to the client. `CommandCreature` sends the
  local Creature's commands through `CreatureAuthority`; `FindJudgedAction` and
  `PreviewCreatureOptions` feed the HUD card and the debug view, and `PreviewGodAi` the god AI
  view. `CommandDiplomacy` and `CheckDiplomacy` send and preview the local god's diplomatic moves
  through `DiplomacyAuthority`, and `PreviewGodDiplomacy` lists an AI god's diplomatic
  candidates.
  `StepOneTick` runs one tick through the same engine call as the scheduler; `RunTicks` and
  `StepToNextSettlementStep` run several. `SimulationSeconds` is the simulation time including
  the scheduler's progress towards the next tick, which drives the citizens' routines.
- `SessionCommands`: the player commands behind the world and simulation hotkeys (quicksave,
  quickload, new world, pause, speed presets, single-tick step, settlement step) and the debug
  drivers (spoil the food stores, add settlers, advance ticks at launch), plus casting a miracle,
  stepping the speed presets and the F4 grant of Divine Power. Each returns the status line shown
  in the overlay.
- `SimulationScheduler`: fixed-step accumulator (10 ticks/s × time scale, pause, a cap on
  ticks per frame so a long frame cannot cause a spiral of death).
- `Camera/GodCameraModel`: engine-agnostic god-camera math: focus, yaw and distance targets
  with exponential smoothing, pitch from zoom, bounds, terrain clearance, and the zoom bands
  Close (≤ 120 m), Regional (≤ 700 m) and Strategic (≤ 2600 m).
- `Diagnostics/DebugReadout`: formats the debug overlay model from state (pure), with
  `SettlementReadout` for the settlement lines. `Diagnostics/RegionDebugMap` builds the F3
  region debug map image (pure).
- `Launch/LaunchOptions`: parses user arguments `--seed=`, `--load=`,
  `--terrain=auto|terrain3d|fallback`, `--advance-ticks=`, `--camera-distance=`,
  `--quit-after-frames=` and `--screenshot=`. Bad input becomes a warning, never an error.
- `Presentation/*`: engine-agnostic presentation data shared by both terrain renderers (colour
  map, fallback mesh chunks, water depth map, region lines, labels) and `Terrain3DShaderPatch`.
  `SettlementLayout.ComputeSite` lays out a settlement's houses, building site and fields as a
  `SettlementSite`, tuned by `SettlementViewSettings` (`game/Data/settlement_view.json`).
  `PresentationHash` (SplitMix64) gives presentation variation without consuming a simulation
  random stream. `PresentationContent` bundles the biome palette and the settlement view
  settings.
- `Hud/HudReadout` builds the player HUD model (spec §57): god and rank, Divine Power and income,
  followers and the next rank, the miracle bar (hotkey, cost, cooldown, locked, affordable), the
  target hint and the Creature card (level, activity, needs, loyalty, morality, what feedback would
  judge), the rival gods with their status towards the player, and the race for victory.
  `Divine/DivineText` words ranks, personalities, clocks and cast verdicts;
  `Creatures/CreatureText` words Creature activities and command outcomes.
- `Diplomacy/DiplomacyReadout` builds the diplomacy panel's entries (status, the rival's
  attitude and why, the player's grievances, the proposal waiting and the moves allowed now), and
  `DiplomacyText` words statuses, commands, memories and rejections.
- `Alerts/AlertFeed` turns events into alerts for the local player (§59): only those about the
  player's god or followers, never the same alert twice within 30 simulated seconds, the most
  urgent kept when full.
- `Presentation/OverlayMap` builds the map overlays (§58): influence (each region in the colour of
  the strongest god, opacity by its influence), religion (each settlement's lands in its main god's
  colour), faith and food (each settlement's lands on a red-to-green ramp).
  `DivineViewSettings` (`divine_view.json`) holds the god colours, overlay opacity and miracle
  looks. `SettlementLabel` adds the main god's share; `SettlementPicker` finds the settlement
  nearest a point.
- `Population/*`: Tier 2 representative citizens, derived from `WorldState` and never saved:
  `TierSelector`, `RepresentativeRoster`, `CitizenRoutine` and `SettlementCrowd` (see
  [Settlement simulation and population tiers](#settlement-simulation-and-population-tiers)).

### Godot client (`game/Scripts`, namespace `Pantheon.Client`)

- `Bootstrap/GameRoot`: composition root. Loads content (`GodotContentLoader`: the game
  content and the presentation content), creates the session, wires presenters, camera and
  overlay, routes hotkeys and picks the region under the cursor.
  `Bootstrap/SmokeTestDriver` implements `--quit-after-frames` and `--screenshot`.
- `Camera/GodCameraController`: maps input (WASD, edge scroll, right-drag grab, wheel, Q/E,
  middle-drag) to `GodCameraModel` and applies the pose to `Camera3D`.
- `Presentation/WorldPresenter`: rebuilds and syncs all world visuals:
  - `Terrain/ITerrainPresenter` with `Terrain3DTerrainPresenter` (primary) and
    `FallbackMeshTerrainPresenter` (fallback, logged and labelled with its reason), chosen by
    `TerrainPresenterFactory` (ADR-005). The Terrain3D adapter switches Terrain3D to a copy of
    its generated shader with bounds-safe region lookups and a colour lookup that also covers the
    world's east and south edges (ADR-007, which amends ADR-005).
  - `Water/WaterPresenter`: sea-level plane (`Materials/water.tres`, `Shaders/water.gdshader`)
    tinted by a baked depth map, so it does not read the depth buffer. Waves flatten and the
    surface gets rougher with camera distance, which avoids flickering sun glints.
  - `Settlements/SettlementPresenter`: one `SettlementView` per settlement with the shrine and
    the name/population label, plus:
    - `SiteView`: the houses (one MultiMesh per hut part) and the fields (crop rows tinted
      through instance colours), rewritten only when their placements change;
    - `ConstructionSiteView`: the house being built, as scaffold poles, a timber pile, walls
      that rise during the first 70% of the work and a roof during the rest;
    - `CitizenView`: the settlement's Tier 2 representatives in one MultiMesh of a two-surface
      figure (a body tinted per role, a head), recoloured when the roster changes.
  - `Regions/RegionBoundaryOverlay`: region grid lines draped on the terrain, plus a hover highlight.
- `UI/DebugOverlay`: FPS, tick, seed, terrain backend, camera band, region under cursor,
  settlement readouts, boundary toggle and status messages, plus the F3 region debug map. Hidden
  at the start; F1 shows it.
- `UI/Hud`: the player HUD, built in code: the top bar, the miracle bar with the target hint and
  the clickable alerts. It shows `HudModel`s and reports clicks.
- `UI/DiplomacyPanel`: the diplomacy panel (P), built in code from `DiplomacyReadout` entries. Its
  buttons send the moves through `SessionCommands.CommandDiplomacy`.
- `Bootstrap/DivineController`: the player's divine interface. It selects miracles (keys 1–9 or
  the bar), casts at the cursor on a left click, rewards, punishes and sends the Creature (Z, X, V
  or the card), drives the hand's gestures, drains the session's events into effects and alerts,
  and refreshes the HUD, the targeting ring and the overlay.
- `Presentation/Creatures/`: `CreatureBody` (the Mossback from primitive meshes, tinted by its
  god), `CreatureView` (follows the simulated position smoothly, grows with level, walks and poses
  per action, flashes on feedback, labels its activity) and `CreaturePresenter`.
  `Presentation/Divine/DivineHand`: the god's hand as a visual: it follows the cursor, presses
  when casting or sending, and pats or swats the Creature.
- `Presentation/Divine/DivinePresenter`: the targeting ring (a decal with a procedural ring
  texture), the map overlay (one decal over the world with one pixel per region) and miracle
  effects (`MiracleEffect`: falling or rising motes, a spreading ring or a lightning bolt with a
  glow, freed when done).
- Settlement labels show the main god's share and take its colour once it holds the majority.

## Frame and tick flow

```mermaid
sequenceDiagram
    participant Godot as Godot frame (_Process)
    participant Root as GameRoot
    participant Session as GameSession
    participant Sched as SimulationScheduler
    participant Engine as SimulationEngine
    participant Settle as IntervalSystem(SettlementSystem)
    participant State as WorldState
    participant View as WorldPresenter
    participant Crowd as SettlementCrowd (per settlement)
    participant Overlay as DebugOverlay

    Godot->>Root: _Process(delta)
    Root->>Session: Update(delta)
    Session->>Sched: Advance(delta, step)
    loop whole ticks (10 Hz x time scale, capped)
        Sched->>Engine: Step(world)
        Engine->>Settle: Update(tick context)
        opt last tick of each 10-tick interval (1 Hz at x1)
            Settle->>State: Tier 1 settlement step (food, health, births, deaths, construction)
            Engine->>State: belief step (drift, conversion, power, ranks, blessings)
            Engine->>State: victory check, diplomacy step (drift, fading, status, lapsed proposals)
        end
        Engine->>State: Creature system (needs, decide at 5 Hz, walk, deeds)
        opt every 30 ticks
            Engine->>State: AI gods (miracles, Creature, diplomacy) through the authorities
        end
        Engine->>State: RNG state, Tick++
    end
    Root->>Session: DrainEvents() (via DivineController)
    Root->>View: effects and alerts for the events, HUD, targeting ring, overlay
    Root->>View: Sync(world), Creatures.Sync (smoothed pose), SetHoveredRegion(region under cursor)
    Root->>View: UpdateCitizens(world, simulation seconds, camera position)
    View->>Crowd: SelectTier, Refresh, Advance (reads WorldState only)
    Crowd-->>View: poses for the citizen MultiMesh (none in Tier 1)
    Root->>Overlay: ShowRegionMap(RegionDebugMap.Build(...)) when the F3 layer, cursor region, tick or world changed
    Root->>Overlay: ShowModel(DebugReadout.Build(snapshot with crowd statuses)) while visible
```

The render frame rate never changes simulation results. Only the number of real seconds
elapsed (× time scale) determines how many ticks run, and the settlement model steps on every
tenth tick, so its cadence follows simulated time, not frames. The citizens move every frame,
but their poses are functions of simulation time, so they freeze while paused and speed up
with the time scale.

## Settlement simulation and population tiers

Population is simulated statistically everywhere and shown in more detail near the camera
(ADR-004, amended by ADR-008). Tier 1 is canonical; Tier 2 is derived from it and never saved.
Tier 3 (full agents with promotion and demotion that write back) comes in a later milestone.

### Tier 1: the statistical settlement step

`SettlementSystem` runs once every `StepIntervalTicks` ticks (10, so once per simulated second
at ×1). `IntervalSystem` runs it on the last tick of each interval, so `10 × n` ticks run
exactly `n` steps. Every rate in `settlement_rules.json` is per step. Each step, for each
settlement in list order:

1. **Farmland:** the land regions (not sea, fertility above 0) within `FarmlandRadius` regions
   of the settlement's region. They set the farming capacity and the yield.
2. **Labour:** `WorkforceShare` of the inhabitants work. When the settlement is crowded
   (`Population ≥ ConstructionStartCrowding × Housing`) or a house is under way, below
   `MaxHousing`, and the stores hold `ConstructionFoodReserveSteps` of consumption, some
   workers build (capped by share, by `BuildersPerSite` and by the work left); the rest farm.
3. **Food:** production grows with farmers with diminishing returns
   (`maxYield × (1 − e^(−farmers / capacity))`); consumption is `FoodPerPersonPerStep` per
   person. Stores keep what is left, up to `FoodStoragePerHousing` per unit of housing, and the
   rest spoils.
4. **Health:** falls in proportion to the unfed share while food runs short and recovers by
   `HealthRecoveryPerStep` when everyone is fed.
5. **Births and deaths:** Poisson draws from the saved `SimulationRandom`. Births need free
   housing, production covering consumption and stores of `SurplusFoodPerPerson` per person,
   and scale with health. Deaths have a baseline plus an ill-health term in `(1 − Health)²`.
6. **Construction:** builders add their work to `ConstructionProgress`. At `WorkPerHouse` the
   house is finished: `Housing` grows by `HousingPerHouse` and the progress resets.

The settlement acts on its own: the player never places houses. Health does not change the
workforce share; during a famine construction stops for want of food, so the builders go back
to farming, and deaths shrink the population until the land feeds it again. The formulas and
the measured balance are in the Milestone 2 plan
([.docs/milestone-2-living-settlement.md](../.docs/milestone-2-living-settlement.md)).

```mermaid
flowchart TD
    A["Survey farmland<br/>land regions within FarmlandRadius"] --> B["Labour: workers = Population × WorkforceShare"]
    B --> C{"House wanted?<br/>crowded or under way, below MaxHousing"}
    C -- no --> F["farmers = workers"]
    C -- yes --> D{"Stores ≥ reserve?"}
    D -- no --> F
    D -- yes --> E["builders = min(share, BuildersPerSite, work left)<br/>farmers = workers − builders"]
    E --> G
    F --> G["production (diminishing returns), consumption"]
    G --> H{"Food + production ≥ consumption?"}
    H -- yes --> I["Food = min(rest, storage)<br/>Health recovers"]
    H -- no --> J["Food = 0<br/>Health falls with the unfed share"]
    I --> K["Births: free housing, surplus and stores<br/>Poisson, scaled by Health"]
    J --> K
    K --> L["Deaths: Poisson, baseline + ill health × (1 − Health)²"]
    L --> M["ConstructionProgress += builders<br/>house complete → Housing += HousingPerHouse"]
```

### Tier 2: representative citizens

When the camera comes within `RegionalTierEnterDistance` (450 m) of a settlement, the
settlement enters Tier 2 and shows `N = min(Population, MaxRepresentatives)` representatives
(300 at most), each standing for `Population / N` people. It returns to Tier 1 beyond
`RegionalTierExitDistance` (550 m); the gap keeps the tier from flickering. Tier 1 keeps
running for every settlement either way.

- `TierSelector` picks the tier from the camera distance. The tier is client-local
  presentation state, not `WorldState`.
- `RepresentativeRoster` derives the representatives from the aggregates, the layout and the
  world seed. Roles (builder, farmer, villager) follow `SettlementEconomy.AllocateLabour`, with
  both role boundaries rounded so every count is within one of its share. Each representative
  keeps a hash rank, so one more inhabitant changes at most two roles. Homes and fields come
  from rendezvous hashing over house indices and field slots, so a new house or field draws a
  share of the representatives and leaves everyone else in place. The hashes mix the seed, the
  settlement id and the representative index; no random stream is consumed.
- `CitizenRoutine` gives each representative a closed-form schedule in simulation time: rest
  by the door, walk to work (the fields, a ring around the building site, or chores near
  home), work with a forward lean per stroke, walk to the shrine, gather facing it, walk home.
  Walks bob once per stride.
- `SettlementCrowd` keeps the roster in step with the aggregates. It rebuilds the roster only
  when the role counts or the layout change, and bumps `RosterVersion` so the view recolours.
  Rerouted figures walk to their new routine at `CatchUpSpeed` instead of jumping; newcomers
  step out of their door; on promotion everyone appears in place.
- Representatives have no effects of their own: they show the work the aggregates already
  account for. Demotion has nothing to fold back, and saves, loads and new worlds rebuild them.

The tuning (distances, durations, speeds, colours, figure scale) is in the `Citizens` section
of `game/Data/settlement_view.json`.

```mermaid
flowchart LR
    State["WorldState<br/>SettlementState"] --> Labour["SettlementEconomy.AllocateLabour"]
    State --> Layout["SettlementLayout.ComputeSite"]
    Seed["World seed"] --> Roster
    Labour --> Roster["RepresentativeRoster.Build<br/>roles, homes, work, gathering"]
    Layout --> Roster
    Camera["Camera distance"] --> Tier["TierSelector"]
    Tier --> Crowd["SettlementCrowd<br/>roster, catch-up, terrain heights"]
    Roster --> Crowd
    Time["Simulation seconds"] --> Routine["CitizenRoutine.Evaluate"]
    Routine --> Crowd
    Crowd --> View["CitizenView<br/>one MultiMesh per settlement"]
```

```mermaid
stateDiagram-v2
    [*] --> Statistical
    Statistical --> Regional: camera distance ≤ RegionalTierEnterDistance (450 m)
    Regional --> Statistical: camera distance > RegionalTierExitDistance (550 m)
    Regional --> Statistical: world replaced (views rebuilt)
    note right of Regional
        Tier 1 keeps running for every settlement.
        Tier 2 only adds derived representatives.
    end note
```

## Gods, belief and miracles

ADR-009 records the decisions; the Milestone 3 plan
([.docs/milestone-3-first-god.md](../.docs/milestone-3-first-god.md)) has the formulas and the
measured balance.

- **Belief step** (`BeliefSystem`, every `DivineRules.StepIntervalTicks` ticks, after the
  settlement step). For each settlement:
  - the conditions: hardship is the worse of ill health and hunger, and comfort is health × food
    security;
  - every belief drifts from its values before the step: Faith grows with worship, erodes with
    Doubt and is forgotten by non-followers; Love grows with comfort among followers, fades and
    wears down in hardship; Fear fades in safety; Doubt drifts towards a "silence" level and rises
    among followers in hardship;
  - each god converts part of the free share with a pull of `Faith × (base + Love + Fear) ×
    (1 − Doubt)` and loses followers with a push of `Doubt × (base + Fear) × (1 − Love)`; gains
    are scaled so the shares never pass 1;
  - crossing half of the people raises `FollowingChanged`.

  Then every god gains its worshippers' Divine Power and the rank its followers earn
  (`RankAchieved`), and blessings fade.
- **Worship** takes `WorshipWorkforceShare × devotion` of the workers out of the fields. Power per
  step is worshippers × intensity (rising with Love and more with Fear) × shrine ×
  `PowerPerWorshipperPerStep`.
- **Influence** is derived, never saved. Each settlement with followers is a source with strength
  `min(1, k × adherence × Faith)`, fading linearly to a reach that grows with √followers. Sources
  combine as `1 − Π(1 − i)`.
- **Miracles** (`miracles.json`): Food, Heal, Bless Crops, Lightning and Mass Healing.
  `MiracleCaster` checks the request:
  - in §73's order: god, miracle, issuer, rank, cooldown, world, influence, power;
  - the cost is `cost × (1 + factor × (1 − influence))`.

  When it accepts, it applies the effects to the settlements within `Radius + SettlementRadius`
  and to the regions within `Radius`. It changes the belief of everyone within that distance plus
  `WitnessRadius`: closer people feel more, Love scales with need, and strangers start to believe.
  It raises `MiracleCast`.

```mermaid
flowchart LR
    Followers["Settlement beliefs<br/>(adherence, Faith, Love, Fear, Doubt)"] --> Worship["Worshippers<br/>(leave the fields)"]
    Worship --> Power["Divine Power"]
    Followers --> Influence["InfluenceField (derived)"]
    Power --> Cast{"MiracleCaster<br/>§73 checks"}
    Influence --> Cast
    Cast -- accepted --> Effects["Effects: food, health,<br/>blessing, deaths"]
    Cast -- accepted --> Witness["Witnesses' belief<br/>(scaled by closeness and need)"]
    Effects --> Conditions["Hardship and comfort"]
    Conditions --> Followers
    Witness --> Followers
```

## The Creature

ADR-010 records the decisions, and the Milestone 4 plan
([.docs/milestone-4-creature.md](../.docs/milestone-4-creature.md)) has the tables of options and
deeds.

- **State machine** (`CreatureSystem`, every tick). Needs grow and health follows hunger and rest.
  Deciding picks an option at most every `DecisionIntervalTicks` (5 Hz). Travelling walks the
  path at the level's speed. Performing counts down and then applies the deed. Banished counts
  down to the Creature's return home.
- **Brain** (`CreatureBrain`). Options come from the Creature's needs and from each settlement
  within `PerceptionRadius`, classified as in need, building or thriving. Utility is learned
  preference × drive × personality × closeness, plus seeded jitter; the best reachable option
  wins.
- **Learning** (`CreatureAuthority`). A reward or punishment judges the current action, or the
  last one within `FeedbackWindowSeconds`. It moves the preference for that situation and action,
  the trait behind it and loyalty; a punishment also stops the action.
- **Deeds** (`CreatureDeeds`) change food, houses and population. They change the people's belief
  in the Creature's god and the Creature's morality, and they add experience and a memory. The
  Creature's presence carries its god's influence.

```mermaid
flowchart LR
    Needs["Needs<br/>hunger, fatigue, boredom"] --> Options
    Villages["Settlements in view<br/>in need / building / thriving"] --> Options["Options"]
    Options --> Score["preference x drive x personality x closeness + jitter"]
    Score --> Act["Travel, then perform"]
    Act --> Deeds["Deeds: food, houses, deaths,<br/>belief, morality, XP, memory"]
    Player["God: reward / punish"] --> Learn["Preferences, traits, loyalty"]
    Learn --> Score
    Deeds --> Villages
```

## Rival gods

ADR-011 records the decisions, and the Milestone 5 plan
([.docs/milestone-5-rival-god.md](../.docs/milestone-5-rival-god.md)) has the goal formulas and
the measured contests.

- **Homes** are spread out: the first god takes the best site, and each later god takes the free
  settlement furthest from the homes already taken.
- **The god AI** (`GodAi`, every 3 s) scores every allowed miracle on every settlement, sending its
  Creature, and waiting. A cast's value is the sum over the settlements it reaches of the
  personality's goals:
  - protect followers in need;
  - defend followers being courted;
  - convert others, more in hardship or doubt;
  - bless land;
  - intimidate rivals' followers.

  Utility is value × (1 − thrift × cost / power). The best candidate (plus jitter) goes through
  the authorities as the god itself, and the god then rewards or punishes its Creature's latest
  deed by its personality. Candidates keep their reasons for the debug view.
- **Competition.** People leaving a god follow the others by their pull, or no god by
  `ApostateFreedom`. A miracle plants doubt about the other gods its witnesses follow. Casting
  under a stronger rival's influence costs more.
- **Victory** (`VictorySystem`): 60% of all followers held for 600 s wins (`WinnerGodId`,
  `VictoryAchieved`), and the world goes on.

## Diplomacy

ADR-012 records the decisions, and the Milestone 6 plan
([.docs/milestone-6-diplomacy.md](../.docs/milestone-6-diplomacy.md)) has the tables of acts and
the AI diplomat's utilities.

- **Relations are directional**: what one god thinks of another (trust, fear, respect, rivalry,
  gratitude, suspicion) and the acts it remembers of it. **Status is shared** by the pair.
- **Acts are recorded where they happen.** `MiracleCaster` (help, courting, harm), `CreatureDeeds`
  (attacks), `BeliefSystem` (conversions) and `DiplomacyAuthority` (alliance, peace, war,
  betrayal) report to one `DiplomacyLedger`, which updates the observer's view at once and
  remembers the act.
- **The diplomacy step** (1 Hz) lets relations drift back to rest and memories fade (betrayal
  slowest), sets fear and respect from the balance of followers and ranks, lets proposals lapse,
  and turns pairs that are neither allied nor at war Friendly or Hostile by their warmth.
- **Decisions go through the authority**: alliance, betrayal, war and peace are commands, checked
  against status, waiting proposals and cooldowns. Breaking an alliance costs the betrayer every
  god's trust.
- **Status matters**: allies plant no doubt in each other's followers and do not raise each
  other's costs, and their AI and Creatures spare each other's people. At war, peoples resist the
  enemy where their own god leads, and AI gods and Creatures target the enemy more.
- **The AI diplomat** (`GodDiplomat`) answers proposals and weighs alliance, war, betrayal and
  peace by its view, the balance of followers, war-weariness and its personality.

```mermaid
flowchart LR
    Acts["Miracles, Creature deeds,<br/>conversions, decisions"] --> Ledger["DiplomacyLedger"]
    Ledger --> Relations["GodRelation<br/>(observer to subject)"]
    Step["DiplomacySystem (1 Hz)"] --> Relations
    Relations --> Stance["GodStance (pair)"]
    Relations --> Diplomat["GodDiplomat (AI)"]
    Player["Diplomacy panel (P)"] --> Authority["DiplomacyAuthority"]
    Diplomat --> Authority
    Authority --> Stance
    Authority --> Ledger
    Stance --> Effects["Miracle doubt and costs,<br/>war resistance, AI and Creature targets"]
```

## Debug tools

Every system gets tools to see and drive it from the start (see [AGENTS.md](../AGENTS.md)).
The readouts are built by pure functions in `Pantheon.Game.Diagnostics` and only displayed by
the client.

- F1: the debug panel (hidden at the start), built by `DebugReadout` from a `DebugSnapshot`.
  `BeliefReadout` adds a line per god (power and income, rank, followers, next rank) and every
  god's influence under the cursor. The settlement nearest the cursor is shown in detail, with its
  belief conditions and each belief (followers, values, worshippers and the power they raise); the
  others get one line each. `SettlementReadout` adds, for the detailed settlement: population / housing, houses and health; food stored / storage with
  the net change per second, production and consumption; the jobs (farming, building, not
  working) and the farmland; expected births and deaths per second; the house under
  construction and the time to the next settlement step; the simulation tier, with the
  camera distance that changes it and, in Tier 2, the citizens shown and the people each
  stands for; and warnings when the settlement starves, construction waits for food or births
  are blocked. `SettlementEconomy.Assess` previews the next step without its random draws, so
  the panel shows rates rather than samples.
- B: the region grid and the hover outline (`RegionBoundaryOverlay`).
- F3: the region debug map. `RegionDebugMap.Build` turns one `RegionDebugLayer` (biome,
  elevation, temperature, moisture, fertility, vegetation, blessing or the local god's
  influence) into a `RegionDebugMapImage` with
  one RGBA8 pixel per region and a legend. Numeric layers use a dark-to-bright ramp from the
  lowest to the highest value; the region under the cursor is white and settlement regions are
  red. `GameRoot` rebuilds it only when the layer, the cursor region, the tick or the world
  changes, and `DebugOverlay.ShowRegionMap` displays it.
- `.`: `SessionCommands.StepTick` pauses the scheduler and calls `GameSession.StepOneTick`, the
  same engine call a scheduled tick makes. Stepping N times therefore equals N scheduled ticks,
  which `SessionCommandsTests` checks.
- `,`: `SessionCommands.StepSettlement` pauses and runs the ticks up to and including the next
  settlement step, so the panel shows what one step changed.
- F6 (`SpoilFood`) empties every settlement's food stores, and F7 (`AddSettlers`) grows every
  settlement by half (at least 10 people). Together they drive a famine: the stores run out,
  health falls, deaths rise and the settlement recovers once the land feeds it again.
- The F1 panel's Creature view (`CreatureReadout`, §106) shows its level, health, loyalty,
  morality and position, its activity, needs and personality, its best options with their
  utilities, its learned preferences and its latest memories.
- The F1 panel's god AI view (`GodAiReadout`, §106) shows each AI god's personality, its goal now
  (its best candidate) and its top three candidates with utilities and reasons, followed by its
  diplomatic candidates and the factors behind them.
- P: the diplomacy panel shows each rival's status, attitude and memories, and drives every
  diplomatic move. `--diplomacy-panel=on` opens it in scripted runs.
- F4 (`GrantPower`) gives the player's god 500 Divine Power.
- O cycles the player map overlays (influence, religion, faith, food), which double as debug views.
- Launch options and `SmokeTestDriver` for reproducible and scripted runs. `--advance-ticks=<n>`
  runs n ticks before the first frame and `--camera-distance=<m>` sets the starting camera
  distance, so a grown settlement can be captured up close. `--divine-power`, `--cast`,
  `--select`, `--overlay`, `--focus` (a settlement or `creature`), `--judge` and `--debug-panel`
  script captures of casting, targeting, overlays and the Creature; in scripted runs the cursor falls back to the screen centre. Unattended runs end with a
  one-line summary per settlement (`DebugReadout.Summarize`), including its tier and the
  citizens shown.

## Save and load flow

```mermaid
sequenceDiagram
    participant Player
    participant Root as GameRoot
    participant Cmd as SessionCommands
    participant Session as GameSession
    participant Save as WorldSaveSerializer
    participant Mig as SaveMigrations
    participant Gen as TerrainGenerator
    participant View as WorldPresenter

    Player->>Root: F5 (quicksave)
    Root->>Cmd: QuickSave(session, path)
    Cmd->>Session: SaveTo(path)
    Session->>Save: SaveToFile(world)
    Player->>Root: F9 (quickload)
    Root->>Cmd: QuickLoad(session, path)
    Cmd->>Session: LoadFrom(path)
    Session->>Save: LoadFromFile(path)
    Save->>Save: ReadSaveVersion(json)
    alt version 6 (current)
        Save->>Save: deserialize (strict)
    else versions 1 to 5
        Save->>Mig: Upgrade(json, version)
        Mig-->>Save: JsonObject at version 6, one step at a time (1→2: health, construction; 2→3: player god, beliefs, blessings; 3→4: Creatures; 4→5: personalities, dominance, winner; 5→6: diplomacy)
        Save->>Save: deserialize (strict)
    else missing, unsupported or newer
        Save-->>Session: SaveFormatException
    end
    Save->>Save: SaveIntegrity.Validate(world)
    Session->>Gen: Generate(seed, generation settings)
    Session-->>Root: WorldReplaced
    Root->>View: Rebuild(world, terrain)
    Cmd-->>Root: status line (or the error, with the current world kept)
```

## World generation and terrain backends

```mermaid
flowchart TD
    Seed["WorldSeed + WorldGenSettings"] --> TG["TerrainGenerator<br/>FastNoiseLite layers"]
    TG --> TF["TerrainField<br/>height / moisture / temperature / biome"]
    TF --> RB["RegionBuilder<br/>32x32 regions"]
    RB --> SF["SettlementFounder"]
    SF --> WS["WorldState"]
    TF --> TPF{"ClassDB has Terrain3D?"}
    TPF -- yes --> T3["Terrain3DTerrainPresenter<br/>import_images(height, -, colour)"]
    T3 --> SP["Terrain3DShaderPatch<br/>bounds-safe region lookups,<br/>edge-safe colour lookup"]
    TPF -- "no, or --terrain=fallback" --> FB["FallbackMeshTerrainPresenter<br/>(clearly marked, with reason)"]
```

Default world: 1024 × 1024 height samples at 2 m spacing (a 2048 m island), 32 × 32 regions of
64 m, sea level at 0 m. The biome set is Sea plus three land biomes: Grassland, Forest and Highland.

## Determinism

- World generation is a pure function of `(WorldSeed, WorldGenSettings)`.
- Tick results are a pure function of `WorldState` (including RNG state), `SettlementRules` and
  `DivineRules`, and a cast is a pure function of the world, the request and the content; the
  belief step and the caster draw no random numbers.
  The settlement step's births and deaths draw from the saved `SimulationRandom` in settlement
  list order, so a run continues identically across a save and load.
- Presentation never consumes a simulation random stream. The settlement layout and the Tier 2
  roster use `PresentationHash` of the seed, the settlement id and a slot or index, and the
  routines are functions of simulation time. The same world, tick and camera therefore show the
  same houses, fields and citizens on both renderers.
- Guaranteed per platform and runtime. Bit-exact cross-platform determinism is not required,
  because networking will be host-authoritative (ADR-003).

## Testing

- `src/Pantheon.Tests` (xUnit) covers determinism, save/load round trips, save versioning and
  the version 1 → 2 migration (against a save written by the Milestone 1 build, kept in
  `Persistence/Fixtures/save-v1.json`), RNG, the scheduler and the settlement step cadence,
  the settlement rules and step (starvation, recovery, growth up to housing, construction when
  crowded, storage), the raycaster, the camera model and edge scrolling, session commands
  (including the single-tick and settlement steps, F6 and F7), content parsing, launch options,
  presentation helpers and the settlement layout, the Tier 2 roster, routines, tier selection
  and crowd (including that it never changes the world), the debug readouts and region debug
  map, and the Terrain3D shader patch. Since Milestone 3 it also covers the belief rules and
  step, influence, every miracle check and effect, the session as the authority, the HUD model,
  alerts, overlays, the version 2 → 3 migration (against `save-v2.json`, written by the
  Milestone 2 build) and several settlements. Since Milestone 4 it covers the Creature's
  learning, brain, deeds, banishment, commands and navigation, and the version 3 → 4 migration
  (against `save-v3.json`, written by the Milestone 3 build). Since Milestone 5 it covers conversion
  between gods, contested costs, rival doubt, the god AI's choices for each personality, dominance
  and victory, and the version 4 → 5 migration (against `save-v4.json`). Since Milestone 6 it
  covers relations, acts and memory, status changes, betrayal, war and peace through the
  authority, the effects of status, the AI diplomat, the diplomacy panel and the version 5 → 6
  migration (against `save-v5.json`). The tests load the shipped `game/Data/*.json`; rule tests
  pin their own rules (`SettlementWorlds.Rules`, `DivineWorlds.Rules`, `DiplomacyWorlds.Rules`)
  so retuning the data does not change their meaning.
- `ArchitectureRulesTests` checks the rules that can be checked mechanically: the layering
  above, no Godot reference in core projects, no unseeded randomness or clock in core
  assemblies, and Windows, macOS and Linux libraries (debug and release) in every vendored
  GDExtension.
- The Godot client is verified by building the solution and running the main scene, headless
  and windowed. `--quit-after-frames=<n> --screenshot=<file>` gives a scripted smoke run that
  saves a frame capture.

## Extending (later milestones)

- New simulation features add state to `Pantheon.Domain`, systems to `Pantheon.Simulation`
  (registered in `SimulationEngine`, wrapped in an `IntervalSystem` when they step less often
  than every tick), and presenters to `game/Scripts/Presentation`.
- Breaking changes to the save shape bump `WorldSaveSerializer.CurrentSaveVersion` and add a
  `SaveMigrations` step from the previous version, tested against a save written by the
  previous build.
- The step, the layout, the tiers and the views iterate over every settlement, and world
  generation founds eight. More gods (Milestone 9) are more entries in `gods.json`; the AI, belief
  and victory rules already handle any number.
- Tier 3 adds agents with effects and promotion and demotion that write back to the
  aggregates; the milestone that adds it amends or supersedes ADR-008.
