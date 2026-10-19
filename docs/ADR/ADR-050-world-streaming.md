# ADR-050: World streaming

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-005](ADR-005-terrain3d-for-terrain.md) (Terrain3D and the fallback),
  [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (tiers),
  [ADR-033](ADR-033-resources-gathering-forests-and-roads.md) (forests) and
  [ADR-044](ADR-044-stress-scenario-and-debug-overlays.md) (measuring)

## Context

§84 asks the world to be divided spatially: nearby regions load high-detail terrain, actors,
vegetation and effects, and distant regions remain simulation data.

| Visual | Before this part |
| --- | --- |
| Actors | Streamed already: citizens appear within 450 m (ADR-008) and labels give way from afar (ADR-027) |
| Terrain3D | Draws its own detail by distance |
| Fallback terrain | One level of detail everywhere (4 m cells) |
| Forests | Every tree of the island instanced at once |
| Fires | Drawn for every burning region |

Measured on the vast island (12 km, seed 42, an M3 Pro):

- the visuals take 1.58 s to build: the terrain 1.02 s, forests 0.19 s, rivers 0.16 s and region
  lines 0.10 s;
- the engine holds 241 MB with Terrain3D (182 MB with the fallback) in 12,900 objects and 5,460
  nodes;
- with the whole island in view the game runs at 208–215 FPS.

So streaming is not needed for frame time. It is needed for what §84 describes: detail where the
camera is, and memory and start-up that do not grow with the whole island.

## Decision

### The plan (`ChunkStreamer`)

- **Chunks:** the island is cut into square chunks, 256 m for forests and terrain.
- **Loading:** a chunk loads when the camera comes within a load radius of its nearest point at
  sea level. The distance is measured in three dimensions, so a camera high above loads only what
  it could see. The nearest chunks load first, a few an update.
- **Unloading:** a loaded chunk goes beyond a wider unload radius, so a camera at the edge does
  not make chunks flicker.
- **Purity:** it is pure and tested; each presenter acts on its plan.

### What streams

| Visual | Near the camera | Far off |
| --- | --- | --- |
| Forests | Planted within 2.1 km, 6 chunks a frame (everything in view at once on a world's first frame) | Cleared beyond 2.5 km |
| Fallback terrain | Rebuilt from every sample (2 m) within 650 m, one chunk a frame | The coarse mesh, kept for each chunk, beyond 850 m |
| Terrain3D | Its own clipmap | Its own clipmap |
| Fires | Drawn within 2.5 km | Simulation data; labels and the Danger overlay show them |
| Citizens | Within 450 m (ADR-008) | Within 450 m (ADR-008) |

- **Cracks:** a detailed terrain chunk's border vertices take the heights the coarse edge
  interpolates (`TerrainMeshBuilder.BuildChunk`'s `edgeStride`). So detailed and coarse chunks meet
  without cracks, and so do two detailed chunks.
- **The radii** are `Streaming` in `settlement_view.json`.

### Tools

- F1 and unattended runs print the streaming line: "Streaming: forests planted in 200 of 1849
  chunks within 2100 m; terrain in detail in 6 of 256 chunks within 650 m; fires drawn 0 of 0
  within 2500 m".

## Consequences

- **§84:** implemented for terrain, actors, vegetation and effects.
- **The vast island:**

  | Measure | Before | After |
  | --- | --- | --- |
  | Visuals built | 1.58 s | 1.38 s |
  | Engine memory, Terrain3D | 241 MB | 194 MB |
  | Engine memory, fallback | 182 MB | 136 MB |
  | Objects | 12,900 | 6,300 |
  | Nodes | 5,460 | 2,160 |
  | Frame rate, Terrain3D | 229 FPS | 220 FPS |
  | Frame rate, fallback | 269 FPS | 252 FPS |

  The fallback now draws 2 m terrain near the camera.
- **The stress world:** 111 FPS, with managed memory 273 MB (386 before).
- **Not streamed:** the simulation, which runs everywhere as before. The terrain's height data
  stays whole, as the simulation needs it.
