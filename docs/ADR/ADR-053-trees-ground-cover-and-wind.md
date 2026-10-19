# ADR-053: Trees, ground cover and wind

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-033](ADR-033-resources-gathering-forests-and-roads.md) (forests),
  [ADR-050](ADR-050-world-streaming.md) (streaming) and
  [ADR-051](ADR-051-ground-textures-and-light.md) (the art pass)

## Context

§61 asks for distinct biomes. The trees were two shapes, a conifer of two cones and a broadleaf,
over every biome. Between them the ground was bare texture, and nothing moved but the people.

## Decision

### Seven species (`TreeMesh`, `TreeSettings`)

- **The species:** pine, fir, oak, birch, palm, willow and a bare snag, each built in code with
  `MeshBuilder`. Willows' leaves hang in switches from a dome, and palms' fronds droop from a
  leaning trunk.
- **Which grow where:** each biome grows its species in shares (`Trees.Species` in
  `settlement_view.json`); a biome not listed grows grassland's.

  | Biome | Species |
  | --- | --- |
  | Grassland | Oak, birch, a few pines |
  | Forest | Pine, oak, birch |
  | Highland | Fir, pine |
  | Tundra | Fir, birch |
  | River valleys | Willow, oak |
  | Swamp | Willow, snags |
  | Coast | Pine, oak, palm |
  | Island | Palm |
  | Desert | Palm, snags |
  | Volcanic land | Snags, pine |

- **Colours:** each species has its trunk and leaf colours; leaves keep each tree's own shade and
  whiten under snow.

### Ground cover (`GroundCoverLayout`, `GroundCoverMesh`)

- **Where:** within 100 m of the camera the ground is cut into 1.2 m cells, and each may hold one
  thing, drawn by `PresentationHash` from its place.
- **What:** by the biome's chances (`GroundCover`):
  - tufts of grass and flowers as thick as the land's vegetation;
  - bushes and stones;
  - reeds in swamps.
- **Stable places:** stones, bushes and reeds take their shares of a cell's roll first, so they
  stay where they are as the grass grows and withers.
- **Bare ground:** nothing grows in water, on bare rock or on a road, nor in a village by its
  houses, on its fields or on its square. Snow pales it.
- **Streaming:** it streams in 32 m chunks (ADR-050). `ChunkStreamer` now looks only at the chunks
  within its load radius, so small chunks over a 12 km island cost nothing far away.

### Wind (`Breeze`, `tree.gdshader`)

- **The wind felt:** `Breeze.At` finds the wind over a place: the strongest weather cell's wind,
  fading towards the cell's edge, blowing the weather's way.
- **Bending:** the tree shader bends trees and ground cover by their height in that wind, with
  gusts out of step from tree to tree and fluttering leaves. Trees sway 0.15 m in a light breeze
  and up to 0.9 m more in a storm.

## Consequences

- **§61:** biomes read by their trees and ground (palm-fringed islands, weeping willows by rivers,
  fir-dark heights, grey snags on volcanic land, meadows of grass and flowers) and the land moves
  with the weather.
- **Cost:**

  | View | Frame rate | Triangles |
  | --- | --- | --- |
  | Grown game at ×16 | 133 FPS (135 before) | 4 million |
  | Vast island at 300 m | 135 FPS | 5.7 million |
  | Stress world | 92 FPS (108 at the end of part 29, the cost of parts 30–32 together) | |

- **Not done:** wildlife (the next part), and grass that the camera's nearness thickens further.
