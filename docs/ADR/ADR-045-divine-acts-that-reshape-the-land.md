# ADR-045: Divine Acts that reshape the land

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md) (Divine Acts),
  [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (rivers and lakes) and
  [ADR-038](ADR-038-the-biomes-of-the-world.md) (biomes and landforms)

## Context

§14's Divine Acts include redirecting a river, creating an island and splitting a mountain, and
§14 says such acts become historical events. ADR-030 built three acts that leave the terrain as it
is: Hallow the Land, Wake the Volcano and Still the World.

The terrain is never saved (ADR-006). It is made again from the seed and the generation settings,
and the rivers are found in it and cut into it as it is made. So a lasting change to the land's
shape has to be kept in the world, and the terrain made with it. Making the whole terrain again
costs 1.5 s on the default island and 6 s on the vast one, too long to pause the game for an act.

## Decision

### Three acts, for a Greater God

| Act | Domain | Cost | Where | What it does |
| --- | --- | --- | --- | --- |
| Raise an Island | Ocean | 3,000 | The open sea, not a lake | A lobed island 110 m across rises with a peak 14 m above the sea |
| Split the Mountain | Chaos | 2,500 | Ground at least 45 m high | A gorge 320 m long and 80 m wide, its floor falling from one low side to the other |
| Turn the River | Nature | 2,500 | Land at least 3 m lower than a river within 900 m | A channel from the river's nearest inland point, and a 6 m dam 30 m down its old course |

- **Splitting:** the gorge runs along whichever of eight directions leads to the lowest ground at
  both ends, and both ends must lie at least 15 m below the mountain.
- **Turning a river:** the channel starts 3 m below the river and falls to 1 m below the point. The
  water must run downhill, and the new course must be at least 60 m long.
- **Refusals:** each act refuses where it cannot be done ("an island rises only from the open sea").
- **The numbers:** in `divine_rules.json` (`Land`).

### Keeping and making the land

- **Kept in the world:** `WorldState.Land` keeps every `TerrainReshape` in order: its kind, its
  ends, its size and its floor heights, which are fixed when it is cast.
- **Shaping the ground:** `TerrainGenerator` applies them to the ground before the rivers are found.
  `TerrainReshaper` raises a dome, cuts a floor between two heights, or carves a channel and raises
  a dam. So the rivers, lakes, biomes and landforms follow the new land; a raised island near a
  small landmass is an island biome.
- **No full regeneration:** a `TerrainField` keeps the ground as the noise made it (`Ground`).
  `TerrainGenerator.Reshape` makes a reshaped terrain from it without sampling the noise again: only
  the moved ground's climate, the rivers, the biomes and the landforms are found anew. It takes
  200–280 ms on the default island and is identical to making the terrain again from the seed.
- **The world follows (`LandShaper.Settle`):**
  - a region whose ground changed takes its new height, climate, biome and fresh water;
  - new land takes the soil, plants, forest and stone of new land;
  - the rivers and lakes are found again, each river keeping the level of the old one it shares
    most land with.

### The act in the simulation

- **The current terrain (`LandStage`):** the miracle caster holds it, set by whoever holds the
  world's terrain. `MiracleEffects` checks and does the acts through `LandActs`.
- **No terrain:** where none is held (an engine on its own), the acts find no target and cannot be
  cast.
- **The session:** it sees the new terrain after the command and raises `TerrainChanged`. The client
  rebuilds the terrain and everything set on it, without moving the camera.
- **The history and alerts:** each act enters the history (Raising, Splitting, Diverting, with the
  river's name) and is announced ("You turn Brenna towards Kestrel Rise: its old course runs dry
  below the dam").
- **The AI:** gods never reshape the land; they aim at villages, not at the sea, mountains or
  rivers, so balance is unchanged.

### Saves, sharing and tools

- **Saves:** move to version 32. Older saves keep the land as the seed made it, and a loaded world
  makes its terrain with its reshapings.
- **Sharing:**
  - the protocol moves to version 20;
  - `WorldSection.Land` carries the reshapings, and a joined player makes its terrain again
    (`GameSession.SyncLand`); its regions and water arrive in their own sections.
- **Tools:**
  - F1 lists the reshapings;
  - `--reshape=island|gorge|river` makes one near the focused settlement at the start, for
    captures;
  - the acts are cast from the Domains panel (G) like the others.

## Consequences

- **§14:** every kind of miracle it names exists.
- **§15 (systemic consequences):** reshaping is systemic:
  - a turned river waters new land and leaves its old course below the dam dry;
  - a gorge can capture a river: on seed 42 the big river now runs through it;
  - an island is new land that villages may settle.
- **Not modelled:** islands far out at sea that stand alone (one near the coast joins it), lowering
  land into the sea, and acts the AI chooses.
