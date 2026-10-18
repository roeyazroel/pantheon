# PANTHEON

An original 3D god game for Windows, macOS and Linux, built with Godot 4.7.2 (.NET) and C#. It
runs fully offline.

The owner's complete game design and technical architecture specification,
[docs/PANTHEON_SPEC.md](docs/PANTHEON_SPEC.md), is the source of truth for what PANTHEON is and
how it is built. Where any other document disagrees with it, the specification wins.
[docs/PROGRESS_SUMMARY.md](docs/PROGRESS_SUMMARY.md) sums up what is built so far against the
specification, section by section, with the known gaps and the recommended next steps.

**Milestone 1 (Foundation)** generates a seeded island with Grassland, Forest and Highland
biomes and the surrounding sea. A grid of 32 × 32 regions covers it, with one settlement.
You can explore it with a free god camera and save or load the world. A debug overlay shows
what the simulation is doing.

**Milestone 2 (Living Settlement)** makes the settlement live on its own. Its villagers farm
the land around it, eat, fall ill when food runs short, are born and die, and build new houses
when they get crowded. You never place houses. Near the camera, a few hundred representative
citizens walk between their homes, the fields, the building site and the shrine. A new house
goes up as housing grows and fields spread around the village.

**Milestone 3 (First God)** makes you a god. You are Ilyra, a Minor Spirit followed by most of
one village on an island of eight. Your followers worship at their shrines, and their worship
gives you Divine Power. Spend it on miracles: Food, Heal, Bless Crops, Lightning and, once you
are a Lesser God, Mass Healing. You can only work miracles where your influence reaches, and
your influence spreads with your followers. People who see a miracle change their minds about
you. Help them when they need it and they love you. Strike them and they fear you; fear converts
quickly but does not last. Neglect your followers in a famine and they doubt you and leave.

## Requirements

