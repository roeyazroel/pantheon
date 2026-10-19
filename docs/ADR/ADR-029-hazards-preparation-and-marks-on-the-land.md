# ADR-029: Hazards, disaster preparation and marks on the land

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-014](ADR-014-disasters-as-processes.md) (disasters as processes),
  [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (floods and waves),
  [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md) (buildings) and
  [ADR-028](ADR-028-seasons-extremes-and-ecology.md) (seasons and the land's life)

## Context

The world had earthquakes, wildfires, floods, droughts, storms, hurricanes and cold waves. The
specification asks for more:

- **§19:** tsunamis, tornadoes, volcanic eruptions, landslides, meteors, crop disease and locust
  swarms, each a process rather than a blow of damage.
- **§20:** an earthquake's chain runs on to blocked roads.
- **§21:** dams, sea walls, irrigation and evacuation routes beside the shelters, firebreaks,
  granaries and hospitals settlements already build.
- **§64:** earthquakes leave visible cracks, landslides and blocked roads, without remeshing the
  terrain.

## Decision

### One hazard step

`HazardSystem` runs after the disaster step, on the same interval, with a stream derived from the
seed and the tick (stream `hazards`). It leaves the shared stream alone, like the weather and the
disasters (ADR-013). The pure parts are in `HazardModel`; the moving and spreading hazards are in
`LivingHazards`. Rules are `disasters.json`'s `Hazards` (`HazardRules`). Every chance defaults to
off, so content without the section has no hazards.

### The hazards (§19, §20)

- **Tsunamis:**
  - An earthquake of at least `MinMagnitude` within `CoastDistance` of the sea raises a wave on
    the nearest coast.
  - The wave floods the land around the shore, `RadiusPerMagnitude` inland and `HeightPerMagnitude`
    high per magnitude above the minimum, plus one. It uses the flood of ADR-022.
  - It strikes villages by their flood and their distance from the shore.
- **Landslides:**
  - A region at least `MinDrop` above its lowest neighbour may slide: with `SlidePerSecond` chance
    while its soil is soaked, and `ShakeChance` a second per unit of shaking while an earthquake
    shakes it.
  - The slope and the land below are buried in rubble (`RegionState.Rubble`) and harmed. A village
    below is struck in full, one on the slope by half.
  - Rubble of `RegionPathfinder.BlockingRubble` or more blocks the way for migrants and Creatures
    (the blocked roads of §20). It clears at `RubbleClearPerSecond`.
- **The volcano:**
  - The island's highest land region is its volcano, if it rises `MinElevation` or more. It is
    found when first needed and kept (`DisasterState.Volcano`).
  - It wakes `EruptionsPerHour`, then rumbles for `RumbleSeconds` with a tremor every
    `TremorSeconds` (an earthquake of `TremorMagnitude`).
  - It erupts for `EruptSeconds`. Lava runs down the steepest slope through `LavaRegions` regions,
    stopping at the sea, and burns, scorches and wrecks them. Ash within `AshRadius` harms the land,
    sickens the people and spoils their stores.
  - It sleeps at least `CooldownSeconds` before it can wake again.
- **Tornadoes:**
  - A thunderstorm over land at least `MinTemperature` warm (with the season's shift) spins one down
    with `PerStormPerSecond` chance.
  - It runs roughly with the storm at `Speed`, for `MinSeconds` to `MaxSeconds`, and lifts over the
    sea.
  - It tears down `HousingLossPerSecond` of the houses of any village within `Radius`, and harms the
    land it crosses.
- **Meteors:**
  - A falling star strikes somewhere on the land `PerHour`.
  - It harms and scars the land within `Radius`, sets the struck region alight, sets off an
    earthquake of `Magnitude` and leaves a crater. Villages within reach are struck by their
    distance.
- **Blight (crop disease):**
  - Blight appears in warm, wet fields (`MinTemperature`, `WetSoil`), grows and spreads to a
    neighbouring field while they stay so, and dies away when they do not.
  - Fire burns it out, and a blessing (Bless Crops) cures it.
  - Blighted fields yield less: fertility times (1 - blight).
  - A village is told when the mean blight of its fields reaches `Notice`, and again once it has
    fallen below half of that. The villages told are kept (`DisasterState.BlightedSettlementIds`),
    so the two always pair.
- **Locusts:**
  - Swarms rise `SwarmsPerHour` from hot, dry land, drift with the wind at `Speed` and eat the
    stores of every village within `Radius`.
  - Rain drowns them and fire burns them. They die after `MinSeconds` to `MaxSeconds`, or when
    they leave the island.

Every hazard strikes a village through `DisasterDamage.Strike`. It applies the god's shield, the
Protection domain, architecture, the village's preparations and its technology, then frightens the
people and reports the damage as `SettlementDamaged` with the hazard as its cause.

### Preparation (§21)

New buildings, raised through the existing priorities (ADR-023), each only where it helps:

| Building | Priority | Where | Effect |
| --- | --- | --- | --- |
| Sea wall | Preparation | Beside the sea | Spares `SeaWallDamage` of a wave's or a hurricane's damage |
| Dam | Preparation | Beside a river or lake | Spares `DamDamage` of a flood's damage, beside a levee's |
| Evacuation routes | Preparation | Anywhere | Save `EvacuationDeaths` of the lives every disaster would take |
| Irrigation | Food | Beside a river or lake | Waters the fields: their soil is `IrrigationWater` of the way from its moisture to wet |

Reservoirs are left out: the cistern (ADR-023) already stores water against drought.

### Marks on the land (§64)

The terrain is not remeshed. Disasters leave marks instead (`DisasterState.Marks`, `TerrainMark`):

- a crack where an earthquake begins;
- a slide below a landslide;
- a crater where a meteor strikes.

They fade over `MarkSeconds` and are then forgotten. The client draws them as dark decals, the
same on both terrain renderers. Rubble and blight are drawn as one island-wide layer, like the
snow.

### Saves, multiplayer and presentation

- **Saves:** version 21. Migrating from 20 gives no blight or rubble, nothing on the move, no
  marks, no village told of blight, and a volcano still to be found.
- **Multiplayer:** protocol 9. The disasters section carries the hazards; the regions section
  carries blight and rubble.
- **Alerts and history:**
  - Alerts tell of waves, landslides, the volcano's waking, eruption and sleep, tornadoes, falling
    stars, blight coming and going, and locusts.
  - The history keeps the great ones: the Great Wave, the Landslide, the Eruption of Mount X, the
    Whirlwind, the Falling Star, the Blight and the Locusts.
  - Village labels carry "blight" and "cut off".
- **Debug:**
  - F1's hazards line: the volcano, tornadoes, swarms, blighted and buried regions, and marks.
    Each village's blight and rubble.
  - F3's blight and rubble layers; the O Danger overlay shows rubble and blight.
  - I starts the next hazard of the cycle at the cursor; `--disaster` takes each of them.
- **The look:**
  - A grey funnel for each tornado; a cloud of dark specks for each swarm.
  - The volcano's plume: thin smoke while it rumbles, thick smoke, embers and a red glow while it
    erupts. Lava shows as the fire and scorch it leaves.

## Consequences

- **Rates:** ten one-hour games (seeds 100–109) saw, each hour, about one tsunami, three
  landslides, half a tornado, a fifth of an eruption, a fifth of a meteor, half a blight and half a
  swarm reaching a village.
- **Tuning found:**
  - Landslides first came 134 times an hour, from quakes rolling every shaken slope each second at
    a 12 m drop. They now need a 30 m drop and a per-second chance: about 3 an hour.
  - Blight at first needed wetter fields than most are, and never reached a village's notice. It
    now needs fields of 0.5 and is told at 0.15.
  - Blight was first told gone when its mean fell through half the notice. That told villages it
    had gone when they had never been told it came, so the villages told are now kept.
  - A swarm rising on a village's fields went unannounced; it is now told at once.
- **Not modelled:**
  - Reservoirs, apart from the cistern.
  - Lava as its own flow.
  - Landslides that change the terrain's height.
  - Tornadoes that lift what they carry.
  - Disease in people, beyond the cold's and the ash's sickness.
