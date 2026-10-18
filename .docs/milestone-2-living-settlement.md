# Milestone 2 — Living Settlement Implementation

## Overview

Milestone 2 makes the starting settlement live on its own. Deliverables:

- An aggregate (statistical, Tier 1) population model for the settlement: population growth and
  decline, food production and consumption, housing capacity with autonomous house construction,
  and health. Starvation lowers health; health changes deaths and births.
- The settlement acts autonomously: the player never places houses. Villagers split into farmers
  and builders, start a house when the settlement gets crowded and stop when food runs short.
- The settlement model steps at about 1 Hz (once per simulated second), decoupled from rendering
  and from the 10 Hz tick.
- Visible representative citizens (Tier 2) near the camera: up to a few hundred figures drawn
  with one MultiMesh per settlement, each standing for several villagers. They walk, work in the
  fields and on the building site, and gather at the shrine. They are derived from the
  statistical state and are never canonical.
- New houses appear as housing grows; the house under construction and the fields are shown too.
- All state lives in `WorldState` and is saved as versioned JSON. `SaveVersion` goes from 1 to 2,
  with a migration from version 1.
- Data-driven balance (`game/Data/settlement_rules.json`) and presentation tuning
  (`game/Data/settlement_view.json`). Seeded randomness only.
- Debug overlay readouts for population, food, housing, health, jobs, construction and the
  simulation tier, plus hotkeys that drive the settlement.
- xUnit tests for the rules, the cadence, the Tier 2 derivation and the save migration.

Playable outcome: the settlement visibly lives and changes. It grows from 40 people in 12 houses
to several hundred, builds houses one at a time, and its villagers walk between homes, fields,
the building site and the shrine. A famine (driven with the debug hotkeys) lowers health, raises
deaths and the settlement recovers.

**Scope.** The roadmap row for Milestone 2 in `docs/GAME_SPEC.md` also lists several
settlements, founding and the gather and worship jobs. The brief for this milestone asks for one
living settlement, so those items are listed under Tasks → Future: founding and migration need
more than one settlement, worship needs belief (Milestone 3) and gathering needs the ecology of
Milestone 6. The code iterates over every settlement, so a second settlement needs no rework of
the model, the views or the saves.

**Plan pattern: Non-TDD (architecture first).** The architecture follows from ADR-004 and
ADR-006 and from this milestone's brief, so design drives implementation and the tests are
specified in the Test Plan and written with each part. The brief asks for fully autonomous
work, so the pattern could not be confirmed interactively. This is recorded as an explicit
assumption, as in Milestone 1.

**Decision record.** [ADR-008](../docs/ADR/ADR-008-settlement-step-and-representative-citizens.md)
amends ADR-004: Tier numbering, the 1 Hz settlement step, and representative citizens as the
Regional tier's derived, non-canonical presentation until Milestone 12.

**Status:** in progress.

## Architecture

### What changes, by project

| Project | Change |
| --- | --- |
| `Pantheon.Domain` | `SettlementState` gains `Health` and `ConstructionProgress`. `SettlementRules` is rewritten for the per-step model. |
| `Pantheon.Simulation` | `SettlementEconomy` (pure rule functions), the M2 `SettlementSystem` step, `IntervalSystem` (runs a system every N ticks), the rules validator, founding with full health. |
| `Pantheon.Persistence` | `CurrentSaveVersion = 2`, `SaveMigrations` (version 1 → 2), integrity checks for the new fields. |
| `Pantheon.Game` | `Population/` (Tier 2: tier selection, representative roster, routines, crowd smoothing), `Presentation/` (house, construction and field layout, `SettlementViewSettings`), readouts, session commands, launch options. |
| Godot client | Settlement view with houses, construction site and fields; citizens in a MultiMesh; hotkeys; content loading. |
| Content | `settlement_rules.json` rewritten, new `settlement_view.json`. `worldgen.json` is unchanged, so the generation settings saved inside every world keep their shape. |

### Tier 1: the statistical settlement step

`SettlementSystem` runs once every `StepIntervalTicks` ticks (10 ticks, so once per simulated
second at ×1, four times per real second at ×4). `IntervalSystem` wraps it and runs it on the
last tick of each interval: ticks 9, 19, 29 and so on, so that after `10 × n` ticks exactly `n`
steps have run. The scheduler still ticks at 10 Hz (ADR-006 is unchanged). Every rate in
`settlement_rules.json` is per step.

Each step, for each settlement in list order (all symbols per step):

