# Developing PANTHEON

This is the guide for building, testing and changing PANTHEON. If you only want to play, the
[README](../README.md) is all you need.

Read [AGENTS.md](../AGENTS.md) before changing anything, whether you are a person or a coding
agent: it lists the owner's binding coding rules, where code goes, the build and test commands
and the definition of done. The owner's specification, [PANTHEON_SPEC.md](PANTHEON_SPEC.md), is
the source of truth for what PANTHEON is and how it is built; where any other document disagrees
with it, the specification wins. [PROGRESS_SUMMARY.md](PROGRESS_SUMMARY.md) sums up what is built
so far against the specification, section by section, with the known gaps and the recommended
next steps. Then read [ARCHITECTURE.md](ARCHITECTURE.md), [GAME_SPEC.md](GAME_SPEC.md) (a short
vision summary) and the [ADRs](ADR/README.md). The milestone plans and their status are in
`.docs/`, from [.docs/milestone-1-foundation.md](../.docs/milestone-1-foundation.md) to
[.docs/milestone-15-part-54-builds-in-ci.md](../.docs/milestone-15-part-54-builds-in-ci.md).

- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Building releases](#building-releases)
- [Tests](#tests)
- [Debug tools](#debug-tools)
- [Command-line options](#command-line-options)
- [Taking the README's screenshots](#taking-the-readmes-screenshots)
- [Where the numbers live](#where-the-numbers-live)
- [Saves and the profile](#saves-and-the-profile)
- [Repository layout](#repository-layout)
- [Known issues](#known-issues)

## Requirements

- [Godot 4.7.2-stable, .NET edition](https://godotengine.org/download/archive/4.7.2-stable/)
  (pinned by [ADR-001](ADR/ADR-001-godot-dotnet-pinned-version.md)).
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0), feature band 8.0.4xx (CI uses
  8.0.426), or newer. The older 8.0.1xx band, which some Linux distributions package, fails to
  compile a collection expression passed to `string.Split` (CS0121); a .NET 9 or 10 SDK builds it.
- A GPU with Vulkan support, or Metal on macOS (the project uses the Forward+ renderer).

On macOS with Homebrew: `brew install --cask dotnet-sdk@8 godot-mono` installs both; the Godot
executable is then `godot-mono`.

The Terrain3D GDExtension lives in `game/addons/terrain_3d`, but its native libraries are not
committed: `tools/vendor-terrain3d.sh` fetches them from the pinned, checksummed release
([ADR-075](ADR/ADR-075-builds-in-ci-and-fetched-libraries.md)). Nothing else needs to be
installed.

## Getting started

```bash
# Once after cloning: fetches Terrain3D's libraries, imports assets and registers the extension.
tools/vendor-terrain3d.sh
godot --headless --path game --import

dotnet build Pantheon.sln
godot --path game
```

`godot` stands for the Godot 4.7.2 .NET executable. You can also open `game/project.godot` in the
Godot .NET editor and press F5. Started without options, the game opens on its title screen; the
[README](../README.md#your-first-game) describes it from the player's side. If you skip either of
the first two steps, the first run cannot load Terrain3D. In that case it uses the fallback
terrain renderer and labels it in the debug overlay.

## Building releases

GitHub Actions builds the releases ([ADR-075](ADR/ADR-075-builds-in-ci-and-fetched-libraries.md)).
The Build workflow (`.github/workflows/build.yml`) builds and tests every push to `main` on Linux.
To export the game, run it by hand (Actions > Build > Run workflow) or push a version tag:

```bash
git tag v0.1.0 && git push origin v0.1.0
```

Each platform is exported on its own system, and its package is on the run's page:

| Package | Contents |
| --- | --- |
| `Pantheon-windows-x86_64.zip` | `Pantheon.exe`, its `.pck`, the .NET assemblies and Terrain3D's library |
| `Pantheon-macos-universal.zip` | `Pantheon.app`, one app for Apple Silicon and Intel, signed ad hoc |
| `Pantheon-linux-x86_64.tar.gz` | `Pantheon.x86_64`, its `.pck`, the .NET assemblies and Terrain3D's library |

A tag also publishes the three as a GitHub release. The macOS app is signed ad hoc, so macOS
quarantines it when it is downloaded. Before the first launch, clear the quarantine with
`xattr -dr com.apple.quarantine Pantheon.app`, or use Privacy & Security > Open Anyway. To sign it
properly, use a developer identity and notarize it (the preset's `codesign` and `notarization`
options, with the owner's Apple account as CI secrets).

To export locally, `game/export_presets.cfg` builds Windows x64, macOS and Linux x64 into `build/`.
It needs the Godot 4.7.2 .NET export templates: in the editor, Editor > Manage Export Templates >
Download, or run `tools/install-godot.sh --dest <directory>`, which installs the editor and the
templates for the system it runs on. Then:

```bash
godot --headless --path game --export-release "Windows Desktop" ../build/windows/Pantheon.exe
godot --headless --path game --export-release "macOS" ../build/macos/Pantheon.zip
godot --headless --path game --export-release "Linux" ../build/linux/Pantheon.x86_64
```

The macOS export must run on a Mac, since Apple's `codesign` signs it.

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
- the Creature: learning from rewards and punishments (spec §25's example), choices from needs,
  preferences and personality, walking around the sea, deeds and their effect on villages and
  belief, levels, banishment and the checks on commands;
- rival gods: conversion between gods, contested costs, the god AI's choices for each personality,
  dominance and victory;
- four gods: blocs of allies and their joint victory, allies' memory of wrongs, coalitions against
  a leader, fresh peace, betrayal near a joint victory and the new personalities;
- diplomacy: what each act does to a god's view of another and how long it is remembered,
  friendly and hostile relations, alliances, betrayal, war and peace through the authority's
  checks, what each status changes, and the AI's diplomatic choices with their reasons;
- weather: cells that rain, refill over the sea and drift on the wind, spells, soil water,
  droughts and storms, the Rain miracle and when the AI calls it, and that the weather is the same
  every time and across a save and load;
- disasters: earthquakes and their damage, wildfires that grow, spread with the wind and burn
  out, rain and firefighting, ruins and rebuilding, the homeless, fear of the gods, the Fire,
  Earthquake and Shield miracles and how the AI uses them;
- migration: what pushes people out and what keeps them, the wish to leave, groups setting out,
  the gods they give up, where they go, the road, welcome, conversion and refusal, and refugee
  policies for you and the AI;
- Creatures together: the views they hold of each other, meetings, play, greetings, flight,
  betrayal, fights and how they end (banishment, scars, loyalty, experience, mercy, draws), what
  gods teach their Creatures about others and when AI gods send them against an enemy's;
- seasons and the land's life: the season from the tick, seasons and waves combining, what each
  season does to harvests, stores, health and migration, snow lying and melting, fog and
  hurricanes, harm healing into scars and Nature's healing, wildlife and hunting, pollution, the
  words, alerts, history, readouts and overlay, and the migration of part 7's saves;
- the Book of the World's four pages, the population, disaster risk and political control
  overlays, the strategic overview, the book's options and the export presets;
- the Creature's paths and attributes, the Divine Hand (rescues, abductions, carrying, drowning,
  throwing, trees, the Creature in the hand, every refusal, through a host) and the Divine Acts
  (hallowing, waking the volcano, stilling every disaster, the rank and power they need), their
  alerts, history, card, panel and options, and the migration of part 9's saves;
- prophets and elders: elders chosen, keeping faith and turning, prophets called and anointed up to
  each god's limit, sermons, walking, martyrs, danger remembered, temples and prophets as
  influence, the same notables for the same world, their alerts, history, book, readout, option,
  save and section, the checks on their rules, and the migration of part 11's saves;
- wood, stone and roads: forests and stone from the seed, gathering and what it takes from the
  land, houses and buildings that wait for wood and stone, timber growing back and burning,
  wildlife leaving cleared forests, settlers' stores, roads planned, built with stone, sped along,
  carrying trade and food, cut by floods and mended, the trees and the road drawn, their alerts,
  panel, book, readouts, options, save and sections, the checks on their rules, and the migration of
  part 12's saves;
- great events: the plague's ripeness, course, spread, shields and cure, the great plague and the
  council it calls, eclipses and their dread and awe, reformations, world wars, great droughts and
  great migrations, the same world across a save, their alerts, history, bar, book, labels,
  readouts, commands, options and section, the checks on their rules, and the migration of part 13's
  saves;
- the beast and the anomaly: the beast's lair and name, its waking, its ravages, Creatures and
  miracles fighting it, its fall and the glory, its going home, the AI gods sending their Creatures,
  the anomaly's power and cheaper miracles, their alerts, history, commands and section, the checks
  on their rules, and the migration of part 14's saves;
- hazards: the volcano, its lava and ash, waves from coastal quakes and sea walls, landslides and
  the rubble that blocks the way, meteors, tornadoes, locusts and rain, blight and what cures it,
  evacuation routes, dams and irrigation, marks fading, the same hazards across a save and load,
  their words, alerts, history, readouts, overlay, cover and commands, and the migration of part
  8's saves;
- world sizes and victories: the shipped sizes and what a larger one adds, the same world for the
  same seed and size, the checks on `world_sizes.json`, conquest, culture and the sandbox, the
  first victory being the only one, how each victory is told (alerts, history, end screen, HUD),
  the AI's influence cache and turns, labels giving way, the camera's reach, the options and the
  migration of part 6's saves;
- war and intrigue: war aims, scores, goals and dictated peace, what gods know of each other,
  veiled miracles and their tracing, proxy acts, holy wars and their gods' word, the panels,
  alerts, history and secrets kept from other players;
- treaties and the council: each treaty's effect, terms, breaking, secrets and their leaks,
  defensive pacts, tributes, favours, requests and promises, absorbing an ally, what summons the
  council, public and secret ballots, decrees and defiance, the AI's offers, answers and votes,
  the panels, alerts, history, and secrets kept from other players;
- culture: doctrines and what they follow, secularism and its stages, dependency and resentment,
  missionaries, charity, revolts, the traits gods earn by their acts, what witnesses take from a
  miracle, and how culture is shown (panel, HUD, diplomacy panel, alerts, history, overlay);
- the world's history (what is significant, how it reads), the end screen, the guide, the list of
  saves and that a pantheon wins as a god and its strongest ally;
- domains (slots by rank, the authority, cheaper miracles, every boon and every signature miracle,
  the AI's choices), holy sites (where they are found, claims, keepers' gains, the AI's
  Creatures), schisms (splits, the Old Faith's return or fading, betrayal doubt, sanctuaries),
  Creature species, the settings file, the synthesized sounds and music and what the world sounds
  like, and the migration of Milestone 14 saves;
- multiplayer: the messages and their encoding, a host and its players over an in-memory network
  (joining, gods, refusals, intents checked by the host, snapshots, events, leaving), each part of
  the world reaching players at its own rate, four players, coming back to one's god, and that only
  the host saves, keeps the clock or uses the debug tools;
- the HUD model, alerts, map overlays, the diplomacy panel, the weather's and disasters' looks and
  tools, migration's walkers, overlay and alerts, Creatures' bonds on the card, panel and debug
  panel, and the migration of Milestone 1 to 10 saves;
- Milestone 15's later parts (16 to 36): known gods and plans, Creature temperament, the new
  biomes, offerings, the followed villager, the stress scenario and performance tools, reshaping
  the land, local navigation and bridges, the Creature's state machine and behaviour trees,
  structures and debris, people and their motions, streaming, the ground, village architecture,
  trees, ground cover and wildlife, the Creature's face and the miracles' looks, the CPU budget and
  graphics scaling, regions sent within a tolerance, the hand's swing and the camera above the
  roofs, and the migration of every later save version;
- camera maths, picking, content parsing, launch options, the settlement layout and other
  presentation helpers;
- the debug panel readouts and the F3 region debug map;
- the architecture rules: each core library references only the layers below it and never
  Godot, core code uses no unseeded randomness or clock, and every vendored GDExtension has
  Windows, macOS and Linux libraries (run `tools/vendor-terrain3d.sh` first).

## Debug tools

The debug tools need Settings, under Interface, to turn them on; a game run from the project has
them on (ADR-068). The god's bar's Debug button opens a palette with a button for each of the keys
below that change the world. While they are on, the council panel offers Summon now (J), and an
unmet god's card offers Bring its Creature (K). An exported game has them off unless Settings
turns them on.

| Action | Key |
| --- | --- |
| Toggle region boundaries | Shift+B |
| Toggle the debug overlay | F1 |
| Next region debug map layer, then off | F3 |
| New world from a random seed (small, won by Divine Ascendancy, with four seasons) | F2 |
| Pause and step the simulation by one tick | `.` |
| Pause and run to the next settlement step | `,` |
| Grant your god 500 Divine Power | F4 |
| Spoil every settlement's food stores | F6 |
| Settlers arrive: every settlement grows by half | F7 |
| Start the next weather spell: normal, dry, wet, heat wave, cold wave | F8 |
| Call a storm at the cursor, short of a hurricane | F10 |
| Begin the next season | Y |
| Start the next hazard at the cursor: tornado, landslide, meteor, blight, locusts, tsunami, eruption | I |
| Start the next great event: the plague at the village nearest the cursor, an eclipse, your faith's reformation, the beast's waking, the anomaly at the cursor | ; |
| Start an earthquake at the cursor | F11 (fn+F11 on Mac laptops) |
| Start a fire at the cursor | F12 (fn+F12 on Mac laptops) |
| Flood the nearest river, or send a wave over the land at the cursor | L |
| Send 20 people out of the village under the cursor | M |
| Bring the nearest other Creature to yours, so they meet | K |
| Summon the Divine Council now, on the oldest tabled motion or to condemn your strongest rival | J |

The simulation runs at a fixed 10 ticks per second, independent of the frame rate.

With seed 1234 the home village, Duskwater, grows from 30 people to about 390 in 15 simulated
minutes (about a minute at ×16). Press F7 and then F6 to start a famine. Food runs out, your
followers' doubt rises, and unless you help them some turn away; and if the land cannot feed the
newcomers, health falls and deaths rise until it can.

Each settlement is simulated as a whole (Tier 1, statistical) and steps once per simulated
second, every tenth tick. Near the camera (within 450 m of a settlement), it switches to Tier 2
and shows representative citizens: one person per inhabitant, up to 300, each standing for
several people in a larger settlement (ADR-049). They walk on a clock of their own that stops
when the game is paused, and the F1 panel shows it; they find their way on the village's local
navigation grid (ADR-046). The citizens are derived from the settlement's numbers and never
saved. Beyond 550 m the settlement shows only its houses, building site and fields (Tier 1); the
simulation is the same in both tiers. A followed villager (/) becomes one of the world's
individuals, simulated in full wherever the camera is (ADR-043), but changes nothing the world
counts.

- **F1** shows the debug panel (hidden at the start): frame rate, tick, seed with the island's
  width and the world's victories ("Seed 42   4096 m island   Ascendancy, conquest and cultural
  victory"), terrain renderer,
  camera band, the performance metrics (frame time, the simulation's share of a frame and a tick's
  time, ticks per second, memory, the engine's objects, nodes and draw calls, the citizens drawn,
  the last save's times, and the simulation budget: how often it cut a frame short and the ticks
  waiting and dropped) and the world's load (settlements, people, gods, Creatures, weather cells,
  disasters, groups on the road, notables and followed villagers), one line per god (power and income, rank, followers, the next rank), each AI god's
  goal now, its persona (who wrote it, and its temper in numbers, ADR-065) and its top candidate actions with their scores and reasons, its plans (the goals it
  holds with their weights and ages, when it plans next, the gods it knows) and the goals it is
  weighing, its diplomatic moves with the factors behind them, the climate (the season and what it and a heat or cold wave
  change), the weather (the spell, the wind and the strongest cells),
  disasters (earthquakes and the fiercest fires), hazards (the volcano, tornadoes, swarms, blighted
  and buried regions, and marks on the land), migration (the groups on the road and every
  god's refugee policy with the AI's reason), the Creature
  view (level, health, loyalty, morality, attributes, paths and callings, activity, its state and
  behaviour tree, its face (mood, joy, anger, fear, sleep) and nature, needs, personality, its best options with
  their scores, how it would answer the nearest Creature, its combat skill, scars and power, its
  view of every Creature it has met with their latest encounters, its learned preferences by
  situation and its latest memories), every followed villager with what they do and their faith,
  and for yours their needs, options with their reasons, danger and last memory, the land's
  reshapings, the region
  under the cursor with its statistics, soil, rain, wind, snow, wildlife, harm, scar, pollution and
  biodiversity and every god's influence there, one
  line per settlement, and, for the settlement nearest the cursor:
  - its belief: the share that follows no god, its hardship and comfort, and for each god the
    followers, Faith, Love, Fear, Doubt, worshippers and the power they raise;
  - population / housing, houses and health;
  - food stored / storage, the net change per second, production and consumption;
  - the jobs (farming, building, not working) and the farmland;
  - expected births and deaths per second, the house under construction and the time to the
    next settlement step;
  - the simulation tier, the camera distance that changes it and, in Tier 2, the citizens
    shown and the people each stands for, and the local navigation grid's cells, blocked cells and
    ways searched;
  - its water: its farmland's soil, the drought and the harvest it allows, and the weather over it;
  - its dangers: the fire threat, the shaking, its ruins, the homeless, scorched land and any shield;
  - its migration: the pressure to leave and its cause, the people wanting to go and those on
    their way there;
  - warnings when the settlement starves, construction waits for food, or births are blocked.

  F1 also shows your angel (ADR-074):
  - its name and stage, its lean, how formed and how torn it is, the kindness and harm behind it,
    and its marks;
  - who speaks for it, with the questions this hour and the tokens;
  - the moments waiting;
  - its last words and the deeds it urged.

  Its log lines begin "Angel:".
- **B** draws the region grid and outlines the region under the cursor.
- **F3** shows the region debug map in the bottom-left corner, one pixel per region, oriented
  like the camera's starting view. Each press shows the next layer: biome, elevation,
  temperature, moisture, fertility, vegetation, then off. Numeric layers run from dark (the
  lowest value) to bright (the highest), and the legend gives the range. The region under the
  cursor is white and settlement regions are red. The map stays up when F1 hides the panel. After
  vegetation come blessing, your god's influence, the soil's water, fire, scorch, fresh water,
  flood, snow, wildlife, damage, scar, pollution, blight, rubble, timber and stone, all on a fixed 0 to 1 scale.
- **O** cycles the map overlays: influence, religion, faith, food, weather, danger, migration,
  culture, nature, population, disaster risk, political control, navigation and authority:
  - **Navigation** shows the land walkers may cross, the open roads, the land buried in rubble they
    go round, and the ways every migrant group, prophet, followed villager and Creature is on.
  - **Authority** shows who commands the god each village follows: you in gold, another player in
    blue, the AI in grey (ADR-044).
- **K** brings the nearest other Creature beside yours, so they meet at once and you can watch them
  play, run or fight.
- **F4** grants your god 500 Divine Power.
- **`.`** pauses the simulation and runs exactly one tick, so the panel shows what a single
  tick changed. Space resumes.
- **`,`** pauses and runs the ticks up to and including the next settlement step, so the panel
  shows what one step changed.
- **F6** spoils every settlement's food stores and **F7** brings settlers (every settlement
  grows by half, at least 10 people). Use them to drive famines and recoveries.
- **F8** starts the next weather spell (normal, dry, wet, heat wave, cold wave) for its longest
  length, and **F10** calls a storm at the cursor, short of a hurricane. Use them to drive
  droughts and storm damage. **Y** begins the next season.
- **I** starts the next hazard of the cycle at the cursor: a tornado, a landslide, a meteor,
  blight in the fields, a locust swarm, a wave on the nearest coast, then the volcano's waking.
- **F11** starts an earthquake (magnitude 6.5) and **F12** a fire at the cursor.
- **L** makes the nearest river within 400 m burst its banks, or sends a wave over the low land at
  the cursor. The debug panel lists the rivers' levels and the lakes, and a selected settlement's
  water, access to water and river; the region map gains fresh water and flood layers.
- **M** sends 20 people out of the village under the cursor to the best place they can reach.
- The debug panel also lists every god's domains, each holy site with its keeper, its claimant
  and who leads its influence by how much, and the selected settlement's Old Faith and sanctuary.
  `--rank` and `--domains` try out domains at once, and `--focus=siteN` looks at a holy site.
- The command-line options below reproduce a world (`--seed`, `--load`, `--world-size`,
  `--victory`), force a renderer
  (`--terrain`), start from a grown settlement up close (`--advance-ticks`,
  `--camera-distance`) and script smoke runs (`--quit-after-frames`, `--screenshot`).
  Unattended runs end by logging one summary line per settlement, with its tier and the
  citizens shown, then the performance lines with a save timed, and the world's load.
- The selected settlement's dangers line in the debug panel shows its houses' integrity (1 is
  sound), and the performance lines count the debris pieces and those still moving.
  `--disaster=meteor` and the I key's falling star strike villages as a natural one does, and
  `--hand=throw` throws a rock or a tree at the focused settlement.
- **World streaming (spec §84, ADR-050).** The land near the camera carries its detail and the
  rest stays simulation data: forests are planted only within 2.1 km of the camera, a few chunks
  a frame, the fallback terrain is drawn in 2 m detail within 650 m and coarser beyond, and fires
  are drawn within 2.5 km (their villages' labels and the Danger overlay show the rest). A camera
  high above loads only what it could see. The debug panel's streaming line, also printed by
  unattended runs, says what is loaded; the radii are in `Streaming` in
  `game/Data/settlement_view.json`.
- **Stress testing (spec §104, ADR-044).** `--stress=on` makes the stress scenario: the vast island
  with 50 villages of 2,000 people (100,000 in all), 8 gods and their Creatures, 20 weather cells,
  and an earthquake, a fire and a flood at once, the camera over the busiest villages and thousands
  of citizens drawn. For example:

  ```bash
  godot --path game -- --stress=on --seed=42 --quit-after-frames=600 --screenshot=/tmp/stress.png
  ```

  On an Apple M3 Pro it runs at about 100 FPS with 7,000 citizens drawn, a tick takes 1.5 ms,
  and a 10 MB save is written in about 130 ms. A shared game on the stress world sends a player
  about 60 KB/s (144 KB/s before regions were sent within a tolerance, ADR-056).

## Command-line options

Pass options after `--`, for example `godot --path game -- --seed=42 --terrain=fallback`.

| Option | Effect |
| --- | --- |
| `--seed=<number or text>` | Seed of the generated world (text is hashed). Default: `1234`. |
| `--load=<path>` | Load a save at startup, for example `user://saves/quicksave.json`. If it fails, a new world is generated. |
| `--terrain=auto\|terrain3d\|fallback` | Terrain renderer. `auto` (the default) and `terrain3d` use Terrain3D when it is available; `fallback` forces the built-in mesh renderer. Without it, the settings' Simple terrain chooses the fallback (ADR-068). |
| `--advance-ticks=<n>` | Run `n` simulation ticks (10 per simulated second) before the first frame, for example `--advance-ticks=9000` for a settlement of about 425 people. |
| `--camera-distance=<m>` | Starting camera distance in metres (default 380, clamped to 12–2600, or further over a larger island). |
| `--quit-after-frames=<n>` | Quit after `n` frames (smoke tests). The run prints its frame times, the performance lines with a save timed, and the world's load. |
| `--stress=on\|off` | Start the stress scenario of spec §104 (ADR-044): the vast island with 50 villages of 2,000 people, 8 gods and their Creatures, 20 weather cells and an earthquake, a fire and a flood, the crowd drawn in thousands. |
| `--wildlife=on\|off` | Take the camera to the herd of deer nearest the focused settlement at the start (ADR-054). |
| `--screenshot=<file.png>` | Together with `--quit-after-frames`, save a screenshot just before quitting (not in headless mode). |
| `--divine-power=<n>` | Your god starts with `n` Divine Power. |
| `--cast=<id>[,<id>...]` | Cast these miracles (ids from `miracles.json`) on the focused settlement at the start. |
| `--select=<id>` | Start with a miracle selected; scripted runs show its ring at the screen centre. |
| `--overlay=influence\|religion\|faith\|food\|weather\|danger\|migration\|culture\|nature\|population\|risk\|politics\|navigation\|authority` | Start with a map overlay shown. |
| `--book=world\|religion\|miracles\|creature\|villager` | Open the Book of the World on a page at the start. |
| `--overview=on\|off` | Start with the camera over the whole island. |
| `--focus=<n>\|creature\|site<n>` | Start looking at settlement `n` (1 is your home; `--cast` targets it), at your Creature's face or at holy site `n`. |
| `--judge=reward\|punish` | Reward or punish your Creature on the first frame (for captures of the hand). |
| `--roads=on\|off` | Every road the villages would plan is built at the start (for captures). |
| `--stores=full\|empty` | Every village's wood and stone start full or empty. |
| `--great-event=plague\|eclipse\|reformation\|beast\|anomaly` | Start a great event at the start: the plague in the focused settlement, an eclipse, your faith's reformation, the beast's waking or the anomaly at the focused settlement; the camera looks at the beast or the anomaly. |
| `--prophet=on\|off` | A prophet of your god rises at the focused settlement at the start (for captures). |
| `--villager=on\|off` | You follow one of the focused settlement's people at the start, the camera on them (ADR-043). |
| `--reshape=island\|gorge\|river` | Raise an island, split a mountain or turn a river near the focused settlement at the start, as the Divine Act would, and look at it (ADR-045; without `--camera-distance`, which looks at the settlement). |
| `--hand=villagers\|creature\|offering\|throw` | Your hand lifts villagers of the focused settlement, your Creature, or an offering from the focused settlement's stores, or throws a rock or a tree from beside the focused settlement at it, at the start (for captures). |
| `--debug-panel=on\|off` | Show or hide the F1 debug panel at the start. |
| `--diplomacy-panel=on\|off` | Show or hide the diplomacy panel (P) at the start. |
| `--weather=normal\|dry\|wet\|storm\|heatwave\|coldwave\|hurricane` | Start in a weather spell, or with a storm or a hurricane over the focused settlement. |
| `--disaster=quake\|fire\|flood\|tornado\|landslide\|meteor\|blight\|locusts\|tsunami\|eruption` | Start a disaster or a hazard at the focused settlement (the volcano wakes wherever it is, and the camera goes to it). |
| `--priority=<priority>` | Ask the focused settlement to put something first (Food, Health, Growth, Worship, Defence, Preparation, Expansion, Temple). |
| `--settlement-panel=on\|off` | Open the Settlements panel (N) on the focused settlement at the start. |
| `--doctrine=<doctrine>\|secular` | Give the focused settlement a doctrine in full (Pacifism, Militarism, Sacrifice, Charity, NatureWorship, KnowledgeSeeking, Expansionism, Isolationism), or turn it against the gods. |
| `--treaty=<treaty>` | Sign a treaty with your strongest rival at the start (NonAggression, DefensivePact, JointWar, SharedInfluence, ReligiousAccess, CreatureNonAggression, DisasterAssistance, MutualDefense, TerritorialAgreement, Tribute, MigrationAgreement). |
| `--council-panel=on\|off` | Open the council panel (U) at the start. |
| `--summon-council=on\|off` | Summon the Divine Council at the start. |
| `--gods-met=on\|off` | Every god has met every other at the start, so all can deal from the first moment (a New World can choose this too: The gods, known to one another). |
| `--war-goal=conquest\|holysite\|tribute\|humiliation` | Declare war on your strongest rival at the start, for that goal. |
| `--holy-war=on\|off` | Send the settlement the camera looks at to holy war on its nearest neighbour of another faith at the start. |
| `--veil=on\|off` | Cast veiled miracles from the start. |
| `--migrate=<n>` | Send `n` people out of the focused settlement at the start. |
| `--refugees=welcome\|convert\|turnaway` | Your refugee policy from the start. |
| `--menu=on\|off` | Start on the start screen (the default without options) or not. |
| `--guide=on\|off` | Show the first-minutes guide from the start. |
| `--history-panel=on\|off` | Open the history panel (H) at the start. |
| `--pause-menu=on\|off` | Open the pause menu at the start. |
| `--speed=1\|4\|16` | The simulation's speed from the start. |
| `--host=on\|off\|<port>` | Host this world for other players (port 7777 by default). |
| `--join=<address>[:<port>]` | Join a host's world instead of making one. |
| `--name=<player>` | Your name in a shared world. |
| `--god=<name>` | The god you ask for when joining. |
| `--upnp=on\|off` | Ask the router to forward the host's port. |
| `--meet=on\|off\|<name>` | Bring the nearest other Creature (or the one with that name) beside yours at the start, so they meet. |
| `--species=<name>` | The species of your Creature in a new world: Cow, Ape, Tiger, Lion, Wolf, Bear, Horse or Tortoise. |
| `--temperament=<words>` | Give your Creature these traits strongly at the start, separated by commas: brave, cowardly, kind, sadistic, curious, lazy, protective, jealous, playful, aggressive, loyal or independent. |
| `--world-size=small\|large\|huge\|vast` | The size of a new world (see [World sizes](../README.md#world-sizes)). An unknown size makes a small world. |
| `--victory=all\|sandbox\|<list>` | How a new world may be won: every way, no way, or a list of `ascendancy`, `conquest` and `cultural` separated by commas, for example `--victory=conquest,cultural`. Default: ascendancy. |
| `--seasons=on\|off` | Whether a new world turns through four seasons (default on; see [Seasons](../README.md#a-living-world)). |
| `--season=spring\|summer\|autumn\|winter` | Begin this season at the start, giving an even-climate world seasons (debug). |
| `--rank=<rank>` | Your god starts at this rank, such as LesserGod or God (debug). |
| `--domains=<domain>[,<domain>...]` | Take up these domains at the start, as your rank allows. |
| `--domains-panel=on\|off` | Open the Domains panel (G) at the start. |
| `--settings-page=on\|off` | Open the settings page at the start. |
| `--tutorial=on\|<step>` | Start the tutorial, or at a step from 1 to 15 (for captures, ADR-064). |
| `--title-page=new\|worlds\|together\|settings\|minds\|keys` | Open the title screen on a page; `minds` and `keys` are the settings' Minds and Keys tabs (for captures, ADR-060, ADR-065, ADR-068). |
| `--bar=book\|villages\|gods\|council\|domains\|history\|map\|overview\|keys\|debug\|menu` | Press one of the god's bar's buttons at the start: for example `keys` opens the key map, `map` the overlays' chooser and `debug` the debug palette (for captures, ADR-068). |
| `--minds=scribes\|claude\|openai\|gemini\|local` | Who writes the gods of new worlds, and speaks for them, in this run, over the settings' choice (ADR-065, ADR-067). |
| `--pantheon=on\|off` | Bear the world's gods anew at the start, by the minds chosen, and show their page (ADR-065). |
| `--mind-check=on\|off` | Ask the chosen model for a greeting at the start and print its answer (ADR-065). |
| `--agents=on\|off` | Let the gods' and Creatures' agents think through the chosen model for the run, or not (ADR-072). |
| `--wake=<god>` | Wake an AI god (by its number, 2 or more), or every one with 0, to look about it at the start (ADR-072). |
| `--angel=on\|off\|<stage>` | Keep your angel for the run, or not; a stage (radiant, kind, unformed, torn, stern, fallen) holds its look and calls it at the start (ADR-074). |
| `--angel-ask=<words>` | Ask your angel something at the start (ADR-074). |
| `--talk=<god>` | Talk with a rival god (by its number, 2 or more) at the start (ADR-066). |
| `--talk-say=<words>` | Say this to that god's model at the start (ADR-066). |
| `--work=village\|forest` | Set your Creature to work at the start: for the focused village, or in the forest richest in timber near it (ADR-063). |
| `--music=theme\|calm\|dark\|tension\|war\|disaster\|triumph` | Hold the music in a mood for the run (ADR-071). |
| `--music-export=<folder>` | Write every piece of music as a WAV file once made, then quit, to listen outside the game (ADR-071). |
| `--visit=<n>` | Set your Creature down beside village `n` (1 is your home) at the start, so its god's watch answers it (ADR-070). |
| `--fight=on\|off` | Set your Creature on the rival nearest it at the start (with `--meet` to bring one beside it, ADR-062). |

Unknown options and invalid values produce a warning and are otherwise ignored.

## Taking the README's screenshots

The screenshots in `docs/images/` are taken by the game itself, with `--quit-after-frames` and
`--screenshot`. A machine without a GPU can take them too, with Mesa's software Vulkan driver
(lavapipe) under a virtual display; Terrain3D's shader is patched for it (ADR-007). On Ubuntu:

```bash
sudo apt-get install xvfb mesa-vulkan-drivers libvulkan1
VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.json \
  xvfb-run -a -s "-screen 0 1600x900x24" \
  godot --path game --resolution 1600x900 -- --seed=1234 --angel=off \
  --advance-ticks=9000 --camera-distance=110 --quit-after-frames=50 --screenshot=/tmp/village.png
```

A run takes three to four minutes at about seven frames a second. Set `XDG_DATA_HOME` to a fresh
folder for each run so that runs side by side do not share a profile. The README's images were
taken at 1600 × 900 from seed 1234 and saved as JPEG; the option lines for each are in
[images/README.md](images/README.md).

## Where the numbers live

Every tunable number, name and colour is in `game/Data/*.json`, parsed strictly (unknown members
and out-of-range values are errors).

| System | Files |
| --- | --- |
| World generation, biomes, landforms, rivers | `worldgen.json` (`Biomes`, `Landforms`, `Hydrology`); colours and ground textures in `biome_palette.json` (`Ground`) |
| World sizes | `world_sizes.json`; the larger worlds' extra gods are `ExtraGods` in `gods.json` |
| Settlements, villagers, technology and buildings | `settlement_rules.json` (`Villagers`, `Civics`, the drought threshold) |
| How settlements, trees, ground cover, wildlife, houses and styles look | `settlement_view.json` (`Trees`, `GroundCover`, `Wildlife`, `Architecture`, `Style`, `Streaming`) |
| Belief, ranks, culture, victory, reshaping the land | `divine_rules.json` (`Culture`, `Land`, `VictoryShare`, `ConquestShare`, `CulturalShare`, `VictorySeconds`) |
| Miracles and how they look | `miracles.json`, `divine_view.json` |
| Gods, their personalities and the AI | `gods.json`, `god_ai.json` (diplomatic weights, refugee policies, `TemperReach`) |
| Diplomacy | `diplomacy.json` |
| The gods' minds and the angel | `minds.json`, `angel.json` |
| Creatures, their bonds and combat | `creatures.json` (`Bonds`, `Combat`) |
| Domains and holy sites | `domains.json`, `holy_sites.json` |
| Weather and seasons | `weather.json` (`Seasons`, `Extremes`); how clouds look in `weather_view.json` |
| Disasters, hazards and preparedness | `disasters.json` (`Hazards`, `Preparedness`) |
| Rivers, lakes, floods and wells | `water.json` |
| The land's life | `ecology.json` |
| Society | `society.json` |
| Migration | `migration.json` |
| History | `history.json` |
| Music | `music.json` |

## Saves and the profile

Your worlds, their saves and your settings are kept in your profile, one SQLite file,
`user://pantheon.db` (ADR-060). If SQLite's library cannot be loaded, the profile is kept in plain
files in `user://profile/` instead. In a project run (not an export), `user://` is:

- Linux: `~/.local/share/godot/app_userdata/PANTHEON/`
- Windows: `%APPDATA%\Godot\app_userdata\PANTHEON\`
- macOS: `~/Library/Application Support/Godot/app_userdata/PANTHEON/`

Saves are JSON of the simulation state, compressed in the profile, starting with `"SaveVersion": 34`. The scene tree
is never saved. The terrain is regenerated from the seed and loading rebuilds every visual
([ADR-006](ADR/ADR-006-simulation-state-separate-from-scenes.md)). The representative
citizens are not saved either; they are derived again after a load. Saves from Milestone 1
(version 1) still load: each settlement starts healthy with no house under construction. Saves
from Milestones 1 and 2 (versions 1 and 2) get the Milestone 3 god, Ilyra, followed in their one
settlement. Saves from Milestones 1 to 3 get a newborn Creature, Bramble, for every god. Saves from
Milestones 1 to 4 stay single-god worlds with nobody to compete with. Saves from Milestones 1 to 5
start with every pair of gods neutral, with no memories of each other. Saves from Milestones 1 to 6
start under clear skies, with each region's soil as moist as its climate. Saves from Milestones 1
to 7 start with nothing burning, shaking or in ruins. Saves from Milestones 1 to 8 keep their one
winner, if any, as the list of winners. Saves from Milestones 1 to 9 start with nobody on the road
and every god welcoming migrants. Saves from Milestones 1 to 10 start with Creatures that have
never dealt with each other: no combat skill, no scars and no views of the others. Saves from
Milestones 1 to 13 start with an empty history: the world remembers from then on. Saves from
Milestones 1 to 14 start with gods who have chosen no domains and with no village split, and find
their holy sites when they are loaded. Saves from before Milestone 15, part 7 are won by Divine
Ascendancy alone. Saves from before part 8 keep an even climate, without seasons, with no snow or
harm and their land's animals at its cover. Saves from before part 9 start with no blight, rubble,
tornadoes, swarms or marks, and find their volcano when it is first needed. Saves from before part 10
start with Creatures that have walked no path, empty hands and no hallowed land. Saves from before part 12
start with no prophets or elders; their villages choose elders at once. Saves from before part 14
start with nobody sick and no great event, and set their first eclipse when loaded. Saves from before part 15
find their beast's lair and set their first anomaly when loaded. In saves from before part 16
every god has met every other, and the AI gods make their first plans when loaded. Creatures from before part 17
start with middling courage, protectiveness, jealousy and playfulness. Worlds from before part 18
keep their four biomes. Hands from before part 19 hold no offering. In saves from before part 22
no god follows a villager. In saves from before part 24 the land is as the seed made it. Saves from before part 27 give every village sound houses. Saves from before part 40 keep their Creatures as the animals of their natures (a Mossback a Cow, a Stonehide a Tortoise, a Swiftclaw a Tiger, a Lumen an Ape). Saves from before part 13
grow each region's forest from its vegetation (without a new world's patches), give each village a
new world's stores of wood and stone, and have no roads yet. A save keeps its world's
size, how it may be won and its seasons.

## Repository layout

```text
game/                 Godot project: scenes, client scripts (Pantheon.Client), data, materials, shaders
  Data/               worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                      creatures.json, god_ai.json, diplomacy.json, weather.json, disasters.json, migration.json,
                      history.json, domains.json, holy_sites.json, world_sizes.json, ecology.json,
                      society.json, water.json, settlement_view.json, biome_palette.json, divine_view.json, weather_view.json,
                      minds.json (the Claude models and the scribes' lore, ADR-065; the agents' rules and voices, ADR-072)
                      angel.json (the player's angel: its names, looks, timing and lines, ADR-074)
  Fonts/              Cinzel and Alegreya Sans, with their SIL Open Font Licences (ADR-059)
  addons/terrain_3d/  Vendored Terrain3D 1.0.2 runtime; tools/vendor-terrain3d.sh fetches its libraries
src/
  Pantheon.Domain/        World, region, settlement, god, belief, Creature, diplomacy, weather and disaster
                          state, events, and content contracts
  Pantheon.Simulation/    Seeded RNG, noise, terrain and world generation, the settlement and belief models,
                          influence, miracles, the Creature, navigation, the god AI, victory, diplomacy,
                          weather, disasters, tick systems
  Pantheon.Persistence/   Versioned JSON saves with migrations, content parsing, and the player's profile
                          (SQLite, or files when it cannot load: ADR-060)
  Pantheon.Game/          Engine-agnostic session (the local authority), scheduler, camera model, readouts,
                          HUD model, alerts, overlays, settlement layout and representative citizens
  Pantheon.Networking/    The multiplayer protocol, its codecs and the transport contract (ADR-003, ADR-018)
  Pantheon.Oracle/        The language models behind the gods' minds: Claude through Anthropic's SDK, GPT
                          through OpenAI's Responses API, Gemini through generateContent, and a local
                          model through Ollama's API (ADR-065, ADR-067)
  Pantheon.Tests/         xUnit tests
docs/                 The owner's specification (PANTHEON_SPEC.md, the source of truth), the progress
                      summary (PROGRESS_SUMMARY.md), the vision summary (GAME_SPEC.md), architecture
                      and ADRs
.docs/                Milestone plans
tools/                vendor-terrain3d.sh (Terrain3D's libraries) and install-godot.sh (Godot and its
                      export templates)
.github/workflows/    build.yml: tests on every push, and exports for Windows, macOS and Linux (ADR-075)
```

## Known issues

- Every start logs `WARNING: instance_reset_physics_interpolation() is deprecated`. Terrain3D
  1.0.2 calls this deprecated Godot 4.7 function, and the warning is harmless.
- Without a GPU driver, Godot falls back to Mesa's software Vulkan driver (lavapipe), which
  manages only a few frames per second. Terrain3D 1.0.2's shader crashes that driver. PANTHEON
  patches the shader at runtime to prevent this
  ([ADR-007](ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md)). If a different Terrain3D version
  cannot be patched, a warning is logged; use `--terrain=fallback` on such machines.
- macOS may block the unsigned Terrain3D binaries until they are removed from quarantine. See
  Terrain3D's platform documentation.
- The game reads `res://Data/*.json` at startup, so every export must include those files (the
  presets in `game/export_presets.cfg` do).
- On a fresh checkout, the first `godot --headless --path game --import` may crash as Godot shuts
  down (a segmentation fault), after the import has finished. Run it again; the second runs clean.
  CI does the same (ADR-075).
- About one run in four, Godot warns at exit that a few ObjectDB instances were leaked. It happens
  since at least part 15 and never with `--verbose`; it is harmless and not yet traced.
- Four gods crowd the small island: a player who does nothing keeps only a few followers after ten
  minutes, and Seren the Guardian is the strongest AI god (kindness converts better than fear).
- The heightfield is generated whole (1.1 s for a huge world); only its detail, forests and fires
  stream near the camera (ADR-050). No world is larger than 12 km or holds more than eight gods.
- A history record names two winners, so a pantheon of three or four is told by its first two.
- Snow does not slow travel, grass and trees keep their colour through the year, and pollution
  does not depend on a village's technology. Biomes do not change how fast anyone walks.
- The representative citizens are presentation only: they show what the settlement's numbers
  say ([ADR-008](ADR/ADR-008-settlement-step-and-representative-citizens.md)), and a citizen
  becomes an individual only when you follow them with / (ADR-043).
- Frame rates measured while the Mac's screen is locked are not trustworthy: macOS stops taking
  the window's frames, and each one then waits up to a second (ADR-056).
