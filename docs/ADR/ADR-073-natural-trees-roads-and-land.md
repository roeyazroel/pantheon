# ADR-073: Natural trees, roads and land, grown in code

- Status: Accepted (amends ADR-053 and ADR-033)
- Date: 2026-10-21
- Amends:
  - [ADR-053](ADR-053-trees-ground-cover-and-wind.md): its trees and ground cover of balls and cones;
  - [ADR-033](ADR-033-resources-gathering-forests-and-roads.md): the roads' flat ribbon and stretched bridges.

## Context

The owner, playing: "Let's try to make the trees, the roads and the nature more realistic."

| Before | What was there |
| --- | --- |
| Trees | Seven species, each a few smooth balls or cones on a tube: up close an oak was three green blobs. |
| Roads | A flat brown ribbon 0.25 m above the ground, in straight legs from region to region. |
| Bridges | A small mesh stretched along each span, its posts and rails stretched with it. |
| Grass | Thin tufts of tubes, few and sparse, on a ground texture of coarse painted blotches. |
| Stones | Ellipsoids. |

Spec §61 asks for a stylised, readable world, and the game makes everything it shows in code, with no
assets (ADR-021). So this direction stays: natural forms and light, made in code.

## Decision

### Plants grown in code

`Pantheon.Game.Presentation.Nature` grows every plant as a `PlantMesh`: positions, normals, texture
coordinates, and each vertex's part (bark, leaves, blade, stone or petal) and how much light reaches
it.

**`TreeGrowth`** grows each of the seven species in three variants, near and far:

| Species | How it grows |
| --- | --- |
| Oak, birch | A trunk flaring at its roots and leaning a little, five to nine branches curving up and out at the golden angle, twigs, and clusters of crossed leaf cards at their tips. More clusters fill out the crown's shell. |
| Willow | Like the oak, with switches hanging from its dome nearly to the ground. |
| Pine, fir | A straight trunk, with whorls of needle sprays narrowing to the tip: the fir a cone, the pine rounded and bare below. |
| Palm | A ringed trunk curving up to eleven fronds that arch out and droop. |
| Snag | A broken, leaning trunk with a few bare branches. |

- **Light in the crown.** Leaf cards' normals bend out from the crown's middle, so a crown is lit as
  one rounded mass. Cards deeper in the crown are darker.
- **Far trees** have fewer and larger cards, fewer sides to their limbs, and no twigs.
- **Cost.** A near oak is about 700 triangles, and a far one about 200.

**`LeafAtlas`** draws six cells, leaf by leaf:

- an oak's broad leaves;
- a birch's small leaves;
- needle sprays;
- a palm frond;
- willow switches;
- a shrub's dark leaves.

The atlas also builds its own mipmaps, raising each smaller mipmap's alpha until as much stands above
the cut as in the largest, so distant crowns do not thin away. **Bark** has three patterns: rough
furrows, a birch's dark marks and a palm's rings. The same pixels come every time.

**`GroundPlants`** grows the ground cover in variants:

- tufts of 34 curved blades, dark at the foot;
- flowers with five petals;
- bushes of shrub cards;
- ferns of arching fronds, a new kind on forest floors and by rivers and swamps;
- stones (an icosahedron split once and pushed in and out, flattened);
- reeds with seed heads.

### The plant shader

`plant.gdshader` draws every plant:

- **bark** in its pattern and its species' colour;
- **leaves** cut from the atlas, letting light through (backlight);
- **blades** shaded from foot to tip;
- **stones** grained, with moss on their tops;
- **petals** in their own colour.

**Wind.** Everything sways with the weather's wind by its height, and leaves and blades flutter.

**Two-sided lighting.** Godot turns a back face's normal round. The shader undoes that for cards,
blades and petals, which are lit the same from either side. The sun's shadow normal bias is raised to
2.2, against acne on the two-sided cards.

### Forests and ground cover

**Forests.**

- **Density.** They are twice as dense: a tree to 60 m² of whole forest, not 120.
- **Near and far.** Each chunk draws its trees within `Trees.DetailDistance` (420 m) of the camera
  as near trees, in their three variants, and beyond that their far forms; the two cross-fade over
  60 m.
- **Shadows.** Far trees cast none.
- **Distance.** Each chunk's node stands at its middle, at the ground's height, so its distance
  from the camera is the chunk's.

**Ground cover.**

- Its cells are 0.8 m (from 1.2), with the chances of bushes and stones lowered to keep their
  numbers.
- Grass shows within `GroundCover.GrassRadius` (55 m), and everything else within 100 m.
- Each kind and variant has its own node.

### The ground's grass

The terrain's grass texture is drawn as 70,000 short blades over soft patches, in place of cellular
blotches, and its two colours are closer.

### Roads

**`RoadLayout`:**

- **Rounded corners.** It cuts the corners of the planned way three times (Chaikin's method).
- **Cross-sections.** One every 1.5 m along the built share, with the width wandering within a tenth
  of 3.6 m.
- **On the ground.** The left edge, middle and right edge are each laid on the ground, 6 cm above
  it.

**`road.gdshader`:**

- a worn track with ragged, soft edges into grassy verges;
- two ruts, and grass down the middle in patches;
- darker where a disaster has cut it;
- drawn a little towards the eye, so the ground's coarser mesh in the distance does not hide it;
- facing up whatever its triangles' winding.

The roads' rules still see the way as planned. Trees and ground cover keep clear of the planned way.

**Bridges** are built to their span:

- a planked deck on beams;
- rails on posts every two metres;
- piles every four metres down into the water.

The deck lies level with the banks' tops, three metres back from the water.

## Consequences

- **Woods, crowns, grass, tracks and bridges that read as such**, near and from the play height. The
  island looks wooded rather than dotted with balls.
- **Cost.** On the owner's Mac (M3 Pro, a 2720 × 1530 window, Terrain3D), frames take 2.4–3.9 ms
  more, about 12%. The fallback renderer runs at 41–56 FPS.

  | View | Before | After |
  | --- | --- | --- |
  | village and road | 27.0 ms | 30.4 ms |
  | forest's edge | 26.5 ms | 30.4 ms |
  | meadow | 26.2 ms | 30.0 ms |
  | play height (260 m) | 29.3 ms | 32.9 ms |
  | high (700 m) | 30.4 ms | 32.8 ms |

- **Start-up.** The textures and the 28 tree meshes are made when the game starts, in well under a
  second.
- **Pixel art.** Still none: everything is made in code.

## Alternatives considered

- **Bought or free tree models and textures.** They look more photographic, but break the rule that
  the game makes everything it shows, and they weigh megabytes per species.
- **Painting roads into the terrain's control map.** It would follow the ground exactly. But the map
  would have to be repainted as roads grow, and the fallback renderer has no such map. The track over
  the ground, drawn towards the eye, serves both renderers.
- **Grass made on the GPU from a height map.** Cheaper on the CPU for denser grass, but it needs the
  villages' bare ground as a texture too. Tufts placed in chunks reuse the ground cover's layout and
  its clearings.