- [Godot 4.7.2-stable, .NET edition](https://godotengine.org/download/archive/4.7.2-stable/)
  (pinned by [ADR-001](docs/ADR/ADR-001-godot-dotnet-pinned-version.md)).
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
- A GPU with Vulkan support, or Metal on macOS (the project uses the Forward+ renderer).

On macOS with Homebrew: `brew install --cask dotnet-sdk@8 godot-mono` installs both; the Godot
executable is then `godot-mono`.

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

- deterministic world generation, also across a save and load;
- save/load round trips, save versioning and the migration of Milestone 1 saves;
- the settlement rules: farming, eating, storage, starvation and recovery, births and deaths,
  growth up to housing, and building a house when crowded;
- the seeded RNG, the fixed-step tick scheduler, the settlement step cadence, the single-tick
  and settlement steps, and the famine and settler hotkeys;
- the representative citizens: tier selection, roles, homes, routines, smooth reroutes, and
  that showing them never changes the world;
- belief and miracles: drift, conversion, love against fear, worship and Divine Power, ranks,
  influence, every check on a cast (a god cannot cast without enough power), every miracle's
  effects and what witnesses make of them;
- the HUD model, alerts, map overlays and the migration of Milestone 2 saves;
- camera maths, picking, content parsing, launch options, the settlement layout and other
  presentation helpers;
- the debug panel readouts and the F3 region debug map;
- the architecture rules: each core library references only the layers below it and never
  Godot, core code uses no unseeded randomness or clock, and every vendored GDExtension ships
  Windows, macOS and Linux libraries.

## Controls

| Action | Input |
| --- | --- |
| Pan | W A S D, arrow keys, or move the mouse to a screen edge |
| Grab and drag the ground | Hold the right mouse button and drag |
| Zoom (towards the cursor) | Mouse wheel, `=` / `-`, keypad `+` / `-` |
| Rotate | Q / E, or hold the middle mouse button and drag |
| Select a miracle (again to deselect) | 1–9, or click it in the miracle bar |
| Cast the selected miracle at the cursor | Left mouse button |
| Deselect the miracle | Esc |
| Next map overlay (influence, faith, food), then off | O |
| Look at the next settlement | F or Home |
| Toggle region boundaries | B |
| Toggle the debug overlay | F1 |
| Next region debug map layer, then off | F3 |
| New world from a random seed | F2 |
| Quicksave / quickload | F5 / F9 |
| Pause / resume the simulation | Space |
| Simulation speed slower / faster (×1, ×4, ×16) | `[` / `]` |
| Pause and step the simulation by one tick | `.` |
| Pause and run to the next settlement step | `,` |
| Grant your god 500 Divine Power (debug) | F4 |
| Spoil every settlement's food stores (debug) | F6 |
| Settlers arrive: every settlement grows by half (debug) | F7 |

The camera has three zoom bands, by distance from the point it looks at: **Close** (up to
120 m), **Regional** (up to 700 m) and **Strategic** (up to 2600 m). The simulation runs at a
fixed 10 ticks per second, independent of the frame rate. Choosing a speed also resumes a paused
simulation.

## The living settlements

Each settlement is simulated as a whole (Tier 1, statistical): population, food, housing,
health and the house being built. It steps once per simulated second, every tenth tick, so ×4
and ×16 speed it up. Each step:

- Most inhabitants work. They farm the land regions around the settlement, with diminishing
  returns, so each site has its own limit. When the settlement is crowded and has food in
  store, some of them build the next house instead, one house at a time. Followers of a god
  spend part of their time worshipping instead of farming.
- Everyone eats. Surplus food is stored up to what the houses can hold, and the rest spoils.
- When food runs short, health falls with the share of people left hungry, and recovers once
  everyone is fed again.
- Children are born while there is free housing, a food surplus and stocked stores; fewer when
  health is low. People die at a low baseline rate, faster when health is low.

The island has eight settlements, founded on the best land at least five regions (320 m) apart
with 26 to 54 people each. With seed 1234 your home village, Duskwater, grows from 30 people to
about 390 in 15 simulated minutes (about a minute at ×16). Press F7 and then F6 to start a
famine. Food runs out, your followers' doubt rises, and unless you help them some turn away;
and if the land cannot feed the newcomers, health falls and deaths rise until it can.

Near the camera (within 450 m of a settlement), the settlement switches to Tier 2 and shows
representative citizens: one figure per inhabitant, up to 300, each standing for several
people in a larger settlement. Farmers (red) work in the fields, builders (amber) at the
building site and villagers (blue) do chores near home. Each follows a repeating round: rest by
the door, work, gather at the shrine, go home. The citizens are derived from the settlement's
numbers and never saved. Beyond 550 m the settlement shows only its houses, building site and
fields (Tier 1); the simulation is the same in both tiers. Balance values are in
`game/Data/settlement_rules.json` and presentation values (layout, colours, tier distances,
routines) in `game/Data/settlement_view.json`.

## Being a god

The bar at the top shows your god and rank, your Divine Power with its income per second, your
followers and the next rank. The miracle bar at the bottom shows each miracle's key and cost (or
its cooldown); hover over a button to read what it does.

- **Followers and power.** In every settlement, a share of the people follow you. Followers
  spend part of their working time at the shrine instead of in the fields, and their worship
  raises Divine Power: more followers, stronger faith, and love or fear all raise it.
- **Influence.** Your influence spreads from the settlements that follow you, further the more
  followers they have; press O to see it. A miracle needs influence at the target and costs more
  where your influence is weak.
- **Casting.** Select a miracle and move the cursor: a ring shows its radius, green where you can
  cast it and red where you cannot, and the hint names the settlements it would reach, its cost,
  or what stands in the way. Click to cast.
- **What people think.** Faith (they believe you exist), Love, Fear and Doubt drift every
  second. Comfortable followers grow fond of you. Hunger and illness breed doubt and wear down
  love, and fear fades in safety. People who follow no god join you through faith, love and fear,
  and doubt drives them away; fear works fast, love holds. Miracles change the minds of everyone
  who sees them, and help that was needed earns more love. Strangers who see a miracle start to
  believe.
