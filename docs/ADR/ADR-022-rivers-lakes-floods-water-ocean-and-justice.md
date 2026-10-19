# ADR-022: Rivers, lakes, floods and drinking water, with the Ocean and Justice domains

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-013](ADR-013-weather-cells-and-soil-water.md) (weather and soil water),
  [ADR-014](ADR-014-disasters-as-processes.md) (disasters as processes) and
  [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (domains)

## Context

Milestone 15 left two of §12's thirteen domains out, Ocean and Justice. One waited for water
bodies and the other for laws. Water appears across the specification too:

- **The world (§5):** a region holds water, and "river valley" is one of the biomes.
- **Procedural generation (§93):** river candidates.
- **Systemic consequences (§15):** rain raises the soil's water, then the river, and the river's
  rise raises the flood risk downstream.
- **Ecology (§18):** water availability.
- **Disasters (§19):** floods.
- **Settlement state (§30):** water.
- **The MVP miracles (§110):** Water, the one of the nine still missing.
- **Miracles (§14):** "create water" as a minor miracle.

So the world needs rivers and lakes that matter to the simulation, are drawn on both terrain
renderers, and keep old saves loadable.

## Decision

### Rivers and lakes are cut into the land at generation

- **Hydrology settings:** `worldgen.json` gains an optional `Hydrology` block. `TerrainGenerator`
  routes water over a coarse grid of `CellSize`-metre cells after the heightfield is made, then
  classifies the biomes.
- **Flow:** a priority flood (Barnes et al.) from the sea and the map's edge fills every hollow
  to the level it spills at, so each cell drains towards the sea. Wet land accumulates down the
  drainage tree.
- **Lakes:** hollows deeper than `MinLakeDepth` over at least `MinLakeArea` become lakes, filling
  their whole basin to the spill level.
- **Rivers:** a river runs where at least `RiverCatchment` square metres of wet land drain, and up
  from there to its headwaters (`HeadwaterCatchment`). The `MaxMainRivers` longest that reach the
  sea are kept first: on a small island the largest basins are not always the longest. Then come
  tributaries that join a river already found, up to `MaxRivers`.
- **Carving:**
  - A river's course is smoothed. Its width and depth grow with the water it carries.
  - Its surface follows the valley floor down, never rising towards the mouth, and lies below
    both banks.
  - A rounded channel and sloping banks are cut into the heightfield. The ground is only ever
    lowered, so the order of the cuts does not matter.
- **Not saved:** the result, `HydrologyMap`, is derived like the terrain itself. It holds the
  water surface for each height sample, the river courses and the lakes.
- **Old worlds:** a world without `Hydrology` (every world saved before version 14) regenerates
  its land unchanged.

### The world state records what the simulation needs

- **Water state:** `WorldState.Water` lists the rivers and lakes.
  - A river has its name, its regions from source to mouth and a level that changes.
  - A lake has its name, its regions and its surface.
- **Regions:** each region gains `FreshWater`, the share of its ground under a river or lake,
  fixed at generation, and `Flood`, which changes.
- **Settlements:** each gains `Water`, the stock in its wells and cisterns.
- **Saves:** version 14. Migrating from version 13 adds no rivers or lakes, no fresh water and no
  flood, and fills every settlement's wells to the storage that shipped (six per unit of
  housing).

### Rivers rise and flood

- **Levels:** a new `WaterSystem` runs at 1 Hz between the weather and the disasters. A river's
  level moves towards `BaseLevel + SoilLevelGain × (wetness above SoilThreshold) +
  RainLevelGain × rain`. Wetness is the mean soil water along the river and beside it; rain is
  the mean rain falling on it.
- **Floods:** above `FloodLevel` the river overflows. The flood is deepest at the mouth and
  shallowest at the source. Land beside the river that lies less than `FloodBankHeight` above it
  floods half as deep.
- **What a flood does:** it soaks the soil, puts out fires and leaves silt as blessing.
  - Flooded farmland yields less (`FloodYieldLoss`, in `settlement_rules.json`).
  - A flood in a settlement's own region drowns houses, people and stores and frightens the
    survivors. This goes through the same damage as earthquakes and fires (`DisasterDamage`),
    so Shields and the Protection domain lessen it.
