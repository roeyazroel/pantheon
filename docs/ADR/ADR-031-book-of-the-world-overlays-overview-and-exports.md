# ADR-031: The Book of the World, the last overlays, the strategic overview and export presets

- Status: Accepted; amended by [ADR-075](ADR-075-builds-in-ci-and-fetched-libraries.md)
- Date: 2026-10-19
- Builds on: [ADR-020](ADR-020-vertical-slice-history-flow-and-eligible-pantheons.md) (the game's
  flow and panels), [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (the
  Domains panel) and [ADR-005](ADR-005-terrain3d-for-terrain.md) (native libraries per platform)

## Context

- **§57:** asks for World, Settlements, Creature, Diplomacy, Religion, History, Domains and
  Miracles panels, and warns against excessive permanent RTS UI. The World, Religion and Miracles
  panels did not exist, and the Creature had only its card.
- **§58:** asks for population, disaster risk and political control overlays beside those built.
- **§56:** asks for a strategic overview.
- **§62:** asks for Windows x64, macOS Apple Silicon (and Intel if practical) and Linux x64. No
  export presets existed, and every letter key is now bound.

## Decision

### The Book of the World (§57)

One panel with four pages, opened with Tab, rather than four more panels on four more keys:

- **World:**
  - the island, its victories, the time, the season and the weather, and its people;
  - every god's rank, followers and share, power, domains, how it is seen, its alliances and wars,
    and its Creature;
  - the race for victory: who holds which lead for how long, or who won;
  - the disasters under way and the holy sites with their keepers.
- **Religion:**
  - the player's followers and their feelings: faith, love, fear, doubt, dependency and resentment;
  - every village where they live, failing ones marked;
  - every god's faith;
  - the Old Faiths and the villages that have turned from the gods.
- **Miracles:** every miracle by kind (general, the domains' signatures, the Divine Acts), with its
  scale, its cost to this god, its reach and what it still needs (a rank, a domain, time or
  power). Aim selects one that may be cast, like the bar.
- **Creature:**
  - growth, attributes, temperament and paths, with their callings;
  - what it has learned in each situation;
  - its latest memories, with the god's judgements;
  - how it regards every Creature it has met.

The pages are pure readouts (`Pantheon.Game.Book`); the client's `BookPanel` shows them and
reports clicks. The Settlements (N), Diplomacy (P), History (H) and Domains (G) panels stay where
they are.

### The last overlays (§58)

`StateOverlays` adds three overlays, after Nature in the O cycle:

- **Population:** each village's lands from deep blue to gold by its people against the largest
  village, and people on the road in white.
- **Disaster risk:** every land region by the greatest of four risks:
  - fire: dry, green land;
  - flood: low land beside rivers, lakes or the sea;
  - landslide: steep, wet slopes;
  - the volcano: land within its reach.
- **Political control:** each village's lands in the colour of the strongest god of the bloc that
  holds it, so allies share a colour. Lands of gods at war with the player are darkened.

### The strategic overview (§56)

Backtick (`` ` ``) shows the whole island from as far as the camera may go
(`GodCameraModel.ToggleOverview`), and again returns the camera to where it was. `--overview` starts
there. Scripted runs now always aim at the screen centre, whatever the real mouse does, so captures
are the same every time.

### Export presets (§62)

`game/export_presets.cfg` exports to `build/` (ignored by git):

| Platform | Output | Notes |
| --- | --- | --- |
| Windows | x64 `.exe` | |
| macOS | universal `.app` in a zip, Apple Silicon and Intel | Ad-hoc signed with Apple's `codesign`, without the hardened runtime |
| Linux | x64 | |

- Each carries the content files (`Data/*.json`), the .NET assemblies and the platform's Terrain3D
  library.
- The project imports ETC2/ASTC textures, which Apple Silicon needs.
- The ad-hoc signature runs on the machine that built it; distributing it needs a developer
  identity and notarization, which need the owner's Apple account.
- The Godot .NET export templates for 4.7.2 are installed from the official release; the README
  says how.

## Consequences

- §56, §57 and §58 are complete. §62's builds exist for every platform; the macOS build was run
  (114 FPS), and the Windows and Linux builds are made but not run, as no such machine is at hand.
- **The macOS signature:** Godot's built-in ad-hoc signing adds the hardened runtime. macOS 26 kills
  an ad-hoc signed app with the hardened runtime at launch, even when it is entitled to JIT and
  unsigned memory. Signing with Apple's `codesign` leaves the runtime out, and the app runs.
- **Not done:** signing with a developer identity, notarization, Steam (all need the owner's
  accounts), and Tier 3 entities (§80), which part 12 takes up.
