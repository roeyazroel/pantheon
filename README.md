# PANTHEON

An original 3D god game for Windows, macOS and Linux, built with Godot 4.7.2 (.NET) and C#. It
runs fully offline.

**Milestone 1 (Foundation)** generates a seeded island with Grassland, Forest and Highland
biomes and the surrounding sea. A grid of 32 × 32 regions covers it, with one living settlement.
You can explore it with a free god camera and save or load the world. A debug overlay shows
what the simulation is doing.

## Requirements

- [Godot 4.7.2-stable, .NET edition](https://godotengine.org/download/archive/4.7.2-stable/)
  (pinned by [ADR-001](docs/ADR/ADR-001-godot-dotnet-pinned-version.md)).
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
- A GPU with Vulkan support (the project uses the Forward+ renderer).

The Terrain3D GDExtension is vendored in `game/addons/terrain_3d`; nothing else needs to be
installed.

## Getting started

```bash
# Once after cloning: imports assets and registers the Terrain3D extension.
godot --headless --path game --import

dotnet build Pantheon.sln
godot --path game
```

`godot` stands for the Godot 4.7.2 .NET executable. You can also open `game/project.godot` in the
Godot .NET editor and press F5. If you skip the import step, the first run cannot load
Terrain3D. In that case it uses the fallback terrain renderer and labels it in the debug overlay.

## Tests

```bash
dotnet test Pantheon.sln
```

The xUnit suite in `src/Pantheon.Tests` runs headless and does not need the Godot executable. It
covers:

- deterministic world generation;
- save/load round trips and save versioning;
- the seeded RNG and the fixed-step tick scheduler;
- camera maths, picking, content parsing, launch options and presentation helpers.

## Controls

| Action | Input |
| --- | --- |
| Pan | W A S D, arrow keys, or move the mouse to a screen edge |
| Grab and drag the ground | Hold the right mouse button and drag |
| Zoom (towards the cursor) | Mouse wheel, `=` / `-`, keypad `+` / `-` |
| Rotate | Q / E, or hold the middle mouse button and drag |
| Focus the settlement | F or Home |
| Toggle region boundaries | B |
| Toggle the debug overlay | F1 |
| New world from a random seed | F2 |
| Quicksave / quickload | F5 / F9 |
| Pause / resume the simulation | Space |
| Simulation speed ×1 / ×4 / ×16 | 1 / 2 / 3 |

The camera has three zoom bands, by distance from the point it looks at: **Close** (up to
120 m), **Regional** (up to 700 m) and **Strategic** (up to 2600 m). The simulation runs at a
fixed 10 ticks per second, independent of the frame rate. Choosing a speed also resumes a paused
simulation.

## Command-line options

Pass options after `--`, for example `godot --path game -- --seed=42 --terrain=fallback`.

| Option | Effect |
| --- | --- |
| `--seed=<number or text>` | Seed of the generated world (text is hashed). Default: `1234`. |
| `--load=<path>` | Load a save at startup, for example `user://saves/quicksave.json`. If it fails, a new world is generated. |
| `--terrain=auto\|terrain3d\|fallback` | Terrain renderer. `auto` (the default) and `terrain3d` use Terrain3D when it is available; `fallback` forces the built-in mesh renderer. |
| `--quit-after-frames=<n>` | Quit after `n` frames (smoke tests). |
| `--screenshot=<file.png>` | Together with `--quit-after-frames`, save a screenshot just before quitting (not in headless mode). |

Unknown options and invalid values produce a warning and are otherwise ignored.

## Saves

F5 writes the world to `user://saves/quicksave.json` and F9 loads it back. In a project run
(not an export), `user://` is:

- Linux: `~/.local/share/godot/app_userdata/PANTHEON/`
- Windows: `%APPDATA%\Godot\app_userdata\PANTHEON\`
- macOS: `~/Library/Application Support/Godot/app_userdata/PANTHEON/`

Saves are indented JSON of the simulation state, starting with `"SaveVersion": 1`. The scene tree
is never saved. The terrain is regenerated from the seed and loading rebuilds every visual
([ADR-006](docs/ADR/ADR-006-simulation-state-separate-from-scenes.md)).

## Repository layout

```text
game/                 Godot project: scenes, client scripts (Pantheon.Client), data, materials, shaders
  Data/               worldgen.json, settlement_rules.json, biome_palette.json
  addons/terrain_3d/  Vendored Terrain3D 1.0.2 runtime (reproduce with tools/vendor-terrain3d.sh)
src/
  Pantheon.Domain/        World, region and settlement state and content contracts
  Pantheon.Simulation/    Seeded RNG, noise, terrain and world generation, tick systems
  Pantheon.Persistence/   Versioned JSON saves and content parsing
  Pantheon.Game/          Engine-agnostic session, scheduler, camera model and readouts
  Pantheon.Networking/    Reserved for host-authoritative ENet networking (ADR-003)
  Pantheon.Tests/         xUnit tests
docs/                 Game spec, architecture and ADRs
.docs/                Milestone plans
```

Start with [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md),
[docs/GAME_SPEC.md](docs/GAME_SPEC.md) and the [ADRs](docs/ADR/README.md). The Milestone 1 plan
and its status are in [.docs/milestone-1-foundation.md](.docs/milestone-1-foundation.md).

## Known issues

- Every start logs `WARNING: instance_reset_physics_interpolation() is deprecated`. Terrain3D
  1.0.2 calls this deprecated Godot 4.7 function, and the warning is harmless.
- Without a GPU driver, Godot falls back to Mesa's software Vulkan driver (lavapipe), which
  manages only a few frames per second. Terrain3D 1.0.2's shader crashes that driver. PANTHEON
  patches the shader at runtime to prevent this
  ([ADR-007](docs/ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md)). If a different Terrain3D version
  cannot be patched, a warning is logged; use `--terrain=fallback` on such machines.
- macOS may block the unsigned Terrain3D binaries until they are removed from quarantine. See
  Terrain3D's platform documentation.
- Export presets are not part of Milestone 1. The game reads `res://Data/*.json` at startup, so
  every export must include those files.

## Third-party code

- [Terrain3D](https://github.com/TokisanGames/Terrain3D) 1.0.2: MIT licence,
  `game/addons/terrain_3d/LICENSE.txt`.
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) 1.1.1: MIT licence, header of
  `src/Pantheon.Simulation/Noise/FastNoiseLite.cs`.
