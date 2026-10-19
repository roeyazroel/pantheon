# ADR-033: Wood and stone, gathering, forests and roads

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md) (the
  villages' society, buildings and expansion), [ADR-028](ADR-028-seasons-extremes-and-ecology.md)
  (the land's life) and [ADR-029](ADR-029-hazards-preparation-and-marks-on-the-land.md) (landslides)

## Context

- **§29:** settlements decide for themselves their housing, roads, gathering, expansion, job
  allocation and local construction. Roads and gathering were not built.
- **§5:** gives a region resources.
- **§93:** generates forests and resources from the seed.

The land had vegetation but no wood or stone. Houses and buildings cost only work and wealth, and
the client drew no trees.

## Decision

### The land's wood and stone (§5, §93)

Each region gains three values:

| Value | Meaning |
| --- | --- |
| `Forest` | The most wood it grows |
| `Timber` | Its standing wood, up to its forest |
| `Stone` | Its stone, which never comes back |

- **Generation:** a region's forest is its biome's factor times its vegetation, and its stone its
  biome's stone. Both vary in patches of noise drawn from the seed (`ResourceFounder`). No random
  stream is used, so nothing else in a world changes.

  | Biome | Forest factor | Stone |
  | --- | --- | --- |
  | Grassland | 0.8 | 0.15 |
  | Forest | 1.1 | 0.2 |
  | Highland | 1.6 | 0.75 |

- **`ResourceSystem`** steps once a second:
  - timber grows back towards the forest, as far as the land's life is unharmed and not under
    deep snow;
  - fire burns it.
- **What clearing changes:**
  - a cleared forest holds less wildlife;
  - a cleared wet slope slides more often;
  - the Regrowth miracle brings the forest back;
  - the hand's trees are timber: a tree lifted is cut, and a tree planted grows forest.

### Gathering (§29)

- **Stores:** each village keeps stores of wood and stone. It wants some for each place of housing,
  and a least amount.
- **Gatherers:** while either store runs short, up to a share of the workforce gathers instead of
  farming (`LabourAllocation.Gatherers`), in proportion to the larger shortfall.
  - They split between wood and stone by the shortfalls.
  - They bring in as much as the land within two regions holds.
  - They take it from that land in proportion to what each region holds.
- **Costs:**
  - a house takes wood when it is begun, less for a ruin rebuilt, and none is begun without it;
  - a building takes wood and stone from the stores, as well as wealth, when it is begun.
- **New villages:** the starting villages have stores, and settlers carry their share of their
  home's.

### Roads (§29)

- **Planning:** a village of 50 or more plans a road to its nearest neighbour within 700 m that has
  none, by the region pathfinder, one at a time. One village plans each second, in turn, so a large
  world pays for one path search a second.
- **Building:** a share of its workforce lays it, each metre taking stone. Without stone the work
  stops. A whole road is announced (`RoadBuilt`).
- **What a whole road does:**
  - **walkers:** migrants and prophets take it between its villages, and anyone within 20 m of it
    walks 1.6 times as fast;
  - **trade:** it brings both villages wealth;
  - **relief:** it carries food from a village with food to spare to a hungry one.
- **Wear:** flood, fire or rubble anywhere along the way wears a road until it is cut, and its
  villages mend it (`RoadChanged` both ways). A road to a village that is gone decays away.

### What the player sees and does

- **In the world:**
  - **trees:** conifers in the forests and on the heights, broadleaves on the grassland, in
    proportion to each region's timber. They are kept out of the villages' clearings and off the
    roads, and white under snow. Cutting takes a region's last trees first, so the others stand
    still (`ForestLayout`, drawn in chunks of MultiMeshes).
  - **roads:** a ribbon of beaten earth along the part built so far, darker when cut
    (`RoadLayout`).
- **Panels and readouts:**
  - the Settlements panel shows the stores, the gatherers and the roads;
  - the book's World page says how much forest stands and how many roads there are;
  - F1 adds the island's forest and roads and the selected village's stores, land and roads;
  - F3 adds timber and stone layers.
- **Alerts:** roads built, cut and opened again among the player's villages.
- **Launch options:** `--roads=on` builds every road the villages would plan; `--stores=full|empty`
  sets every village's stores.
- **Saves and sharing:**
  - saves move to version 24;
  - the protocol moves to version 12, with a Roads section and timber and stone in region changes.

## Consequences

- **§29:** complete.
- **§5 and §93:** complete for the resources the game uses: wood and stone. Ores and other
  resources are not modelled.
- **Balance:** over 40 seeds (30–69), 38 games are won within the hour, at a median of 25.7
  minutes and a mean of 27.3. Part 12 gave 38, at 28.3 and 30.9.
- **Rates:** over ten one-hour games, a game sees:
  - 28 roads built, and 9.5 cuts, nearly all mended;
  - 65–91% of the forest still standing at the end;
  - villages seldom waiting for wood to begin a house.

  On a Huge world the two systems take 50 ms over 3000 ticks.
- **Not modelled:**
  - gatherers as visible figures;
  - roads inside a village;
  - trade in goods other than food.