```text
land        = land regions (not sea, fertility > 0) within FarmlandRadius regions (Chebyshev) of the settlement's region
capacity    = FarmersPerRegion × land.Count
maxYield    = FoodPerFarmerPerStep × land.MeanFertility × capacity

workers     = Population × WorkforceShare
wanted      = Housing < MaxHousing and (ConstructionProgress > 0 or Population ≥ ConstructionStartCrowding × Housing)
funded      = Food ≥ ConstructionFoodReserveSteps × consumption
builders    = wanted and funded ? min(workers × MaxBuilderShare, BuildersPerSite, WorkPerHouse − ConstructionProgress) : 0
farmers     = workers − builders

production  = capacity > 0 ? maxYield × (1 − e^(−farmers / capacity)) : 0      # diminishing returns
consumption = Population × FoodPerPersonPerStep
available   = Food + production
fed         = consumption > 0 ? min(1, available / consumption) : 1
Food        = clamp(available − consumption, 0, Housing × FoodStoragePerHousing)   # surplus beyond storage spoils

Health      = fed < 1 ? max(0, Health − StarvationHealthLossPerStep × (1 − fed))
                      : min(1, Health + HealthRecoveryPerStep)

births      = production ≥ consumption and Food ≥ Population × SurplusFoodPerPerson and Housing > Population
              ? min(Housing − Population, Poisson(Population × BirthChancePerPersonPerStep × Health)) : 0
deaths      = min(Population, Poisson(Population × (DeathChancePerPersonPerStep
                                         + IllHealthDeathChancePerPersonPerStep × (1 − Health)²)))
Population += births − deaths

ConstructionProgress += builders
if ConstructionProgress ≥ WorkPerHouse: Housing = min(MaxHousing, Housing + HousingPerHouse), ConstructionProgress = 0
```

Design notes:

- `Housing` stays the canonical capacity in people; a house shelters `HousingPerHouse` people,
  so the starting 60 housing is 12 houses. `ConstructionProgress` is the only new construction
  state: one house is built at a time.
- Farmland comes from the regions around the settlement, not only its own region, because the
  fields reach about 150 m from the centre (two 64 m regions). With diminishing returns the land
  sets a soft population limit that differs between sites.
- Health does not change labour. A prototype that made sick villagers work less turned a famine
  into a trap: at low health a settlement of any size could not feed itself and died out. With
  health acting only on births and deaths, a famine shrinks the population until the land feeds
  it again, and health then recovers.
- Births need production to cover consumption, so a healthy settlement stops growing at the
  land's limit instead of overshooting into a permanent famine.
- Builders are capped per site, so even a large settlement finishes a house about every ten
  simulated seconds and each house shows a visible building phase.
- The two random draws per settlement (births, then deaths) come from the shared
  `SimulationRandom`, in list order, so runs and save/load continuations stay deterministic.

Balance (the shipped step and values, measured headless on seed 1234: 24 land regions with mean
fertility 0.46; simulated time at ×1): the settlement starts its first house after about a
minute (tick 590) and finishes it 15 seconds later, then builds a house every 10 to 15 seconds
while crowded. It reaches 95% of its limit after 18 minutes and levels off at about 540 people
in 129 houses after about 25 minutes (about 90 seconds at ×16), healthy, with spare housing and
full stores. A poor site (seed 2: 14 land regions, fertility 0.39) levels off near 210.
Emptying the stores at the limit (F6) stops births for about 11 minutes while they refill; the
population falls to about 425 by ordinary deaths and climbs back. Arriving settlers beyond what
the land feeds (F7 at the limit: 541 → 811 people) eat the stores in about 6 minutes, then
starve for about 4 minutes: health falls to about 0.7, deaths bring the population back within
the land's limit, health is full again a minute after its lowest point, and births return once
the stores refill.

### Tier 2: representative citizens (ADR-008)

