# ADR-038: The biomes of the world

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-005](ADR-005-terrain3d-for-terrain.md) (the terrain and its two renderers),
  [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (rivers and lakes) and
  [ADR-029](ADR-029-hazards-preparation-and-marks-on-the-land.md) (the volcano)

## Context

§5 lists the possible biomes: grassland, forest, desert, mountain, tundra, swamp, volcanic region,
coast, island and river valley. The world had four: sea, grassland, forest and highland (the
mountain).

The world generator classifies each terrain sample by the first matching rule on its elevation,
temperature and moisture (`worldgen.json`). A region takes its samples' most common biome, and its
fertility and vegetation come from that biome's base traits.

Three of the missing biomes are climates:

- desert, hot and dry;
- tundra, cold;
- swamp, wet and low.

Four are places:

- an island off the main one;
- the coast;
- a river's valley;
- the land around the volcano.

Climate rules cannot express a place.

## Decision

### Climate biomes

Tundra, desert and swamp are climate rules in the ordered `Biomes` list:

| Biome | Rule | Fertility | Vegetation |
| --- | --- | --- | --- |
| Tundra | Colder than 0.40 | 0.30 | 0.35 |
| Desert | At least 0.56 warm and below 0.40 moisture | 0.25 | 0.10 |
| Swamp | At least 0.80 moisture, below 8 m | 0.50 | 0.85 |

They sit after highland and before forest and grassland. Tundra is thus the cold band below the
heights.

### Landforms (`WorldGenSettings.Landforms`)

`LandformMarker` marks the four place biomes on the samples after the climate biomes and the rivers,
in order of precedence. Sea is never replaced.

1. **Island:** every land mass but the largest that is smaller than 0.12 km².
2. **Volcanic land:** land within 140 m of the centre of the highest land region, its reach
   swelling and shrinking in lobes around the crater, when that region rises 60 m.
   - That region is exactly the one `HazardModel.FindVolcano` takes for the volcano.
   - `GameContent` checks that `VolcanoMinElevation` equals the volcano's own threshold.
3. **River valley:** grassland, forest, desert or tundra within 30 m of a river or lake and no
   higher than 40 m.
4. **Coast:** grassland or forest within 32 m of the sea and no higher than 10 m.

| Landform | Fertility | Vegetation |
| --- | --- | --- |
| Coast | 0.65 | 0.35 |
| River valley | 1.00 | 0.65 |
| Island | 0.70 | 0.60 |
| Volcanic land | 0.80 | 0.25 |

The marking runs inside `TerrainGenerator`, so the client's terrain, grown again from the seed and
the saved settings, matches the world's.

### Regions

With landforms, a region is land when most of its samples are land, and then takes its most common
land biome. Otherwise the sea, against land split among more biomes, would win mixed coastal
regions. Without landforms the old rule holds, so older worlds and test worlds keep their regions.

### Effects

- **Fertility and vegetation:** come from the biome's base traits, so the existing systems follow:
  - harvests, villages' founding places, forests and wildlife;
  - fire, which needs dry growth;
  - the soil's water;
  - drought and thirst in the desert;
  - snow in the cold.
- **Forests and stone** (`Resources.Biomes`): conifers in the tundra, little wood in the desert,
  much stone on volcanic land.
- **Villages** settle most on grassland and in river valleys, the most fertile land.

### Presentation

- **Colours:** each biome has its own ground colour (`biome_palette.json`): desert sand, tundra
  moss, dark olive swamp, dune grass, lush valleys, island grass and dark ash.
- **Blending:** land biomes' colours blend over 8 m (`BiomeBlendMetres`, a sliding box filter in
  `TerrainColorizer`), so no border is a hard line, on both renderers.
- **Trees:** the tundra grows conifers, and volcanic land a mix.
- **The book's World page:** counts the land by biome ("grassland 25%, forest 19%, coast 17%…").
- **F1 and F3:** show the region's biome as before, with the new colours.

### Saves, sharing and tests

- **Saves:** move to version 29. A version 28 world gains no landforms and grows its terrain as
  before.
- **The protocol:** moves to version 17.
- **Test worlds:** pin the four old biomes (`TestContent.FourBiomes`) and no landforms, as they
  pinned the land before rivers. `TestContent.BiomeWorldGen` has every biome.

## Consequences

- **§5:** every biome it lists exists.
- **Shares:** over ten shipped worlds, the land is:
  - grassland 23%, forest 20%, coast 17%, river valley 9%;
  - swamp 8%, highland 7%, volcanic land 6%;
  - desert 5%, tundra 3%, island 2%.
- **Villages:** over those ten worlds they are founded on grassland 30 times, in river valleys 21,
  in forest 10, on the coast 5, on volcanic land and in swamp 4 times each, and on highland and
  islands 3 times each.
- **The volcano:** always stands on volcanic land.
- **Balance (passive player):** every shipped world is a different world now. Over 160 seeds
  (30–189), 138 games are won within the hour; part 17 won 141. Over seeds 30–69, 35 are won at a
  median of 27.5 minutes.
- **Cost:** building the terrain's colours takes about 40 ms more (431 ms for seed 42, against 389).
- **Not modelled:** biome-specific walking costs, fishing on the coast, and swamp sickness.
