# ADR-007: Patch Terrain3D's generated shader and report why the fallback is used

- Status: Accepted
- Date: 2026-10-18
- Amends: [ADR-005](ADR-005-terrain3d-for-terrain.md)

## Context

ADR-005 renders terrain with Terrain3D 1.0.2 behind an adapter. Its fallback mesh renderer is
"logged as a warning and shown as 'FALLBACK MESH' in the debug overlay". Verifying Milestone 1
found two defects in the shader that Terrain3D 1.0.2 generates:

- **Crash on software Vulkan.** `get_index_coord` reads
  `_region_map[pos.y * _region_map_size + pos.x]` before it discards positions outside the region
  map. Godot uses Mesa's software Vulkan driver (lavapipe) on machines without a GPU driver, and
  lavapipe does not bounds-check uniform arrays. Core dumps showed the llvmpipe rasterizer reading
  about 4 GB past the uniform buffer (lane index `0x3ffffe90`) and crashing with SIGSEGV, both in
  interactive use and in a scripted stress loop.
- **White line along the world's edge.** The colour map lookup (`get_index_uv(UV2)`) is offset by
  half a texel. The last half texel along the east and south edges of the outermost regions
  therefore finds no region and gets `COLOR_MAP_DEF`, which is white. With `world_background` set
  to NONE and 2 m texels, that is a 1 m pale strip, visible through the sea on every GPU.

Both defects are in Terrain3D's code, and no Terrain3D release fixes them yet.

The fallback renderer is also used for two different reasons: Terrain3D is unavailable (missing
or incompatible binaries, a skipped asset import, an unsupported platform), or the player forced
the fallback with `--terrain=fallback`. A bare "FALLBACK MESH" label does not say which, so a
broken setup looks the same as a deliberate choice.

## Decision

- `Terrain3DTerrainPresenter` renders Terrain3D with a patched copy of its generated shader:
  1. It sets every material setting, then enables the material's shader override, which copies
     the code Terrain3D generated for those settings.
  2. It applies `Terrain3DShaderPatch.Apply` (engine-agnostic, in `Pantheon.Game`) and assigns
     the result.
  3. If either expected lookup is missing, it disables the override, keeps the stock shader and
     logs a warning.
- The patch makes exactly two textual replacements:
  - Every region map read clamps its index to the array bounds. Positions outside the region map
    are still discarded afterwards, so every result the shader keeps is unchanged.
  - When the colour map lookup finds no region, the colour comes from the colour map texel under
    the pixel (`index[3]`, which `get_index_coord` searches inwards for when there is no world
    background). Only if that also fails does it use the white default.
- The fallback renderer carries its reason, "Terrain3D unavailable" or "forced by
  --terrain=fallback". The log states it (as a warning when Terrain3D is unavailable), the
  overlay's terrain line shows it, and a "FALLBACK TERRAIN RENDERER" badge stays visible even
  when the debug panel is hidden. This replaces ADR-005's "FALLBACK MESH" label.
- The rest of ADR-005 stands.

## Consequences

- Positive: Terrain3D runs on lavapipe. Before the patch, both stress runs crashed; with it, 24
  stress cycles ran without a crash. The world's edges now match the fallback renderer, and
  captures are otherwise unchanged apart from the animated water.
- Positive: the patch is plain C# with unit tests that pin the exact lookups it expects from
  Terrain3D 1.0.2, so it is tested without Godot.
- Positive: a broken Terrain3D setup and a forced fallback are told apart at a glance.
- Negative: PANTHEON depends on the text of Terrain3D's generated shader. Every Terrain3D upgrade
  must re-check the patch; an unrecognised shader is reported at startup and runs unpatched.
- Negative: the override is a snapshot of the code for the current material settings. Changing a
  setting that alters the generated code later would need the patch applied again.
- Negative: where the patch cannot be applied, software Vulkan can still crash. The README's
  known issues give `--terrain=fallback` as the workaround.

## Alternatives considered

- Always use the fallback renderer on software Vulkan: avoids the crash, but not the edge line. It
  also needs driver detection and gives up Terrain3D's level of detail where performance is
  lowest.
- Fork Terrain3D and ship rebuilt native binaries: fixes the source, but means maintaining a C++
  build for three platforms. That is infrastructure without need for two lines of shader code.
- Hide the edge strip with Terrain3D's FLAT world background: the background plane at sea level
  z-fights with the water.
- Wait for an upstream fix: leaves the crash and the line in the game until then. Reporting both
  defects upstream is a follow-up, and the patch is removed once a fixed release is vendored.
