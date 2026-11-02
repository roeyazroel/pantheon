# ADR-077: The land's breadth, living water, and villages of lanes

- Status: Accepted; amended by [ADR-078](ADR-078-a-world-that-turns-with-the-year.md) and [ADR-079](ADR-079-day-and-night.md)
- Date: 2026-11-02
- Amends: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the house spiral),
  [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (the water's look),
  [ADR-051](ADR-051-ground-textures-and-light.md) (borders and light),
  [ADR-052](ADR-052-village-architecture.md) (one house per village) and
  [ADR-054](ADR-054-wildlife-and-bridges.md) (level bridges)

## Context

The owner asked whether moving to Unreal would look much better. Captures of seed 1234 showed that
what held the look back was not the engine:

- **The ground** repeats a texture that reads as one flat colour from the god's height. Biome borders
  blended over 6 m, a hard line at that height, and rock read as static.
- **The light** was front-on and flat, and the greens neon.
- **Water** was flat colour, and the rivers' 8 m cells showed as saw-teeth along their banks.
- **Villages** were one house mesh on an even spiral, every roof one colour.

The owner also found a bridge standing in the air, joined to nothing. Measured over seed 1234, 13
bridges stood up to 5.9 m above the road at an end, several roads each drew their own bridge over
one crossing, and walkers waded the riverbed under them.

## Decision

### The land's breadth (`GroundShade`, pure, on both terrain renderers)

The colour map carries what a texture cannot: hollows darkened and ridges lightened against the land
around them at two scales, soft patches of light and dark, grass drying in wide patches and growing
lush along rivers and lakes, wet ground at the sea's edge, and bands in rock. Settings are
`Ground.Shade` in `biome_palette.json`.

### Borders that wander (`TerrainControl`)

Each sample reads the biome at a place near it, moved by fractal noise, so borders wander by tens of
metres, and blends over rings out to 18 m. A second ground shows through a texture in patches
(`Ground.Patches`): grass through leaf litter, earth through ash. The edges of rock, snow and sand are
frayed by noise.

### Light (`Main.tscn`)

A lower sun from the side, so that land and buildings show their form; AgX tone mapping; more sky
light; aerial haze. Edges are smoothed by SMAA, a new setting ("Smooth edges", on by default).

Measured at 2720x1530 on an M3 Pro, uncapped, near view: SMAA +1.1 ms, FXAA +0.5, TAA +2.7, MSAA 2x
+3.7. In crops SMAA matched MSAA. Bounced light (SSIL) cost 3 ms and side by side could not be told
from none, so it is not used. Ambient occlusion's quality settings barely change its cost, which lies
in the normals pass that redraws the scene.

### Water

- **Ripples** (`WaterTextures`): one tile made in code, slope in two channels and foam lace in a third.
- **The sea:** three layers of ripples drifting across each other, a turquoise-to-deep gradient, and
  waves that roll in over the shallows and break into foam, fading with distance.
- **Rivers and lakes:** `WaterSurfaceMesh.FlowAt` gives each vertex the way the surface falls; the
  shader carries ripples and streaks of foam along it in two phases of a flow map. Water fades out at
  its banks, which hides the mesh's cells.

### Bridges (`BridgeLayout`, `BridgeSpan`)

A bridge reaches from the top of one bank to the top of the other, walked up the road from the
water's edge, and its deck arches between its two ends, so it meets the road at both whatever their
heights, and clears the water by at least 0.6 m. Piles reach the bed. Roads sharing a crossing share a
bridge. The road's own track stops at the water, and walkers stand on the deck.

### Villages of lanes (`VillagePlan`, replacing the spiral)

- **Plan:** a green around the shrine, eight lanes running out from it and cross lanes every 32 m.
- **The lanes follow the roads.** Roads run between region centres in eight directions, and a village
  stands at a region centre, so a road built later lands on a lane and no house has to move. The plan
  takes nothing but the village's id, so growth still only adds houses.
- **Houses** line the lanes with their doors to them. Each village builds on its own sides of its
  lanes, and leaves gaps, so no two look alike. Houses keep off ground steeper than `HouseMaxSlope`.
- **Drawn:** `LaneView` draws the green, the lanes and the paths to doors with the road shader, giving
  out in water and on steep slopes. `FieldView` lays each field on the land as its own patch, with
  furrows and ragged edges (`field.gdshader`), replacing tilted slabs that the ground poked through.

### Houses

`HouseMesh` has four variants of each style, with deep roofs and wide eaves: hipped roofs, lean-tos,
porches, chimneys, side rooms, a veranda and a walled yard. `MeshBuilder` gains `Roof`, `Hip` and
`Beam`. Each house picks its own colours (`VillageArchitecture.ColorsOf`): half keep the village's and
half take any of the style's, and where a god's look shows, `MotifShare` of them take it. The building
shader weathers each house: darker eaves, a paler ridge, moss low on roofs and damp at the foot of walls.

## Consequences

- **Performance**, uncapped at 2720x1530 on an M3 Pro, before and after, without SMAA: near 17.2 →
  19.0 ms, wide 21.7 → 21.0, high 17.8 → 18.6. With SMAA about 1 ms more.
- No save, protocol or simulation change: all of it is derived from `WorldState` and never saved.
- `HutPlacement` gains `Variant`, and `BareGround` keeps grass off the lanes.
- The citizens' paths change with the houses (`LocalNavGrid` reads the same site).
- The village's look no longer changes every house at once when a god's style arrives: a share of
  them keep their own colours.

## Alternatives considered

- **Moving to Unreal:** rejected. The look was held back by content and tuning, not the renderer. See
  the Context.
- **Streets from the roads themselves:** a new road would have moved houses, and `DebrisLayout` would
  have seen them fall.
- **MSAA, TAA, SSIL:** see the measurements above.