- **Drainage:** floods drain at `FloodRecedePerSecond`.
- **Irrigation:** rivers and lakes keep the soil beside them moist.
- **Randomness:** like the disasters, the system draws from a stream derived from the seed and
  the tick.
- **Events:** `FloodStarted` (a river over its banks, or a god's wave) and `SettlementDamaged`
  with the new `DisasterKind.Flood`. Big floods enter the history as "The Flood of …".
- **Danger:** a flood counts towards a settlement's danger, so the belief model's worry, the alerts
  and the danger overlay see it.

### People drink

- **Drawing water:** each step a settlement draws `WaterGatherFactor` times what it drinks, times
  its access to water:
  - a river or lake in its region or beside it;
  - plus deep wells (`WellFloor`);
  - plus shallow wells that dry up with its soil.
- **Thirst:** without water, health falls by `ThirstHealthLossPerStep` times the share left
  thirsty, and `ThirstStarted` is raised.
- **Belief:** thirst counts as a water need, alongside the drought, so Rain and Water earn
  gratitude from the thirsty.
- **Founding:** new worlds prefer sites beside water (`WaterPreference`) and never found a village
  in a river. Houses and fields keep clear of the water.

### Three miracles

| Miracle | Domain | Kind | Effects | What it does |
| --- | --- | --- | --- | --- |
| Water | Ocean | General: §110's ninth MVP miracle, on key 0 | `Water` and `Soak` | Fills the settlement's wells and soaks the fields around it |
| Tidal Wave | Ocean | Signature | `Flood` | Floods the land within its radius that lies less than 12 m above the sea, the lowest deepest |
| Judgement | Justice | Signature | `Judge` | In each settlement it reaches, strikes only the followers of gods who wronged the caster: some die, and the rest doubt that god |

A god has wronged the caster if they are at war, or if the caster bears it a grudge of at least
`JusticeGuiltGrudge`.

### Ocean and Justice

- **Ocean:** the god's villages fish the sea, rivers and lakes within their farmland, gaining food
  for each water region up to `OceanMaxWaterRegions`.
- **Justice:** the god's villages keep its law. Their wish to leave fades
  (`JusticeCalmPerStep`), and doubt about the god fades (`JusticeDoubtFadePerStep`).
- **AI gods:** the Zealot now takes up Justice first and the Isolationist Ocean first. The shipped
  pantheon's personalities (Tyrant, Guardian and Trickster) are unchanged.
- **AI valuation:** the AI values Water by need alone: it is not a courting gift. Tidal Wave
  counts as a disaster aimed at rivals' low villages, and Judgement by the guilty share of a
  village.

### Presentation

- **Water surfaces:** `WaterSurfaceMesh` builds quads over the heightfield for the rivers and
  lakes, and sheets of water over flooded regions. A dry corner meets the ground, so the edge
  follows the bank. The client draws them with `fresh_water.gdshader`, using the vertex colour
  as depth, on both terrain renderers.
- **River beds:** beds fade from the land's colour to the sea floor's with depth.
- **Map views:** the Weather overlay shows fresh water, and the Danger overlay shows floods. The F3
  map gains Fresh water and Flood layers.
- **Readouts:** the F1 panel lists the rivers' levels and the lakes, and shows a settlement's
  water, access and river.
- **Debug:** L floods the nearest river (or sends a wave), and `--disaster=flood` does it at
  start.
- **Shared sessions:** rivers travel as a `Water` section, and a region's flood with its other
  changing values.

## Consequences

- **Balance:** measured over 40 seeds against Milestone 15's build (see the plan). The AI uses
  Water only in drought. Floods happen a few times an hour in wet weather. Thirst is rare: it
  comes only in long droughts away from water.
- **Islands:** rivers on a 2 km island run 200–800 m. Larger worlds will have longer ones.
- **Determinism:** generation stays a pure function of the seed and the settings, and the water
  step draws no shared randomness. Determinism tests cover both.
- **Not modelled:**
  - River valleys as a biome of their own (fertility comes from the soil water instead).
  - Migration routes avoiding water.
  - Dams and the other preparations of §21.
  - Redirecting a river (a Divine Act).
  - Seas that rise.