- **Ranks.** At 100 followers you become a Lesser God, then a God at 400, a Greater God at 1,200,
  a High God at 3,000 and Ascendant at 8,000. You never lose a rank. Higher ranks unlock
  miracles.
- **Alerts** on the right tell you when a settlement that follows you starves, when a settlement
  comes to follow you or turns away, and when you reach a rank. Click one to look there.

A good start: Food or Heal on the neighbours of your home village, then Bless Crops when your
followers grow hungry. Try Lightning to see how fear behaves.

## Debug tools

- **F1** shows the debug panel (hidden at the start): frame rate, tick, seed, terrain renderer,
  camera band, one line per god (power and income, rank, followers, the next rank), the region
  under the cursor with its statistics and every god's influence there, one line per settlement,
  and, for the settlement nearest the cursor:
  - its belief: the share that follows no god, its hardship and comfort, and for each god the
    followers, Faith, Love, Fear, Doubt, worshippers and the power they raise;
  - population / housing, houses and health;
  - food stored / storage, the net change per second, production and consumption;
  - the jobs (farming, building, not working) and the farmland;
  - expected births and deaths per second, the house under construction and the time to the
    next settlement step;
  - the simulation tier, the camera distance that changes it and, in Tier 2, the citizens
    shown and the people each stands for;
  - warnings when the settlement starves, construction waits for food, or births are blocked.
- **B** draws the region grid and outlines the region under the cursor.
- **F3** shows the region debug map in the bottom-left corner, one pixel per region, oriented
  like the camera's starting view. Each press shows the next layer: biome, elevation,
  temperature, moisture, fertility, vegetation, then off. Numeric layers run from dark (the
  lowest value) to bright (the highest), and the legend gives the range. The region under the
  cursor is white and settlement regions are red. The map stays up when F1 hides the panel. After
  vegetation come blessing and your god's influence, both on a fixed 0 to 1 scale.
- **O** cycles the map overlays: influence, faith and food.
- **F4** grants your god 500 Divine Power.
- **`.`** pauses the simulation and runs exactly one tick, so the panel shows what a single
  tick changed. Space resumes.
- **`,`** pauses and runs the ticks up to and including the next settlement step, so the panel
  shows what one step changed.
- **F6** spoils every settlement's food stores and **F7** brings settlers (every settlement
  grows by half, at least 10 people). Use them to drive famines and recoveries.
- The command-line options below reproduce a world (`--seed`, `--load`), force a renderer
  (`--terrain`), start from a grown settlement up close (`--advance-ticks`,
  `--camera-distance`) and script smoke runs (`--quit-after-frames`, `--screenshot`).
  Unattended runs end by logging one summary line per settlement, with its tier and the
  citizens shown.

## Command-line options

Pass options after `--`, for example `godot --path game -- --seed=42 --terrain=fallback`.

| Option | Effect |
| --- | --- |
| `--seed=<number or text>` | Seed of the generated world (text is hashed). Default: `1234`. |
| `--load=<path>` | Load a save at startup, for example `user://saves/quicksave.json`. If it fails, a new world is generated. |
| `--terrain=auto\|terrain3d\|fallback` | Terrain renderer. `auto` (the default) and `terrain3d` use Terrain3D when it is available; `fallback` forces the built-in mesh renderer. |
| `--advance-ticks=<n>` | Run `n` simulation ticks (10 per simulated second) before the first frame, for example `--advance-ticks=9000` for a settlement of about 425 people. |
| `--camera-distance=<m>` | Starting camera distance in metres (default 380, clamped to 25–2600). |
| `--quit-after-frames=<n>` | Quit after `n` frames (smoke tests). |
| `--screenshot=<file.png>` | Together with `--quit-after-frames`, save a screenshot just before quitting (not in headless mode). |
| `--divine-power=<n>` | Your god starts with `n` Divine Power. |
| `--cast=<id>[,<id>...]` | Cast these miracles (ids from `miracles.json`) on the focused settlement at the start. |
| `--select=<id>` | Start with a miracle selected; scripted runs show its ring at the screen centre. |
| `--overlay=influence\|faith\|food` | Start with a map overlay shown. |
| `--focus=<n>` | Start looking at settlement `n` (1 is your home); `--cast` targets it. |
| `--debug-panel=on\|off` | Show or hide the F1 debug panel at the start. |

