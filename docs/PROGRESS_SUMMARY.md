# PANTHEON — Progress Summary

This document sums up the work so far, Milestones 1 to 3, against the owner's complete
specification, [PANTHEON_SPEC.md](PANTHEON_SPEC.md), which is the source of truth. Section numbers
(§) refer to it. It describes `main` on 2026-10-18.

Milestones 1 and 2 were built in pull requests #1 and #2. Pull request #2 had been merged into the
Milestone 1 branch rather than `main`, so `main` merged that branch at the start of Milestone 3.
From Milestone 3 on, work goes straight to `main`, with one push per milestone.

Status words used below:

- **Implemented**: built as the section asks, and verified.
- **Partial**: some of it is built; the notes say what is missing.
- **Not started**: nothing of it is built yet.

Sections that set a rule or a target rather than a feature say instead whether the rule is
followed or the target has been measured.

## In short

- Three of the specification's fifteen milestones are implemented: 1 Foundation (§116),
  2 Living Settlement (§117) and 3 First God (§118). Their playable outcomes hold: the player can
  explore a seeded island, its settlements visibly live and change, and the player can gain and
  spend belief.
- The world is one 2 km island with eight settlements. Each settlement is simulated
  statistically (Tier 1) and, near the camera, shown by up to 300 representative citizens
  (Tier 2).
- The player is a god, Ilyra:
  - Followers worship and raise Divine Power, and the god spends it on five data-driven
    miracles, cast with a click where its influence reaches.
  - Faith, Love, Fear and Doubt change with what the god does and with how the people fare;
    people convert or fall away.
  - A HUD, alerts and map overlays show all of it.
- Not built yet: the Creature, the Divine Hand, rival gods, diplomacy, weather, disasters,
  migration, history, audio and multiplayer.
- 426 xUnit tests pass headless (331 at the end of Milestone 2). The solution builds with no
  warnings.
- Milestone 3 was built and played on macOS 26 (Apple M3 Pro, Metal 4), at about 120 FPS at
  1600 × 900 with eight grown settlements and 300 citizens shown. Milestones 1 and 2 were built
  on a Linux virtual machine without a GPU. Windows has not been run.

## Milestones (§116–130)

