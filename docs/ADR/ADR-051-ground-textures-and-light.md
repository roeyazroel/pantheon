# ADR-051: Ground textures and light

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-005](ADR-005-terrain3d-for-terrain.md) (Terrain3D),
  [ADR-007](ADR-007-terrain3d-shader-patch-and-fallback-reason.md) (its shader) and
  [ADR-038](ADR-038-the-biomes-of-the-world.md) (biomes)

## Context

§61 asks for a stylized fantasy look, readable from high altitude, with distinct biomes. The owner
asked for much better graphics.

- **The ground:** one grey noise texture under Terrain3D, tinted by a colour map of biome colours.
  It read as flat colour up close, and biomes were told apart only by hue.
- **The light:** the sky, sun and fog were plain, with Filmic tonemapping and no ambient
  occlusion.

Terrain3D can blend up to 32 textures, two per sample, chosen by a control map. The base
texture's id is in bits 27–31 of each 32-bit word, the overlay's in bits 22–26, the blend in bits
14–21 and a UV turn in bits 10–13. Its shader multiplies the textures' colour by the colour map's.

## Decision

### Nine ground textures, made in code (`GroundTextures`)

Grass, forest floor, earth, rock, sand, snow, moss, mud and ash. Each is a 512 px tile:

- a pattern from seamless noise between its darker and lighter colour (grass in mottled patches
  with tufts, rock cracked between plates, pebbles in the earth, ripples in the sand, cushions of
  moss, puddles in the mud, cinders in the ash);
- its height in the albedo's alpha, which Terrain3D blends by;
- a normal map from that height, with its roughness in the alpha.

The colours are `Ground.Textures` in `biome_palette.json`. The textures are made once and kept
for every world after.

### The control map (`TerrainControl`, pure, in the core)

- **Base:** each sample's base is its biome's texture (sand under the sea).
- **Borders:** near a border, the commonest other texture within 6 m becomes the overlay, by its
  share (at most a half). Neighbouring biomes then meet in a gradual mix, not a hard line.
- **Rock, snow and sand:** these take the overlay wherever they show more, using the palette's
  existing thresholds as the colour map does:
  - rock between the rock slopes;
  - snow above the snow height;
  - sand fading in below the beach height.
- **Tiling:** every texture but rock turns at random by sixteenths, which hides the tiling.
- **Speed:** rows are worked out in parallel, with the same result.

### The tint

- **The colour map** brings each biome colour to the brightness of white and applies it at
  `TintStrength` (0.15). So the textures keep their own light and dark, and biomes keep a little
  of their hue.
- **Edges:** Terrain3D's `blend_sharpness` is lowered to 0.2 for softer edges.
- **The fallback renderer** keeps its vertex colours.

### The light (`Main.tscn`)

- **Tonemapping:** ACES, with exposure 1.05 and white 6, and a little more contrast.
- **Ambient occlusion:** screen-space, in the folds of the land and at the feet of houses.
- **Glow:** soft, from bright miracles and the shrine's crown.
- **Sky and sun:** a deeper sky with a softer sun disc, and softened sun shadows.
- **The setting:** "Lighting effects" on the settings page switches ambient occlusion and glow
  (on by default).

## Consequences

- **§61:** distinct biomes now show in texture and relief, not only in hue: green mottled meadows,
  dark forest floors, brown highland earth, cracked rock on mountains, snow on peaks, sand along
  coasts.
- **Cost:**
  - building visuals: 1.22 s on the default island (about 0.6 s before) and 2.57 s on the vast
    one (1.38 s); the textures are made once, so new worlds and reshaped land are faster after;
  - frame rate: a grown game at ×16 runs at 135 FPS on an M3 Pro, against 198 before, from
    sampling the textured ground. Ambient occlusion and glow cost nothing measurable (133 FPS
    without them).
- **Not done:** grass blades and ground clutter near the camera (the vegetation part), textures on
  the fallback renderer, and painted roads.
