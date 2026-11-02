# ADR-078: A world that turns with the year and the weather

- Status: Accepted
- Date: 2026-11-02
- Amends: [ADR-051](ADR-051-ground-textures-and-light.md) (ambient occlusion),
  [ADR-052](ADR-052-village-architecture.md) (how a building site rises) and
  [ADR-077](ADR-077-land-water-and-villages.md) (what the light costs)

## Context

The owner asked to raise the bar of realism and art across the whole picture, keeping the performance.
A tour of the game's conditions found the largest gap was not in any still image: the world did not
change. Winter looked like summer, although the game said snow lay on the land; a storm barely showed;
fire was pale blobs. Nothing that draws the land knew the season.

The budget had to come from somewhere. Measured at 2720x1530 on an M3 Pro, ambient occlusion was the
largest single cost, 4.5 ms of a near frame, because it needs a pass that redraws the scene for
normals. Lowering its quality saved almost none of that. Side by side, near and close, on and off
could hardly be told apart: `GroundShade` already darkens the land's hollows, and the sun's shadows
ground the buildings.

## Decision

### No ambient occlusion

It is removed. That pays for everything below, and leaves frames shorter than before.

"Lighting effects" now switches the glow.

### The world's look (`WorldLook`, `WorldLookView`, `world_look.gdshaderinc`)

One small state, read by every shader of the land through global shader uniforms, so that the whole
picture turns together for the cost of two vectors a frame:

- **The year:** `WorldLook.Of` gives how far spring, autumn and winter show. Each season shows in full,
  then fades into the next over the last 30% of it. It is pure, and is never saved. A world with an
  even climate stays as it is.
- **The sky:** how much of it is cloud, how wet the land is, and how far the clouds have drifted.
- **Ground:** what grows turns fresh, straw and rust, then dead and pale; wet earth darkens and shines;
  frost lies on level land in winter, in broad soft patches.
- **Trees:** oak, birch and willow turn gold and rust, each in its own time, and winter strips them to
  their boughs; conifers and palms stay.
- **Grass, flowers, fields:** grass turns as the ground does; flowers are gone by winter; fields show
  green shoots, then gold, then lie fallow.
- **Roofs:** frost dusts them in winter; rain darkens thatch.
- **Clouds' shadows** drift over the land on the weather's wind, wider as the sky fills. Under a full
  overcast there is no shadow to see, and the storm's gloom dims the sun instead, so the two do not
  stack.
- **Storms:** the sun's light cools and the haze thickens.

The terrain takes the look through `Terrain3DShaderPatch.WithLook`. A Terrain3D shader it does not
recognise is left as it is. The fallback renderer's terrain becomes a small shader for the same reason.

### Details

- Fire: deep orange flames that no longer blow out to cream, and dark smoke that the haze does not wash
  out.
- A carved stone shrine (`CivicMesh.Shrine`): steps, a tapering shaft with bands and a cradle for its light.
- A grain of light and dark in the colour map (`Ground.Shade.Grain`), for the middle distance, where a
  texture's own detail has gone.
- Stones, ruts and light-catching relief in the roads' dirt.
- Low haze over low land (`fog_height`).
- SMAA is skipped on the Compatibility renderer, which cannot do it.

### How far a building has risen is the site's own

`built` was an instance uniform on the building shader. Godot reserves a block for every object whose
material has one, and the Compatibility renderer's buffer holds far fewer than the world has. When it
overflowed, buildings read `built` as 0 and collapsed: on that renderer **no house was drawn and bridges
were spikes**, in every build since ADR-052. Each building site now has its own copy of the material
with a plain uniform. Everything else stands.

The overflow itself remains on that renderer (`plant.gdshader`'s instance uniforms), and logs errors;
trees there fall back to default trunks.

## Consequences

- **Performance**, uncapped at 2720x1530 on an M3 Pro, the build before part 55 against this one, each
  run alternating:

  | View | Before | After |
  | --- | --- | --- |
  | Close (40 m) | 15.6 ms | 13.4 ms |
  | Near (110 m) | 17.2 ms | 15.7 ms |
  | Wide (420 m) | 21.9 ms | 17.5 ms |
  | High (1400 m) | 17.8 ms | 16.0 ms |

- No save, protocol or simulation change.
- Lowland snow still follows the simulation's `RegionState.Snow`; frost is only a look.
- Every shader of the land now depends on three global shader uniforms in `project.godot`.
- F1 shows how far the year has turned the land.

## Alternatives considered

- **Keeping ambient occlusion at a lower quality:** it saved 1.3 ms at best, as the cost is in the
  normals pass.
- **Baking the seasons into the colour map:** a rebuild of a million samples per change, and it would
  leave trees, fields and roofs out.
- **Per-material season uniforms:** every presenter would have to be told; global uniforms reach them
  all, and the terrain's generated shader, in one place.