| # | Milestone | Status |
| --- | --- | --- |
| 1 | Foundation (§116) | Implemented ([#1](https://github.com/roeyazroel/pantheon/pull/1)) |
| 2 | Living Settlement (§117) | Implemented ([#2](https://github.com/roeyazroel/pantheon/pull/2), merged into `main` at the start of Milestone 3) |
| 3 | First God (§118) | Implemented, on `main` |
| 4 | Creature (§119) | Not started; next |
| 5 | Rival God (§120) | Not started; the belief rules already share each settlement between gods |
| 6 | Diplomacy (§121) | Not started |
| 7 | Weather (§122) | Not started |
| 8 | Disaster Simulation (§123) | Not started |
| 9 | Four Gods (§124) | Not started |
| 10 | Population Movement (§125) | Not started |
| 11 | Creature Relationships (§126) | Not started |
| 12 | Multiplayer Foundation (§127) | Not started; decided in ADR-003. Casting is already a request that an authority checks (§73). |
| 13 | Multiplayer Simulation (§128) | Not started |
| 14 | Complete Vertical Slice (§129) | Not started |
| 15 | Polish and Expansion (§130) | Not started |

ADR-004 and ADR-008 cite an earlier working roadmap with different milestone numbers;
[GAME_SPEC.md](GAME_SPEC.md#earlier-working-roadmap-superseded) maps them. Every other document
uses the specification's numbers.

### Milestone 1: Foundation (§116)

| §116 deliverable | Status | What exists |
| --- | --- | --- |
| Godot project | Implemented | `game/`: Godot 4.7.2-stable .NET, pinned by ADR-001, with the Forward+ renderer. |
| C# solution | Implemented | `Pantheon.sln` with Domain, Simulation, Persistence, Game, Networking (reserved, empty) and Tests, plus the Godot client project `game/Pantheon.csproj` (ADR-002). Warnings are errors. |
| Camera | Implemented | A free god camera with Close, Regional and Strategic zoom bands (§56). |
| Terrain | Implemented | A seeded island rendered with Terrain3D 1.0.2 (ADR-005), patched at runtime (ADR-007), with a fallback mesh renderer. |
| One settlement | Implemented (eight since Milestone 3) | Founded on fertile land near the island centre. |
| Saveable WorldState | Implemented | `WorldState` is the only canonical state (ADR-006) and is saved as versioned JSON. |
| Debug UI | Implemented | The F1 debug panel, the B region grid, the F3 region debug map and the `.` single-tick step. |

The plan, with its verification results, is
[.docs/milestone-1-foundation.md](../.docs/milestone-1-foundation.md).

### Milestone 2: Living Settlement (§117)

| §117 deliverable | Status | What exists |
| --- | --- | --- |
| Aggregate population | Implemented | A statistical (Tier 1) settlement step once per simulated second (ADR-008), with births and deaths drawn from the saved random stream. |
| Food | Implemented | Farmers farm the land within two regions with diminishing returns; everyone eats; the stores are capped by housing. |
| Housing | Implemented | Crowded settlements with food in reserve build one house at a time. The player never places houses. |
| Health | Implemented | Falls with hunger, recovers when everyone is fed, and changes births and deaths. |
| Visible representative citizens | Implemented | Within 450 m of the camera, up to 300 figures per settlement follow the labour split and a daily round (ADR-008). |

The plan is [.docs/milestone-2-living-settlement.md](../.docs/milestone-2-living-settlement.md).

### Milestone 3: First God (§118)

| §118 deliverable | Status | What exists |
| --- | --- | --- |
| Divine Power | Implemented | Followers spend part of their working time worshipping. Each belief step, a settlement raises worshippers × worship intensity × the shrine × a rate for each god (§9). Worship intensity rises with Love, and more with Fear. The HUD shows the power, its income per second and the cost of each miracle. |
| Influence | Implemented | Derived from followers, never stored (ADR-009). Each settlement with followers reaches further the more followers it has. Influence overlaps and adds up, and several gods can hold it over one place. Miracles need influence at the target and cost more where it is weak (§10). The O overlay and the F3 layer show it. |
| Belief | Implemented | Faith, Love, Fear and Doubt per god per settlement (§7, §109), plus the share of the people who follow each god. Belief drifts with worship, comfort, hardship and time. Fear converts fast and holds badly; love converts slower and holds (§8). Miracles change the belief of everyone who sees them, and help that was needed earns more love. |
| Basic miracles | Implemented | Food, Heal, Bless Crops, Lightning and Mass Healing, defined in `game/Data/miracles.json` (§107). Each cast is checked by an authority, in §73's order, before anything changes. Click-to-cast with a targeting ring and a hint. |

Playable outcome, "Player can gain and spend belief": met.

- Worship raises Divine Power from the first second.
- Food, Heal or Bless Crops on a neighbouring village converts it in about three minutes with
  the shipped numbers.
- Followers bring influence, which reaches further villages, and ranks, which unlock Mass Healing
  at 100 followers.
- A famine at home without help costs about half of the god's followers there; with help they
  rise to 95%.

The plan, with its verification results, is
[.docs/milestone-3-first-god.md](../.docs/milestone-3-first-god.md), and
[ADR-009](ADR/ADR-009-belief-influence-and-miracles.md) records the decisions.

Also built in Milestone 3:

- Eight settlements founded on the best land, at least five regions apart, with varied
  populations and names drawn from the seed (§108).
- Ranks from Minor Spirit to Ascendant by followers (§13), never lost.
- A player HUD (§57), alerts (§59) with de-duplication and urgency, map overlays for influence,
  faith and food (§58), and labels that show and colour each settlement's main god.
- Transient domain events (§91): `MiracleCast`, `FollowingChanged`, `RankAchieved` and
  `FamineStarted`.
- Save version 3 with a migration from version 2, tested against a save written by the
  Milestone 2 build.
- Debug tools: god and belief lines in F1, the blessing and influence layers in F3, F4 for Divine
  Power, and launch options for scripted captures of casting and overlays.
- The toolchain on macOS, and captures on both terrain renderers with Metal.

## Coverage by specification section

Sections that only state intent are summarised at the end of this part.

### World and generation

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 5 | The world | Partial | One continuous world divided into regions. Regions have a biome, elevation, temperature, moisture, fertility, vegetation and a divine blessing, and religious influence is derived for any point. Missing: wildlife, water, resources, ecological health and disaster risk. Grassland, forest and mountain (as Highland) exist, plus the sea. |
| 6 | World scale | Partial | A 2 km island against 10–20 km. 8 settlements (the MVP number) and one god against 4. A few thousand people after half an hour against 10,000–100,000+. Hundreds of visible citizens, and no heavyweight object per citizen. |
| 84 | World streaming | Not started | The whole island is loaded. |
| 93 | Procedural generation | Partial | Elevation, moisture, temperature, fertility, vegetation, biome and settlement sites come from the seed (tested). Missing: trees, resources, river candidates and sacred sites. |

### Settlements and population

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 29 | Settlements | Partial | Housing, job allocation (farm, build, worship) and construction are autonomous. Missing: roads, gathering, expansion and priorities set by a god. |
| 30, 90 | Settlement state | Partial | Population, food, housing, health, construction, and belief, love, fear and doubt per god. Missing: water, safety, happiness, education, wealth, political stability and resentment. |
| 31, 33–36 | Migration, culture, schisms, atheism, technology | Not started | |
| 32 | Religious conversion | Partial | Conversion is gradual and driven by miracles and prosperity, and several gods can share a settlement. With one god, conversion is from people who follow no god; conversion between gods comes with Milestone 5. |
| 80 | Simulation tiering | Partial | Tier 1 simulates the economy, demographics, health and belief. Tier 2 shows walking, working and gathering figures (presentation only). Tier 3 is not started. |
| 81 | Population representation | Implemented | Up to 300 figures stand for each settlement's population. |
| 82 | Migration representation | Not started | |
| 83 | Rendering large crowds | Implemented | One MultiMesh per settlement for citizens, and one per house part and for fields. |
| 112 | MVP settlements | Partial | Settlements farm, build housing, grow, worship and respond to miracles. Missing: reacting to danger. |

### Gods, belief and miracles

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 7–8 | Belief, love versus fear | Partial | Faith, Love, Fear and Doubt with their trade-offs. Dependency and Resentment are left out, as §109 allows. |
| 9 | Divine Power | Implemented | Followers × Faith × worship intensity × religious infrastructure (the shrine) × a rate, with Love and Fear raising the intensity differently. |
| 10 | Divine influence | Partial | From followers; overlapping; weaker influence costs more. Missing sources: shrines and temples as their own sources, holy sites, prophets, the Creature and allies. |
| 11 | God attributes | Partial | Id, Name, Rank, Divine Power, followers (derived) and controller. Missing: domains, reputation, known gods, relationships, Creature, holy sites, historical actions and traits. |
| 12 | God domains | Partial (data only) | Every miracle has a domain; gods do not specialise yet. |
| 13 | Divine rank | Implemented (followers only) | Six ranks by followers, never lost, unlocking miracles. Achievements, holy places and the Creature do not count yet. |
| 14 | Miracles | Partial | Minor: Food, Heal, Bless Crops and Lightning. Regional: Mass Healing. No Great miracles or Divine Acts yet. |
| 15 | Systemic consequences | Partial | Bless Crops raises the yield of the land; Food and Heal change hunger and health, which drive doubt and love; Lightning kills and frightens. Weather, fire and ecology chains need Milestones 7–8. |
| 47–49, 87 | AI gods | Not started | |
| 109 | MVP belief | Implemented | Faith, Love, Fear and Doubt. |
| 110 | MVP miracles | Partial | Heal, Food, Lightning and Bless Crops. Missing: Water, Rain, Fire, Earthquake and Shield, which need water, weather and disasters. |
| 115 | MVP victory | Not started | |

### The Creature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 22–28, 86, 111 | Creature, progression, learning, personality, relationships, AI, MVP | Not started | `src/Pantheon.Domain/Creatures` is reserved. |

### Nature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 16–17 | Weather, seasons | Not started | |
| 18 | Ecology | Partial (data only) | Regions carry fixed temperature, moisture, fertility and vegetation, plus a blessing that miracles set and that fades. No ecological process yet. |
| 19–21, 94, 114 | Disasters, preparation, physics and disasters, MVP nature | Not started | |

### Politics and history

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 37–46, 113 | Divine politics, diplomacy, betrayal, war, MVP diplomacy | Not started | |
| 50–54 | History, sacred locations, world events, victory, alliance endgame | Not started | |
| 91 | Event architecture | Partial | Systems and the miracle authority raise transient domain events; alerts and effects read them. |
| 92 | History system | Not started | |

### Player experience

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 55 | Player interaction | Partial | Miracles are cast with a click (ADR-009). There is no Divine Hand yet; it comes with the Creature and Jolt. |
| 56 | Camera | Partial | WASD, edge movement, drag, zoom, rotate, focus on each settlement in turn, three zoom bands. Missing: focus on the Creature and a strategic overview. |
| 57 | User interface | Partial | HUD with Divine Power, income, followers, rank, the miracle bar and the selected miracle's hint, plus alerts. Missing: Creature status and the panels (World, Settlements, Diplomacy, Religion, History, Domains, Miracles). |
| 58 | Map overlays | Partial | Influence, faith and food for the player; the F3 debug map adds the region statistics, blessing and influence. Missing: population, ecology, weather, danger, disaster risk, migration and political control. |
| 59 | Alert system | Partial | Starving followers, settlements gained or lost, ranks. Alerts are filtered to what concerns the player, not repeated within 30 simulated seconds, and the most urgent are kept. |
| 60 | Audio | Not started | |
| 61 | Visual direction | Partial | Stylised terrain, sea, huts, fields, figures, glowing miracle effects, and god colours on labels and overlays. No original art assets, and religion does not yet change how settlements look. |

### Platforms, technology and performance

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 62 | Supported platforms | Partial | Built, tested and played on macOS (Apple Silicon, Metal) and Linux x64. Not run on Windows. No export presets. |
| 63 | Core technology stack | Partial | In use: Godot .NET 4.7.2, C#, Terrain3D, FastNoiseLite and MultiMesh. Not used yet: Jolt, navigation, LimboAI, a utility AI and Blender assets. |
| 64 | Terrain destruction | Not started | |
| 88 | Threading model | Followed | Everything runs on the main thread. |
| 89 | Update frequencies | Partial | Settlements and belief step at 1 Hz. Citizen poses are evaluated every frame from simulation time. |
| 104 | Performance testing | Not started | No stress scenarios. |
| 105 | Target performance | Partly measured | About 120 FPS at 1600 × 900 on an Apple M3 Pro with 8 settlements and 300 citizens shown. Not measured at 1080p on the other platforms; no graphics options. |

### Architecture, saves and process

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 65 | Local-first architecture | Implemented | Fully offline. |
| 66 | Single-player architecture | Partial | `GameSession` is the local authority over the world, its settlements and the player's god. There are no AI gods yet. |
| 67–69, 71–72, 95–98 | Multiplayer, ENet, Steam, host disconnect and migration, replication, prediction, cheating | Not started | ADR-003 decides host-authoritative ENet without lockstep. |
| 73 | Network authority | Partial (local) | `CastMiracleRequest` is an intent; `MiracleCaster` checks power, location, influence, cooldown, rank and authorisation before anything changes. There is no network yet. |
| 70 | No required server infrastructure | Implemented | |
| 75 | Save architecture | Partial | One quicksave holds the seed, generation settings, tick, random state, regions, settlements, gods and beliefs. No GameTime; the other listed state does not exist yet. |
| 76, 99 | Save principle, persistence format | Implemented | `WorldState` is serialized as human-readable JSON with `SaveVersion` 3 and migrations from versions 1 and 2. |
| 77–78 | Core rule, layered architecture | Implemented | The core projects never reference Godot (tested). |
| 79 | Simulation systems | Partial | Settlement, population and belief systems and the miracle authority. |
| 90 | Core data models | Partial | `GodState` (subset), `SettlementState` with belief, love, fear and doubt per god, `RegionState` (no wildlife, pollution or weather). No CreatureState, DiplomacyRelation or WorldEvent records with duration. |
| 100 | Repository structure | Implemented, with differences | As recommended, plus `World/` and `Miracles/` in `Pantheon.Domain`, `game/Data/`, `game/addons/`, `.docs/` and `AGENTS.md`. |
| 101 | Architecture decision records | Implemented | ADR-001 to ADR-009. |
| 102–103 | Testing, deterministic testing | Implemented | Every rule has tests, including §102's "Fear decreases after prolonged safety" and "God cannot cast miracle without enough power". Randomness is seeded and checked. |
| 106 | Debug tools | Partial | Influence, belief, population, region boundaries, simulation tier and the frame rate. Missing: AI intentions, navmesh, weather cells, migration routes, network authority, other performance metrics, and the god AI and Creature views. |
| 107 | Content data | Partial | World generation, settlement and divine balance, miracles, the gods of a new world and presentation are data files, parsed strictly. No Creature abilities or domain trees yet. |
| 108 | MVP world | Partial | One map, three land biomes, 8 settlements. One god of 4, and no Creatures or AI players. |
| 131 | Non-goals | Respected | |
| 132 | Validation priorities | Partly testable | "Is converting populations fun?" can now be tried with one god. The others need the Creature, rivals, diplomacy or disasters. |
| 134 | Coding agent rules | Followed | `AGENTS.md`; `ArchitectureRulesTests` checks the mechanical ones. |

### Sections that state intent

- §1–4 (vision, originality, pillars, core loop): names, art and interface are original. The core
  loop runs as far as "Observe world → Influence mortals → Gain belief → Generate Divine Power →
  Expand influence". The Creature, rival gods and world events are still to come.
- §133, §135 and §136: the split between simulation and presentation holds for gods, belief and
  miracles, with `GameSession` as the local authority. "Mortals remember what you did" has begun:
  belief remembers help and harm. The rest needs the Creature, rivals and nature.

## Architecture decision records

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR/ADR-001-godot-dotnet-pinned-version.md) | Godot 4.x .NET, pinned to 4.7.2-stable | Accepted |
| [ADR-002](ADR/ADR-002-csharp-primary-language.md) | C# as the primary language | Accepted |
| [ADR-003](ADR/ADR-003-host-authoritative-enet-networking.md) | Host-authoritative networking over ENet, no lockstep | Accepted; implementation in Milestones 12 and 13 |
| [ADR-004](ADR/ADR-004-statistical-population-simulation-with-tiering.md) | Statistical population simulation with tiering | Accepted; amended by ADR-008 |
| [ADR-005](ADR/ADR-005-terrain3d-for-terrain.md) | Terrain3D for terrain rendering | Accepted; amended by ADR-007 |
| [ADR-006](ADR/ADR-006-simulation-state-separate-from-scenes.md) | Simulation state is separate from scenes | Accepted |
| [ADR-007](ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md) | Patch Terrain3D's generated shader and report why the fallback is used | Accepted; amends ADR-005 |
| [ADR-008](ADR/ADR-008-settlement-step-and-representative-citizens.md) | Settlement step cadence and representative citizens | Accepted; amends ADR-004 |
| [ADR-009](ADR/ADR-009-belief-influence-and-miracles.md) | Belief, influence and miracles (Milestone 3) | Accepted |

No decision has been reversed.

## Tests

The xUnit suite in `src/Pantheon.Tests` has 426 tests, and they all pass headless in about ten
seconds, without Godot (`dotnet test Pantheon.sln`).

| Area | Test classes | M2 | Now |
| --- | --- | --- | --- |
| World generation and determinism | `WorldGenerationTests`, `BiomeClassifierTests`, `WorldSeedTests`, `DeterminismTests`, `SimulationRandomTests`, `TerrainRaycasterTests` | 47 | 49 |
| Settlement rules and step (Tier 1) | `SettlementEconomyTests`, `SettlementSystemTests`, `IntervalSystemTests` | 43 | 43 |
| Belief, influence and miracles | `BeliefModelTests`, `BeliefSystemTests`, `InfluenceFieldTests`, `MiracleCasterTests` | 0 | 45 |
| Representative citizens (Tier 2) | `RepresentativeRosterTests`, `CitizenRoutineTests`, `SettlementCrowdTests`, `TierSelectorTests` | 68 | 68 |
| Saves and migration | `SaveLoadTests`, `SaveMigrationTests`, `SaveMigrationV2Tests`, `DivineSaveTests` | 34 | 47 |
| Session, scheduler, commands, launch options | `GameSessionTests`, `SimulationSchedulerTests`, `SessionCommandsTests`, `LaunchOptionsTests`, `DivineSessionTests` | 38 | 51 |
| Camera | `GodCameraModelTests`, `EdgeScrollTests` | 27 | 27 |
| Presentation, HUD, alerts and layout | `PresentationTests`, `SettlementLayoutTests`, `Terrain3DShaderPatchTests`, `DivinePresentationTests` | 30 | 45 |
| Debug readouts and region map | `DebugReadoutTests`, `RegionDebugMapTests` | 22 | 22 |
| Content parsing | `ContentTests` | 11 | 18 |
| Architecture rules | `ArchitectureRulesTests` | 11 | 11 |
| **Total** | | **331** | **426** |

Not covered by automated tests: the Godot client (`game/Scripts`), which is checked by building
it and by scripted runs with captures, and Windows.

## How to run

You need Godot 4.7.2-stable, .NET edition, the .NET SDK 8.0 or newer, and a GPU with Vulkan
support or Metal on macOS. On macOS, `brew install --cask dotnet-sdk@8 godot-mono` installs both,
and the Godot executable is `godot-mono`.

```bash
godot --headless --path game --import    # once after cloning: registers Terrain3D
dotnet build Pantheon.sln                # warnings are errors
dotnet test Pantheon.sln                 # headless, no Godot needed
godot --path game                        # play
```

`godot` stands for the Godot 4.7.2 .NET executable. Other useful runs:

```bash
godot --path game -- --seed=42                                     # another world
godot --path game -- --advance-ticks=9000 --camera-distance=300    # grown settlements
godot --path game -- --divine-power=900 --overlay=influence        # power to spare, influence shown
godot --path game -- --terrain=fallback                            # the fallback terrain renderer
```

Main controls:

- Camera: W A S D, the arrow keys or the screen edge to pan, the right mouse button to grab and
  drag the ground, the mouse wheel to zoom, Q and E to rotate, and F to look at each settlement
  in turn.
- Miracles: keys 1–9 or the miracle bar select one; left click casts it; Esc cancels.
- Overlays and time: O cycles the map overlays. Space pauses, `[` and `]` change the speed.
- Debug: F1 shows the debug panel, F3 the region debug map and F4 grants 500 Divine Power.
- Saves: F5 and F9 quicksave and quickload, and F2 makes a new world.

The [README](../README.md) lists every control and option.

## Known gaps and deviations from the specification

### Scope and scale

- **One god.** The MVP has four (§108); rivals come in Milestones 5 and 9.
- **A small world.** One 2 km island instead of 10–20 km (§6). No trees or other vegetation are
  drawn.
- **Miracles.** Five of §110's nine kinds; Water, Rain, Fire, Earthquake and Shield wait for
  water, weather and disasters.

### Belief and power

- Dependency and Resentment are not modelled (§109 allows it); fear's long-term costs are only
  faster apostasy until resentment, migration and rebellion exist.
- Religious infrastructure is only the shrine; there are no temples or holy sites.
- Divine Power has no upkeep or cap, and a god with a large following earns far more than five
  minor miracles can spend. Rival gods and more expensive miracles are the intended sinks.
- Settlements' stores grow large in good times, so food miracles mostly earn faith rather than
  love until weather or disasters bring hardship.

### The population tiers

- Tier 2 citizens are presentation only (ADR-008). Worshippers are shown as villagers, and
  citizens walk in straight lines (no navigation, §85).

### Saves

- One quicksave slot and no GameTime.
- Events are not saved, so an alert raised just before a save does not repeat after a load.

### Platforms, performance and tooling

- Not run on Windows. No export presets, so no packaged builds; an export must include
  `Data/*.json`.
- macOS may quarantine the unsigned Terrain3D libraries.
- No stress scenarios (§104) or graphics options (§105).
- The Terrain3D shader patch (ADR-007) is still needed, and every start logs a harmless
  deprecation warning.

### Interface

- No Divine Hand, panels, audio, menu or settings screen.

### Documents written before the specification

- [GAME_SPEC.md](GAME_SPEC.md) differs from the specification in places, and the specification
  wins: its roadmap numbers, a cap on Divine Power by total Faith (§9 has none, and none is
  implemented), contested influence weakening every god (§10 makes weaker gods pay more, as
  implemented), its own example miracles, and a campaign milestone that §131 rules out.

## Recommended next steps

### Milestone 4: Creature (§119)

Deliverables: the Creature, movement, interaction, needs, simple AI, and reward and punishment.
Playable outcome: the Creature behaves independently.

1. Decide in ADRs:
   - how the Creature's AI is built and stored so that it is saved, runs headless and can run on
     a host: behaviour trees and state machines as data in the core, rather than LimboAI nodes in
     the scene tree, if the reasons hold;
   - how physics (Jolt) results reach `WorldState`;
   - how the Divine Hand interacts with the Creature.
2. Add `CreatureState` (§23, §90) to `WorldState`, with save version 4.
3. A Creature system at 5–10 Hz (§89): needs (hunger, rest, joy), movement over the heightfield,
   and actions chosen from learned preference weights (§25) and personality, with reward and
   punishment that shift the weights.
4. The Creature's effects reach the world: it eats from settlements' stores, helps or hurts
   villagers, and its presence spreads influence (§10). Settlements' belief responds.
5. Presentation: an original Creature model, a focus key, a Creature panel and a debug view of
   its behaviour, needs and weights (§106).

### Alongside

- Run on Windows and measure at 1080p; add export presets.
- Report the Terrain3D shader defects upstream.