- Tier numbering: Tier 1 Statistical, Tier 2 Regional, Tier 3 Full (ADR-004's tiers).
- `TierSelector` promotes a settlement to Tier 2 when the camera comes within
  `RegionalTierEnterDistance` (450 m) of its centre and demotes it beyond
  `RegionalTierExitDistance` (550 m). The gap stops the tier from flickering at the boundary.
  Tier selection is client-local presentation state; Tier 1 always runs for every settlement.
- In Tier 2, `RepresentativeRoster` derives `N = min(Population, MaxRepresentatives)`
  representatives (300 at most). Each stands for `Population / N` people. Their roles follow the
  aggregate labour split of `SettlementEconomy.AllocateLabour`: builders, farmers and villagers
  (the inhabitants who do not work), with counts by largest remainder.
- Identities are stable: representative `i` keeps a hash rank, and the lowest ranks become
  builders, the next farmers and the rest villagers. When a count changes by one, one
  representative changes role. Homes and fields are chosen by rendezvous hashing, so a new house
  takes residents from the others without reshuffling everyone. Hashes mix the world seed, the
  settlement id and the representative index (no random stream is consumed).
- `CitizenRoutine` gives each representative a simple schedule: rest at home, walk to work (a
  spot in a field, around the building site, or chores near home for villagers), work, walk to a
  spot around the shrine, gather, walk home. The pose is a closed-form function of simulation
  time (`(Tick + Alpha) × 0.1 s`), so citizens freeze when paused and speed up with the time
  scale.
- `SettlementCrowd` keeps the roster in step with the aggregates and eases rerouted citizens
  (a new home, a new role) towards their new route at `CatchUpSpeed` instead of teleporting.
  Representatives born while the settlement is in Tier 2 step out of their front door; on
  promotion everyone appears in place.
- The client draws all of a settlement's representatives with one `MultiMeshInstance3D`
  (a body tinted per role through instance colours and a head), so a few hundred figures cost
  one draw call per mesh surface.
- Never canonical: Tier 2 reads `WorldState` and writes nothing. Its data is not saved; a load
  or new world rebuilds it. Because representatives have no effects of their own, demotion has
  nothing to fold back and totals are trivially conserved (ADR-004).

### Settlement layout (presentation data)

`SettlementLayout.ComputeSite` returns a `SettlementSite`:

- Houses: `ceil(Housing / HousingPerHouse)` complete houses on the golden-angle spiral around
  the shrine (Milestone 1's spiral, now counting houses rather than people), skipping slots on
  low ground. House `k` is always the `k`-th valid slot, so growth only appends.
- Construction: while `ConstructionProgress > 0`, the next slot shows a building site whose
  walls rise with `ConstructionProgress / WorkPerHouse`.
- Fields: plots on a second spiral, kept outside the disc of houses plus a clearance, on dry,
  gentle ground, tilted to the terrain, `ceil(houses / HousesPerField)` of them up to
  `MaxFields`. As the village grows over its inner fields, new fields appear further out.
- Tuning (spacing, sizes, clearances, crop and role colours, Tier 2 distances and routine
  durations) is in `game/Data/settlement_view.json`, parsed strictly by
  `SettlementViewSettings`.

### Saves

- `WorldSaveSerializer.CurrentSaveVersion` is 2; `MinimumSupportedSaveVersion` stays 1.
- Loading reads `SaveVersion` first. A current save deserializes directly. An older save is
  parsed into a `JsonObject`, upgraded one version at a time by `SaveMigrations`, then
  deserialized with the same strict options, so a migrated save gets exactly the checks of a
  current one.
- Version 1 → 2 adds `"Health": 1` and `"ConstructionProgress": 0` to every settlement.
  Version 1 worlds were all healthy (Milestone 1 had no health), and no house was being built.
- `SaveIntegrity` also checks `0 ≤ Health ≤ 1` and `ConstructionProgress ≥ 0`.
- A version 1 save written by the Milestone 1 build is kept as a test fixture
  (`src/Pantheon.Tests/Persistence/Fixtures/save-v1.json`).

### Debug tools

- F1 panel, per settlement:
  - population / housing, houses, health
  - food stored / storage, the net change per second, production and consumption
  - jobs (farming, building, not working) and the farmland (land regions, mean fertility)
  - expected births and deaths per second, the house under construction, time to the next step
  - the simulation tier, and in Tier 2 the number of representatives and the people each stands for
  - warnings when the settlement is starving, construction waits for food, or births are blocked
- `,`: pauses and runs ticks up to and including the next settlement step.
- F6: spoils every settlement's food stores (sets food to 0).
- F7: settlers arrive: every settlement grows by half (at least 10 people).
- Launch options: `--advance-ticks=<n>` runs n ticks before the first frame, and
  `--camera-distance=<m>` sets the starting camera distance. Together they give reproducible
  captures of a grown settlement up close.

### Coding rules (AGENTS.md)

| Rule | How Milestone 2 meets it | Checked by |
| --- | --- | --- |
| Build incrementally; every milestone leaves the game playable | Each commit builds, passes the tests and launches; the settlement grows on its own. | Windowed runs on both renderers |
| Simulation and presentation separate; core testable without rendering | The rules, Tier 2 derivation and layout are engine-agnostic C#; the client only builds nodes. | `ArchitectureRulesTests`, headless tests |
| `WorldState` authoritative | The settlement step, the session commands (including the `,` F6 F7 debug commands) and the save migration are the only writers. Tier 2 reads state and is never saved. | `RepresentativeRosterTests` (world unchanged), save tests |
| Data-driven content | Balance in `settlement_rules.json`; layout, colours, Tier 2 distances and routines in `settlement_view.json`. | `ContentTests` |
| Don't optimise before profiling | Simple per-frame pose evaluation and per-instance MultiMesh writes; the frame cost is measured and recorded. | Verification results |
| No infrastructure without need | No new tools, services or packages. | Review |
| macOS, Windows and Linux | Plain .NET and Godot APIs only. | Review |
| Seeded randomness | Births and deaths draw from the saved `SimulationRandom`; Tier 2 uses seed-mixed hashes only. | `ArchitectureRulesTests`, determinism tests |
| Tests for important rules | Production, consumption, storage, health, births, deaths, construction, cadence, migration. | xUnit suite |
| Document architecture changes; never silently replace a decision | ADR-008 amends ADR-004; `docs/ARCHITECTURE.md`, the README and this plan change with the code. | Review |
| Gameplay systems over polish; emergent interactions | Growth, construction, famine and recovery come from rules interacting through food, housing and health. No scripted events. | Review |
| Debug tools early | Overlay readouts, `,` step, F6 famine, F7 settlers, launch options. | `DebugReadoutTests`, `SessionCommandsTests` |

## Mermaid Diagrams

New and changed parts (existing references unchanged):

```mermaid
graph TD
    subgraph Client["game/Scripts (Pantheon.Client)"]
        Root["GameRoot<br/>hotkeys , F6 F7"]
        SP["SettlementPresenter"]
        SV["SettlementView<br/>shrine, houses, building site, fields, label"]
        CV["CitizenView<br/>one MultiMesh per settlement"]
    end
    subgraph Game["Pantheon.Game"]
        Cmd["SessionCommands<br/>StepSettlement, SpoilFood, AddSettlers"]
        Layout["SettlementLayout → SettlementSite"]
        Crowd["Population/SettlementCrowd<br/>TierSelector, RepresentativeRoster, CitizenRoutine"]
        Readout["Diagnostics/DebugReadout"]
        ViewSettings["SettlementViewSettings<br/>(settlement_view.json)"]
    end
    subgraph Sim["Pantheon.Simulation"]
        Engine["SimulationEngine"]
        Interval["IntervalSystem (every 10 ticks)"]
        System["SettlementSystem"]
        Economy["SettlementEconomy<br/>survey, labour, food, health, births, deaths"]
    end
    subgraph Persist["Pantheon.Persistence"]
        Serializer["WorldSaveSerializer (v2)"]
        Migrations["SaveMigrations (v1 → v2)"]
    end
    State["WorldState / SettlementState<br/>Population, Food, Housing, Health, ConstructionProgress"]

    Root --> Cmd --> State
    Root --> SP --> SV --> Layout
    SV --> CV --> Crowd
    Crowd --> Layout
    Crowd --> Economy
    Layout --> ViewSettings
    Crowd --> ViewSettings
    Readout --> Economy
    Engine --> Interval --> System --> Economy
    System --> State
    Crowd -. "reads only" .-> State
    Serializer --> Migrations
    Serializer --> State
```

The settlement step inside the tick loop:

```mermaid
sequenceDiagram
    participant Sched as SimulationScheduler (10 Hz × time scale)
    participant Engine as SimulationEngine
    participant Interval as IntervalSystem
    participant System as SettlementSystem
    participant Economy as SettlementEconomy
    participant State as WorldState

    loop every tick
        Sched->>Engine: Step(world)
        Engine->>Interval: Update(tick context)
        alt (Tick + 1) mod StepIntervalTicks == 0
            Interval->>System: Update(tick context)
            loop each settlement, in list order
                System->>Economy: SurveyFarmland, AllocateLabour, production, consumption
                System->>State: Food, Health
                System->>State: births, deaths (Poisson draws from the saved RNG)
                System->>State: ConstructionProgress, Housing
            end
        end
        Engine->>State: Random = rng state, Tick++
    end
```

One step's control flow:

```mermaid
flowchart TD
    A["Survey farmland<br/>land regions within FarmlandRadius"] --> B["Labour: workers = Population × WorkforceShare"]
    B --> C{"House wanted?<br/>crowded or under way, below MaxHousing"}
    C -- no --> F["farmers = workers"]
    C -- yes --> D{"Stores ≥ reserve<br/>(ConstructionFoodReserveSteps of food)?"}
    D -- no --> F
    D -- yes --> E["builders = min(share, BuildersPerSite, remaining work)<br/>farmers = workers − builders"]
    E --> G
    F --> G["production with diminishing returns<br/>consumption = Population × FoodPerPersonPerStep"]
    G --> H{"Food + production ≥ consumption?"}
    H -- yes --> I["Food = min(rest, storage)<br/>Health recovers"]
    H -- no --> J["Food = 0, fed share < 1<br/>Health falls with the unfed share"]
    I --> K["Births: free housing, surplus and stores<br/>Poisson, scaled by Health"]
    J --> K
    K --> L["Deaths: Poisson, baseline + ill health × (1 − Health)²"]
    L --> M["ConstructionProgress += builders<br/>house complete → Housing += HousingPerHouse"]
```

Frame flow with Tier 2:

```mermaid
sequenceDiagram
    participant Root as GameRoot (_Process)
    participant Session as GameSession
    participant SP as SettlementPresenter
    participant SV as SettlementView
    participant Crowd as SettlementCrowd
    participant MM as Citizen MultiMesh

    Root->>Session: Update(delta) (ticks; a settlement step every 10th)
    Root->>SP: Sync(world)
    SP->>SV: Sync(settlement) — on a new tick: outlook, site; houses, building site and fields when they change
    Root->>SP: UpdateCitizens(simulation seconds, camera position)
    SP->>SV: UpdateCitizens(...)
    SV->>Crowd: SelectTier(camera distance)
    alt Tier 2 (Regional)
        SV->>Crowd: Refresh(settlement, labour, site) — rebuild the roster when its inputs change
        SV->>Crowd: Advance(simulation seconds, terrain) — routine poses, eased reroutes
        Crowd-->>SV: poses
        SV->>MM: instance transforms (and role colours after a rebuild)
    else Tier 1 (Statistical)
        SV->>MM: no instances
    end
    Root->>Root: DebugSnapshot with crowd statuses → DebugReadout
```

Tier selection:

```mermaid
stateDiagram-v2
    [*] --> Statistical
    Statistical --> Regional: camera distance ≤ RegionalTierEnterDistance (450 m)
    Regional --> Statistical: camera distance > RegionalTierExitDistance (550 m)
    Regional --> Statistical: world replaced (reset)
    note right of Regional
        Tier 1 keeps running for every settlement.
        Tier 2 only adds derived representatives.
        Full (Tier 3) is Milestone 12.
    end note
```

Loading a save with migration:

```mermaid
sequenceDiagram
    participant Session as GameSession
    participant Save as WorldSaveSerializer
    participant Mig as SaveMigrations
    participant Int as SaveIntegrity

    Session->>Save: LoadFromFile(path)
    Save->>Save: ReadSaveVersion(json)
    alt version == 2 (current)
        Save->>Save: deserialize SaveGame (strict)
    else version == 1
        Save->>Mig: Upgrade(JsonObject, 1)
        Mig->>Mig: v1 → v2: every settlement gets Health 1, ConstructionProgress 0; SaveVersion = 2
        Mig-->>Save: upgraded JsonObject
        Save->>Save: deserialize SaveGame (strict)
    else version > 2 or < 1
        Save-->>Session: SaveFormatException
    end
    Save->>Int: Validate(world) (Health in 0..1, ConstructionProgress ≥ 0, ...)
    Save-->>Session: WorldState
```

## Code Plan

Signatures only; all public members carry XML documentation comments.

### Pantheon.Domain

```csharp
sealed class SettlementState { ...M1 members...;
    required float Health { get; set; }                  // 0 (dying) .. 1 (healthy)
    required float ConstructionProgress { get; set; } }  // work units on the next house; 0 = none under way
sealed record SettlementRules {
    int StepIntervalTicks;                // 10 → one step per simulated second
    float WorkforceShare;                 // share of inhabitants who work
    int FarmlandRadius;                   // Chebyshev radius in regions
    float FarmersPerRegion, FoodPerFarmerPerStep, FoodPerPersonPerStep, FoodStoragePerHousing, SurplusFoodPerPerson;
    float StarvationHealthLossPerStep, HealthRecoveryPerStep;
    double BirthChancePerPersonPerStep, DeathChancePerPersonPerStep, IllHealthDeathChancePerPersonPerStep;
    int HousingPerHouse; float ConstructionStartCrowding, MaxBuilderShare, BuildersPerSite, WorkPerHouse,
    ConstructionFoodReserveSteps; int MaxHousing; }
```

### Pantheon.Simulation

```csharp
sealed class IntervalSystem : ISimulationSystem {            // Systems/
    IntervalSystem(ISimulationSystem inner, int intervalTicks);
    int IntervalTicks { get; }
    static bool IsDue(long tick, int intervalTicks);           // (tick + 1) % interval == 0
    static int TicksUntilDue(long tick, int intervalTicks);    // ticks to run so the inner system runs once: interval − tick % interval
    void Update(TickContext context); }
readonly record struct FarmlandSurvey(int LandRegions, float MeanFertility);           // Settlements/
readonly record struct LabourAllocation(float Workers, float Farmers, float Builders) { float NotWorking(int population); }
enum BirthBlocker { None, NoPeople, NoFreeHousing, NoFoodSurplus }
sealed record SettlementOutlook { FarmlandSurvey Land; LabourAllocation Labour; float Production, Consumption, Storage,
    FedShare, FoodAfter, HealthAfter; bool ConstructionWanted, ConstructionFunded; BirthBlocker BirthBlocker;
    double BirthChance, DeathChance, ExpectedBirths, ExpectedDeaths;
    float NetFood; bool IsStarving; bool ConstructionWaitsForFood; }                   // the next step, before its random draws
static class SettlementEconomy {
    FarmlandSurvey SurveyFarmland(RegionGrid grid, int regionId, int radius); bool IsFarmland(RegionState region);
    float ComputeConsumption(SettlementRules rules, int population);
    float ComputeStorage(SettlementRules rules, int housing);
    int ComputeHouseCount(SettlementRules rules, int housing);                          // ceil(housing / HousingPerHouse)
    bool WantsConstruction(SettlementRules rules, SettlementState settlement);
    bool CanFundConstruction(SettlementRules rules, SettlementState settlement);
    LabourAllocation AllocateLabour(SettlementRules rules, SettlementState settlement);
    float ComputeProduction(SettlementRules rules, float farmers, FarmlandSurvey land);
    float ComputeFedShare(float available, float consumption);
    float ComputeHealth(SettlementRules rules, float health, float fedShare);
    double ComputeBirthChance(SettlementRules rules, float health);
    double ComputeDeathChance(SettlementRules rules, float health);
    BirthBlocker FindBirthBlocker(SettlementRules rules, SettlementState settlement, float production, float consumption, float foodAfter);
    SettlementOutlook Assess(SettlementRules rules, SettlementState settlement, RegionGrid regions); }
sealed class SettlementSystem : ISimulationSystem { SettlementSystem(SettlementRules rules); void Update(TickContext context); }
static class SettlementRulesValidator { void Validate(SettlementRules rules); }        // lists every error
sealed class SimulationEngine { SimulationEngine(SettlementRules rules); }             // [IntervalSystem(SettlementSystem, StepIntervalTicks)]
static class SettlementFounder { const float InitialHealth = 1f; ... }                  // new settlements are healthy, nothing under way
```

`SettlementSystem.Update` (private helpers, each small): `Step(settlement, regions, random)` →
`ApplyFood` → `ApplyHealth` → `DrawBirths` / `DrawDeaths` → `ApplyConstruction`, as in
Architecture → Tier 1.

### Pantheon.Persistence

```csharp
static class WorldSaveSerializer { const int CurrentSaveVersion = 2; const int MinimumSupportedSaveVersion = 1; ... }
internal static class SaveMigrations {
    JsonObject Upgrade(string json, int fromVersion);   // parse, then apply each step up to CurrentSaveVersion
    void UpgradeFrom1(JsonObject root); }               // settlements: Health = 1, ConstructionProgress = 0
internal static class SaveIntegrity { ... Health in 0..1, ConstructionProgress ≥ 0 ... }
```

### Pantheon.Game

```csharp
// Population/ (Tier 2, derived, never saved)
enum SimulationTier { Statistical, Regional, Full }                     // Tier 1, 2, 3 (ADR-004, ADR-008)
enum CitizenRole { Farmer, Builder, Villager }
enum CitizenActivity { AtHome, Walking, Working, Gathering }
sealed record CitizenSettings { int MaxRepresentatives; float RegionalTierEnterDistance, RegionalTierExitDistance,
    WalkSpeed, CatchUpSpeed, RestSeconds, WorkSeconds, GatherSeconds, GatherRadiusMin, GatherRadiusMax, WanderRadius,
    WorkSpread, BuilderRingRadius; RgbColor FarmerColor, BuilderColor, VillagerColor; void Validate(); }
readonly record struct RepresentativeCitizen(int Index, CitizenRole Role, Vector2 Home, Vector2 Work, Vector2 Gathering, float Phase);
readonly record struct CitizenPose(Vector3 Position, float Yaw, CitizenActivity Activity);
sealed record CrowdStatus(int SettlementId, SimulationTier Tier, int Representatives, float PeoplePerRepresentative,
    float CameraDistance, float RegionalTierEnterDistance);
static class TierSelector { SimulationTier Select(SimulationTier current, float cameraDistance, CitizenSettings settings); }
static class RepresentativeRoster {
    int ComputeCount(int population, int maxRepresentatives);
    (int Builders, int Farmers, int Villagers) SplitRoles(int count, int population, LabourAllocation labour);  // largest remainder
    IReadOnlyList<RepresentativeCitizen> Build(WorldSeed seed, SettlementState settlement, LabourAllocation labour,
        SettlementSite site, CitizenSettings settings); }
static class CitizenRoutine { float CycleSeconds(in RepresentativeCitizen c, CitizenSettings s);
    (Vector2 Ground, float Yaw, CitizenActivity Activity) Evaluate(in RepresentativeCitizen c, double seconds, CitizenSettings s); }
sealed class SettlementCrowd {                                                       // one per settlement view
    SettlementCrowd(WorldSeed seed, int settlementId, CitizenSettings settings);
    SimulationTier Tier; IReadOnlyList<RepresentativeCitizen> Roster; IReadOnlyList<CitizenPose> Poses; bool RosterChanged;
    void SelectTier(float cameraDistance);
    void Refresh(SettlementState settlement, LabourAllocation labour, SettlementSite site);
    void Advance(double simulationSeconds, TerrainField terrain);
    CrowdStatus GetStatus(int population, float cameraDistance); }

// Presentation/
static class PresentationHash { float Unit(ulong a, ulong b = 0, ulong c = 0, ulong d = 0); }      // SplitMix64-mixed, 0..1
sealed record SettlementLayoutSettings { float ShrineClearance, HouseSpacing, MinimumGroundHeight, DoorDistance,
    HousesPerField, FieldSize, FieldSpacing, FieldClearance, FieldMaxSlope; int MaxFields; IReadOnlyList<RgbColor> CropColors; void Validate(); }
sealed record SettlementViewSettings { SettlementLayoutSettings Layout; CitizenSettings Citizens;
    const string FileName = "settlement_view.json"; static SettlementViewSettings Parse(string json); }
readonly record struct ConstructionPlacement(Vector3 Position, float Yaw, float Progress);       // progress 0..1
readonly record struct FieldPlacement(Vector3 Position, float Yaw, Vector3 Normal, float Variant);
sealed record SettlementSite(Vector3 Center, IReadOnlyList<HutPlacement> Houses, ConstructionPlacement? Construction,
    IReadOnlyList<FieldPlacement> Fields, float HouseDiscRadius) { Vector2 DoorOf(int house, float doorDistance); }
static class SettlementLayout {
    int ComputeHouseCount(SettlementState s, SettlementRules rules);                 // ceil(Housing / HousingPerHouse)
    int ComputeFieldCount(int houses, SettlementLayoutSettings settings);
    SettlementSite ComputeSite(SettlementState s, SettlementRules rules, TerrainField terrain, SettlementLayoutSettings settings); }

// Diagnostics/
sealed record DebugSnapshot { ...; IReadOnlyList<CrowdStatus> Crowds { get; init; } = []; }
static class DebugReadout { ... the step cadence line ...;
    const string DebugHints = "[F1] overlay  [B] regions  [F3] debug map  [.] step tick  [,] step settlement  [F6] spoil food  [F7] settlers";
    string Summarize(WorldState world, SettlementRules rules); }                 // one line for the log after a capture
internal static class SettlementReadout { void Append(List<string> lines, DebugSnapshot snapshot, SettlementState s); }
                                                                                  // the settlement lines of Architecture → Debug tools

// Session and launch
sealed class GameSession { double SimulationSeconds { get; }  // (Tick + Alpha) × TickDuration
    void RunTicks(int ticks); int StepToNextSettlementStep(); }
static class SessionCommands { string StepSettlement(GameSession); string AdvanceTicks(GameSession, int ticks);
    string SpoilFood(GameSession); string AddSettlers(GameSession); const float SettlerShare = 0.5f; const int MinimumSettlers = 10; }
sealed record LaunchOptions { int? AdvanceTicks; float? CameraDistance; }        // --advance-ticks, --camera-distance
```

### Godot client (`game/Scripts`)

```csharp
sealed class SettlementPresenter { Build(world, terrain); Sync(world); UpdateCitizens(double seconds, Vector3 camera);
    IReadOnlyList<CrowdStatus> CrowdStatuses; }
sealed class SettlementView { Sync(SettlementState s); UpdateCitizens(double seconds, Vector3 camera); CrowdStatus Status; }
                               // shrine, label, house MultiMeshes, building site, field MultiMesh
sealed class CitizenView { Show(IReadOnlyList<CitizenPose> poses, IReadOnlyList<RepresentativeCitizen> roster, bool recolour); Hide(); }
sealed class SettlementAssets { ...; Mesh ScaffoldPole, FieldPlot, CitizenFigure; }   // citizen = body (instance colour) + head
partial class GameRoot { hotkeys ',' F6 F7; --advance-ticks, --camera-distance; crowd statuses in the snapshot }
static class GodotContentLoader { SettlementViewSettings LoadSettlementViewSettings(); }
static class InputActions { SimStepSettlement (','), DebugSpoilFood (F6), DebugAddSettlers (F7) }
```

Materials: `scaffold.tres` (raw timber), `field.tres` (vertex colour as albedo, per-field crop
colour), `citizen_body.tres` (vertex colour as albedo, per-role colour), `citizen_head.tres`.

## Test Plan

Unit and integration tests in `src/Pantheon.Tests` (headless, no Godot).

| Area | Tests | Verifies |
| --- | --- | --- |
| Settlement rules | `SettlementEconomyTests` | Farmland survey counts land regions within the radius, skips sea, clips at the grid edge. Production grows with farmers but never exceeds the land's yield, is about linear for few farmers and zero without land. Starvation lowers health in proportion to the unfed share; fed villagers recover, capped at 1. Death chance rises and birth chance falls as health falls. Builders only when crowded (or under way) and funded, capped by share, site and remaining work. |
| Settlement step | `SettlementSystemTests` | A starving population loses health and dies faster than a healthy one. A fed population recovers health. A food surplus grows the population up to housing and never past it. Births need a surplus and stores. A crowded settlement starts building; an uncrowded one does not. Construction completes a house (+`HousingPerHouse`, progress reset), pauses while food is below the reserve, and stops at `MaxHousing`. Food is capped by storage. The starting settlement on a generated island grows, builds houses and stays healthy. Validator errors list every bad rule. |
| Cadence | `IntervalSystemTests` | The settlement step runs on the last tick of each interval: nothing for 9 ticks, one step on the 10th; `TicksUntilDue` matches. |
| Determinism | `DeterminismTests` (updated) | Same seed and ticks give the same state, also across a save and load, with the per-step rules. |
| Saves | `SaveLoadTests` (updated), `SaveMigrationTests` | `CurrentSaveVersion` is 2. The Milestone 1 fixture (version 1) loads with health 1, no construction and all other values intact, re-saves as version 2 and keeps simulating deterministically. A version 2 save without `Health` is rejected. Out-of-range health or negative progress is rejected. |
| Tier 2 | `RepresentativeRosterTests`, `CitizenRoutineTests`, `TierSelectorTests`, `SettlementCrowdTests` | Count is the population up to the cap. Role counts follow the labour split by largest remainder. Same inputs give the same roster. One more person changes at most one role and keeps every other home. Building rosters and poses never changes the world (JSON before and after). Homes are house doors, farmers work in fields, builders at the building site. The routine visits home, work and the shrine, walks at walking speed and stays on the ground. Tier hysteresis. Crowd: Tier 1 has no poses, promotion places everyone in place, a newcomer starts at its door, a rerouted citizen moves no faster than the catch-up speed. |
| Layout | `SettlementLayoutTests` | House count from housing; houses are a stable prefix as housing grows; the building site uses the next slot with its progress; fields lie outside the house disc on dry, gentle land; deterministic. |
| Content | `ContentTests` (updated) | Shipped `settlement_rules.json` and `settlement_view.json` parse strictly; unknown members and out-of-range values are rejected. |
| Readouts | `DebugReadoutTests` (updated) | Population / housing, houses, health, food and storage, jobs, construction, tier lines; starving and blocked-construction warnings. |
| Commands | `SessionCommandsTests` (updated) | `,` runs to the next step boundary and pauses; F6 empties the stores; F7 adds half the population (at least 10). |
| Session and launch | `GameSessionTests`, `LaunchOptionsTests` (updated) | `SimulationSeconds`, `RunTicks`; `--advance-ticks` and `--camera-distance` parse, bad values warn. |
| Architecture | `ArchitectureRulesTests` (unchanged) | Layering, no Godot, no unseeded randomness or clock in core assemblies. |

Client verification (Godot 4.7.2 .NET on the VM, Forward+ on lavapipe):

- `dotnet build Pantheon.sln` (no warnings) and `dotnet test Pantheon.sln`.
- Headless import and headless runs; the Milestone 1 save fixture loads in the game.
- Windowed captures on Terrain3D and the fallback renderer: the starting settlement, a grown
  settlement (`--advance-ticks`), citizens up close (`--camera-distance`), a building site, and a
  famine driven with F7 and F6 showing health falling in the overlay.
- Tier switching by zooming out past 550 m and back within 450 m.
- The frame cost of the citizen update, measured and recorded here.

## Tasks

### Completed

- [x] Read AGENTS.md, the specification, the architecture and ADR-001…ADR-007.
- [x] Prototype the Tier 1 balance numerically (growth, equilibrium, famine and recovery, poor sites).
- [x] Write a version 1 save with the Milestone 1 build for the migration test.
- [x] Write this plan.
- [x] ADR-008 (amends ADR-004) and the ADR index.
- [x] Saves: `Health` and `ConstructionProgress` on `SettlementState`, `SaveVersion` 2, `SaveMigrations`, integrity checks, migration tests.
- [x] Tier 1: `SettlementRules`, `settlement_rules.json`, `SettlementEconomy`, `SettlementSystem`, `IntervalSystem`, validator, rule and cadence tests.
- [x] Readouts and drivers: settlement readout lines, `,` F6 F7, input map, launch options, tests.
- [x] Measure the shipped balance headless (Architecture → Tier 1).

### In Progress

- [ ] Layout and views: houses from housing, building site, fields, `settlement_view.json`, client views and materials.

### Future (this milestone, in order)

- [ ] Tier 2: `Pantheon.Game.Population`, `CitizenView`, tier readout, tests.
- [ ] Docs: `docs/ARCHITECTURE.md` (+ diagrams), README, AGENTS.md (current tools), `src/Pantheon.Domain/Population/README.md`, ADR-004 status line.
- [ ] Verify in Godot on both renderers; record the results here.
- [ ] Open the pull request against `cursor/milestone-1-foundation-e33e`.

### Future (later milestones)

- [ ] Several settlements, founding and migration (roadmap Milestone 2 items outside this brief).
- [ ] Gather (with ecology, Milestone 6) and worship (with belief, Milestone 3) jobs.
- [ ] Houses that decay when empty; abandoned settlements.
- [ ] A farmland layer on the F3 region debug map.
- [ ] Tier 3 (full) agents with promotion and demotion that write back (Milestone 12, amends ADR-008).

## Relevant Files

- `.docs/milestone-2-living-settlement.md`: this plan.
- `docs/ADR/ADR-008-settlement-step-and-representative-citizens.md`, `docs/ADR/README.md`,
  `docs/ADR/ADR-004-statistical-population-simulation-with-tiering.md` (status line only).
- `docs/ARCHITECTURE.md`, `README.md`, `AGENTS.md`, `src/Pantheon.Domain/Population/README.md`.
- `src/Pantheon.Domain/Settlements/SettlementState.cs`, `SettlementRules.cs`.
- `src/Pantheon.Simulation/Settlements/` (`SettlementEconomy`, `SettlementSystem`,
  `SettlementRulesValidator`, `FarmlandSurvey`, `LabourAllocation`, `SettlementOutlook`),
  `Systems/IntervalSystem.cs`, `Systems/SimulationEngine.cs`, `WorldGen/SettlementFounder.cs`.
- `src/Pantheon.Persistence/WorldSaveSerializer.cs`, `SaveMigrations.cs`, `SaveIntegrity.cs`.
- `src/Pantheon.Game/Population/**`, `Presentation/` (`SettlementLayout`, `SettlementSite`,
  `SettlementViewSettings`, `SettlementLayoutSettings`, `PresentationHash`, placements),
  `Diagnostics/DebugReadout.cs`, `DebugSnapshot.cs`, `SessionCommands.cs`, `GameSession.cs`,
  `Launch/LaunchOptions.cs`.
- `src/Pantheon.Tests/**` (new and updated tests, `Persistence/Fixtures/save-v1.json`).
- `game/Data/settlement_rules.json`, `game/Data/settlement_view.json`, `game/project.godot`,
  `game/Materials/*.tres`, `game/Scripts/Bootstrap/*`, `game/Scripts/Presentation/Settlements/*`,
  `game/Scripts/InputActions.cs`.