Unknown options and invalid values produce a warning and are otherwise ignored.

## Saves

F5 writes the world to `user://saves/quicksave.json` and F9 loads it back. In a project run
(not an export), `user://` is:

- Linux: `~/.local/share/godot/app_userdata/PANTHEON/`
- Windows: `%APPDATA%\Godot\app_userdata\PANTHEON\`
- macOS: `~/Library/Application Support/Godot/app_userdata/PANTHEON/`

Saves are indented JSON of the simulation state, starting with `"SaveVersion": 3`. The scene tree
is never saved. The terrain is regenerated from the seed and loading rebuilds every visual
([ADR-006](docs/ADR/ADR-006-simulation-state-separate-from-scenes.md)). The representative
citizens are not saved either; they are derived again after a load. Saves from Milestone 1
(version 1) still load: each settlement starts healthy with no house under construction. Saves
from Milestones 1 and 2 (versions 1 and 2) get the Milestone 3 god, Ilyra, followed in their one
settlement.

## Repository layout

```text
game/                 Godot project: scenes, client scripts (Pantheon.Client), data, materials, shaders
  Data/               worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                      settlement_view.json, biome_palette.json, divine_view.json
  addons/terrain_3d/  Vendored Terrain3D 1.0.2 runtime (reproduce with tools/vendor-terrain3d.sh)
src/
  Pantheon.Domain/        World, region, settlement, god and belief state, events, and content contracts
  Pantheon.Simulation/    Seeded RNG, noise, terrain and world generation, the settlement and belief models,
                          influence, miracles, tick systems
  Pantheon.Persistence/   Versioned JSON saves with migrations, and content parsing
  Pantheon.Game/          Engine-agnostic session (the local authority), scheduler, camera model, readouts,
                          HUD model, alerts, overlays, settlement layout and representative citizens
  Pantheon.Networking/    Reserved for host-authoritative ENet networking (ADR-003)
  Pantheon.Tests/         xUnit tests
docs/                 The owner's specification (PANTHEON_SPEC.md, the source of truth), the progress
                      summary (PROGRESS_SUMMARY.md), the vision summary (GAME_SPEC.md), architecture
                      and ADRs
.docs/                Milestone plans
```

Start with the specification, [docs/PANTHEON_SPEC.md](docs/PANTHEON_SPEC.md), and the progress
summary, [docs/PROGRESS_SUMMARY.md](docs/PROGRESS_SUMMARY.md), then
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/GAME_SPEC.md](docs/GAME_SPEC.md) (a short
vision summary) and the [ADRs](docs/ADR/README.md). The milestone plans and their status are in
[.docs/milestone-1-foundation.md](.docs/milestone-1-foundation.md),
[.docs/milestone-2-living-settlement.md](.docs/milestone-2-living-settlement.md) and
[.docs/milestone-3-first-god.md](.docs/milestone-3-first-god.md).

## Contributing

Read [AGENTS.md](AGENTS.md) before changing anything, whether you are a person or a coding
agent. It lists the owner's binding coding rules, where code goes, the build and test commands
and the definition of done.

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
- Export presets are not set up yet. The game reads `res://Data/*.json` at startup, so every
  export must include those files.
- The representative citizens are presentation only: they show what the settlement's numbers
  say, but nothing can interact with an individual citizen until a later milestone adds full
  (Tier 3) agents ([ADR-008](docs/ADR/ADR-008-settlement-step-and-representative-citizens.md)).

## Third-party code

- [Terrain3D](https://github.com/TokisanGames/Terrain3D) 1.0.2: MIT licence,
  `game/addons/terrain_3d/LICENSE.txt`.
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) 1.1.1: MIT licence, header of
  `src/Pantheon.Simulation/Noise/FastNoiseLite.cs`.
