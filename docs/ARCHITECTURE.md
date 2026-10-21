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
   the `WorldSeed`. RNG state lives in `WorldState.Random`, so saves resume the same sequence. The
   weather and disaster steps draw a fresh stream each step from the seed and the tick instead,
   so they never shift the shared stream (ADR-013, ADR-014).
   No global or ambient randomness in core systems: `ArchitectureRulesTests` scans the core
   assemblies for `System.Random`, clock and tick-count reads and GUIDs.
6. **Data-driven content** where practical (`game/Data/*.json`), parsed strictly (unknown or
   missing members are errors).
7. **No speculative infrastructure or optimisation.** Reserved areas (the domain's population folder)
   hold no code until their milestone.
8. **Commands go through an authority.** A player's intent (casting a miracle, rewarding,
   punishing or sending the Creature, a diplomatic move, a refugee policy) is a request that
   `MiracleCaster`, `CreatureAuthority`, `DiplomacyAuthority` or `MigrationAuthority` checks
   against the world before anything changes (spec §73, ADR-009, ADR-010, ADR-012, ADR-016). The engine owns them as one `Authorities` bundle.
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
  Scripts/                    Bootstrap, Camera, Network (ENet adapter), Presentation (adapters), UI
  Data/                       worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                              creatures.json, god_ai.json, diplomacy.json, weather.json, disasters.json,
                              migration.json, world_sizes.json, domains.json, ecology.json, history.json,
                              holy_sites.json, society.json, water.json (simulation);
                              settlement_view.json, biome_palette.json, divine_view.json, weather_view.json
                              (presentation); minds.json (the gods' minds: Claude models and the
                              scribes' lore, ADR-065)
  Materials/ Shaders/         Presentation resources (.tres, .gdshader)
  Assets/ Audio/              Original art and audio (none yet; created with the first asset)
  addons/terrain_3d/          Vendored Terrain3D 1.0.2 runtime subset (ADR-005)
src/
  Pantheon.Domain/            State and content contracts: World, Ecology (biomes, EcologyRules), Settlements, Gods, Belief, Miracles,
                              Creatures, Notables, Roads, GreatEvents, Diplomacy, Weather, Disasters, Events
                              (+ reserved folders)
  Pantheon.Simulation/        Randomness, Noise (FastNoiseLite), Terrain, WorldGen, Settlements, Belief, Miracles,
                              Creatures, Notables, Resources, Roads, GreatEvents, Navigation, Gods (AI), Diplomacy,
                              Weather, Ecology, Disasters, Systems, SimulationEngine
  Pantheon.Persistence/       Versioned JSON saves with migrations, content parsing, the player's profile
  Pantheon.Game/              Engine-agnostic application layer: GameSession, scheduler, camera model,
                              diagnostics, HUD model, alerts, overlays, settlement layout,
                              Tier 2 representative citizens (Population), Multiplayer (host and player),
                              Controls (the key map and the god's bar, ADR-068)
  Pantheon.Networking/        Host-authoritative protocol (ADR-003, ADR-018): messages, codecs, transport
                              contract, in-memory loopback
  Pantheon.Oracle/            The language models behind the gods' minds (ADR-065, ADR-067): Claude
                              through Anthropic's SDK, GPT through OpenAI's Responses API, Gemini
                              through generateContent, a local model through Ollama's API, and the
                              factory that opens the one the settings choose
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
    Net["Pantheon.Networking"]
    Oracle["Pantheon.Oracle<br/>(Anthropic SDK, OpenAI, Gemini and Ollama over HTTP)"]
    Tests["Pantheon.Tests"]
    T3D["Terrain3D GDExtension (optional at runtime)"]

    Client --> Game
    Client --> Sim
    Client --> Persist
    Client --> Domain
    Client --> Net
    Client --> Oracle
    Oracle --> Game
    Client -. "ClassDB dynamic calls" .-> T3D
    Game --> Sim
    Game --> Persist
    Game --> Domain
    Game --> Net
    Net --> Domain
    Sim --> Domain
    Persist --> Domain
    Tests --> Game
    Tests --> Sim
    Tests --> Persist
    Tests --> Domain
    Tests --> Oracle
```

Allowed references point down the graph only. `Pantheon.Domain` references nothing.
`Pantheon.Oracle` sits beside the client, above `Pantheon.Game`: it implements the game's
language-model contract with the `Anthropic` package and `HttpClient`, as the client's ENet
adapter implements the networking's transport contract. No core project references it.
Only `game/Pantheon.csproj` references Godot. `ArchitectureRulesTests` reads the core project
files and compiled assemblies and fails when either rule is broken.

## Layers

### Domain (`Pantheon.Domain`)

- `WorldState`: `Seed`, `Generation` (a copy of the `WorldGenSettings` used), `Tick`, `Random`
  (RNG state), `Regions` (`RegionGrid`), `Settlements`, `Gods`, `Creatures`, `Diplomacy`,
  `Weather`, `Disasters`, `Migration` (groups on the road, ADR-016) and `WinnerGodIds` (the god
  or allied gods that have won, spec §53 and §115; ADR-015). Settlements carry their people's
  wish to leave (`Emigration`) and gods their `RefugeePolicy`. Creatures carry their combat
  skill, scars and views of each other (`CreatureRelation`, ADR-017). `History` holds the moments
  the world remembers (`HistoryRecord`, ADR-020). `HolySites` holds the world's sacred places
  (`HolySite`: kind, name, region, keeper, claimant and the claim so far; ADR-021). `Water` holds
  the rivers (`River`: name, regions from source to mouth, level) and the lakes (`Lake`: name,
  regions, surface) (ADR-022). Since ADR-027, `Victory` (`VictorySettings`) records how the world
  may be won (ascendancy, conquest, cultural victory; none of them makes a sandbox), and
  `VictoryAchieved.Kind` (`VictoryKind`: ascendancy, pantheon, conquest, cultural) how it was. A
  world's size lives only in `Generation` and its gods. Since ADR-028, `Seasons` (`SeasonState`:
  the tick the year began) is present when the world turns through seasons, and regions carry
  `Snow`, `Wildlife`, `Damage`, `Scar` and `Pollution`. Since ADR-029 regions carry `Blight` and
  landslide `Rubble`. Since ADR-030 regions carry `Hallowed` (blessing that never fades), gods what
  their hand holds (`GodState.Held`: `HeldObject`, villagers with their beliefs, a rock, a tree or
  the Creature), and Creatures the paths they took up (`Specialisations`) and their `Callings`.
- `RegionGrid`: `Columns × Rows` cells of `RegionState`, each `RegionSize` metres square, with a
  row-major `Id = row * Columns + column`.
- `RegionState`: `Id`, `Biome`, `Elevation` (mean metres, sea level = 0), `Temperature`,
  `Moisture` (the climate), `Fertility`, `Vegetation` (0..1), `Blessing` (0..1, from miracles; it
  fades), `SoilMoisture` (0..1, the water in the soil now; ADR-013), `Fire` (how fiercely it burns
  now), `Scorch` (how much of its fuel fire has eaten; ADR-014), `FreshWater` (0..1, how much of it a
  river or lake waters, fixed at generation), `Flood` (0..1, how deep it lies under a flood now;
  ADR-022), and since ADR-033 `Forest` (the most wood it grows), `Timber` (its standing wood) and
  `Stone`.
- `SettlementState`: `Id`, `Name`, `RegionId`, `Position`, `Population`, `Food`, `Water` (in its
  wells and cisterns; ADR-022), `Society` (`SocietyState`: safety, happiness, stability,
  wealth, education, `TechnologyLevels` in §36's eight fields, buildings, priority and the
  building under way; ADR-023), `Culture` (`CultureState`: how firmly it holds each of §33's
  eight doctrines and how far it has turned from the gods, its secularism; ADR-024), `Housing`
  (capacity in people), `Health` (0..1), `ConstructionProgress` (work done on the one house
  being built; 0 when none is under way), `Ruins` (houses destroyed and not yet rebuilt),
  `ShieldedUntilTick` (while a Shield miracle protects it), `OldFaith` (the share of its people
  who broke away from their god in a schism, with how many have returned or fallen away; ADR-021),
  `SanctuaryGodId` and `SanctuaryUntilTick` (whose followers cannot fall away for now),
  `Beliefs`, `Wood` and `Stone` in store (ADR-033), and `Plague` and `Immunity` (ADR-034). These
  aggregates are the canonical
  population (Tier 1, ADR-004 and ADR-008).
- `GodBelief` (one per god a settlement knows): `GodId`, `Adherence` (the share of the people who
  follow the god; the shares add up to at most 1) and `Faith`, `Love`, `Fear`, `Doubt`,
  `Dependency` and `Resentment` (0..1; the last two §7–8's deeper feelings, ADR-024).
  Followers are `Population × Adherence` and are never stored (ADR-009).
- `GodState`: `Id`, `Name`, `Controller` (Human, Ai or Remote), `Personality` (`None` for players;
  one of §47's eight for AI gods), `DivinePower`, `Rank` (never falls), `DominantSinceTick`
  (while it holds the victory share), `ConquestSinceTick` and `CulturalSinceTick` (while it holds
  a conquest's or a cultural victory's condition; ADR-027), `CreatureId`, `RefugeePolicy`, `Cooldowns` and `Domains`
  (the domains it has taken up, ADR-021), `Conduct` (`GodConduct`: what the world remembers of its
  acts, fading) and `Traits` (what the world calls it, from that conduct; ADR-024), `KnownGods`
  (the gods it has met, sorted), and for an AI god its `Goals` (`GodGoal`: kind, village or god,
  since when and weight) and `NextPlanTick` (ADR-036). Followers and influence are derived from the
  beliefs.
- `CreatureState` (ADR-010): a Tier 3 entity saved whole: position and heading, level, experience,
  health, loyalty, morality, `Needs` (hunger, fatigue, boredom), `Personality` (kindness,
  aggression, laziness, curiosity, and since ADR-037 courage, protectiveness, jealousy and
  playfulness), `Activity` (action, situation, phase, target, path, ticks
  left), `Preferences` (learned weight per situation and action) and `Memories` (recent deeds with
  the god's feedback). `CreatureCommand` (reward, punish, go to) is the intent the authority
  checks. `World/SimulationTime` fixes 10 ticks per simulated second.
- `GreatEvents/GreatEventState` (ADR-034): a great event under way in `WorldState.GreatEvents`
  (`GreatEventsState`, with the next id, the next eclipse, each god's last reformation and since
  when the land has been dry): its kind, name, start, set end, god and peak.
  `DisasterRules.GreatEvents` (`GreatEventRules`: `PlagueRules`, `EclipseRules`, `ReformationRules`,
  the watch's thresholds and the names) holds their numbers. Since ADR-035 the state also holds the
  ancient beast (`BeastState`: name, lair, phase, position, health, hunt, way, harm by god) and the
  next anomaly, and an event may have a place; `BeastRules` and `AnomalyRules` hold theirs.
- `Roads/RoadState` (ADR-033): a road between two villages in `WorldState.Roads` (with
  `NextRoadId`): its villages, its way, its length, the metres built and its wear.
  `SettlementRules.Resources` (`ResourceRules`) and `SettlementRules.Roads` (`RoadRules`) hold
  their numbers, `BuildingDefinition.Wood` and `Stone` what each building takes, and
  `WorldGenSettings.Resources` how generation lays the forests and stone.
- `Notables/NotableState` (ADR-032): a prophet or an elder, a Tier 3 individual saved whole in
  `WorldState.Notables` (with `NextNotableId`): kind, name, god, village, position, destination
  and path, time left to preach, born and death ticks, and `NotableMemory` deeds (rose, preached,
  endured, turned). `NotableRules` (in `DivineRules.Notables`) holds their numbers, and
  `DivineRules.TempleInfluenceStrength` and `TempleInfluenceReach` a temple's influence.
- `Villagers/VillagerState` (ADR-043): a villager a god follows, a Tier 3 individual saved whole in
  `WorldState.Villagers` (with `NextVillagerId`):
  - who they are: name, follower, village, the crowd slot they were chosen as, work, god and faith;
  - where: home, work place, shrine spot, position and path;
  - their state: activity and time left, refuge, hunger, fatigue and health;
  - their life: age when followed, lifespan, the last prayer and `VillagerMemory` deeds.

  `FollowVillager` is the command and `VillagerRejection` its refusals; `VillagerRules` (in
  `SettlementRules.Villagers`) holds their numbers. The events are `VillagerFollowed`,
  `VillagerFled`, `VillagerTurned` and `VillagerDied` (with a `VillagerFate`).
- `CastMiracleRequest` (god, miracle id, target): the intent that the authority checks (§73).
- `DiplomacyState` (ADR-012): `Relations` (one `GodRelation` per observer and subject: trust,
  fear, respect, rivalry, gratitude, suspicion and the `DiplomaticMemory` of acts), `Stances`
  (one `GodStance` per pair: `DiplomaticStatus` Alliance, Friendly, Neutral, Hostile or War, since
  when, and the last proposal), `Proposals` (alliance, peace, a treaty with its `TreatyTerms` or
  a request with its `RequestAsk`, waiting for an answer), and since ADR-025 `Treaties` (`Treaty`:
  kind, parties, target, payer and amount, secrecy, term), `Favours`, `Obligations` (promises of
  assistance or backing) and the `Council` (`CouncilState`: the session in progress with its
  `CouncilMotion` and votes, tabled motions, decrees in force). `DiplomaticCommand` (offer alliance,
  break alliance, declare war, request peace, accept, decline, propose or break a treaty, ask,
  absorb, demand surrender) is the intent the diplomacy authority checks, with a `WarAim` for a
  declaration; `DiplomacyRules.Politics` tunes treaties, favours and the council. Since ADR-026,
  `Wars` keeps each war's `WarRecord` (aggressor, defender, `WarGoal`, site, start, scores), and
  `DiplomacyRules.Conflict` tunes war goals, intelligence, proxy acts and holy wars. A settlement's
  society holds the `HolyWarState` it wages, if any.
- `DisasterState` (ADR-014): the `Earthquakes` under way (epicentre, magnitude, peak intensity,
  radius, end and the god who caused it) and `NextEarthquakeId`. Since ADR-029 also the
  `Tornadoes` and locust `Swarms` on the move with `NextHazardId`, the island's `Volcano` (its
  region and `VolcanoPhase`: dormant, rumbling, erupting) once found, the `Marks` left on the land
  (`TerrainMark`: crack, slide or crater, where and when) and the `BlightedSettlementIds` told of
  blight.
- `WeatherState` (ADR-013): `Cells` (`WeatherCell`: position, velocity, radius, pressure,
  temperature, humidity, precipitation, wind, electrical activity and the god who called it),
  `NextCellId`, `Spell` (normal, dry, wet, heat wave or cold wave, ADR-028), `SpellEndsTick` and
  `WindDirection`. A cell's weather is clear, rain, snow, fog, thunderstorm, windstorm or a
  hurricane (`WeatherKind`).
- `Events/`: transient `WorldEvent` records (`MiracleCast`, `FollowingChanged`, `RankAchieved`,
  `FamineStarted`, `DiplomacyChanged`, `WeatherSpellChanged`, `StormStruck`, `DroughtChanged`,
  `EarthquakeStruck`, `WildfireStarted`, `SettlementDamaged`, `MigrationStarted`, `MigrationEnded`,
  `MigrantsTurnedAway`, `CreaturesMet`, `CreatureFightEnded`, `DomainChosen`, `HolySiteContested`,
  `HolySiteClaimed`, `SchismStarted`, `SchismEnded`, `FloodStarted`, `ThirstStarted`,
  `PriorityChanged`, `BuildingCompleted`, `TechnologyAdvanced`, `SettlementFounded`,
  `SeasonChanged`, `LandScarred`, `TsunamiStruck`, `LandslideStruck`, `VolcanoChanged`,
  `TornadoTouchedDown`, `MeteorStruck`, `BlightChanged`, `SwarmArrived` and others;
  `StormStruck.Hurricane` and `DisasterKind.Hurricane` tell of hurricanes, and `DisasterKind`
  names every hazard that damages a settlement)
  raised during a tick or a command
  and never saved.
- Content contracts: `WorldGenSettings` (and its nested settings records), `SettlementRules`
  (the per-step balance of the settlement model, `game/Data/settlement_rules.json`),
  `DivineRules` (belief, worship, power, influence and ranks, `divine_rules.json`),
  `MiracleCatalog` and `MiracleDefinition` (`miracles.json`), `PantheonSetup` (the gods of a
  new world, their personalities and their Creatures' names, `gods.json`), `CreatureRules`
  with its `CreatureBondRules` and `CreatureCombatRules` (`creatures.json`), `GodAiRules` with one `GodProfile` per personality, including its
  diplomacy weights (`god_ai.json`), `DiplomacyRules` (`diplomacy.json`), `WeatherRules`
  (`weather.json`), `DisasterRules` (`disasters.json`), `MigrationRules` (`migration.json`),
  `HistoryRules` (`history.json`), `DomainRules` with its slots, definitions and `DomainBoons`
  (`domains.json`), `HolySiteRules` (`holy_sites.json`), `WaterRules` (`water.json`, ADR-022) and
  `SocietyRules` (`society.json`, ADR-023). What technology and buildings do lives with the rules
  of the system it changes: `SettlementRules.Civics`, `DisasterRules.Preparedness`, the temple and
  religion factors in `DivineRules`, and unhappiness, unrest and navigation in `MigrationRules`.
  `DivineRules.Culture` (`CultureRules`) holds the culture's numbers (ADR-024).
  `WorldGenSettings.Hydrology` (optional) shapes the rivers and lakes. `DivineRules` carries the
  `SchismRules` and the holy sites' influence, and `CreatureRules` the `CreatureSpecies`
  (ADR-021). `WorldSizes` lists the `WorldSizePreset`s a new world may be made at, with extra
  settlement and river names (`world_sizes.json`), `PantheonSetup.ExtraGods` the gods larger
  worlds add, and `DivineRules.ConquestShare` and `CulturalShare` the other victories' thresholds
  (ADR-027). `CreatureRules.Callings` (`CallingRules`: the calling levels, which deeds lead to which
  path, the paths' effects and the attributes' growth) and `DivineRules.Hand` (`HandRules`) shape
  the paths and the hand (ADR-030). `WeatherRules.Seasons` (`SeasonRules`: the season's length and each season's
  `ClimateEffects`) and `WeatherRules.Extremes` (`ExtremeRules`: snow, fog, hurricanes, heat and
  cold waves), and `EcologyRules` (`ecology.json`), shape the year and the land's life (ADR-028).
  `DisasterRules.Hazards` (`HazardRules`: tsunamis, landslides, the volcano, tornadoes, meteors,
  blight, locusts and how long marks last) shapes the hazards, and `Preparedness` and
  `SettlementRules.Civics` what sea walls, dams, evacuation routes and irrigation do (ADR-029).

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
  4. The land's reshapings (ADR-045) are sculpted into the ground by `TerrainReshaper`, and the
     moved ground's climate is found again. The field keeps the ground as the noise made it
     (`Ground`), so `TerrainGenerator.Reshape` can make a reshaped terrain without the noise.
  5. With `Hydrology` set, rivers and lakes are found and cut into the land (ADR-022):
     `WorldGen/FlowGrid` routes water by a priority flood, `HydrologyGenerator` finds the lakes and
     rivers and `ChannelCarver` cuts the channels; the result is the field's `HydrologyMap`.
  6. Each sample is classified by data-driven `BiomeRule`s (first match wins): the climate's
     biomes, including tundra, desert and swamp (ADR-038).
  6. With `Landforms` set, `WorldGen/LandformMarker` marks the biomes place decides, in order:
     islands, the volcanic land around the highest land region (the one `HazardModel.FindVolcano`
     takes), river valleys and coasts (ADR-038).
- `WorldGen/RegionBuilder` aggregates samples per region (means plus majority biome; with
  landforms, land when most samples are land and then its most common land biome) and derives
  fertility and vegetation from the biome's traits (`RegionBuilder.Traits`: its climate rule or its
  landform traits); `ResourceFounder` lays the forests and stone from the biomes and the
  seed's noise (ADR-033). `SettlementFounder.FoundAll` places up to `Count` settlements (8 on a
  small island, 20 or 40 on larger ones) on
  the best habitable regions, at least `MinimumSpacing` regions apart so their farmland never
  overlaps, with names and populations drawn from seeded streams. `PantheonFounder` creates the
  gods of `gods.json` (with as many of its extra gods as the size asks), each followed in its own
  home settlement, best site first.
- `Settlements/SettlementEconomy`: the settlement rules as pure functions: the farmland survey,
  the labour split into farmers and builders, production with diminishing returns,
  consumption, storage, health, birth and death chances, and `Assess`, which previews the next
  step for the debug panel. `Settlements/SettlementSystem` applies one Tier 1 step to every
  settlement (see [Settlement simulation and population tiers](#settlement-simulation-and-population-tiers)).
  `SettlementRulesValidator` lists every invalid rule.
- `Belief/BeliefModel`: the belief rules as pure functions (conditions, drift, pull and push,
  conversion within the free share, worshippers, worship intensity, Divine Power, impulses).
  `BeliefSystem` applies one belief step (see [Gods, belief and miracles](#gods-belief-and-miracles)).
  `InfluenceField` derives a god's influence from its followers, its Creature, its temples, its
  prophets and the holy sites it keeps (ADR-032). `GodStanding` counts followers,
  sums power and picks ranks.
- `Miracles/MiracleBook` validates and indexes the miracles; `MiracleCaster` checks requests
  (§73: authorisation, a signature miracle's domain, rank, cooldown, place, influence, a target
  to act on, power), computes costs (less for the god's own domains and its holy sites) and
  applies accepted casts through `MiracleEffects` (settlements, land, weather, ground, Creatures
  and the gods' views).
- `Creatures/`: `CreatureModel` (growth, needs and learning as pure functions),
  `CreatureTemperament` (the traits behind each action, judgement, guarding, envy, defending and
  wounds, ADR-037), `CreatureBrain`
  (options and utilities, decisions, planned routes), `CreatureDeeds` (what a finished action does
  to the world), `CreatureSystem` (the per-tick step), `CreatureModes` (the hierarchical state
  machine, ADR-047), `CreatureTree` (the behaviour trees that carry out actions), `CreatureAuthority` (rewards,
  punishments and go-to, checked) and `CreatureFounder` (see [The Creature](#the-creature)).
  `CreatureBonds` (views and urges), `CreatureMeetings` and `CreatureCombat` are the Creatures'
  dealings with each other (see [Creature relationships](#creature-relationships)).
  `CreatureCalling` (callings and taking up paths), `CallingEffects` (what the paths add to deeds,
  fights and disasters) and `CreatureAttributes` (intelligence, resilience, divine affinity) are the
  paths and attributes of ADR-030.
- `Hand/`: `HandAuthority` checks and carries out the Divine Hand's commands, with `HandModel`
  (belief), `HandCarry` (villagers set down), `HandThrow` (things thrown), `HandCreatures` (the
  Creature lifted) and `HandOfferings` (offerings taken and given, ADR-039). See [Paths, the hand and the Divine Acts](#paths-the-hand-and-the-divine-acts).
- `Miracles/DivineActs`: hallowing the land, waking the volcano and stilling every disaster
  (ADR-030), applied by `MiracleEffects`. `Miracles/Judgement` holds the Justice domain's judgement
  (ADR-022).
- `Land/` (ADR-045): `LandShaper` plans an island, a gorge or a river's new course where a god
  points, keeps it in `WorldState.Land`, makes the terrain again and settles the regions and water;
  `LandActs` does the three acts for `MiracleEffects` on the terrain in `LandStage`
  (`MiracleCaster.Land`). See [Reshaping the land](#reshaping-the-land).
- `GreatEvents/`: `PlagueModel` (ripeness, course, shield, catching, cure), `EclipseModel`,
  `ReformationModel`, `GreatEventWatch` (world war, great drought, great migration),
  `GreatEventModel` (beginning, ending, names and an eclipse's awe) and `GreatEventSystem`;
  `BeastModel` (the lair, waking, harm and the fall), `BeastSystem` (its rampage, fights and calls)
  and `AnomalyModel` (the surge, the gods' power and cheaper miracles, ADR-035). See
  [Great events](#great-events).
- `Resources/`: `ResourceModel` (what a village wants in store, its gatherers, what they bring in
  and take from the land, the wood a house takes) and `ResourceSystem` (timber growing back and
  burning). `Roads/`: `RoadModel` (open roads, the way along one, a walker's speed, the next road
  to plan) and `RoadSystem` (planning, building with stone, wear and mending, trade and food).
  See [Wood, stone and roads](#wood-stone-and-roads).
- `Notables/`: `NotableModel` (who is devout, raising a notable, a prophet's next village, a
  sermon, memory) and `NotableSystem` (elders chosen, keeping faith and turning, prophets called,
  walking and preaching, deaths and martyrdom). `MiracleEffects` raises an anointed prophet. See
  [Prophets and elders](#prophets-and-elders).
- `Villagers/`: `VillagerAuthority` (follow and release), `VillagerSystem` (needs, choosing, danger
  and flight, homes, faith, death), `VillagerMind` (the scored options, pure), `VillagerModel` (age,
  memory, danger, walking, ways and new homes), `VillagerWitness` (memories from events, run by the
  engine after each tick and each command) and `VillagerRulesValidator`. See
  [The selected villager](#the-selected-villager).
  `Navigation/RegionPathfinder` runs A* over land regions and smooths the path.
- `Gods/GodAi` is the strategic utility AI of AI gods (see [Rival gods](#rival-gods)), run by
  `GodAiSystem`; `Gods/MiracleValuer` weighs what each miracle is worth to it, and
  `Gods/GodDiplomat` adds its diplomatic moves. It takes up its personality's domains and sends
  its Creature to claim holy sites (ADR-021). `Gods/GodPlanner` makes its major plan every 30 s
  and `Gods/GoalFocus` bends its choices towards the goals (ADR-036); see
  [Known gods and plans](#known-gods-and-plans). `Belief/VictorySystem` tracks
  each god's standing (alone or with its bloc), dominance and victory, by the victories the world
  was made with. `GodAiSystem.IsTurn` lets the AI gods take turns across the evaluation interval,
  and `Belief/InfluenceCache` builds each god's influence field once per evaluation for
  `MiracleCaster.Check` (ADR-027). See [World sizes and victories](#world-sizes-and-victories).
- `Diplomacy/`: `DiplomacyModel` (finding and creating relations and stances, grudge, warmth),
  `DiplomacyLedger` (the `IActRecorder` that miracles, Creature deeds and conversions report
  acts to), `DiplomacySystem` (the diplomacy step), `Acquaintance` (who has met whom, run in the
  diplomacy step, ADR-036) and `DiplomacyAuthority` (diplomatic commands, checked). See
  [Diplomacy](#diplomacy).
- `Politics/`: `TreatyModel` and `CouncilModel` (what treaties and decrees allow and forbid, asked
  by each system they change), `PoliticsChecks`, `TreatyBook` (signing, ending, breaking and
  revealing treaties), `FavourBook`, `PoliticsActions` (treaties, requests and absorption for the
  authority), `CouncilChamber` (sessions, votes and decrees), `CouncilAuthority` (tabling and
  voting, checked), `Defiance` and `PoliticsSystem` (the politics step: terms, tributes, leaks,
  promises, the council). `Gods/TreatyAdvisor` and `Gods/CouncilAdvisor` are the AI's side
  (ADR-025). See [Treaties and the Divine Council](#treaties-and-the-divine-council).
- `Conflict/`: `IntelModel` (what each god knows of another, derived), `WarBook` (war records,
  scores, goals met), `WarAims` (the AI's aims and answers to surrender), `Covert` and `Veil`
  (veiled miracles and the tracing of covert acts), `ConflictAuthority` (proxy acts and words to
  holy wars, checked) and `HolyWarSystem` (mortal holy wars). `Gods/CovertAdvisor` is the AI's
  side (ADR-026). See [War, intrigue and holy wars](#war-intrigue-and-holy-wars).
- `Weather/`: `WeatherModel` (cells' rain, wind and lightning, coverage, the weather at a point,
  soil water, summoned rain) and `WeatherSystem` (the weather step). See [Weather](#weather).
  `ClimateModel` (the season from the tick, a season's and a wave's effects together, the next
  spell) and `SeasonSystem` (the season step: new seasons, spoiling, sickness) since ADR-028.
- `Ecology/`: `EcologyModel` (the land's capacity for animals, health, biodiversity, healing and
  scars, hunting) and `EcologySystem` (the ecology step). See
  [Seasons, extremes and the land's life](#seasons-extremes-and-the-lands-life).
- `Disasters/`: `DisasterModel` (quakes, shaking, flammability, ignition, fire threat, danger,
  shields), `Wildfires` (lighting, burning, spreading, regrowth), `DisasterDamage` (what a quake,
  fire or flood does to a settlement) and `DisasterSystem` (the disaster step). `Belief/Hazards`
  carries a settlement's drought, danger and thirst into its conditions. Since ADR-029
  `HazardModel` (the volcano, lava, waves, slides, impacts, spawning), `HazardSystem` (the hazard
  step) and `LivingHazards` (tornadoes, locusts, blight) run the rest of §19's hazards, and
  `DisasterDamage.Strike` applies any of them to a settlement. Since ADR-048 `DisasterDamage.Destroy`
  weakens cracked houses and cracks those left standing (`SettlementState.Integrity`,
  `DisasterRules.Structure`), `Mend` repairs them, and `FloodStanding` spares houses a share of a
  flood; `HazardModel.FallingStar` is a falling star's whole blow, for the hazard step and the debug
  tools. See [Disasters](#disasters) and [Hazards](#hazards).
- `Society/`: `SocietyModel` (targets of safety, happiness and stability, wealth, learning,
  focus, the next building), `SocietySystem` (the society step), `Expansion` (settlers founding
  villages), `SocietyAuthority` (priorities, checked) and `PriorityAdvisor` (AI gods' priorities).
  `Settlements/CivicModel` turns technology, buildings and priorities into the settlement model's
  factors (ADR-023). See [Settlements' society](#settlements-society).
- `Culture/`: `CultureModel` (secular stages, dependency and resentment, doctrine and secular
  targets, the factors culture puts on worship, conversion, miracles, learning, founding and
  welcome), `ConductModel` (acts noted, fading and traits), `WitnessCulture` (what witnessing a
  miracle adds, and what the world notes of a cast) and `CultureSystem` (the culture step:
  doctrines, secularism, missionaries, charity, revolts and traits; ADR-024). See
  [Culture](#culture).
- `Water/`: `WaterModel` (a river's level, overflow, spreading floods, waves) and `WaterSystem`
  (the water step: levels, floods, silt, irrigation, drowning). `WorldGen/WaterFounder` records
  the rivers and lakes in the state. See [Rivers, lakes and floods](#rivers-lakes-and-floods).
- `Migration/`: `MigrationModel` (pressure, abandonment, appeal, conversion, merging),
  `MigrationSystem` (the migration step), `MigrationAuthority` (refugee policies) and
  `RefugeeAdvisor` (AI gods' policies). See [Migration](#migration).
- `History/HistoryRecorder` weighs the events of each tick, and of commands through
  `SimulationEngine.Chronicle`, and keeps the significant ones as the world's history (ADR-020).
- `Domains/`: `DomainModel` (slots by rank, who holds what, cost factors), `DomainAuthority`
  (taking up a domain, checked) and `DomainSystem` (the lasting boons). `HolySites/`:
  `HolySitePlacer` (finding the sites in the land), `HolySiteSystem` (claims and keepers' gains)
  and `HolySiteModel`. `Belief/Schisms` splits off and evolves Old Faiths within the belief step.
  See [Domains, holy sites and schisms](#domains-holy-sites-and-schisms).
- `Systems/IntervalSystem` runs a wrapped system on the last tick of every interval of N ticks.
  `Systems/Authorities` bundles the miracle, Creature, diplomacy, migration, domain, society,
  council and conflict authorities; the first three, the migration step and the holy sites share one ledger.
- `SimulationEngine.Step(WorldState, events)` runs the systems in order (the weather step, the
  season step, the water step, the disaster step, the ecology step, the settlement step, the belief step, the society step, the culture step, the holy war step, the domains' boons, the holy sites, the
  victory check, the diplomacy step, the politics step and the migration step, each in an
  `IntervalSystem`, then the Creature system every tick, then the AI gods, each once every
  `EvaluationIntervalTicks`, in turn across it) with a
  `SimulationRandom` restored from `WorldState.Random`, collects the events they raise, writes
  the RNG state back and increments `Tick`.
- `Terrain/TerrainRaycaster`: ray against the heightfield (used for cursor picking).

### Networking (`Pantheon.Networking`)

- `NetworkProtocol`: the protocol version (21 since ADR-048, whose settlements carry their houses'
  integrity; 20 since ADR-045, for `WorldSection.Land`; 19 since
  ADR-043, for `VillagerIntent` and the followed
  villagers sent with the notables; 18 since ADR-039, for the hand's offering and its
  action; 17 since ADR-038, whose regions and generation carry the
  new biomes and landforms; 16 since ADR-037, whose Creatures carry their new traits;
  15 since ADR-036, whose Gods section carries the gods
  each knows and its goals; 14 since ADR-035, whose GreatEvents section carries the
  beast; 13 since ADR-034, which added the GreatEvents section; 12
  since ADR-033, which added the Roads section and regions' forest, timber and stone; 11 since
  ADR-032, which added the Notables section; 10
  since ADR-030, which added `HandIntent` and whose Creatures, gods and regions carry paths, what
  hands hold and hallowed land), the default port (7777), the host's peer id and the most players.
- `Protocol/NetMessage` and its records: `Hello`, `Welcome`, `Refused`, `Snapshot`,
  `SectionUpdate` (one `WorldSection` of the world, ADR-019), `EventBatch`, `PlayerList`,
  `ViewFocus`, the intents (`CastIntent`, veiled or not, `CreatureIntent`, `DiplomacyIntent` with a
  treaty's terms, a treaty, an ask or a war aim, `RefugeeIntent`, `DomainIntent`, `PriorityIntent`,
  `CouncilIntent`, `ProxyIntent`, `HolyWarIntent`),
  `Verdict` and `Goodbye`. `MessageCodec` writes them as JSON tagged with their type, gzipped from
  512 bytes; `EventCodec` carries any `WorldEvent` by its type name.
- `Transport/INetworkTransport`: connect, receive, send reliably or not, disconnect, round trip.
  `LoopbackNetwork` is an in-memory host and players for headless tests; the client's
  `Network/EnetTransport` is Godot's ENet. See [Multiplayer](#multiplayer).

### Persistence (`Pantheon.Persistence`)

- The player's profile (ADR-060): `Profile/IProfileStore` keeps values (the settings, progress),
  the worlds started (`WorldRecord`: name, god, seed, size, victory, seasons, species, created,
  last played, time played, tick, followers, rank, outcome) and their saves (`SaveRecord`: kind,
  name, tick, time, save version and the save's gzipped compact JSON). `SqliteProfileStore` keeps
  them in one SQLite file (Microsoft.Data.Sqlite, WAL, cascading deletes, a schema version);
  `ProfileStores.Open` falls back to `FileProfileStore` (an index and one file per save) when
  SQLite's native library or the file cannot be opened. A world keeps every manual save and only
  its newest autosave and quicksave. The core never reads the clock: times come from the client.

- Save envelope: `{ "SaveVersion": 34, "World": { ... } }`, indented, enums as strings,
  property names exactly as in C#, strict members.
- Loading reads `SaveVersion` first. A current save deserializes directly. An older save, back
  to `MinimumSupportedSaveVersion` (1), is parsed into a `JsonObject`, upgraded one version at
  a time by `SaveMigrations` (versions 1–5) and `RecentSaveMigrations` (from version 6), then
  deserialized with the same strict options, so a migrated
  save gets exactly the checks of a current one. Version 1 → 2 gives every settlement
  `"Health": 1` and `"ConstructionProgress": 0`. Version 2 → 3 adds the Milestone 3 player god
  (Ilyra, 100 Divine Power) followed by 60% of the first settlement, empty beliefs elsewhere, no
  blessing on any region and founding settings for one settlement. Version 3 → 4 gives every god
  the Milestone 4 newborn Creature, Bramble, at the settlement where most of the people follow
  it. Version 4 → 5 gives every god no personality and no dominance, and the world no winner.
  Version 5 → 6 adds empty diplomacy: no relations, no stances (every pair neutral) and no
  proposals. Version 6 → 7 adds clear skies (no cells, a normal spell not yet scheduled, a west
  wind) and gives each region its climate's moisture as soil water. Version 7 → 8 adds no
  earthquakes, no fire or scorch, and no ruins or shields. Version 8 → 9 turns the one winner
  into the list of winners (that god, or none). Version 9 → 10 adds no migration groups, no wish
  to leave and a welcoming refugee policy for every god. Version 10 → 11 gives every Creature no
  combat skill, scars or views of the others, and marks nothing it did as concerning another
  Creature. Version 11 → 12 gives the world an empty history. Version 12 → 13 gives every god no
  domains, every settlement no Old Faith or sanctuary and the world no holy sites; a session finds
  the sites as a new world would. Version 13 → 14 gives the world no rivers or lakes, every region
  no fresh water or flood and every settlement full wells (six per unit of housing); its
  generation settings have no hydrology, so its land regenerates unchanged. Version 14 → 15
  (`SocietySaveMigration`) gives every settlement a newly founded society. Version 15 → 16 gives
every settlement a culture with no doctrine and no secularism, every belief (a settlement's or a
migration group's) no dependency or resentment, and every god no conduct or traits. Version 16 → 17
(`PoliticsSaveMigration`) gives the world no treaties, favours or promises and a council that has
not met. Version 17 → 18 gives the world no war records and every settlement no holy war. Version
18 → 19 (`VictorySaveMigration`) makes the world won by ascendancy alone and starts no god's
conquest or cultural clock. Version 19 → 20 (`NatureSaveMigration`) keeps the world's even
climate, without seasons, and gives its regions no snow, harm, scars or pollution and their
animals at their cover. Version 20 → 21 (`HazardSaveMigration`) gives regions no blight or rubble
and the disasters nothing on the move, no marks, no village told of blight and a volcano still to
be found. Version 21 → 22 (`CallingSaveMigration`) gives Creatures no paths or callings, every hand
nothing and no land hallowed. Version 22 → 23 (`NotableSaveMigration`) starts with no prophets or
elders and gives the history's records no name. Version 23 → 24 (`ResourceSaveMigration`) grows
each region's forest from its biome and vegetation, gives each village a new world's stores and
starts with no roads. Version 24 → 25 (`GreatEventSaveMigration`) makes nobody sick or immune,
starts no great event and sets the first eclipse on the first step. Version 25 → 26
(`BeastSaveMigration`) leaves the beast's lair to be found, sets the first anomaly on the first step
and gives the events under way no place. Version 26 → 27 (`PlanSaveMigration`) has every god know
every other, hold no goal and plan on its next turn; `PlanIntegrity` checks known gods and goals.
Version 27 → 28 (`TemperamentSaveMigration`) gives every Creature 0.5 of courage, protectiveness,
jealousy and playfulness. Version 28 → 29 (`LandformSaveMigration`) gives the generation no
landforms, so the terrain grows as it was. Version 29 → 30 (`OfferingSaveMigration`) gives
whatever a hand holds no stores. Version 30 → 31 (`VillagerSaveMigration`) gives the world no
followed villagers; `VillagerIntegrity` checks them (one for each god, known villages and gods,
needs and faith in 0..1, a life longer than their age). Version 31 → 32 (`LandSaveMigration`)
gives the land no reshapings, and `LandIntegrity` checks them. Version 32 → 33
(`StructureSaveMigration`) gives every settlement sound houses (integrity 1), and the save checks
keep it in 0..1. Version 33 → 34 (`AnimalSaveMigration`, ADR-061) makes each Creature of the four
old species the animal of its nature: a Mossback a Cow, a Stonehide a Tortoise, a Swiftclaw a Tiger
and a Lumen an Ape. A missing,
  unsupported or newer version
  raises `SaveFormatException` with a clear message.
- `SaveIntegrity` then checks the world's structure and lists every inconsistency: the grid
  against the generation settings, settlement ids and regions, non-negative quantities, health
  and blessing in 0..1, god ids and Divine Power, and beliefs (known gods, one each, values in
  0..1, shares adding up to at most 1), Creatures (`CreatureIntegrity`: owners, home and
  target settlements, levels, values in range, combat skill and scars, one view per other known
  Creature, references to Creatures that exist), diplomacy (`DiplomacyIntegrity`: known gods,
  one relation per observer and subject and one stance per pair, values in range), soil moisture
  in 0..1, and weather (`WeatherIntegrity`: unique cell ids below the next id, finite positions,
  values in range, known gods), fire and scorch in 0..1, ruins and shields not negative, and
  disasters (`DisasterIntegrity`: unique quake ids below the next id, valid values, known gods),
  rivers and lakes (`WaterIntegrity`: unique ids, names, regions that exist, valid levels), fresh
  water and flood in 0..1, water not negative, societies (`SocietyIntegrity`: values in range,
  each building once, temples to known gods, known priorities), culture (`CultureIntegrity`:
  doctrines, secularism, dependency and resentment in 0..1, conduct not negative, each trait once),
  politics (`PoliticsIntegrity`: unique treaty ids below the next, known gods as parties, payers
  and targets, terms that fit the kind, favours and promises between known gods, valid motions,
  decrees and votes), conflict (`ConflictIntegrity`: wars between two known gods with a known goal
  and finite scores, holy wars on another existing settlement for known gods),
  and migration (`MigrationIntegrity`: unique group ids below the next id, known settlements, valid
  sizes, food, health and positions, and beliefs as a settlement's), and holy sites and faiths
  (`HolySiteIntegrity`: unique sites in known regions with known gods, Old Faiths of known gods
  that leave room for the followers, sanctuaries of known gods), gods' domains (known, once each),
  notables (`NotableIntegrity`: unique ids below the next, known kinds, gods and villages,
  finite places and possible lives), and resources and roads (`RoadIntegrity`: forest, timber and
  stone in 0..1 with timber within the forest, stores not negative, unique road ids below the next
  between two known villages, ways of two points or more, built within the length, wear in 0..1),
  and great events (`GreatEventIntegrity`: plague and immunity in 0..1, unique event ids below the
  next with known kinds and gods, names and possible ticks, reformations of known gods, and the
  beast's name, lair, health, village and harm by known gods).
- Saves are written atomically (temporary file, then rename).
- `ContentJson` parses the content files with the same strict options (comments and trailing
  commas allowed).

### Application (`Pantheon.Game`)

- `WorldMaker` (ADR-027) makes new worlds from `NewWorldOptions` (the Creature's species, the
  size and the victories): `AtSize` turns a `world_sizes.json` size into generation settings and
  gods, and `Validate` checks every size as part of the content's strict parsing.
- `GameSession`: creates or loads worlds, owns `WorldState` + `TerrainField`, runs the
  scheduler, saves and loads (`SessionStore` reads a save with its reshaped terrain), and raises
  `WorldReplaced` so presentation rebuilds, and `TerrainChanged` when a Divine Act reshaped the land
  (ADR-045; a replica's `SyncLand` makes its terrain again when the host's reshapings arrive). It is the local
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
  ticks per frame so a long frame cannot cause a spiral of death). Since ADR-056 it also keeps to
  a CPU budget (§105): once the ticks of a frame have spent `BudgetSeconds`, measured by the client
  and passed to `GameSession.Update`, the ticks still due wait for the next frame, up to
  `MaxBacklogTicks`. The budget is the player's "Simulation budget" setting.
- `Settings/GraphicsScaling` (ADR-056) scales the presentation content by the player's citizens,
  vegetation (the ground cover's and the forests' reach) and terrain quality (the fallback's
  detail; Terrain3D's mesh size through the client's `GraphicsScale`, which also scales every
  particle system).
- The client reacts to a busy world at a pace (ADR-056): a village's roster and walkers' grid are
  rebuilt at most every `RosterRefreshSeconds`, and the grid keeps the land under it
  (`Population/LocalNavLand`) while its frame holds; the flood sheet is looked at once a second and
  `FloodSurface.Mesh` skips dry rows and regions; the forests redraw only the chunks about a
  changed clearing or a new road.
- `Camera/GodCameraModel`: engine-agnostic god-camera math: focus, yaw and distance targets
  with exponential smoothing, pitch from zoom, bounds, terrain clearance, and the zoom bands
  Close (≤ 120 m), Regional (≤ 700 m) and Strategic (≤ 2600 m). `MaxDistance` grows with the
  island's width beyond 2 km (ADR-027).
  The player tilts the view within 30° of the zoom's pitch (`TiltBy`); a trackpad pans by a share
  of the view (`PanViews`) and pinches (`ZoomByFactor`); a let-go grab flings the view on (`Fling`),
  and `AimAbove` frames a followed Creature by its body (ADR-058).
- `Diagnostics/DebugReadout`: formats the debug overlay model from state (pure), with
  `SettlementReadout` for the settlement lines. `Diagnostics/RegionDebugMap` builds the F3
  region debug map image (pure). `Diagnostics/PerformanceReadout` words the client's
  `PerformanceSample` (with a `SaveTiming`) and the world's load (ADR-044), and `NetworkReadout`
  says who decides and, for a joined player, how old each section is (`MultiplayerClient.SectionTicks`).
- `Performance/StressScenario` makes §104's stress world (ADR-044): a world size of the largest
  island with 50 villages and 8 gods, the crowd drawn in thousands, and `Apply` for the 100,000
  people, 20 weather cells and three disasters. See [Performance testing](#performance-testing).
- `Launch/LaunchOptions`: parses user arguments `--seed=`, `--load=`,
  `--terrain=auto|terrain3d|fallback`, `--advance-ticks=`, `--camera-distance=`,
  `--quit-after-frames=` and `--screenshot=`. Bad input becomes a warning, never an error.
- `Presentation/Animals/` (ADR-061): the Creatures' bodies as pure data and maths. `Sdf` holds
  signed-distance parts (ellipsoids, round cones, round boxes) that `SdfShape` blends smoothly
  and paints by the nearest part's `PaintTag`; `SurfaceNets` meshes a shape into positions,
  gradient normals, colours and outward triangles. `AnimalPlans` gives each body (Cow, Ape, Tiger,
  Lion, Wolf, Bear, Horse, Tortoise and the Beast) its `AnimalProportions`, parts and `FacePlan`,
  sharing the torso, limbs, paws, hooves, hands and stumps of `AnimalParts`; `AnimalPaint` paints
  fur, belly, muzzle and the rest with a species' `AnimalPalette` and its pattern (stripes,
  patches, plates). `AnimalMotion` moves them: a walk whose planted foot stays where it landed and
  whose swinging foot arcs to the next step, two-bone `Reach` for hip and knee, and the standing,
  sitting, working, fighting, attacking, playing and dangling `BodyPose`s.
- `Presentation/*`: engine-agnostic presentation data shared by both terrain renderers (colour
  map, fallback mesh chunks, water depth map, region lines, labels) and `Terrain3DShaderPatch`.
  `SettlementLayout.ComputeSite` lays out a settlement's houses, building site and fields as a
  `SettlementSite`, tuned by `SettlementViewSettings` (`game/Data/settlement_view.json`).
  `ReligiousStyles` (ADR-042) gives a village its god's style:
  - `Of` scores a `StyleMotif` (gardens, monuments, columns) from the main god's domains and
    traits, the village's doctrines and its people's love, fear or faith, times the god's hold;
  - `Layout` places the `StylePiece`s, monuments and columns in a ring about the shrine clear of
    the temple, and gardens among the houses;
  - both are tuned by `StyleSettings` and never saved.
  `PresentationHash` (SplitMix64) gives presentation variation without consuming a simulation
  random stream. `PresentationContent` bundles the biome palette and the settlement view
  settings.
- `Hud/HudReadout` builds the player HUD model (spec §57), with the numbers behind its gauges
  (ADR-059: power, followers, the next rank, belief shares, calendar, pause, speed, `RivalChip`s,
  and the Creature's needs, loyalty and morality): god and rank, Divine Power and income,
  followers and the next rank, the miracle bar (hotkey, cost, cooldown, locked, affordable), the
  target hint and the Creature card (level, activity, needs, loyalty, morality, combat skill,
  scars, its bonds with the Creatures it has met, what feedback would judge), the rival gods with their status towards the player, the race for victory (a bloc's
  shared share when allied, ADR-015; a sandbox, a conquest or cultural victory under way, or what
  the player must do when ascendancy is off, ADR-027), and the
  weather and dangers when they matter (a dry or wet spell; droughts, storms, fires and earthquakes
  among the player's people).
  `Divine/DivineText` words ranks, personalities, clocks and cast verdicts;
  `Creatures/CreatureText` words Creature activities and command outcomes;
  `Creatures/CreatureBondText` words how Creatures regard each other, and
  `Creatures/CreatureCommands` brings another Creature to the player's (K);
  `Creatures/CreatureGrowthText` words its paths, callings and attributes (ADR-030), and
  `Creatures/TemperamentText` its character in §26's words; `CreatureCommands.SetTemperament`
  serves `--temperament` (ADR-037).
- `Book/BookReadout` builds the Book of the World's pages (`WorldPage`, `ReligionPage`,
  `MiraclesPage`, `CreaturePage`, `VillagerPage`: `BookPage` of `BookLine`s, ADR-031, ADR-043); `Presentation/StateOverlays`
  paints the population, disaster risk and political control overlays; `Camera/GodCameraModel`
  toggles the strategic overview.
- `GreatEvents/GreatEventText` words the great events and the plague, and the eclipse's darkness
  for the client; `GreatEvents/GreatEventCommands` starts one (`;`, `--great-event`);
  `Alerts/GreatEventAlerts` tells of them and `Diagnostics/GreatEventReadout` gives their F1 lines
  (ADR-034).
- `Resources/ResourceText` words the stores, roads and forests, and `Resources/ResourceCommands`
  builds every planned road (`--roads`) or sets the stores (`--stores`); `Alerts/RoadAlerts` tells
  of roads, `Diagnostics/ResourceReadout` gives their F1 lines, and `Presentation/ForestLayout` and
  `RoadLayout` place the trees and the roads' tracks (ADR-033), the tracks rounded and laid on the ground (ADR-073).
- `Notables/NotableText` words prophets and elders and `Notables/NotableCommands` raises a prophet
  (`--prophet`); `Alerts/NotableAlerts` tells of them, `Book/NotableSection` lists them on the
  World and Religion pages, and `Diagnostics/NotableReadout` gives their F1 lines (ADR-032).
- `Villagers/VillagerCommands` picks the crowd figure at the cursor (from the same roster and
  routines the client draws), follows and releases (/, Shift+/, `VillagerIntent` for a joined
  player, `--villager`), and `Villagers/VillagerText` words them without assuming pronouns;
  `Book/VillagerPage` tells their life, `Diagnostics/VillagerReadout` gives their F1 lines and
  `Alerts/VillagerAlerts` tells of their flight, turning and death (ADR-043).
- `Hand/HandCommands` uses the player's hand (R, Shift+R, Ctrl+R for an offering, `HandIntent` for a joined player) and
  `Hand/HandText` words it; `Alerts/PowerAlerts` tells of hands, paths and Divine Acts (ADR-030).
- `Disasters/DisasterText` words fire causes, disasters and the marks on labels;
  `Disasters/DisasterCommands` starts an earthquake (F11) or a fire (F12) at a point for debugging,
  and `Disasters/HazardCommands` any hazard (I, `--disaster`); `Alerts/HazardAlerts` tells of the
  hazards (ADR-029).
- `Weather/WeatherText` words spells, kinds, the wind's direction and droughts;
  `Weather/WeatherCommands` drives the weather for debugging (F8 next spell, F10 a storm at the
  cursor). `Weather/SeasonText` words the seasons and `Weather/SeasonCommands` begins one (Y,
  `--season`); `Alerts/NatureAlerts` tells of seasons and scarred land (ADR-028).
- `Gods/GoalText` words the AI gods' goals and how gods met; `Alerts/GodAlerts` tells the player
  whom they met, and `PoliticsCommands.MeetAll` introduces every god (`--gods-met`, ADR-036).
- `Diplomacy/DiplomacyReadout` builds the diplomacy panel's entries (whether the player has met
  the god, status, the rival's ties
  with the other gods, how the two gods' Creatures regard each other, its attitude and why, the
  player's grievances, the proposal waiting and the moves allowed now), and
  `DiplomacyText` words statuses, commands, memories and rejections.
- `Alerts/AlertFeed` turns events into alerts for the local player (§59): only those about the
  player's god or followers, plus wars, betrayals and alliances between other gods and every
  god's or bloc's dominance and victory; never the same alert twice within 30 simulated seconds,
  the most urgent kept when full.
- `Presentation/OverlayMap` builds the map overlays (§58): influence (each region in the colour of
  the strongest god, opacity by its influence), religion (each settlement's lands in its main god's
  colour), faith and food (each settlement's lands on a red-to-green ramp), weather (each land
  region's soil water, brown to yellow to blue), danger (fires, ash and shielded settlements) and
  migration (each settlement's lands by the pressure to leave, and every group's way in its god's
  colour) and culture (each settlement's lands in the colour of its strongest doctrine, grey where
  it has turned from the gods). `NavigationOverlay` shows walkable land, open roads, rubble and
  every walker's way, and `AuthorityOverlay` who commands each village's god (ADR-044). `Presentation/CloudLayout` lays out each weather
  cell's puffs, colour, opacity, rain and lightning from `PresentationHash` and
  `WeatherViewSettings` (`weather_view.json`), and how much a storm dims the light.
  `DivineViewSettings` (`divine_view.json`) holds the god colours, overlay opacity and miracle
  looks. `SettlementLabel` adds the main god's share; `SettlementPicker` finds the settlement
  nearest a point.
- `Multiplayer/MultiplayerHost` and `Multiplayer/MultiplayerClient` share a world (ADR-018); see
  [Multiplayer](#multiplayer). `GameSession` holds either the host's world or a player's replica,
  and every player command can name its issuing god (`CastMiracleAs` and the like), which the host
  uses for its players' intents. `SessionEvents` keeps events for the local player and, on a
  host, for the players.
- `History/HistoryText` and `HistoryReadout` word the chronicle ("Year 12 — The Shaking of Elar");
  `Flow/EndSummary` builds the end screen, `Flow/Guide` the first-minutes guide (ADR-020).
  `Flow/Tutorial` (ADR-064) is the tutorial's fifteen steps: each ends when the player does what it
  asks (judged from the world's events, a `TutorialView` of the camera, hand, time and panels, and
  `TutorialSignal`s) or on Next, and names the HUD element (`TutorialTarget`) or the home village it
  points at; the client's `TutorialController` drives it, `TutorialPanel` shows its card and a gold
  frame around `Hud.TargetRect`, and a beacon of light marks the village.
  `Flow/WorldLibrary` keeps the player's worlds in the profile (ADR-060): `Begin` records a new
  world (named by the player or after its first village), `Save` writes a save and brings the
  world's record up to date with the time played, `Load` takes a save back through
  `GameSession.LoadWorld` (`SessionStore.Prepare` readies a saved world as a file load does),
  `Cards` and `Latest` feed the menus, and `ImportFiles` brings the save files from before the
  profile in once. `Flow/VictoryText` words how a world
  may be and was won, for the alerts, the history's titles, the end screen, the start screen's
  choices and F1 (ADR-027).
- `Presentation/LabelDeclutter` (ADR-027) decides, from labels' screen boxes and priorities, which
  floating labels show in full, which by their short text and which not at all; the client
  measures the labels.
- `Culture/CultureText`, `CultureReadout` and `CultureCommands` word doctrines, secular stages and
  traits, build the Settlements panel's and F1's culture lines, and shape a village's culture for
  `--doctrine`; `Alerts/CultureAlerts` adds revolts, godless villages, doctrines and traits, and
  `Presentation/CultureOverlay` paints the Culture overlay (ADR-024). The HUD's god line and the
  diplomacy panel say how the world sees each god.
- `Conflict/ConflictText`, `ConflictCommands` and `SettlementConflict` word wars, intelligence,
  proxy acts and holy wars, carry out proxy acts and words to holy wars (asking the host in a
  shared world) and build the Settlements panel's holy war lines and actions; the diplomacy entry
  adds each god's war, the player's intelligence of it and war aims; the HUD blurs rivals'
  followings by intelligence and marks veiled casting; `Alerts/ConflictAlerts` and
  `Diagnostics/ConflictReadout` add their alerts and debug lines (ADR-026). `SessionPreviews` holds
  the debug previews of what the AI weighs.
- `Domains/DomainText`, `DomainCommands` and `DomainReadout` word domains, sites and Old Faiths,
  take up a domain for the player (asking the host in a shared world) and build the Domains
  panel; `Alerts/FaithAlerts` and `Diagnostics/FaithReadout` add their alerts and debug lines
  (ADR-021). The HUD shows the god's domains and kept sites, and only the miracles it can cast:
  the signature miracles of its domains follow the ninth, on Shift+1–3.
- `Settings/GameSettings` is the player's settings file, forgiving where content is strict.
  `Audio/SoundBank` synthesizes the sounds and `Audio/Orchestra` plays the music (ADR-071); `Audio/AudioDirector`
  decides what the world sounds like. See [Sound and settings](#sound-and-settings).
- `Population/*`: Tier 2 representative citizens, derived from `WorldState` and never saved:
  `TierSelector`, `RepresentativeRoster`, `CitizenRoutine` and `SettlementCrowd` (see
  [Settlement simulation and population tiers](#settlement-simulation-and-population-tiers)).

### Godot client (`game/Scripts`, namespace `Pantheon.Client`)

- `Bootstrap/GameRoot`: composition root. Loads content (`GodotContentLoader`: the game
  content and the presentation content), creates the session, wires presenters, camera and
  overlay, routes hotkeys and picks the region under the cursor. It thins the fog with the island's
  width and, each frame, has `Presentation/LabelDeclutterer` fit every `DeclutteredLabel` (the
  villages' and holy sites') through `LabelDeclutter`, holy sites first, then villages by
  population (ADR-027).
  `Bootstrap/SmokeTestDriver` implements `--quit-after-frames` and `--screenshot`.
  `Bootstrap/DebugView` builds the F1 panel's `DebugSnapshot` four times a second and redraws the
  F3 region map; its `Bootstrap/PerformanceMonitor`, the only code that reads the clock, times the
  frames, the simulation's share of each and a save (ADR-044). With `--stress` GameRoot makes the
  stress world.
- `Camera/GodCameraController`: maps input to `GodCameraModel` and applies the pose to `Camera3D`,
  keeping its clearance above the roofs through `Roofs` (`Rooftops`, ADR-057). The left button grabs
  and flings the land (`LandGrab`), the right or middle button orbits, the wheel zooms towards the
  cursor, a double click flies there, WASD pans, Q/E turn and Page Up/Down tilt; a trackpad's two
  fingers pan (or zoom), Option with two fingers orbits and a pinch zooms, each gesture taken once
  though the engine delivers it twice; the window's edge pans when the settings say so. `Follow`
  keeps the focus on a moving thing, such as the Creature, until the player pans away (ADR-058).
- The light (`Scenes/Main.tscn`, ADR-051): a procedural sky, the sun with soft shadows, ACES
  tonemapping, screen-space ambient occlusion and a soft glow (both switched by the settings'
  lighting effects), distance fog and a little contrast.
- `Presentation/WorldPresenter`: rebuilds and syncs all world visuals:
  - `Terrain/ITerrainPresenter` with `Terrain3DTerrainPresenter` (primary) and
    `FallbackMeshTerrainPresenter` (fallback, logged and labelled with its reason), chosen by
    `TerrainPresenterFactory` (ADR-005). The Terrain3D adapter switches Terrain3D to a copy of
    its generated shader with bounds-safe region lookups and a colour lookup that also covers the
    world's east and south edges (ADR-007, which amends ADR-005). Since ADR-051 Terrain3D draws
    nine ground textures made in code (`GroundTextures`: a pattern from seamless noise between two
    colours, its height in the albedo's alpha, a normal map with roughness in its alpha), chosen
    sample by sample by the control map `TerrainControl.Compute` paints in the core: the biome's
    texture, blended into a neighbouring biome's over 6 m, with rock on steep slopes, snow on cold
    heights or sand near the sea over it, each turned at random but rock. The biome colours tint
    them gently (`TerrainControl.Tint`). The fallback keeps the vertex colours. The terrain streams its detail
    (§84, ADR-050): Terrain3D by its own clipmap, and the fallback by building every chunk from
    every second sample and rebuilding those near the camera from every sample, their borders
    matched to the coarse (`TerrainMeshBuilder.BuildChunk`'s `edgeStride`).
  - `ChunkStreamer` (in `Pantheon.Game.Presentation`, ADR-050) plans what streams: square chunks
    load within a radius of the camera, measured in three dimensions, nearest first and a few a
    frame, and go beyond a wider radius. `StreamingSettings` (`Streaming` in
    `settlement_view.json`) holds the radii, and F1's streaming line shows what is loaded.
  - `Water/WaterPresenter`: sea-level plane (`Materials/water.tres`, `Shaders/water.gdshader`)
    tinted by a baked depth map, so it does not read the depth buffer. Waves flatten and the
    surface gets rougher with camera distance, which avoids flickering sun glints.
  - `Water/FreshWaterPresenter` (ADR-022): the rivers and lakes from the `HydrologyMap`
    (`WaterSurfaceMesh`) and the floodwater (`FloodSurface`), with `Shaders/fresh_water.gdshader`
    (depth in the vertex colour; floods are muddy).
  - `Settlements/SettlementPresenter`: one `SettlementView` per settlement with the shrine and
    the name/population label, plus:
    - `SiteView`: the houses in the village's look (ADR-052): one MultiMesh of the style's house
      from `Buildings/BuildingAssets`, each house's walls in its colour and its roof and patterns
      in its custom data, in its own shade; and the fields (crop rows tinted through instance
      colours). Rewritten only when the placements or the look change;
    - `ConstructionSiteView`: the house being built, as scaffold poles, a timber pile and the
      village's house rising from the ground with the work (the building shader's `built`);
    - `CivicView`: the temple in the style of its god's motif under the god's colour, and the
      walls as a palisade of stakes with timber towers by the gates, or crenellated stone with
      stone towers;
    - `CitizenView`: the settlement's Tier 2 representatives as people (ADR-049), in one
      MultiMesh of the person figure (`People/PersonAssets`, the simpler figure beyond
      `DetailDistance`). Each instance carries its clothes and skin tone in its colour and its
      motion's cycle, the motion, its lean and its look in its custom data, and
      `Shaders/person.gdshader` swings the limbs about the hips and shoulders, bends the body,
      sits it down, shows the tools and headwear and paints each colour slot;
    - `StyleView`: the village's religious style (ADR-042), one MultiMesh per kind of piece
      (flower beds, bushes, obelisks, glowing braziers, columns), rebuilt only when the style or
      the houses change.
  - `Regions/RegionBoundaryOverlay`: region grid lines draped on the terrain, plus a hover highlight.
- `UI/DebugOverlay`: FPS, tick, seed, terrain backend, camera band, region under cursor,
  settlement readouts, boundary toggle and status messages, plus the F3 region debug map. Hidden
  at the start; F1 shows it.
- `UI/UiTheme` writes the interface's look into the engine's default theme before any interface
  is built (ADR-059): Alegreya Sans for text and Cinzel for titles (`game/Fonts/`, OFL), one
  palette, and slate panels with a gilded edge for panels, buttons, fields, sliders, popups and
  tooltips. The display stretches 2D content from a 1600 × 900 design size (`canvas_items`,
  `expand`); `SettingsApplier.FitWindow` opens a windowed game at nine tenths of its screen, and the
  interface size setting scales the window's content further.
- `UI/Hud`: the player HUD, built in code from `UI/HudParts/` (ADR-059): `GodPlaque` (name, rank,
  power, followers and a `Meter` to the next rank, and a chip per rival), the race for victory with
  its meter, `TimeControls` (season, pause and speeds), `AlertList`, `StatusToast`, the miracle bar
  of `MiracleTile`s (a vector glyph from `MiracleGlyphs` by the miracle's look, its key, cost,
  cooldown sweep and lock) and `CreaturePanel` (meters for health, needs, loyalty and nature, the
  command buttons, and its paths, attributes and bonds when opened). Under the time controls,
  `GodBar` (ADR-068): a `CommandButton` per `CommandBar` button, drawn by `CommandGlyphs` with its
  key on a badge (`KeyText`), and the map's chooser of overlays by name. The miracle bar ends with
  the Veil toggle, and a rival's chip is a button. It shows `HudModel`s and reports clicks.
- `Bootstrap/CommandController` (ADR-068) runs the god's bar, and keeps scrolling off the world under
  dialogs (ADR-069). A button presses its input action
  (`Input.ParseInputEvent`), so it runs the same handler as the key; the Map, Keys and Debug
  buttons open the chooser, `UI/Flow/KeysPanel` (the key map, `KeysPage`, also the settings' Keys
  tab) and `UI/DebugPalette`. It lights the buttons whose panels are open, and Esc puts its
  windows away. `Bootstrap/DebugTools` says whether the debug tools are on
  (`GameSettings.DebugTools`, or the build when that is null); `GameRoot` and `PoliticsController`
  run the debug group's keys only then. `GodCameraController.CommandGroundPoint` is the ground a
  command acts on: under the pointer, or the view's centre while the pointer is over the interface.
- `Presentation/Weather/`: `WeatherPresenter` keeps one `CloudView` per weather cell (soft
  camera-facing puffs that fade near the camera, rain streaks seen nearby, falling white and slow
  when it snows) and plays lightning bolts; it dims the sun under storms over the camera's focus,
  and drapes the snow lying on the land (`SnowCover`) over the terrain as one decal, redrawn every
  second (ADR-028).
- `Presentation/Disasters/`: `DisasterPresenter` keeps a `FireView` (flames, smoke, glow) per
  burning region within `EffectRadius` of the camera (ADR-050), ash decals on scorched land and a dome over each shielded settlement, plays a
  shock wave when an earthquake begins and shakes the camera while the ground under its focus
  shakes. `SiteView` draws ruins as rubble. Its `HazardPresenter` (ADR-029) drapes blight and
  rubble (`HazardCover`) over the land, keeps a funnel per tornado and a cloud per swarm
  (`HazardViews`), raises the volcano's plume while it rumbles or erupts, and lays a fading decal
  for each crack, slide and crater.
- `Presentation/Disasters/DebrisPresenter` (ADR-048) throws debris as Jolt rigid bodies: box
  planks, stones and thatch from `DebrisLayout.Burst` where houses fell (each `SettlementView`
  hands its fallen houses, `DebrisLayout.Fallen` of its old and new layouts, to the
  `WorldPresenter`) and where a thrown thing or a falling star struck, with the thrown rock or tree
  itself (`DebrisLayout.Thrown`). Each burst lands on a `HeightMapShape3D` patch of the terrain
  under it. Pieces fade and go after `Lifetime`, the oldest first beyond `MaxBodies`, and none are
  thrown beyond `Reach` of the camera; `DebrisSettings` is in `settlement_view.json`. The pieces
  are only shown and never saved.
- `Presentation/GreatEvents/GreatEventPresenter` draws the beast (the Creature body with the
  "Beast" look, half-buried asleep, walking awake, on its side fallen, with its label) and the
  anomaly's column of light (ADR-035).
- `Presentation/Land/` (ADR-053, ADR-073):
  - **`ForestPresenter`** draws the trees in chunks: one MultiMesh per species and variant of near
    trees (`TreeGrowth`, `PlantDetail.Near`) within `Trees.DetailDistance`, and one per species of
    far trees beyond it, cross-fading by visibility range.
    - Each chunk's nodes stand at its middle at the ground's height. Far trees cast no shadows.
    - It plants only the chunks the camera is near and clears those it leaves (ADR-050), and
      redraws a planted chunk when its timber, snow, villages or roads change.
  - **`plant.gdshader`** draws:
    - bark in its pattern (rough, birch or palm) and its species' colour;
    - leaf cards cut from `LeafAtlas` (`PlantTextures`), tinted by species and letting light
      through;
    - blades, stones and petals.

    All of them sway in the wind `Breeze.At` finds in the weather over the camera.
  - **`GroundCoverPresenter`** streams the ground cover (`GroundCoverLayout`, `GroundPlants`) in
    32 m chunks within 100 m, grass within `GrassRadius`, one node per kind and variant.
  - **`RoadPresenter`** draws each road's track from `RoadLayout`'s sections with `road.gdshader`,
    and its bridges built to their span (ADR-033).
- `Presentation/Migration/`: `MigrationPresenter` keeps a `MigrationView` per group on the road:
  walkers in the migrants' god's colour (about one per three people), strung out two abreast
  along the way, and a label that fades out at strategic zoom. It owns the
  `Presentation/Notables/NotablePresenter`, which keeps a `NotableView` per prophet and elder: a
  robed figure in the god's colour (a prophet with a staff and a halo) that walks the same roads,
  and a label (ADR-032).
- `Bootstrap/VillagerController` handles / and Shift+/ and `--villager`, keeps the camera on the
  followed villager until the player moves it, and keeps a `Presentation/Villagers/VillagerView`
  per followed villager: a larger figure in their work's colour under a turning marker in the
  follower's colour, posed for walking, work, prayer and rest, with a label (ADR-043).
  `CitizenView` hides their crowd double.
- `Bootstrap/NatureDebug` wires the weather, disaster, migration and Creature debug hotkeys (F8,
  F10, F11, F12, M, K; run only while the debug tools are on, ADR-068) and the `--weather`, `--disaster`, `--migrate`, `--refugees` and `--meet`
  launch options.
- `Presentation/Creatures/CreatureView` poses fights (blows traded face to face), games and
  flight, shows a fighter's health on its label, and `CreatureBody` draws scars on the flanks.
- `Bootstrap/GameFlow` is the game's flow (ADR-020, ADR-060): the title screen over the living
  island (the camera circling lower, the HUD and labels hidden), the pause menu (Esc), saves through
  `WorldLibrary` (manual, the autosave every five minutes and on leaving or quitting, F5 and F9),
  hosting and joining from the menu (joining reloads the main scene, whose next start reads the
  host's address from `PendingLaunch`), the end screen, the guide and the history panel (H), drawn
  by `UI/Flow/`: `MenuScreen` (the title with its choices and pages, the pause menu, the end),
  `NewWorldPage`, `WorldsPage`, `MultiplayerPage`, `SettingsPage` (six tabs: graphics, sound,
  controls, the keys (`KeysPage`, ADR-068), the interface and `MindsSettings`, ADR-065), `PantheonPages` (the gods waking and the gods of a world), `HistoryPanel`,
  `GuidePanel` and `UiKit`. `Bootstrap/Startup.Profile` opens the profile once a run and keeps
  the settings in it, and the Anthropic key apart from them. `Bootstrap/Births` bears a new
  world's gods (ADR-065): by the scribes, or by the chosen model off the main thread.
  `Bootstrap/TalkController` and `UI/TalkPanel` hold the player's conversations with the gods
  (ADR-066).
- `Network/EnetTransport` puts Godot's `ENetMultiplayerPeer` behind `INetworkTransport` (reliable and
  unreliable channels, round trips). `Network/NetworkDriver` joins a host before the world is shown
  (`--join`), hosts the local world (`--host`, `--upnp`), polls the network each frame and, at the
  end, says goodbye and saves a host's world.
- `UI/DiplomacyPanel`: the diplomacy panel (P), owned by `Bootstrap/PoliticsController` since
  ADR-068, built in code from `DiplomacyReadout` entries. An unmet god's card offers Seek
  (`CreatureCommands.Seek`) and, with the debug tools, Bring its Creature (K). Its
  buttons send the moves through `SessionCommands.CommandDiplomacy`, and its refugee row sets the
  player's policy through `MigrationCommands.SetPolicy`. Each card lists the player's treaties with
  that god (with Break), its other treaties, favours and its proposal; "Treaties and requests"
  opens the treaties and requests the authority would accept, on the term and secrecy the panel
  row chooses. `UI/CouncilPanel` (U) shows `CouncilReadout`. `Bootstrap/PoliticsController` runs
  both panels' actions through `PoliticsCommands`, J and the politics launch options (ADR-025).
- `Bootstrap/DivineController`: the player's divine interface. It selects miracles (keys 1–9 or
  the bar), casts at the cursor on a left click, rewards, punishes and sends the Creature (Z, X, V
  or the card), drives the hand's gestures, drains the session's events into effects and alerts,
  and refreshes the HUD, the targeting ring and the overlay.
- `Presentation/Creatures/` (ADR-061): `AnimalMeshes` makes each species' body once a run on
  worker threads: every part of its plan meshed by `SurfaceNets` and painted by `AnimalPaint`.
  `AnimalBody` hangs the meshes on a skeleton of joints (hips, knees, ankles, shoulders, elbows,
  wrists, neck, head and a tail of three), takes a `BodyPose` each frame, and shows the face
  (`AnimalFace`), its god's collar, its nature's tint and its scars. `CreatureView` draws the
  Creature between the simulation's last two places by the scheduler's `Alpha`, walks it with
  `AnimalMotion.Walk` (the stride by the distance moved, each foot on the ground under it), leans
  it with the slope, chooses its still pose by what it does, dangles it by its scruff from the
  hand's grip, flashes on feedback and labels its activity; `CreaturePresenter` starts the meshes
  of a world's species as it is shown. The Beast is a great horned bear on the same body.
  `Presentation/Divine/DivineHand`: the god's hand: it follows the cursor, presses when casting or
  sending, pats or swats the Creature, shows the rock, tree or villagers it holds (`HeldThing`) and
  flings them when it throws; a held Creature hangs under it (ADR-030). Since ADR-057 its grip swings
  by `HeldSwing`, and a press raises `Pressed` for `DebrisPresenter.Push`.
- `UI/BookPanel` shows the Book of the World and `Bootstrap/BookController` opens it (Tab,
  `--book`) and sends a miracle chosen in it to `DivineController.Select` (ADR-031). The camera's
  backtick toggles the strategic overview.
- `export_presets.cfg` builds Windows x64, macOS (universal, ad-hoc signed with Apple's `codesign`)
  and Linux x64 into `build/` (ADR-031).
- `Presentation/Divine/DivinePresenter`: the targeting ring (a decal with a procedural ring
  texture), the map overlay (one decal over the world with one pixel per region) and miracle
  effects (`MiracleEffect`: falling or rising motes, a spreading ring or a lightning bolt with a
  glow, freed when done). Since ADR-055 each miracle has a signature look: rain from a cloud, a
  golden harvest pillar, a healing spiral under a dome, flames and smoke, a quake's dust, a
  shield's dome, a fountain, a pillar of holy light, a dark swirl, a wheeling storm or a god's
  strike. `MiracleLayers` builds them: pillars and domes in `Shaders/miracle_glow.gdshader` (bright
  at the rim, fading towards a pillar's top), and clouds and the rest as GPU particles.
- Settlement labels show the main god's share and take its colour once it holds the majority.
- `Bootstrap/FaithController` (ADR-021) owns the Domains panel (`UI/DomainPanel`, G) and the holy
  sites in the world (`Presentation/HolySites/`: an obelisk, a great tree, a pool or a ring of
  stones round a crystal in the keeper's colour that flickers while the site is being taken), and
  applies `--rank`, `--domains`, `--domains-panel` and `--focus=siteN`. `CreatureBody` draws each
  species by its look in `divine_view.json`.
- `Presentation/Audio/AudioPresenter` plays the sound (ADR-021), and `Bootstrap/SettingsApplier`
  and `Bootstrap/Startup` load, apply and keep the settings; the menus have a settings page and
  the new-world page a choice of Creature species, and since ADR-027 of world size and victory.
  The HUD's line of rivals wraps between gods.

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
    Note over Root: NetworkDriver.Update first: a host handles intents and sends events and snapshots, a player takes them
    Session->>Sched: Advance(delta, step)
    loop whole ticks (10 Hz x time scale, capped, within the CPU budget, ADR-056)
        Sched->>Engine: Step(world)
        Engine->>Settle: Update(tick context)
        opt last tick of each 5-tick interval (2 Hz at x1)
            Engine->>State: weather step (spell, cells, soil water, snow, storms, droughts)
            Engine->>State: season step (new season, spoiling, sickness)
        end
        opt last tick of each 10-tick interval
            Engine->>State: disaster step (earthquakes, wildfires, hurricanes, destruction, fear)
            Engine->>State: hazard step (waves, the volcano, landslides, meteors, tornadoes, locusts, blight, marks)
            Engine->>State: ecology step (harm, scars, wildlife, pollution)
        end
        opt last tick of each 10-tick interval (1 Hz at x1)
            Settle->>State: Tier 1 settlement step (food, health, births, deaths, construction)
            Engine->>State: belief step (drift, conversion, power, ranks, blessings)
            Engine->>State: victory check, diplomacy step (drift, fading, status, lapsed proposals)
            Engine->>State: politics step (treaty terms, tributes, leaks, promises, the council)
            Engine->>State: migration step (wish to leave, departures, the road, arrivals)
        end
        Engine->>State: Creature system (needs, decide at 5 Hz, walk, deeds, meetings at 5 Hz, fights, views fade at 1 Hz)
        opt each AI god once every 30 ticks, in turn
            Engine->>State: the AI god whose turn it is (miracles, Creature, diplomacy, refugee policy) through the authorities
        end
        Engine->>State: RNG state, Tick++
    end
    Root->>Session: DrainEvents() (via DivineController)
    Root->>View: effects and alerts for the events, HUD, targeting ring, overlay
    Root->>View: Sync(world), Creatures.Sync (smoothed pose), Weather.Sync (clouds, rain, lightning), SetHoveredRegion(region under cursor)
    Root->>View: UpdateCitizens(world, simulation seconds, camera position)
    View->>Crowd: SelectTier, Refresh, Advance (reads WorldState only)
    Crowd-->>View: poses for the citizen MultiMesh (none in Tier 1)
    Root->>Overlay: ShowRegionMap(RegionDebugMap.Build(...)) when the F3 layer, cursor region, tick or world changed
    Root->>Overlay: ShowModel(DebugReadout.Build(snapshot with crowd statuses)) while visible
```

The render frame rate never changes simulation results. Only the number of real seconds
elapsed (× time scale) determines how many ticks run, and the settlement model steps on every
tenth tick, so its cadence follows simulated time, not frames. The citizens move every frame,
but their poses are functions of the crowds' clock (`CrowdClock`, ADR-049), which stops when
paused and never runs faster than `PaceCap` times real time.

## World history and the game's flow

ADR-020 records the decisions, and the Milestone 14 plan
([.docs/milestone-14-vertical-slice.md](../.docs/milestone-14-vertical-slice.md)) has the rules
with their shipped values and the measurements.

- **History** is world state. `HistoryRecorder` runs after every tick on the events the tick raised,
  and after commands through `SimulationEngine.Chronicle`. It keeps only what `history.json` calls
  significant, does not repeat the same kind for the same place or pair within a while, and notes
  settlements that fall and are settled again. It is saved, replicated as a section, worded by
  `HistoryText` and shown in the History panel and on the end screen.
- **Victory** (amending ADR-015): a god counts with its strongest direct allies only, up to
  `PantheonMaxGods`, or the world's own number when its size sets one (ADR-027).
- **Flow**: `GameFlow` puts menus over the running world. The title screen shows when the game starts
  without options, over the island running on; the pause menu pauses a world the player owns (a joined player's keeps running);
  the end screen opens once per world when it has winners. The guide's steps advance on the events
  the divine controller hands it.

```mermaid
flowchart LR
    Tick["SimulationEngine.Step"] -->|events of the tick| Rec["HistoryRecorder"]
    Cmd["Session commands"] -->|events, via Chronicle| Rec
    Rules["history.json"] --> Rec
    Rec --> Hist["WorldState.History"]
    Hist --> Text["HistoryText"]
    Text --> Panel["History panel (H)"]
    Text --> End["End screen"]
    Hist --> Save["Save version 12"]
    Hist --> Section["WorldSection.History"]
```

## Domains, holy sites and schisms

ADR-021 records the decisions, and the Milestone 15 plan
([.docs/milestone-15-polish-and-expansion.md](../.docs/milestone-15-polish-and-expansion.md)) the
numbers.

```mermaid
flowchart LR
    rank[Rank] -->|slots| choose[DomainAuthority]
    choose --> domains[GodState.Domains]
    domains -->|0.8 cost, signature miracles| caster[MiracleCaster]
    domains -->|boons| boons[DomainSystem, combat, disasters, learning]
    influence[InfluenceField] --> sites[HolySiteSystem]
    creature[Creature presence] --> influence
    sites -->|keeper: power, influence, boon| influence
    sites -->|TookHolySite| ledger[Diplomacy ledger]
    harm[Harmful miracle on loving followers] -->|BetrayalDoubt| doubt[Doubt]
    hardship[Hardship] --> doubt
    doubt -->|past Schism.Doubt| schism[Schisms: Old Faith]
    schism -->|loved again| back[Returns to the god]
    schism -->|otherwise| unbelief[Fades into unbelief]
```

- A domain is taken up through its authority, as a player's intent or by the AI (its
  personality's preferred domains in order), and held for good.
- Holy sites are found in the land from the regions and settlements when a world is made.
  Influence decides who takes them: a Creature at a site adds its own, which is why AI gods send
  theirs to claim sites and to hold their own.
- An Old Faith sits outside the beliefs: `ComputeAdherenceChanges` leaves its share out of the
  people who can be converted, and a Sanctuary leaves its god's followers out of those who can
  fall away.

## Rivers, lakes and floods

ADR-022 records the decisions, and the plan
([.docs/milestone-15-part-2-water-and-law.md](../.docs/milestone-15-part-2-water-and-law.md)) the
numbers.

```mermaid
flowchart LR
    gen[TerrainGenerator with Hydrology] --> map[HydrologyMap: water surface, courses, lakes]
    map --> state[WorldState.Water, Region.FreshWater]
    weather[Rain and soil water] --> step[WaterSystem]
    state --> step
    step --> level[River.Level]
    level -->|over its banks| flood[Region.Flood]
    flood --> damage[DisasterDamage, FloodYieldLoss, silt]
    state --> wells[Drinking water: rivers, lakes, wells]
    wells -->|dry| thirst[Health, ThirstStarted, water need]
    map --> mesh[WaterSurfaceMesh, FloodSurface]
    mesh --> client[FreshWaterPresenter]
```

- The hydrology is derived from the seed and the settings like the heightfield, so a world saved
  before version 14 (no `Hydrology`) regenerates without rivers.
- Floods go through the same damage as quakes and fires, so Shields and Protection lessen them.
- Ocean feeds villages from the water regions around them; Justice calms their wish to leave
  and lets doubt fade; Tidal Wave and Judgement are their signature miracles.
- The client draws the rivers and lakes once from the `HydrologyMap`, and floodwater as the
  nearest water risen by the regions' floods, rebuilt when a flood changes visibly.

## Settlements' society

ADR-023 records the decisions, and the plan
([.docs/milestone-15-part-3-society.md](../.docs/milestone-15-part-3-society.md)) the numbers.

```mermaid
flowchart LR
    life[Food, health, water, danger, war, love, fear] --> step[SocietySystem]
    step --> values[Safety, happiness, stability]
    step --> wealth[Wealth: surplus and trade]
    wealth --> learning[Education]
    learning --> tech[Technology, spread by neighbours]
    priority[Priority: SocietyAuthority, PriorityAdvisor, PriorityIntent] --> projects[Buildings]
    wealth --> projects
    priority --> found[Expansion: new villages]
    tech --> civic[CivicModel: economy, attacks]
    projects --> civic
    projects --> prepared[DisasterDamage: preparedness]
    projects --> temples[BeliefModel: infrastructure]
    values --> push[MigrationModel: unhappiness, unrest]
```

- The player sets a village's priority from the Settlements panel (N), through `SocietyCommands`
  (an intent for a joined player); AI gods through `PriorityAdvisor` at their evaluation interval.
- New villages join `WorldState.Settlements` with the next id and an unused name, and the
  settlement presenter adds their views on its next sync.

## Culture

ADR-024 records the decisions, and the plan
([.docs/milestone-15-part-4-culture.md](../.docs/milestone-15-part-4-culture.md)) the numbers.

```mermaid
flowchart LR
    acts[Miracles, wars, peace, betrayal] --> conduct[GodState.Conduct]
    conduct --> traits[Traits: compassionate, cruel, ...]
    witness[Witnessed miracles: WitnessCulture] --> feelings[Dependency, resentment]
    witness --> doctrines
    life[Peace, war, fear, love, land, learning, crowding] --> doctrines[Doctrines: CultureSystem]
    traits --> doctrines
    beliefs[Doubt, faith, the godless, Old Faith, science] --> secular[Secularism]
    doctrines --> effects[Factors on worship, conversion, miracles, learning, founding, migration]
    secular --> effects
    feelings --> effects
    feelings --> revolt[Revolts: temples torn down]
    temples[Temples] --> missions[Missionaries]
    doctrines --> charity[Charity: food to hungry neighbours]
```

- The culture step runs after the society step, at the belief step's rate. Each factor is
  applied where the system it changes already computes: `BeliefModel.ComputePower`,
  `BeliefSystem`'s pull, `MiracleCaster.Witness`, `SocietySystem`'s learning, `Expansion` and
  `MigrationSystem`'s appeal.
- `MiracleCaster` and `DiplomacyAuthority` note acts in `GodState.Conduct`; traits are derived from
  it each step, and doctrines read a god only through its traits and domains.

## Treaties and the Divine Council

ADR-025 records the decisions, and the plan
([.docs/milestone-15-part-5-politics.md](../.docs/milestone-15-part-5-politics.md)) the numbers.

```mermaid
flowchart LR
    command[DiplomaticCommand: propose, break, ask, absorb] --> authority[DiplomacyAuthority]
    authority --> book[TreatyBook: sign, end, reveal]
    authority --> actions[PoliticsActions: grant, refuse, absorb]
    war[DeclareWar] --> pacts[TreatyModel.Defenders: pacts called]
    book --> model[TreatyModel]
    model --> systems[Caster, belief, culture, migration, Creatures, AI valuer]
    caster[MiracleCaster: rescue] --> ledger[Ledger: favour owed]
    step[PoliticsSystem] --> book
    step --> chamber[CouncilChamber]
    tabled[CouncilAuthority: table, vote] --> chamber
    chamber --> decrees[CouncilModel: decrees]
    decrees --> caster
```

- Every treaty's effect is a query on `TreatyModel`, and every decree's on `CouncilModel`, made
  by the system it changes, so the systems stay independent of each other.
- Wars declared by treaties (joint wars, invoked pacts, war support, pantheon wars) go through
  the authority's one declaration of war, so betrayal of an ally, the victim's memory and broken
  treaties follow the same rules. Only a player's or AI's own declaration calls the victim's
  pacts.
- AI gods act through the same authorities as players: `TreatyAdvisor` and `CouncilAdvisor` run
  with the refugee and priority advisors at each AI evaluation.

## War, intrigue and holy wars

ADR-026 records the decisions, and the plan
([.docs/milestone-15-part-6-conflict.md](../.docs/milestone-15-part-6-conflict.md)) the numbers.

```mermaid
flowchart LR
    declare[DeclareWar with WarAim] --> book[WarBook.Start]
    ledger[DiplomacyLedger.Record] --> score[WarBook.Score]
    score --> achieved[WarBook.Achieved]
    achieved --> surrender[DemandSurrender]
    surrender --> dictate[PoliticsActions.Dictate]
    intel[IntelModel] --> hud[HUD and diplomacy panel]
    intel --> covert[Covert: exposure]
    cast[MiracleCaster: veiled] --> covert
    proxy[ConflictAuthority: incite, propaganda] --> covert
    culture[Militarism] --> holy[HolyWarSystem]
    stance[ConflictAuthority: stance] --> holy
    holy --> ledger
```

- War scores come from the ledger, so a war's score and the gods' memories never disagree.
- Intelligence is derived each time it is asked, never saved; the simulation and the AI see
  everything, and intelligence limits only what a player is shown and what is traced.
- Every random draw (holy war declarations, raid casualties, covert exposure, leaks) comes from a
  stream derived from the seed and the tick, so replays and saves stay deterministic.

## World sizes and victories

ADR-027 records the decisions, and the plan
([.docs/milestone-15-part-7-worlds.md](../.docs/milestone-15-part-7-worlds.md)) the numbers.

```mermaid
flowchart LR
    sizes[world_sizes.json] --> maker[WorldMaker.AtSize]
    gods[gods.json: Gods, ExtraGods] --> maker
    options["NewWorldOptions: start screen, --world-size, --victory"] --> maker
    maker --> gen[WorldGenerator.Generate]
    gen --> state[WorldState: Generation, gods, Victory]
    state --> system[VictorySystem: ascendancy, conquest, culture]
    system --> clocks[DominantSinceTick, ConquestSinceTick, CulturalSinceTick]
    clocks --> event[VictoryAchieved with its Kind]
    event --> words[VictoryText: alerts, history, end screen]
    clocks --> hud[HUD race line]
    cache[InfluenceCache] --> caster[MiracleCaster.Check]
    turns[GodAiSystem.IsTurn] --> ai[GodAi]
    ai --> caster
    labels[LabelDeclutter] --> client[Village, holy site, Creature and migrant labels]
```

- **Sizes.** Small (2 km), Large (4 km), Huge (8 km) and Vast (12 km). A size is generation
  settings, gods and a pantheon size: `WorldMaker.AtSize` scales the island, the regions (32 to
  128 a side), the settlements founded, the rivers and the hydrology's cell, adds the extra names,
  and makes up its number of gods from the first of `ExtraGods`. `WorldMaker.Generate` writes the
  size's `PantheonMaxGods` (two of four gods, three of six, four of eight) into the world's
  `VictorySettings`, and `VictorySystem.StandingOf` uses it. The first size must
  be the world `worldgen.json` and `gods.json` make, so that world is unchanged. The save keeps
  the generation settings, so a world loads at its size.
- **Victories.** `VictorySystem` tracks dominance whatever the settings, for the council and the
  AI, and the conquest and cultural clocks only while their victory is on. The first victory
  reached is the only one; its kind travels in `VictoryAchieved` and in the history record, and
  `VictoryText` words it.
- **The AI's cost** was measured on a huge world before it was cut (seed 42, ten simulated
  minutes): 4.14 ms a tick, the AI step 9.76 s over 3000 ticks with a worst tick of 110 ms. Then:
  - `GodAi` skips villages outside its influence before weighing casts;
  - an `InfluenceCache` builds each god's influence field once per evaluation, and
    `MiracleCaster.Check` takes it;
  - `TreatyAdvisor` counts every god's followers once per weighing (11.6 to 0.4 ms for all AI
    gods);
  - the AI gods take turns across the evaluation interval (`GodAiSystem.IsTurn`); the first still
    thinks when the interval is due.

  After: 0.08 ms a tick on a small world, 0.20 ms on a large, 0.59 ms on a huge and 0.56 ms on a
  vast one; the AI
  step 0.72 s over 3000 ticks, worst 18 ms. Without the turn-taking the 40-game balance probe
  repeats part 6's results exactly, so the other changes alter no outcome.
- **Presentation.** `LabelDeclutter` places holy sites first, then villages by population, then
  Creatures and migrants: each label in full where it overlaps nothing placed, by its name alone
  where only that fits, or hidden. A label faded out by its distance takes no place. A small
  world's village labels rarely overlap, so it looks as before. `GodCameraModel.MaxDistance` and the
  fog scale with the island's width.
- **Not modelled:** worlds beyond 12 km (the heightfield is still generated whole, in 1.1 s for a
  vast world; its detail streams since ADR-050), more than eight gods, a size chosen on the start screen's
  host page (`--host` with `--world-size` hosts any size), victories chosen per player, and history
  records of more than two winners.


## Seasons, extremes and the land's life

ADR-028 records the decisions, and the plan
([.docs/milestone-15-part-8-nature.md](../.docs/milestone-15-part-8-nature.md)) the numbers.

```mermaid
flowchart LR
    tick[The tick and SeasonState.OriginTick] --> season[ClimateModel.SeasonOf]
    season --> effects[ClimateModel.Now: season and wave combined]
    spell[WeatherSpell: heat or cold wave] --> effects
    effects --> weather[WeatherSystem: warmth, rain, snow, hurricanes]
    effects --> harvest[SettlementSystem: harvest]
    effects --> fire[Wildfires: fire risk]
    effects --> leave[MigrationSystem: wish to leave]
    effects --> step[SeasonSystem: spoiling, sickness, SeasonChanged]
    weather --> snow[RegionState.Snow]
    snow --> harvest
    snow --> soil[Melt into the soil and the rivers]
    harm[Fire, flood, quakes, lightning] --> land[EcologySystem: damage, scar, wildlife, pollution]
    nature[A god of Nature, Regrowth] --> land
    land --> harvest
    land --> hunt[EcologyModel.Hunt in the settlement step]
```

- **The year.** A world made with seasons has a `SeasonState`; the season follows from the tick
  and the year's origin, so nothing else is saved. `ClimateModel.Now` combines the season's and a
  heat or cold wave's `ClimateEffects`: shifts and losses add, factors multiply. A world without
  seasons uses `Seasons.Steady`. Each step that a season changes reads the factor it needs:
  - the weather step: warmth for cells and soil, how often cells blow in, hurricanes;
  - the settlement step: the harvest (`SettlementEconomy.Assess`'s climate yield);
  - the fire step: how readily land catches;
  - the migration step: the wish to leave.

  `SeasonSystem` spoils stores, lets the cold or heat sicken and raises `SeasonChanged`.
- **Extremes.**
  - Rain on land below the snow line lies as snow; snow above the melt line melts into the soil,
    which the rivers follow. Farmland under snow yields nothing, and snow does not burn.
  - Fog is a calm, cool, humid cell.
  - A storm born over warm sea grows into a hurricane by the season's share; other storms are
    capped below its winds. A hurricane fills slowly over land, and the disaster step tears down
    the houses under it (`DisasterKind.Hurricane`).
  - Heat and cold waves are spells chosen after an ordinary one by the season's chances.
- **The land's life.**
  - `EcologySystem` harms land that burns or floods, heals harm down to the scar, turns harm
    above the threshold into scar, heals faster and lifts scars where a god of Nature holds the
    villages, regrows wildlife towards the land's capacity and clears pollution. Crowded villages
    pollute.
  - The disaster step harms land an earthquake shakes; lightning harms the land it strikes;
    Regrowth heals harm and scars.
  - Villages hunt in their own settlement step (`EcologyModel.Hunt`), so the food lands in the
    same step's stores.
  - Harmed and polluted farmland counts for less in `SurveyFarmland`.
  - Gaps under `EcologyModel.Settled` close at once, so settled land stops changing and is not
    sent again in the regions section.
- **Presentation.**
  - The HUD's weather line starts with the season (`SeasonText`).
  - `NatureAlerts` tells of seasons and scarred land; the history keeps great storms and scars.
  - F1 shows the climate and the land's life at the cursor; F3 has its layers.
  - The Nature overlay (`NatureOverlay`) paints biodiversity, scars and snow.
  - The client drapes `SnowCover` over the terrain.
- **Cost:** a tenth more per tick: 0.09 ms on a small world, 0.68 ms on a huge one.
- **Not modelled:** snow slowing travel, grass and trees changing colour with the seasons, and
  pollution by technology. The rest of §19's disasters came in part 9 ([Hazards](#hazards)).
## Sound and settings

```mermaid
flowchart LR
    events[Events of a frame] --> director[AudioDirector.Cues]
    world[World] --> mood[AudioDirector.Explain: Triumph, Disaster, War, Tension, Dark, Calm; Theme on the title]
    weather[Weather at the camera's focus] --> ambience[AudioDirector.Ambience]
    bank[SoundBank on a worker thread] --> streams[AudioStreamWav]
    orchestra[Orchestra on worker threads: music.json's scores, the Theme first] --> streams
    director --> players[Positional and news players]
    mood --> music[Two music players crossfading]
    ambience --> loops[Rain and wind loops]
    settings[Settings in the profile] --> applier[SettingsApplier: window, vsync, shadows, lighting effects, render scale, particles, simulation budget, buses]
```

- Sounds of events play where they happen (`AudioStreamPlayer3D`, heard from far off at a
  low volume); the player's news plays everywhere. The same kind of sound starts at most every
  0.4 s.
- **The music** (ADR-071):
  - `Orchestra/Arranger` turns a `MoodScore` (chords, parts playing pads, basses, ostinatos,
    arpeggios, melodies, rhythms and swells) into notes;
  - `Voices` and `Struck` play them as synthesized instruments into dry and hall buses;
  - `Dsp.Hall` (Freeverb) and `Mastering` finish a seamless loop.

  A more urgent mood comes in at once over 1.5 s. A calmer one waits until the last has held 20 s,
  then crossfades over 3 s.
- Settings apply at once from the menus' settings page; citizens, vegetation and terrain quality
  apply when the game next starts (ADR-056), since the presenters are built with them.

## Multiplayer

ADR-018 records the decisions, and the Milestone 12 plan
([.docs/milestone-12-multiplayer-foundation.md](../.docs/milestone-12-multiplayer-foundation.md))
has the measured run.

- **One canonical world.** The host's `GameSession` runs the simulation, as alone. A joining player
  takes over a god the AI played (controller `Remote`), and the AI leaves it alone.
- **Intents** (§73): a player's commands travel to the host as intents without a god. The host
  carries each out as the player's god through the same authorities as its own commands, and
  answers with a `Verdict`. Saving, loading, the clock and the debug tools are the host's alone.
- **Replication** (§95–96, ADR-019): the whole world when a player joins, for a new host world
  and as a correction every 20 s. In between, `Replicator` sends each `WorldSection` at its own
  rate when it changed:
  - Creature motion at 5 Hz near the player's view (`ViewFocus`) and 1 Hz elsewhere;
  - gods at 4 Hz;
  - migration at 2 Hz;
  - weather, disasters and the rivers (ADR-022) at 1 Hz;
  - settlements (with their society and culture, ADR-023 and ADR-024), whole Creatures, regions
    changed past `RegionTolerance` (0.01) and rounded to thousandths (ADR-056), the history and the
    holy sites every 2 s;
  - diplomacy when a status, proposal or memory changes.

  Secret treaties reach only their parties: while one exists the host strips the others' from the
  snapshot, the diplomacy section and the events it sends each player (`SecretFilter`, ADR-025).
  A proxy act's event reaches only its author, and a veiled miracle reaches the others without its
  caster (ADR-026).

  Every event goes out as it is raised, and the list of players when it changes. After an intent
  the sections it can change go out with the verdict. `WorldSections` writes a section in compact
  save JSON and applies it to a replica in place.
- **Replicas**: a player's session runs the same simulation between snapshots, only to predict.
  It discards its own events and takes each snapshot as its world.
- **Leaving** (§71): a departed player's god goes back to the AI; a host that quits tells its
  players and saves its world. A saved world loads with the AI playing every `Remote` god.

```mermaid
flowchart LR
    subgraph Player["Player's game"]
        PUI["HUD, hand, panels"] --> PCmd["SessionCommands<br/>(intent when a replica)"]
        PCmd --> PClient["MultiplayerClient"]
        PClient --> Replica["Replica GameSession<br/>(predicts, discards its events)"]
    end
    subgraph Host["Host's game"]
        HServer["MultiplayerHost"] --> HCmd["SessionCommands.*As<br/>(as the player's god)"]
        HCmd --> Auth["Authorities"]
        Auth --> World["Canonical WorldState"]
        World --> Outbox["Event outbox"]
    end
    PClient -- "Hello, intents, Goodbye" --> HServer
    HServer -- "Welcome, Verdict, Players" --> PClient
    Outbox -- "EventBatch" --> PClient
    World --> Rep["Replicator<br/>(rates, interest, changes)"]
    Rep -- "SectionUpdate by rate; Snapshot every 20 s" --> PClient
    PClient -- "ViewFocus" --> Rep
```

## Settlement simulation and population tiers

Population is simulated statistically everywhere and shown in more detail near the camera
(ADR-004, amended by ADR-008 and ADR-032). Tier 1 is canonical; Tier 2 is derived from it and
never saved. Tier 3 is the individuals that matter, kept in `WorldState` everywhere: the
Creatures, what the hands hold, prophets and elders (see
[Prophets and elders](#prophets-and-elders)), and the villagers the gods follow (see
[The selected villager](#the-selected-villager)). No villager is promoted into an agent by the
camera; a villager becomes one only when a player chooses them.

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
   (`maxYield × (1 − e^(−farmers / capacity))`), times the blessing bonus and the water yield of
   the farmland's soil (ADR-013); consumption is `FoodPerPersonPerStep` per person. Stores keep what is left, up to `FoodStoragePerHousing` per unit of housing, and the
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
- `CitizenRoutine` gives each representative a closed-form schedule in the crowds' time: rest
  by the door, walk to work (the fields, a ring around the building site, or chores near
  home), work with a forward lean per stroke, walk to the shrine, gather facing it, walk home.
  Each pose carries its motion's `Cycle` (strides of two steps, work strokes, breaths or
  prayers), and the body rises gently over each step.
- `CrowdClock` (ADR-049, amending ADR-008) is the crowds' time, held by `GameSession.Crowds`: it
  runs with the game but never faster than `PaceCap` times real time, and stops when paused, so
  people keep a human pace at any game speed. It is never saved, and `VillagerCommands.Pick`
  poses the figures by it, as the client draws them.
- `ForestLayout` picks each tree's species from its biome's shares (`TreeSettings.Pick`, ADR-053).
  `GroundCoverLayout` lays out the ground cover: at most one thing in each 0.8 m cell, by the
  biome's chances (grass and flowers thinned by the land's vegetation, stones, bushes and reeds
  taking their shares first so they stay put), never in water, on bare rock, on a road, by a house,
  on a field or on a village's square (`BareGround`). `ChunkStreamer` looks only at the chunks
  within its load radius, so small chunks over a large island cost nothing far away.
- `CreatureExpression` (ADR-055) derives a Creature's face from its state (what it is doing, its
  needs, health, nature and its god's last word, or one the client has just seen given): joy,
  anger, fear, sleep and a wag, and the mood they show most. `CreatureBody.Express` moves its brows,
  lids, cheeks, pupils, mouth and tail to match. `CreatureBody.Align` shows its nature: a kind
  Creature warm and softly glowing, a cruel one dark, red-eyed and long-horned. Looking at the
  Creature turns the camera to its face (`GodCameraModel.FaceFront`).
- `Hand/HeldSwing` (ADR-057) swings what the hand holds as a damped pendulum driven by the hand's
  acceleration in hand sizes (`DivineHand` tilts its grip by it, and a held Creature hangs offset by
  it); the hand raises `Pressed` when a press reaches the ground and `DebrisPresenter.Push` knocks
  the debris there away. `Presentation/Rooftops` gives the highest roof near a point, and the god
  camera keeps its clearance above it (`GodCameraController.Roofs`). A village's walkers' grid
  opens the water under its roads' bridges (`LocalNavGrid.Build(..., bridges)`).
- `Wildlife` (ADR-054) places the herds and flocks near the camera from each region's wildlife
  and biome, closed-form in the crowds' time (a herd grazes, then walks on to the next spot; a
  flock wheels round its point), never on water or a village's bare ground. `AnimalMesh` builds a
  deer and a bird, their parts tagged for `Shaders/animal.gdshader` (legs swing, heads lower to
  graze, wings beat, antlers for stags). `BridgeLayout` finds where a road crosses water and lays
  a plank bridge from bank to bank (`RoadPresenter`).
- `VillageArchitecture` (ADR-052) gives a village its look: the style its land builds in
  (`StyleOf`: adobe, stilt houses, stone cottages, timber longhouses, round huts), longhouses and
  an upper storey as it grows, colours from `Architecture` in `settlement_view.json`, and its
  god's colours where its style (ADR-042) is strongly felt. `HouseMesh` and `CivicMesh` build the
  houses, temples, stakes, stone walls and towers with `MeshBuilder`, tagging each vertex with its
  `BuildingSlot`; `CivicLayout.Towers` places the towers by the gates. `Shaders/building.gdshader`
  paints each slot with a pattern worked out in the building's own space (daub, planks, coursed
  stone, thatch, slate, timber grain) and hides what is not yet built.
- `PersonLooks` gives each person their skin, hair, clothes (by work), height and headwear (a
  straw hat, a scarf) and a dress from `PresentationHash`; prophets and elders wear a robe of their
  god's colour and carry a staff. `PersonMotions` maps what a figure does to a motion (sitting,
  walking, hoeing, hammering, chores, praying). `PersonMesh` builds the figure in two levels of
  detail, each vertex tagged with its part and colour slot. The tuning is `People` in
  `settlement_view.json`. Given the crowd's ways (`ICitizenWays`, ADR-046), a walk follows its
  leg's way round obstacles, a little faster, so the leg keeps its straight line's time.
- `LocalNavGrid` is a village's local navigation (§85, ADR-046), baked from its layout:
  - cells of `NavCellSize` metres; huts, ruins, the building site, the shrine, a built temple and
    water are blocked, with `NavClearance` of room;
  - a way is straight when clear, else A* over the cells pulled taut, cached by its two ends;
  - since ADR-048 the grid numbers its connected areas when baked, so a way between two areas is
    known to be missing without a search, and A* runs on arrays kept for the grid's searches.

  `RepresentativeRoster` moves work spots that fall inside a hut just outside its wall.
- `SettlementCrowd` keeps the roster in step with the aggregates. It rebuilds the roster only
  when the role counts or the layout change, and bumps `RosterVersion` so the view recolours.
  Rerouted figures walk to their new routine at `CatchUpSpeed` instead of jumping; newcomers
  step out of their door; on promotion everyone appears in place. It bakes the village's
  `LocalNavGrid` (`Navigation`) while figures are shown, and again only when what stands in the
  way changes (the huts, the building site's place, the ruins or the temple), and supplies the ways; `CrowdStatus.Navigation` gives F1 the grid's figures.
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
    Time["CrowdClock<br/>real time, capped"] --> Routine["CitizenRoutine.Evaluate"]
    Routine --> Crowd
    Crowd --> View["CitizenView<br/>one MultiMesh of people per settlement"]
    Looks["PersonLooks"] --> View
    View --> Shader["person.gdshader<br/>limbs, poses, colours"]
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
  `CreatureModes` (ADR-047) is the hierarchical state machine:
  - Free, with Deciding, Travelling and Performing within it;
  - Engaged, fighting or playing with another Creature;
  - Held, in its god's hand;
  - Banished, which counts down to the Creature's return home.

  Deciding picks an option at most every `DecisionIntervalTicks` (5 Hz).
- **Behaviour trees** (`CreatureTree`, ADR-047). They carry out the chosen action:
  - an interrupt: a pressing need ends a commanded walk;
  - a guard: the reason the action was chosen must still hold, or the Creature gives up
    (`CreatureGaveUp`) and decides again;
  - a sequence: travel at the level's speed, perform for the action's time, then the deed.

  The sequence's place is the activity's phase, so the trees keep no state.
- **Brain** (`CreatureBrain`). Options come from the Creature's needs and from each settlement
  within `PerceptionRadius`, classified as in need, building or thriving. Utility is learned
  preference × drive × personality × closeness, plus seeded jitter; the best reachable option
  wins.
- **Learning** (`CreatureAuthority`). A reward or punishment judges the current action, or the
  last one within `FeedbackWindowSeconds`. It moves the preference for that situation and action,
  the traits behind it (`CreatureTemperament.Judge`), jealousy and loyalty; a punishment also stops
  the action.
- **Temperament** (ADR-037). `CreatureTemperament.Traits` names the traits behind each action, and
  the brain's personality factor is 0.5 plus their mean (fleeing draws on a lack of courage).
  - `CreatureBonds.Drives` adds a protective Creature's guard near its god's villages.
  - `CreatureMeetings` lets the jealous envy stronger Creatures.
  - `BeastSystem` sends a brave, protective player's Creature against the beast
    (`CreatureBrain.Defend`, `CreatureDefending`).
  - `CreatureCombat`, `CreatureMeetings`, `CreatureDeeds`, `BeastSystem` and `MiracleEffects` move
    courage, jealousy and playfulness by victories, defeats, flight, wounds and play.
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

## Creature relationships

ADR-017 records the decisions, and the Milestone 11 plan
([.docs/milestone-11-creature-relationships.md](../.docs/milestone-11-creature-relationships.md))
has the rules with their shipped values and the measured runs.

- **Views** (`CreatureRelation`, one per Creature met, each side its own): friendship, fear,
  rivalry, respect and trust, the last meeting and the latest encounters. `CreatureBonds` holds the
  pure rules: the urges to fight, play and flee, what each encounter does to both views, and how
  views fade.
- **Meetings** (`CreatureMeetings`, on decision ticks): two free Creatures within `MeetRadius`
  meet once a cooldown. Each answers through `CreatureBrain.Respond`: preference × drive ×
  personality in the `MeetsCreature` or `MeetsEnemyCreature` situation (gods at war). The answers
  make a fight, a game, a greeting, a flight or nothing. Creatures in sight are also options in the
  brain's ordinary decisions, to seek or flee.
- **Fights on command** (ADR-062): `CreatureAuthority` takes `CreatureCommandKind.Fight` at a
  place and picks the rival nearest it within `HuntReach`, unless a treaty binds the gods;
  `CreatureBrain.Hunt` sets the Creature after it (`Commanded`, `Travelling`, `HuntSeconds` to
  run); `CreatureTree` re-plans the way to the rival about once a second and when the way runs out
  (`CreatureBrain.Chase`), and gives up when time runs out or the rival is gone. A hunter meets its
  rival whatever their cooldown and falls on it; outside a war the attacked god's ledger records
  `CreatureAttacked`. The AI gods' confrontations at war use the hunt (`AiActionKind.SetCreatureOn`).
  The client aims Fight from the Creature card (a ring of the reach at the cursor) or Shift+V, and
  `FightEffects` shows a fight: a health bar over each fighter (`health_bar.gdshader`), sparks and
  dust as each blow lands (`AnimalMotion.Blows`, `MiracleLayers.Impact`), and the loser falling
  (`AnimalMotion.Fallen`) before it is banished from sight.
- **Work for the villages** (ADR-063): `CreatureAction.Farm` waters, weeds and tills the village's
  farmland and brings in a harvest by its fertility; `CreatureAction.GatherWood` uproots trees in
  the forest richest in timber around the village (`ResourceModel.RichestForest`), which thins by
  the village's `TimberPerWood`, and the wood goes to its stores (`CreatureActed.Wood`); HelpBuild
  spends a house's wood by its share. The brain offers farming to villages in need and thriving
  ones, and fetching wood to those short of it. `CreatureCommandKind.Work` at a village does what
  it needs most (`CreatureBrain.NeededWork`), at a wood fetches its timber, in the village's own
  situation so a reward teaches it; the client's B and the card's Work aim it, and the poses
  `AnimalMotion.Farm` and `Uproot` (with `AnimalBody.ShowTree`) show it.
- **Combat** (`CreatureCombat`, every tick): blows by strength × combat skill with jitter from a
  stream derived from the seed and the tick. It ends in a banishment (scar, loyalty, experience,
  witnesses' belief, the god's `CreatureDefeated` memory), mercy, a draw or a broken-off fight.
- **Gods**: `GodAi` rewards fights with enemies' Creatures and play with allies', and at war may
  send its Creature against an enemy's Creature that threatens its people.

```mermaid
flowchart LR
    Brain["CreatureBrain<br/>options and answers"] --> Meet["CreatureMeetings"]
    Bonds["CreatureBonds<br/>views and urges"] --> Brain
    Stance["Gods at war / allied"] --> Bonds
    Meet -->|play, greet, flee| Bonds
    Meet -->|fight| Combat["CreatureCombat<br/>(derived stream)"]
    Combat --> Bonds
    Combat --> Banish["Banishment, scars,<br/>loyalty, experience"]
    Combat --> Belief["Witnesses' belief"]
    Combat --> Ledger["DiplomacyLedger:<br/>CreatureDefeated"]
    Player["God: reward / punish / send"] --> Brain
    Ai["GodAi: teach, confront"] --> Brain
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
- **Victory** (`VictorySystem`): 60% of all followers held for 600 s wins (`WinnerGodIds`,
  `VictoryAchieved`), and the world goes on. Allied gods count together (see
  [Four gods and pantheon victory](#four-gods-and-pantheon-victory)). Since ADR-027 a world may
  also, or instead, be won by conquest or culture, or by nobody (see
  [World sizes and victories](#world-sizes-and-victories)).

## The gods' minds

ADR-065 records the decisions, and the plan is
[.docs/milestone-15-part-44-the-gods-minds.md](../.docs/milestone-15-part-44-the-gods-minds.md).

```mermaid
flowchart LR
    menu[New world / host] --> births[Births: client]
    births -->|Scribes, or no key| scribe[PersonaScribe: seed + minds.json lore]
    births -->|Claude / OpenAI / Gemini / Local| model[ILanguageModel: Anthropic, OpenAI, Gemini or Local]
    model -->|"PantheonPrompt: JSON of the schema"| read[PantheonPrompt.Read: strict, per god]
    scribe --> born[BornPantheon: a god per slot]
    read -->|"bad gods: the scribes'"| born
    born --> maker[WorldMaker.Generate: slots renamed, personalities, GodState.Persona]
    maker --> world[(WorldState, saved)]
    world --> ai[GodAi.ProfileOf god: profile + temper × TemperReach]
    world --> text[PersonaText: Book, diplomacy, pantheon page, F1]
```

- **The contract.** `ILanguageModel.Ask(ModelQuestion)` returns a `ModelAnswer`: JSON that
  follows the question's schema, or `Refused`, `CutShort` or `Failed` with the reason in words.
  The adapters, in `Pantheon.Oracle`, never throw for a model's or the network's failure:
  - `AnthropicLanguageModel` uses the beta Messages API with structured output at the question's
    effort, cached instructions and the server-side fallback;
  - `OpenAILanguageModel` (ADR-067) uses the Responses API, with strict `json_schema` output, the
    question's reasoning effort and `store: false`;
  - `GeminiLanguageModel` (ADR-067) uses `generateContent`, with a JSON `responseFormat`, the
    question's thinking level, and the thought parts left out;
  - `LocalLanguageModel` uses Ollama's `/api/chat` with `format` set to the schema.

  The three web adapters without an SDK share `JsonHttp`.
- **The choice.** `GameSettings.Minds` (Scribes, Claude, OpenAI, Gemini or Local), `ClaudeModel`,
  `OpenAIModel`, `GeminiModel`, `LocalUrl` and `LocalModel`.
- **The keys.** Each provider's key is kept apart in the profile (`anthropic_key`, `openai_key`,
  `gemini_key`), read as `MindKeys`. Without one, the environment's is used: `ANTHROPIC_API_KEY`,
  `OPENAI_API_KEY`, or `GEMINI_API_KEY` then `GOOGLE_API_KEY`.
- **Opening.** `MindFactory.Open` makes the chosen model with its own key, or none and says why.
- **The birth.** A new world from the menu, or a hosted one, is born first:
  - by the scribes at once;
  - or by the model, asked off the main thread while the title screen shows the gods waking.

  The world is made on the main thread from `NewWorldOptions.Born`. Every god a model did not
  write well is the scribes'.
- **In the world.** `GodState.Persona` (save version 35) holds the epithet, history, voice,
  desire, dread, temper and author. The temper's seven leanings lean the god's profile
  (`GodTempers.Apply`), and every reader of a god's profile reads `GodAi.ProfileOf(GodState)`.
  A god with no persona plays exactly as before.
- **Determinism.** A model's answer is an input: it enters only between ticks, when a world is
  made, and is kept in the world.
  - A scribes' world is reproduced by its seed; a model's world by its save.
  - Players of a shared world receive the host's gods with the world.

## Talking to the gods

ADR-066 records the decisions, and the plan is
[.docs/milestone-15-part-45-talking-to-the-gods.md](../.docs/milestone-15-part-45-talking-to-the-gods.md).

- **Acts** (`TalkActs.For`): every act the authority allows between the god and the player, with
  the god's own utility from `GodAi.EvaluateDiplomacy` and `TreatyAdvisor.AnswerUtility`:
  - agree to an alliance, peace or a treaty the player asks for;
  - accept or decline the player's proposal;
  - offer an alliance or peace;
  - declare war, or break an alliance.

  `TalkActs.Do` runs each act through `PoliticsCommands.DiplomacyAs`: an agreement is the player's
  proposal and the god's Accept; the rest are the god's own commands.
- **Goodwill** (`GodTalk`): -1 to 1, moved by each reply's regard and halving with the world's
  time. It leans an act by at most `TalkRules.PersuasionReach`: towards the player for friendly
  acts, against for hostile ones. Each act is willing, reluctant or unwilling; unwilling acts are
  never offered to the model.
- **The conversation** (`Talks`):
  - `Ask` adds the player's words and builds the question (`TalkPrompt`: cached instructions, then
    a turn with the situation, the acts by id and the words);
  - the client asks the model off the main thread;
  - `Answer`, on the main thread between ticks, moves the goodwill, judges the chosen act again, and
    does it only if the god is now willing.

  Without a model, `Choose` takes a suggestion as the asking of one act, and `TalkScribe` answers.
- **The client.** `TalkController` (created by `GameFlow`) opens `TalkPanel` from Speak on a
  diplomacy card, pauses the world while it is open, and lets only the host talk.

## Gods and Creatures as agents

ADR-072 records the decisions, and the plan is
[.docs/milestone-15-part-51-agents.md](../.docs/milestone-15-part-51-agents.md).

```mermaid
flowchart LR
    events[World events] --> reader[HookReader: hooks per mind]
    reflect[A god's reflection] --> director
    talk[A talk ended] --> director
    reader --> director[AgentDirector: gather, turn, hourly limit, rest]
    director -->|model| ask[Question off the main thread]
    ask --> replies[AgentReplies between ticks]
    replies --> tools[GodTools: the authorities]
    replies --> aims[GodAims: the planner]
    replies --> will[CreatureWill: the brain]
    replies --> said[GodSpoke and CreatureSpoke events]
    director -->|no model| scribe[AgentScribe: minds.json Voices]
    scribe --> said
    said --> shown[Alerts, talk lines, speech above Creatures]
```

- **Minds.** Every AI god and every Creature has one (`Mind`). The player's god has none.
- **Hooks** (`Minds/Agents/HookReader`): each event that matters to a god or a Creature becomes a
  `Hook` for it, in words from its side, with the scribes' moment (`Voice`). The `AgentDirector`
  also wakes a Creature that grows hungry or tired, and a god every `ReflectSeconds` of the world's
  time. The client adds a talk's end.
- **Turns** (`AgentDirector`, in `Pantheon.Game`, driven each frame by the client's
  `AgentController`):
  - **When a mind is asked:** its hooks gather for `GatherSeconds`, and its turn is at most every
    `GodTurnSeconds` or `CreatureTurnSeconds` of real time, `MaxAsking` at once, not while paused.
    The hour's questions (`AgentOptions.TurnsPerHour`) are checked for each mind.
  - **How:** the question goes to the model off the main thread. The answer is taken in a later
    frame, between ticks, and dropped if the world was replaced.
  - **Words between AI gods** wait for another hook.
  - **After three failures** in a row, the agents rest `RestSeconds`.
- **A god's turn** (`GodAgentPrompt`, `GodView`): what woke it, itself, the gods it knows (their
  standing in its own words, `MindWords`), the villages that concern it, the holy sites, proposals,
  aims, the miracles it can afford, its instincts (the utility AI's candidates, diplomatic moves and
  goal options), its journal, and its last results.

  Its answer (strict JSON) has a reason, acts, messages and a journal. `AgentReplies.God` does the
  acts through `GodTools` (the miracle, Creature and diplomacy authorities, as `PoliticsCommands`
  does for a player), raises `GodSpoke` for words to gods it knows, and writes `GodState.Journal`
  (saved).
- **Aims** (`Simulation/Gods/GodAims`): the planner judges each aim (`GodPlanner.Score`) and refuses
  a moot one. A kept aim weighs at least `AimWeight` and holds `NextPlanTick` back by
  `AimHoldSeconds`, so `GoalFocus` favours it as it favours the planner's goals.
- **A Creature's turn** (`CreatureAgentPrompt`): what happened, how it is, who is near, what it was
  taught, what it did last, and its choices. `Simulation/Creatures/CreatureWill`, an authority in
  `Authorities.Will`, lists them: the brain's options, each once, with no errands. It carries a
  choice out through `CreatureBrain.TryStart`, drawing any randomness from the seed and the tick. It
  refuses while the Creature obeys its god, fights, plays, is held or banished.
- **The scribes** (`AgentScribe`) speak when there is no model, the agents are off, the hour is spent
  or they rest. Their lines come from `VoiceLines`, chosen from the seed, the speaker and the tick: a
  god's only to a player's god, every `ScribeGodSeconds` at most; a Creature's aloud, every
  `ScribeCreatureSeconds`.
- **Words and talk.**
  - `MindAlerts` shows a god's words to the player.
  - `AgentController` shows a Creature's words above it (`CreatureView.Say`) and adds a god's words
    to its conversation (`Talks.Heard`); `TalkPrompt` gives them to the god's model with its
    journal.
  - `TalkController.Ended` wakes the god with what was said.
- **The model** (`MindFactory.OpenAgents`): the chosen provider, its quick model when
  `GameSettings.AgentsQuick` is set; none when `GameSettings.Agents` is off. `--agents` overrides it.
  No mind thinks while the title screen is open.
- **Determinism and sharing.**
  - Acts are commands between ticks, and speech is events, which travel to joined players.
  - The host runs every mind, and a replica none.
  - Save version 36 adds the journals, and protocol version 22 carries them.
- **Debugging.** `AgentReadout` gives the F1 lines, and the log's "Mind:" lines show each turn.

## The player's angel

ADR-074 records the decisions, and the plan is
[.docs/milestone-15-part-53-angel.md](../.docs/milestone-15-part-53-angel.md).

```mermaid
flowchart LR
    conduct[GodConduct and traits] --> look[AngelLooks: stage, lean, conflict, marks]
    events[World events] --> hooks[AngelHooks: the god's moments]
    call[A call or a question] --> director
    look --> director[AngelDirector: gather, turn, hourly limit, rest]
    hooks --> director
    director --> choices[AngelChoices: the god AI as a kind and a harsh heart]
    director -->|model| prompt[AngelPrompt off the main thread]
    director -->|no model| scribe[AngelScribe: angel.json Lines]
    prompt --> words[AngelWords]
    scribe --> words
    words --> card[AngelCard beside the hand]
    look --> figure[AngelFigure: a SubViewport, angel.gdshader]
    card -->|Show me| act[Camera, chosen miracle, Creature order, gods' panel]
```

- **Its look** (`Angel/AngelLooks`):
  - **What it reads.** The player's god's `GodConduct`, weighed by `angel.json`'s `Weights`, gives a
    kindness and a harm. The lean is their balance times how formed the angel is (half at
    `Softness`), and the conflict is high when both are large.
  - **Stages:** Radiant, Kind, Unformed, Torn (at `TornConflict`), Stern and Fallen.
  - **Marks.** `GodState.Traits` give `AngelMarks`.
  - **Never a rule.** Nothing in the simulation reads the look, and nothing of it is saved. `--angel=<stage>`
    holds it (`AngelDirector.ForcedStage`).
- **Moments** (`Angel/AngelHooks`):
  - **From the frame's events:** the god's own deeds, kind or harsh, and what happens to it.
  - **From the `AngelDirector`:**
    - its greeting in a world;
    - the god's call (`Call`);
    - a change of its own stage;
    - the god's Creature growing hungry or tired;
    - a quiet of `QuietSeconds`.
- **Deeds it may urge** (`Angel/AngelChoices`):
  - **Miracles.** `GodAi.Evaluate(world, god, profile)` weighs the god's choices with the Guardian's
    profile (kind) and the Tyrant's (harsh), through `SessionPreviews.PreviewGodAiAs`. Only miracles
    the caster's check allows come back.
  - **For its moments:** help for a village, the Creature at work or set on a visitor, and an answer to
    a god.
  - **Each deed** has an id, a text, a kindness and its reasons.
- **Turns** (`AngelDirector`, driven each frame by the client's `AngelController`):
  - **Gathering.** Moments gather for `GatherSeconds`. Words come at most every `WordsSeconds` of real
    time, by `AngelTerms.Chatter`, or a third of that for urgent moments.
  - **Calls.** A call (and the greeting) is answered after `CallSeconds`, even paused or while the
    tutorial hushes it.
  - **With a model**, `AngelPrompt` asks the model off the main thread:
    - what moved it;
    - `GodView.Describe` for the god;
    - its last `Remembered` words;
    - the deeds offered.

    The answer is its words and up to two deed ids, taken in a later frame and dropped if the world
    was replaced.
  - **Without one**, after three failures (for `RestSeconds`), or when `TurnsPerHour` is spent,
    `AngelScribe` speaks the weightiest moment's line in its tone (Light, Silver, Torn or Dark) and
    picks deeds by its nature.
- **The figure** (`Presentation/Angel/AngelMesh`):
  - a robed body, a head and hair, open hands and a sash;
  - two wings of three layers of feathers;
  - a halo with thorns;
  - a blade, a veil, chains and a crown.

  UV2 is each vertex's part and side.
- **The client.**
  - `AngelFigure` draws the figure in a `SubViewport` of its own world (lights, motes or embers), so
    the island's terrain never cuts it. `angel.gdshader` colours each part by the lean, its side and
    the conflict, beats the wings and drops a dark angel's, breaks the halo and grows its thorns, and
    shows only the god's marks.
  - `AngelCard` shows the picture and its card beside the hand, over the cursor's ground point, and
    holds still under the pointer. The figure's view stops updating when the angel is away.
  - `AngelController` wires it all:
    - the key (`angel_call`, ') and the bar's first button;
    - Show me: the camera, `DivineController.Select`, `BeginWork`, `BeginFight` and the diplomacy
      panel;
    - Ask;
    - the tutorial: `TutorialController.CardRect` and `Speaker`.
- **The model** (`Births.OpenAgentModel` with `GameSettings.AngelThinks`): the chosen provider, its
  quick model with `AgentsQuick`. `GameSettings.Angel` keeps the angel at all, and `--angel=off`
  overrides it.
- **No save or protocol change.** Joined players have their own angel.
- **Debugging.** `AngelReadout` adds the F1 lines, and the log's "Angel:" lines show its words.

## Gods answering Creatures, the hand and fights

ADR-070 records the decisions, and the plan is
[.docs/milestone-15-part-49-gods-answer-creatures.md](../.docs/milestone-15-part-49-gods-answer-creatures.md).

- **The watch** (`Simulation/Gods/CreatureWatch`), run by `GodAiSystem` after the AI gods' own turns,
  every `WatchRules.IntervalTicks`:
  - every god looks for other gods' Creatures near the villages it leads;
  - `Hostility` reads its standing with the visitor's god, its view of it and its aggression, and
    `AnswerFor` turns that into Welcome, Watch or Repel;
  - the god remembers a `Trespassed` or `Visited` act once in the cooldown, through the ledger, and a
    `CreatureWatched` event goes out;
  - it sends its Creature after the most pressing visitor, through the Creature authority (`Fight`
    on a foe it is a match for, else `GoTo`);
  - a player's god is only told (`Unanswered`);
  - `GodAi` does not call a hunting Creature off for an errand.
- **Fights:** `CreatureCombat.Recovering` (a fight remembered within `RespiteSeconds`) turns a
  meeting's fight into walking on in `CreatureMeetings`. `CreatureCommands.SlowForFight` slows the
  host to ×1 when the player's Creature starts one.
- **The hand:**
  - `HandAuthority` lets a god set its own Creature down or throw it without influence;
  - `HandCreatures` lifts it within half the reach before villagers;
  - the client keeps a held Creature on the hand's grip whatever the pointer does;
  - `CreaturePresenter.Pointed` tests the pointer's ray against a Creature's body, so the hand's
    target is the Creature pointed at.

## Feet, dialogs and territory

ADR-069 records the decisions, and the plan is
[.docs/milestone-15-part-48-feet-dialogs-and-territory.md](../.docs/milestone-15-part-48-feet-dialogs-and-territory.md).

- **Feet** (`Presentation/Animals/AnimalFooting`): a last pass over any pose made for flat ground.
  - `Ankle` reads where each ankle is from its leg.
  - `HipShift` finds how far the hips must drop or rise for the ground under each foot (a
    `FootGround`), a rise winning so no foot sinks.
  - `Plant` moves each ankle by its ground, keeping its lift, bends the hip and knee to reach it and
    lays the foot along the slope.
  - `CreatureView` samples the ground under each foot and its slope, raises the hips at once and
    lowers them smoothly, and leans only the torso.
- **Territories** (`Presentation/Territories`, `TerritoryImage`):
  - each god's lead (its influence less the greatest of 0.1 and every other god's) is sampled over
    a grid of at most 160 cells a side; a god's land is where its lead is above nothing, and a
    point's distance inside the border is its lead over the lead's slope;
  - `TerritoryImage` paints the borders, the glow and the player's wash, land only, with widths by
    island and zoom band (`TerritoryLook`);
  - `TerritoryPresenter` lays the picture over the island as a decal, painted on a worker thread when
    influence moves or the zoom band changes.
- **Dialogs:** `CommandController` keeps the wheel, pan gestures and pinches from the camera while a
  dialog is open or the pointer is over the interface. Godot's interface stops mouse buttons but not
  gestures.

## Every command in view

ADR-068 records the decisions, and the plan is
[.docs/milestone-15-part-47-commands-in-view.md](../.docs/milestone-15-part-47-commands-in-view.md).

- **The key map** (`Pantheon.Game.Controls.KeyGuide`): every key and gesture of play as
  `KeyEntry`s in seven `KeyGroup`s, naming `project.godot`'s actions (and a held `KeyModifier`, or
  a gesture in words). Its debug group is the debug tools: `IsDebug` tells the client which keys
  run only while they are on. A test holds it to `project.godot` both ways.
- **The god's bar** (`CommandBar.Buttons`): the buttons in order, each with the action it presses;
  the Debug palette's only with the debug tools on.
- **The overlays offered** (`OverlayChoices`): by name, the debug overlays only with the debug
  tools; `OverlayMap.Next` skips them likewise.
- **Seeking a god** (`CreatureCommands.Seek`): the Creature's GoTo, through the authority, aimed at
  the other god's Creature; the meeting is the simulation's own (ADR-017, ADR-036).
- **Choices** in the menus: `NewWorldOptions.GodsMet` (`WorldMaker` introduces every pair before
  the history begins), and `GameSettings.DebugTools` and `SimpleTerrain`.

```mermaid
flowchart LR
    guide[KeyGuide] --> map[KeysPage: the key map]
    guide --> palette[DebugPalette]
    guide --> gate[GameRoot: debug keys only while on]
    bar[CommandBar] --> godbar[GodBar]
    godbar --> controller[CommandController]
    palette --> controller
    controller -->|presses the action| handlers[the keys' own handlers]
```

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
  peace by its view, the balance of followers, war-weariness and its personality. Beside betraying
  an ally it weighs absorbing it, by the believers it would take (ADR-040).
- **Lightning and hurricanes (ADR-041).** `MiracleEffects.Strike` lets a smiting bolt light the
  struck region by its flammability, from a stream of the seed and the tick. `WeatherSystem` raises
  `HurricaneApproaching` when a hurricane forms, and `WeatherText.Coast` names the coast for the
  alert.

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

## Four gods and pantheon victory

ADR-015 records the decisions, and the Milestone 9 plan
([.docs/milestone-9-four-gods.md](../.docs/milestone-9-four-gods.md)) has the terms and the
measured politics.

- **Four gods**: the player and three AI gods (Tyrant, Guardian and Trickster in `gods.json`), each
  with a home and a Creature. Eight personalities have profiles in `god_ai.json`. Large and huge
  worlds add two or four of `gods.json`'s `ExtraGods` (Zealot, Nature Keeper, Warlord,
  Isolationist; ADR-027).
- **Blocs**: a god and every god allied with it, directly or through allies
  (`DiplomacyModel.BlocOf`). With `PantheonVictory`, a god's standing is its bloc's share when that
  is more than its own, so a bloc dominates and wins together; a god holding the share alone wins
  alone.
- **Allies remember**: a wrong done to a god is noted, at half strength, by each of its allies
  (`WrongedAlly`).
- **The AI diplomat weighs third gods**: a third god's lead draws two gods into alliance and away
  from war with each other; a leader draws war; the goodwill of a fresh peace or alliance holds war
  and betrayal back; near a joint victory a treacherous god that outweighs its ally may break away.

```mermaid
flowchart LR
    Shares["Followers' shares"] --> Diplomat["GodDiplomat"]
    Memory["Relations and memories"] --> Diplomat
    Diplomat --> Authority["DiplomacyAuthority"]
    Player["Diplomacy panel (P)"] --> Authority
    Authority --> Stances["Alliances and wars"]
    Stances --> Bloc["DiplomacyModel.BlocOf"]
    Bloc --> Standing["VictorySystem.StandingOf"]
    Shares --> Standing
    Standing --> Victory["Dominance, WinnerGodIds"]
    Stances --> Ledger["DiplomacyLedger:<br/>allies remember wrongs"]
    Ledger --> Memory
```

## Migration

ADR-016 records the decisions, and the Milestone 10 plan
([.docs/milestone-10-population-movement.md](../.docs/milestone-10-population-movement.md)) has the
rules with their shipped values and the measured runs.

- **Pressure and the wish to leave**: hunger, homelessness, danger, war, and followers' fear and
  doubt push a settlement's people out; love keeps them. Above a threshold the wish to leave
  (`SettlementState.Emigration`) builds, and once enough want to go a group sets out.
- **Groups** (`MigrationGroup`, §82) carry their people, beliefs, food and health along a route
  from the region pathfinder to the most appealing settlement they can reach. On leaving they
  give up gods they fear or doubt, in proportion.
- **Arrival**: the god followed most at the destination welcomes them, demands that they convert
  or turns them away, by its `RefugeePolicy`. The migrants' god remembers it (`Sheltered`,
  `Converted`, `TurnedAway`), and joining mixes beliefs by population, so the host's gods convert
  them over time.
- **Policies** are commands checked by `MigrationAuthority`: the player sets theirs in the
  diplomacy panel, and `RefugeeAdvisor` sets the AI gods' from their profiles, refusing migrants
  while their own followers lack room.

```mermaid
flowchart LR
    Conditions["Hunger, ruins, danger,<br/>war, fear, doubt"] --> Push["MigrationModel.Push"]
    Push --> Urge["Emigration"]
    Urge --> Group["MigrationGroup<br/>(beliefs, food, health, path)"]
    Group --> Road["MigrationSystem: walk, eat"]
    Road --> Policy{"Host god's RefugePolicy"}
    Player["Diplomacy panel"] --> Authority["MigrationAuthority"]
    Advisor["RefugeeAdvisor (AI)"] --> Authority
    Authority --> Policy
    Policy --> Merge["Merge into the settlement"]
    Policy --> Ledger["DiplomacyLedger"]
    Merge --> Belief["Belief step converts over time"]
```

## Weather

ADR-013 records the decisions, and the Milestone 7 plan
([.docs/milestone-7-weather.md](../.docs/milestone-7-weather.md)) has the rules with their
numbers and the measured droughts and storms.

- **Cells** drift on the prevailing wind. Over the sea they take up water; over land they rain it
  out, their lows fill and they thin away. Rain, wind and lightning follow humidity, the depth of
  the low and warmth, and name the weather: clear, rain, thunderstorm or windstorm, and since
  ADR-028 snow, fog and hurricanes (see
  [Seasons, extremes and the land's life](#seasons-extremes-and-the-lands-life)).
- **Spells** (normal, dry, wet) of seeded length set how often cells blow in and how stormy they
  are; the prevailing wind turns between them.
- **Soil water** per land region rises with rain, falls with warmth (faster in a dry spell) and is
  refilled slowly by springs. A settlement's drought follows its farmland's soil. It lowers the
  harvest and counts as hardship, so people worry before their stores run out.
- **Storms** ruin a share of the stores and building work of the settlements they cover.
- **Rain** (a miracle) calls a cloud over its target; AI gods call it where drought is deep.

```mermaid
flowchart LR
    Sea["Sea: humidity"] --> Cells["Weather cells<br/>(wind, spell)"]
    Cells --> Rain["Rain"]
    Rain --> Soil["Soil moisture"]
    Sun["Warmth, dry spells"] --> Soil
    Soil --> Harvest["Harvest (water yield)"]
    Soil --> Hardship["Drought hardship"]
    Cells --> Storm["Storm damage"]
    Storm --> Stores["Stores, building work"]
    Harvest --> Stores
    Miracle["Rain miracle"] --> Cells
```

## Disasters

ADR-014 records the decisions, and the Milestone 8 plan
([.docs/milestone-8-disasters.md](../.docs/milestone-8-disasters.md)) has the rules with their
numbers and the measured fires and earthquakes.

- **Wildfires** live in the regions they burn. Flammability is vegetation × unscorched share ×
  dryness², so fire follows drought. Dry storms, villages' hearths, earthquakes and the Fire
  miracle light it. It grows on its fuel, scorches the land, spreads (more downwind), and dies in
  the rain, under a village's firefighting or for want of fuel. Scorched land yields less and
  regrows.
- **Earthquakes** are processes: a few seconds of shaking, fading with distance from the
  epicentre, that collapse houses into ruins, kill, spill stores and start fires.
- **Houses crack** (ADR-048): every blow that leaves houses standing lowers the village's
  integrity, cracked houses fall more easily in the next, and villages mend them over minutes.
  Houses resist quakes, fires and floods each by their own share.
- **Settlements respond**: they fight fires in their region and rebuild ruins at half the work;
  the homeless fall ill. Survivors fear their gods and doubt that the gods care, and danger counts
  as hardship.
- **Gods intervene** with Fire, Earthquake and Shield (protection from disasters for three
  minutes) and Rain (which quenches fire).

```mermaid
flowchart LR
    Dry["Dry land (ADR-013)"] --> Fire["Wildfire"]
    Sparks["Storms, hearths,<br/>quakes, miracles"] --> Fire
    Fire --> Scorch["Scorch: smaller harvests"]
    Fire --> Damage["Houses, people, stores"]
    Quake["Earthquake"] --> Damage
    Damage --> Cracks["Cracked houses (integrity)"]
    Cracks --> Damage
    Mending["Mending"] --> Cracks
    Damage --> Ruins["Ruins, the homeless"]
    Ruins --> Debris["Debris (shown only)"]
    Ruins --> Rebuild["Rebuilding"]
    Damage --> Belief["Fear and doubt"]
    Shield["Shield"] --> Damage
    Rain["Rain, firefighting"] --> Fire
```

## Hazards

ADR-029 records the decisions, and the plan
([.docs/milestone-15-part-9-hazards.md](../.docs/milestone-15-part-9-hazards.md)) the numbers.
`HazardSystem` runs after the disaster step, on its interval, with a stream derived from the seed
and the tick.

```mermaid
flowchart LR
    quake[Earthquake begins] --> crack[Crack mark]
    quake --> wave[Tsunami near the coast]
    quake --> slide[Landslide on shaken slopes]
    soaked[Soaked steep slope] --> slide
    slide --> rubble[RegionState.Rubble]
    rubble --> blocked[RegionPathfinder: the way is blocked]
    volcano[Volcano wakes] --> tremor[Tremors]
    tremor --> quake
    volcano --> lava[Lava: fire, scorch, harm]
    volcano --> ash[Ash: harm, sickness, spoiling]
    storm[Thunderstorm over warm land] --> tornado[Tornado]
    meteor[Falling star] --> crater[Crater, fire, quake]
    wet[Warm, wet fields] --> blight[RegionState.Blight]
    blight --> harvest[Smaller harvests]
    dry[Hot, dry land] --> locusts[Locust swarm]
    locusts --> stores[Eaten stores]
    wave --> strike[DisasterDamage.Strike]
    slide --> strike
    tornado --> strike
    crater --> strike
    strike --> village[Houses, people, fear]
    prepared[Sea walls, dams, evacuation routes] --> strike
```

- **Each step** cracks the land where earthquakes began and raises a wave from those near the
  coast, runs the volcano's phases, clears rubble and lets soaked or shaken slopes slide, and may
  bring a meteor. `LivingHazards` then spins tornadoes down from thunderstorms, raises and moves
  locust swarms, and grows, spreads and cures blight. Old marks are forgotten.
- **Striking a village** goes through `DisasterDamage.Strike`: the shield, Protection,
  architecture, the village's works (`Exposure`) and evacuation routes (`Deadliness`), then fear
  and a `SettlementDamaged` report.
- **Preparation.** `SocietyModel.NextProject` raises sea walls only beside the sea, and dams and
  irrigation only beside water. Irrigation waters a village's fields in `SettlementEconomy.Assess`.
- **Blight** lowers farmland's fertility in `SurveyFarmland`. A village is told as its fields'
  mean blight reaches the notice and again below half of it; `BlightedSettlementIds` keeps who
  has been told.
- **Presentation.** `HazardAlerts`, the history's hazard titles, the labels' "blight" and "cut
  off", F1's hazards line, F3's layers, the Danger overlay, `HazardCover` and the client's
  `HazardPresenter`.
- **Not modelled:** lava as its own flow, slides that change the terrain's height, tornadoes
  that carry things, and reservoirs beyond the cistern.

## Paths, the hand and the Divine Acts

ADR-030 records the decisions, and the plan
([.docs/milestone-15-part-10-powers.md](../.docs/milestone-15-part-10-powers.md)) the numbers.

```mermaid
flowchart LR
    deeds[Deeds, fights, rewards] --> callings[CreatureState.Callings]
    callings --> grow[CreatureCalling.Grow at levels 3, 6 and 9]
    grow --> paths[CreatureState.Specialisations]
    paths --> effects[CallingEffects: deeds, fights, disasters]
    key[R, Shift+R and Ctrl+R] --> hand[HandAuthority]
    hand --> held[GodState.Held]
    hand --> carry[HandCarry: join, walk or drown]
    hand --> throw[HandThrow: ruin, crater, fear]
    carry --> belief[Rescue or abduction in belief]
    panel[Domains panel] --> act[DivineActs: hallow, awaken, still]
    act --> history[History records]
```

- **Paths.** `CreatureDeeds.Complete` calls the Creature down the deed's paths and applies its
  paths' effects (`CallingEffects.AfterDeed`); a reward and the outcome of a fight call it too.
  `CreatureSystem` lets each Creature take up its due paths every tick (`CreatureCalling.Grow`) and
  skips one its god's hand holds. A Warrior's power is in `CreatureBonds.Power`, a Destroyer's and
  a Commander's effects in the Attack deed, and a Guardian's in `DisasterDamage.Protection`, which
  every disaster uses.
- **Attributes.** `CreatureAttributes` derives intelligence (in `CreatureModel.Learn`), resilience
  (in combat, starvation and wounds) and divine affinity (in `InfluenceField`) from level.
- **The hand.** `HandAuthority` checks the god, the hand's rules, the island, a full or empty hand
  and the god's influence, then lifts the nearest of the Creature and villagers, else a tree or a
  rock, sets it down or throws it. It is one of the `Authorities`, so the player's commands and a
  joined player's `HandIntent` take the same path.
- **Offerings (ADR-039).** `HandAction.TakeOffering` has `HandOfferings` take a share of the stores
  of a village the god leads. Setting it down or throwing it gives it to the nearer of a village
  (stores, and gratitude by its food need) or the god's Creature (its hunger and loyalty), and
  raises `OfferingGiven`.
- **Divine Acts.** `MiracleScale.DivineAct` miracles leave the miracle bar
  (`HudReadout.AvailableMiracles`) for the Domains panel, which selects them for the targeting ring.
  `MiracleEffects` hallows regions and calls `DivineActs` to wake the volcano or still every
  disaster; `DivineActDone` reaches the alerts and the history. `BeliefSystem` keeps a hallowed
  region's blessing at least its `Hallowed`.
- **Not modelled:** carried offerings, Creatures above level 10 and the AI using the hand. The acts
  that change the terrain came in part 24: see [Reshaping the land](#reshaping-the-land).

## Reshaping the land

§14's Divine Acts that change the land's shape (ADR-045) are kept in the world and made into the
terrain: Raise an Island, Split the Mountain and Turn the River.

```mermaid
flowchart LR
    cast[A Greater God casts an act] --> plan[LandShaper.Plan: where and how]
    plan --> keep[WorldState.Land keeps the reshaping]
    keep --> make[TerrainGenerator.Reshape: ground, rivers, biomes]
    make --> settle[LandShaper.Settle: regions and water]
    make --> session[GameSession.TerrainChanged]
    session --> client[The client rebuilds the terrain]
    keep --> history[History, alerts and the Land section]
```

- **Planning.** `LandShaper.Plan` fixes everything the terrain needs when the act is cast:
  - an island's centre in a sea region;
  - a gorge's ends, along the direction whose ends lie lowest, with its floor heights;
  - a channel from the nearest inland river point down to the point, with the dam's place a little
    downstream and its crest.

  Where nothing can be done it returns nothing, and the cast is refused.
- **Making the terrain.** `TerrainReshaper` sculpts every reshaping into the ground before the
  rivers are found, so they follow the new land: a lobed dome, a floor between two heights,
  or a falling channel and a dam. `TerrainGenerator.Reshape` starts from the kept ground: an
  act costs 200–280 ms on the default island instead of 1.4 s, with the same result.
- **Settling the world.** `LandShaper.Settle` gives changed regions their new height, climate,
  biome and fresh water, new land its soil, plants, forest and stone, and finds the rivers and
  lakes again, each river keeping the level of the old one it shares most land with.
- **Holding the terrain.** `MiracleCaster.Land` (`LandStage`) holds the current terrain, set by
  `GameSession`. The acts replace it, and the session takes it up after the command and raises
  `TerrainChanged`. The client rebuilds the terrain and everything set on it, without moving the
  camera.
- **Shown and shared.**
  - `DivineActDone` reaches the history (Raising, Splitting, Diverting) and the alerts;
  - `LandReadout` lists the reshapings in F1, and `--reshape` makes one for captures;
  - `WorldSection.Land` carries them to joined players.

## Great events

ADR-034 records the decisions, and the plan
([.docs/milestone-15-part-14-great-events.md](../.docs/milestone-15-part-14-great-events.md)) the
numbers.

```mermaid
flowchart LR
    state[Crowding, health, pollution] --> outbreak[PlagueModel.Ripeness]
    outbreak --> sick[SettlementState.Plague]
    sick --> spread[Neighbours and roads]
    spread --> sick
    sick --> health[Health and deaths]
    heal[Healing miracles, hospitals] --> sick
    seed[Seed and tick] --> eclipse[EclipseModel]
    eclipse --> dread[Dread and awe]
    doubt[Doubt and learning] --> reform[ReformationModel]
    wars[Wars, soil, migrants] --> watch[GreatEventWatch]
    sick --> great[GreatEventSystem: great events]
    eclipse --> great
    reform --> great
    watch --> great
    great --> history[History, alerts, council]
```

- **The plague.** `GreatEventSystem` breaks villages out by their ripeness, spreads the sick to
  neighbours and along open roads, and lets `PlagueModel.Advance` run each village's course. Three
  villages sick at once make a great event. `MiracleEffects` cures by a heal's amount, and
  `CouncilModel.Struck` counts a sick village as struck.
- **Eclipses.** `EclipseModel` sets each eclipse from the seed and the tick, darkens and lights the
  sun, and gives the dread. `MiracleCaster` multiplies the witnesses' closeness by
  `GreatEventModel.Awe`.
- **The beast (ADR-035).** `BeastSystem` finds its lair the first time, wakes it after its first
  sleep (a great quake, the volcano, or by itself), walks it to the nearest village, strikes through
  `DisasterDamage` with `DisasterKind.Beast`, lets nearby Creatures fight it, calls the AI gods'
  Creatures through `CreatureBrain.Command`, and sends it home tired. `MiracleEffects` lets smiting
  harm it, and `BeastModel.Fall` shares the glory.
- **The anomaly (ADR-035).** `AnomalyModel` surges power at a seeded land region; the gods draw power
  there by `InfluenceField`, and `MiracleCaster` prices miracles within its reach at its factor.
- **Reformations and the watch.** `ReformationModel` measures and reforms each god's faith.
  `GreatEventWatch` begins and ends world wars, great droughts and great migrations from the
  world's state. The council answers a plague or a great drought as it answers a disaster.

## Known gods and plans

ADR-036 records the decisions, and the plan
([.docs/milestone-15-part-16-known-gods-and-plans.md](../.docs/milestone-15-part-16-known-gods-and-plans.md))
the numbers.

```mermaid
flowchart LR
    villages[Neighbours, believers, roads] --> meet[Acquaintance.Step]
    creatures[Creatures, deeds, council] --> meet
    meet --> known[GodState.KnownGods]
    known --> authority[DiplomacyAuthority: NotMet]
    known --> planner[GodPlanner every 30 s]
    world[Danger, doubt, plague, drought, the beast] --> planner
    planner --> goals[GodState.Goals]
    goals --> focus[GoalFocus]
    focus --> casts[GodAi: boosts, smites, hunts]
    focus --> diplomat[GodDiplomat: bias]
    focus --> priorities[PriorityAdvisor: prepare]
```

- **Meeting.** `DiplomacySystem` runs `Acquaintance.Step` each second. It introduces gods whose
  villages are neighbours, share believers or a whole road, whose Creatures have met, whose deeds
  touched each other's people, who have dealings, or who sit in council (`GodsMet`).
  - With `DiplomacyRules.Acquaintance.MeetFirst`, `DiplomacyAuthority.Check` refuses any move but
    answering, peace and breaking with `NotMet` until the two have met.
  - `GodDiplomat` and `GodPlanner` consider only known gods.
- **Planning.** `GodAi.Act` asks `GodPlanner.PlanIfDue` first.
  - The planner scores one best option of each `GodGoalKind` (`GoalOption`, with its reason) and
    keeps young goals unless they are moot.
  - It fills up to `MaxGoals` of weight `Threshold` or more, announcing each new one
    (`GodGoalTaken`).
- **Focus.** `GoalFocus` acts on what the AI does:
  - `Apply` multiplies the utility of casts and sends that serve a goal by
    `1 + (Boost − 1) × weight`;
  - `AddSmites` and `AddHunt` aim smiting miracles and the Creature at the beast;
  - `Diplomacy` biases `GodDiplomat`'s alliance, peace, war and betrayal;
  - `Preparing` gives `PriorityAdvisor` health or food.
- **Seen.** F1 shows each AI god's plans, its next plan, the gods it knows and its options
  (`SessionPreviews.PreviewGodGoals`). The P panel and the top bar mark unmet gods, and the
  intelligence line shows a well-informed player a rival's goals.

## Wood, stone and roads

ADR-033 records the decisions, and the plan
([.docs/milestone-15-part-13-resources.md](../.docs/milestone-15-part-13-resources.md)) the numbers.

```mermaid
flowchart LR
    gen[ResourceFounder] --> land[RegionState: Forest, Timber, Stone]
    land --> gather[ResourceModel.Gather]
    labour[AllocateLabour: gatherers] --> gather
    gather --> stores[SettlementState: Wood, Stone]
    stores --> houses[Houses and buildings]
    stores --> roads[RoadSystem: build with stone]
    roads --> open[Open roads]
    open --> walkers[Faster walkers]
    open --> trade[Trade and food]
    land --> regrow[ResourceSystem: regrow, burn]
    land --> wild[Wildlife and landslides]
    land --> trees[ForestLayout and ForestPresenter]
    open --> ribbon[RoadLayout and RoadPresenter]
```

- **Gathering.** `SettlementEconomy.AllocateLabour` takes gatherers out of the farmers while the
  stores run short. `SettlementSystem` then calls `ResourceModel.Gather` and takes a house's wood
  when one is begun. `SocietySystem` begins a building only when the stores hold its wood and
  stone.
- **The land.** `ResourceSystem` grows timber back towards the forest and burns it.
  `EcologySystem` holds less wildlife where the forest is cleared, `HazardSystem` slides cleared
  wet slopes sooner, and the Regrowth miracle and the hand's trees change timber.
- **Roads.** `RoadSystem` plans one village's road a step, in turn, builds the unfinished ones,
  and wears, mends, decays and trades on the whole ones. `MigrationSystem` and `NotableModel.SetOut`
  take an open road between its villages, and both walk faster on one (`RoadModel.SpeedAt`).

## Prophets and elders

ADR-032 records the decisions, and the plan
([.docs/milestone-15-part-12-notables.md](../.docs/milestone-15-part-12-notables.md)) the numbers.

```mermaid
flowchart LR
    beliefs[Settlement beliefs] --> rise[NotableSystem: rise]
    anoint[Anoint a Prophet] --> raise[NotableModel.Raise]
    rise --> raise
    raise --> notables[WorldState.Notables]
    notables --> elders[Elders: keep faith, or turn]
    notables --> prophets[Prophets: preach, then walk]
    prophets --> sermon[NotableModel.Preach]
    sermon --> beliefs
    elders --> beliefs
    notables --> influence[InfluenceField]
    temples[Temples] --> influence
    danger[Danger] --> martyr[Martyrdom impulse]
    martyr --> beliefs
```

- **Rising.** Every divine step `NotableSystem` gives each village of `ElderPopulation` or more an
  elder of the god it follows most, and lets a village devout to a god (`DevoutTo`) call a
  prophet by chance, within the god's `MaxProphetsPerGod`. The Anoint effect raises one at once.
  A notable's name and lifespan come from the seed and their id.
- **Elders** add faith and take doubt every step. When the village's resentment of their god
  reaches `ElderTurnResentment`, they turn to the next god it follows, leaving `TurnDoubt`
  (`ElderTurned`, `Resented`). When another god leads the village with more than twice their
  god's following, they follow it quietly.
- **Prophets** preach for `PreachSeconds`, then `NextVillage` picks the village within
  `ProphetRange` with the most people not yet their god's, nearer ones first, and
  `RegionPathfinder` the way. A sermon wins the godless and some of each rival's followers
  (`NotablePreached`).
- **Deaths.** Age, the loss of the village, or danger takes a notable (`NotableDied`). A prophet
  taken by danger is a martyr, and the god's followers within `MartyrRadius` take the
  `Martyrdom` impulse. Danger lived through is remembered once for each place.
- **Influence.** `InfluenceField` adds a source at every temple to the god and at every prophet
  of the god.
- **Shown and shared.** `NotableAlerts`, the history (Prophet, Martyrdom, ElderTurned),
  `NotableSection` in the book, `NotableReadout` in F1 and `NotableView` in the world.
  `WorldSection.Notables` goes to joined players every second.

## The selected villager

§80's Tier 3 lists a selected villager (ADR-043). Like prophets and elders they live in
`WorldState`, never promoted by the camera: the player chooses one, and they are simulated in full
wherever anyone looks.

```mermaid
flowchart LR
    crowd[Tier 2 figure at the cursor] -->|"/"| commands[VillagerCommands.Pick]
    commands --> authority[VillagerAuthority.Follow]
    authority --> state[WorldState.Villagers]
    state --> system[VillagerSystem]
    system --> mind[VillagerMind options]
    system --> danger[VillagerModel danger and ways]
    events[Tick and command events] --> witness[VillagerWitness]
    witness --> state
    system --> state
    state --> shown[VillagerView, VillagerPage, VillagerReadout, VillagerAlerts]
```

- **Choosing.** `VillagerCommands.Pick` rebuilds the village's roster with `RepresentativeRoster`
  and poses it with `CitizenRoutine` at the crowds' time (ADR-049), as the client draws it, and takes the
  figure within 6 m of the cursor. The command carries the figure's home, work and shrine spots;
  `VillagerAuthority` checks them and draws the person from `SeedMixer` with the village and slot,
  so the same figure is the same person.
- **Living.** `VillagerSystem` runs every settlement step:
  - **Needs:** hunger, fatigue and health;
  - **Death:** age, hunger, illness, or danger by `Mortality`;
  - **Flight:** they flee at `FleeAt` (lower once wary) to the nearest safe village, shelter there
    fed, and return when the danger falls to half;
  - **Homes:** a villager whose village is emptied seeks a new home;
  - **Choosing:** when an activity ends they choose by `VillagerMind.Options`;
  - **Faith:** it drifts to the village's and grows in prayer, and they turn when it fails.
- **Memory.** `SimulationEngine.Step` and `Chronicle` pass every event to
  `VillagerSystem.Witness`, so miracles cast by commands are seen too. A recent hardship adds to the
  wish to pray; a remembered danger lowers the flight threshold.
- **Nothing else changes.** Villagers eat, believe and die alone: the village's numbers already
  count them, so the statistical world is the same with or without them.
- **Shown and shared.** `VillagerView`, the camera's tracking, the Book's page, F1 and alerts.
  Villagers travel to joined players in `WorldSection.Notables`.

## Performance testing

§104 asks for repeatable stress scenarios and measurements (ADR-044).

```mermaid
flowchart LR
    option["--stress=on"] --> content[StressScenario.Content and Presentation]
    content --> world[Stress world: 50 villages, 8 gods and Creatures]
    world --> apply[StressScenario.Apply: 100,000 people, 20 cells, 3 disasters]
    apply --> run[The game runs]
    run --> monitor[PerformanceMonitor: frames, ticks, memory, a save]
    monitor --> readout[PerformanceReadout: F1 and the run's report]
```

- **The scenario.** `StressScenario` is pure and seeded:
  - its world size is the largest island with 50 villages and 8 gods;
  - `Apply` gives each village 2,000 people with houses and stores, adds weather cells until there
    are 20, and starts an earthquake, a fire and a flood;
  - its presentation draws up to 1,000 figures a village within 1,300 m.
- **Measuring.** `PerformanceMonitor` in the client wraps `GameSession.Update` in a stopwatch and
  keeps the last 240 frames: frame time, the simulation's share of a frame, a tick's time, ticks
  per second. It reads memory and the engine's objects, nodes and draw calls from Godot's monitors,
  and times a save written and read back without touching the disk.
- **Reporting.** `PerformanceReadout` turns a `PerformanceSample` into F1's lines, with the simulation
  budget's line (ADR-056), and adds a census
  of the world's load; unattended runs print the same lines.
- **Measured** (Apple M3 Pro, seed 42; numbers in the ADR):
  - 100 FPS with 7,000 citizens drawn, on both renderers;
  - 1.5 ms a tick;
  - a 10 MB save in about 130 ms;
  - 60 KB/s to a joined player since regions are sent within a tolerance (ADR-056; 144 KB/s
    before, mostly the regions' small changes).

## Debug tools

Every system gets tools to see and drive it from the start (see [AGENTS.md](../AGENTS.md)).
The readouts are built by pure functions in `Pantheon.Game.Diagnostics` and only displayed by
the client. The keys that change the world, and the debug views, are the debug tools of ADR-068:
they run while the settings turn them on (on, by default, in a game run from the project), and the
god's bar's Debug palette has a button for each.

- F1: the debug panel (hidden at the start), built by `DebugReadout` from a `DebugSnapshot`. Its
  second line gives the seed, the island's width and the world's victories (ADR-027).
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
  changes, and `DebugOverlay.ShowRegionMap` displays it. The soil moisture layer follows the
  weather.
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
  utilities, its learned preferences and its latest memories. Since Milestone 11 it also shows
  each Creature's combat skill, scars and power, its view of every Creature it has met (friendship,
  fear, rivalry, respect, trust and the latest encounters), and how the player's Creature would
  answer the nearest one if they met now. K brings the nearest Creature beside the player's, and
  `--meet=on|<name>` does so at the start. Since ADR-055 a face line gives its mood, joy, anger,
  fear, sleep and nature (`CreatureExpression`).
- The F1 panel's god AI view (`GodAiReadout`, §106) shows each AI god's personality, its goal now
  (its best candidate) and its top three candidates with utilities and reasons, followed by its
  diplomatic candidates and the factors behind them.
- P: the diplomacy panel shows each rival's status, its alliances and wars with the other gods,
  its attitude and memories, and drives every diplomatic move. `--diplomacy-panel=on` opens it in scripted runs.
- The F1 panel's weather lines (`WeatherReadout`, §106) show the spell and its time left, the wind,
  the strongest cells with their state, the soil and weather under the cursor and the selected
  settlement's water and yield. F8 starts the next spell (normal, dry, wet, heat wave, cold wave),
  F10 calls a storm at the cursor, and `--weather=normal|dry|wet|storm|heatwave|coldwave|hurricane`
  sets one up at the start. Since ADR-028 a climate line gives the season and what it and a wave
  change, the cursor line the snow and the land's life, F3 has snow, wildlife, damage, scar and
  pollution layers, O has the Nature overlay, Y begins the next season and `--season` one at the
  start.
- The F1 panel's disaster lines (`DisasterReadout`) list the earthquakes under way, the fiercest
  fires and the selected settlement's dangers (fire threat, shaking, ruins, homeless, scorched
  land, shield). F3 has fire and scorch layers, the Danger overlay shows fires, ash and shields,
  F11 starts an earthquake and F12 a fire at the cursor, and `--disaster=quake|fire` starts one at
  the focused settlement. Since ADR-029 a hazards line gives the volcano, tornadoes, swarms,
  blighted and buried regions and marks, each settlement's line its blight and rubble, F3 has
  blight and rubble layers, the Danger overlay shows them, I starts the next hazard at the cursor
  and `--disaster` takes `tornado|landslide|meteor|blight|locusts|tsunami|eruption`.
- The F1 panel's migration lines (`MigrationReadout`, §106 "migration routes") list the groups on
  the road (size, way, cause, food, health, the god they follow), every god's refugee policy with
  the AI's reason, and the selected settlement's pressure, cause and people wanting to leave. The
  Migration overlay shows the routes, M sends 20 people out of the village under the cursor,
  `--migrate=<n>` sends n out of the focused village and `--refugees=welcome|convert|turnaway`
  sets the player's policy.
- The F1 panel's notable lines (`NotableReadout`) count the prophets of each god and the elders,
  list each prophet's walk, last deed and time left, and name the selected village's elder and
  preachers. `--prophet=on` raises a prophet of the player's god at the focused village.
- The F1 panel's resource lines (`ResourceReadout`) give the share of the island's forest standing
  and the roads built, being built and cut, and the selected village's stores, gatherers, land and
  roads; F3 adds Timber and Stone layers; `--roads=on` builds every road the villages would plan,
  and `--stores=full|empty` sets their stores.
- The F1 panel's great event lines (`GreatEventReadout`) give the events under way, the next
  eclipse, the sick villages and every faith's doubt and learning, and the selected village's
  sickness, immunity and shield; `;` starts the next of the plague at the cursor, an eclipse and the
  player's reformation, and `--great-event` one at the start.
- F4 (`GrantPower`) gives the player's god 500 Divine Power.
- O cycles the player map overlays (influence, religion, faith, food, weather, danger, migration,
  culture, nature, population, risk, politics, navigation, authority),
  which double as debug views.
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
    alt version 35 (current)
        Save->>Save: deserialize (strict)
    else versions 1 to 34
        Save->>Mig: Upgrade(json, version)
        Mig-->>Save: JsonObject at version 35, one step at a time (1→2: health, construction, 2→3: player god, beliefs, blessings, 3→4: Creatures, 4→5: personalities, dominance, winner, 5→6: diplomacy, 6→7: weather, soil water, 7→8: disasters, fire, scorch, ruins, shields, 8→9: winners, 9→10: migration, refugee policies, 10→11: Creatures' skill, scars and views, 11→12: history, 12→13 to 17→18: see Persistence, 18→19: victories, 19→20: seasons, the land's life, 20→21: hazards, 21→22: paths and hands, 22→23: notables, 23→24: forests, stores and roads, 24→25: plague and great events, 25→26: the beast and the anomaly, 26→27: known gods and goals, 27→28: Creatures' temperament, 28→29: landforms, 29→30: offerings, 30→31: followed villagers, 31→32: the land's reshapings, 32→33: the houses' integrity, 33→34: the Creatures' animals, 34→35: the gods' personas)
        Save->>Save: deserialize (strict)
    else missing, unsupported or newer
        Save-->>Session: SaveFormatException
    end
    Save->>Save: SaveIntegrity.Validate(world)
    Session->>Session: gods a connected player played (Remote) go back to the AI
    Session->>Gen: Generate(seed, generation settings)
    Session-->>Root: WorldReplaced
    Root->>View: Rebuild(world, terrain)
    Cmd-->>Root: status line (or the error, with the current world kept)
```

## World generation and terrain backends

```mermaid
flowchart TD
    Seed["WorldSeed + WorldGenSettings"] --> TG["TerrainGenerator<br/>FastNoiseLite layers"]
    TG --> LM["LandformMarker<br/>islands, volcanic land, valleys, coasts"]
    LM --> TF["TerrainField<br/>height / moisture / temperature / biome"]
    TF --> RB["RegionBuilder<br/>32x32 regions"]
    RB --> SF["SettlementFounder"]
    SF --> WS["WorldState"]
    TF --> TPF{"ClassDB has Terrain3D?"}
    TPF -- yes --> T3["Terrain3DTerrainPresenter<br/>import_images(height, -, colour)"]
    T3 --> SP["Terrain3DShaderPatch<br/>bounds-safe region lookups,<br/>edge-safe colour lookup"]
    TPF -- "no, or --terrain=fallback" --> FB["FallbackMeshTerrainPresenter<br/>(clearly marked, with reason)"]
```

Default world: 1024 × 1024 height samples at 2 m spacing (a 2048 m island), 32 × 32 regions of
64 m, sea level at 0 m. The biomes are Sea and §5's land biomes: Grassland, Forest, Highland,
Tundra, Desert and Swamp by climate, and Coast, River Valley, Island and Volcanic by place
(ADR-038). `TerrainColorizer` gives each its colour from `biome_palette.json` and blends
neighbouring land biomes over `BiomeBlendMetres` with a sliding box filter.

## Determinism

- World generation is a pure function of `(WorldSeed, WorldGenSettings)`.
- Tick results are a pure function of `WorldState` (including RNG state) and the content, and a
  cast is a pure function of the world, the request and the content; the belief step and the
  caster draw no random numbers. The weather and disaster steps draw from streams seeded by the
  world seed and the tick, so they need no saved state and never shift the shared stream
  (ADR-013, ADR-014).
  The settlement step's births and deaths draw from the saved `SimulationRandom` in settlement
  list order, so a run continues identically across a save and load.
- Presentation never consumes a simulation random stream. The settlement layout and the Tier 2
  roster use `PresentationHash` of the seed, the settlement id and a slot or index, and the
  routines are functions of the crowds' clock (ADR-049). The same world, tick and camera therefore show the
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
  migration (against `save-v5.json`). Since Milestone 7 it covers the weather rules and step,
  spells, soil water, droughts and storms, the Rain miracle and the AI's use of it, the weather's
  presentation and tools, and the version 6 → 7 migration (against `save-v6.json`). Since
  Milestone 8 it covers earthquakes, wildfires, destruction, ruins, the settlements' response, the
  Fire, Earthquake and Shield miracles and the AI's use of them, the disasters' presentation and
  tools, and the version 7 → 8 migration (against `save-v7.json`). Since Milestone 9 it covers
  blocs and pantheon victory, allies' memory, the AI diplomat's coalition, balance, goodwill and
  endgame terms, the new personalities, the four-god presentation, and the version 8 → 9 migration
  (against `save-v8.json`). Since Milestone 10 it covers migration's pressure, abandonment, appeal,
  conversion and merging, the migration step from leaving to arriving, refugee policies for the
  player and the AI, migration's presentation and tools, and the version 9 → 10 migration
  (against `save-v9.json`). Since Milestone 11 it covers the Creatures' views and urges,
  meetings, play, flight, betrayal, fights and their outcomes, the AI's teaching and confronting,
  their presentation and tools, and the version 10 → 11 migration (against `save-v10.json`). Since
  Milestone 12 it covers the protocol and its codecs, the loopback transport, a host and its players
  over the loopback (joining, refusals, intents and verdicts, snapshots, events, leaving), and the
  multiplayer launch options and readouts. Since Milestone 13 it covers every world section's trip
  into a replica, the sections' rates, change detection, interest, intents reaching players with
  their verdicts, replicas staying with the host, four players, and players coming back to their
  gods. Since Milestone 14 it covers the world's history, the end screen, the guide, the save
  list, eligible pantheons and the version 11 → 12 migration (against `save-v11.json`). The tests
  load the shipped `game/Data/*.json`; rule tests
  pin their own rules (`SettlementWorlds.Rules`, `DivineWorlds.Rules`, `DiplomacyWorlds.Rules`,
  `WeatherWorlds.Rules`, `DisasterWorlds.Rules`, `MigrationWorlds.Rules`)
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
  generation founds 8, 20, 40 or 48 by the world's size (ADR-027). Four gods share them since Milestone 9; more gods are more entries in
  `gods.json` (and god colours in `divine_view.json`), as the AI, belief, diplomacy and victory
  rules handle any number.
- A new Tier 3 individual is state in `WorldState` with its own system, like the notables
  (ADR-032). Promoting ordinary villagers into agents whose effects write back to the aggregates
  would need a new ADR.
