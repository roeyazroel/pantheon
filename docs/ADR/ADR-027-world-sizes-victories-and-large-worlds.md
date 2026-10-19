# ADR-027: World sizes, conquest, cultural and sandbox victories, and large worlds

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) (four gods and
  pantheon victory),
  [ADR-020](ADR-020-vertical-slice-history-flow-and-eligible-pantheons.md) (the start screen)
  and [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (rivers and lakes)

## Context

Every world was the same size, held four gods, and was won one way. The specification asks for
more:

- **§6:** an island 10–20 km across, four gods at first and up to eight later, 8 settlements for
  the MVP and 20–50 in the full game, and 10,000–100,000 or more people.
- **§53:** Divine Ascendancy (about 70% of global belief for a configurable time), pantheon
  victory, conquest (all opponents below a minimum influence threshold), cultural victory (a
  religion globally dominant without eliminating rivals), and a sandbox with no victory.
- **§84:** the world is divided in space. Nearby regions load detailed terrain, actors,
  vegetation and effects; distant regions remain simulation data.

A first measurement of a world eight kilometres across, with eight gods and forty villages, gave
4.1 ms a tick, nine times a small world's. Nearly all of it was the AI gods: a 110 ms tick when
they all thought at once. Seen from afar, forty villages' labels buried the island.

## Decision

### World sizes (§6)

- **Content:** `world_sizes.json` lists the sizes, smallest first:

  | Size | Island | Regions a side | Villages | Gods | Rivers (main) | Water cell | Pantheon |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | Small | 2 km (1024 × 2 m) | 32 | 8 | 4 | 7 (4) | 8 m | 2 |
  | Large | 4 km (1024 × 4 m) | 64 | 20 | 6 | 14 (8) | 8 m | 3 |
  | Huge | 8 km (2048 × 4 m) | 128 | 40 | 8 | 24 (14) | 16 m | 4 |
  | Vast | 12 km (2048 × 6 m) | 128 | 48 | 8 | 24 (14) | 12 m | 4 |

  - Vast meets §6's 10–20 km at Huge's cost, since its heightfield has as many samples. Its
    villages lie further apart and grow more slowly.
  - Water cells must be a whole number of samples. Coarser cells than these drew blocky rivers.
  - The first size is the world `worldgen.json` and `gods.json` describe; a first size that does
    not match them is refused.
  - Larger sizes add the file's extra settlement and river names to `worldgen.json`'s, and the
    first of `gods.json`'s `ExtraGods` (Aurel the Zealot, Maren the Nature Keeper, Thorn the
    Warlord, Ysolde the Isolationist) to the pantheon.
- **Making a world:** `WorldMaker` turns a size into generation settings and gods
  (`NewWorldOptions`: the Creature's species, the size and the victories). An unknown size makes
  the first.
- **Validation:** strict content parsing checks every size: distinct names, two gods at least
  and no more than there are, a pantheon of two to all of them, valid generation settings and
  names enough.
- **Saves:** a save keeps its generation settings, so a world loads at the size it was made.

### Victories (§53)

- **Settings:** `WorldState.Victory` (`VictorySettings`) records which victories a world was made
  with: ascendancy (the default), conquest and culture. With none it is a sandbox.
- **Larger pantheons:** the size sets how many gods may win a pantheon victory together
  (`VictorySettings.PantheonMaxGods`, from the size's `PantheonMaxGods`): two of four, three of
  six, four of eight. The divine rules' number serves a world with none.
  - Measured: in ten one-hour Large games with a passive player, pantheons of three ended four
    games, against one with pantheons of two.
- **Conquest:** a god wins when every god outside its alliances holds less than `ConquestShare`
  (5%) of all followers for `VictorySeconds` without a break. Its allies win with it.
- **Cultural victory:** a god wins when it leads (has the largest share of) at least
  `CulturalShare` (three in four) of the living villages for `VictorySeconds`, while some rival
  keeps at least the conquest share: dominant without eliminating its rivals.
- **Clocks:** `GodState.ConquestSinceTick` and `CulturalSinceTick` start when a condition holds
  and clear when it breaks, like `DominantSinceTick`.
- **Dominance:** in a sandbox, and when ascendancy is off, dominance is still tracked, for the
  council's dominance motion and the AI.
- **The first victory is the only one.** `VictoryAchieved.Kind` (`VictoryKind`: Ascendancy,
  Pantheon, Conquest, Cultural) and the history record tell how it was won.
- **Choosing them:** the start screen's new-world page offers the size and the victories
  (Divine Ascendancy, Every victory, Conquest, Culture, Sandbox) beside the Creature;
  `--world-size` and `--victory` do the same from the command line.

### Large worlds (§6, §84)

- **The AI's cost:** measured, then cut, without changing what any god does:
  - It weighs casts only on villages within its influence; the rest were refused anyway.
  - `InfluenceCache` builds each god's influence field once for an evaluation, and
    `MiracleCaster.Check` uses it.
  - `TreatyAdvisor` counts every god's followers once per weighing, not once per treaty kind.
  - **Turn-taking:** the AI gods take turns across the evaluation interval instead of all thinking
    on one tick (`GodAiSystem.IsTurn`). The first still thinks when the interval is due, so a
    lone AI god is unchanged.
- **Presentation:**
  - **Labels:** floating labels give way to more important ones (`LabelDeclutter`, pure; the
    client measures them each frame). Holy sites come first, then villages by population, then
    Creatures and migrants. Each shows in full where it overlaps nothing placed, by its name
    alone where only that fits, and is otherwise hidden. A label faded out by its distance takes
    no place. A small world's village labels seldom overlap, so it looks as before; your Creature's
    label no longer covers your village's at the start.
  - The camera zooms out further over a wider island, in proportion to its width.
  - The fog thins with the island's width.
  - The HUD's line of rivals wraps between gods.
- **Streaming (§84), in part:** villages far from the camera already show no citizens (ADR-008),
  and their labels now give way. The terrain is still built whole, and the simulation is
  aggregate throughout.

### Saves, multiplayer and presentation

- **Saves:** version 19. Migrating from 18 gives a world won by ascendancy alone, and no
  conquest or cultural clocks.
- **Multiplayer:** protocol 7, since the world and the victory event carry new members. The
  menu's host page makes a world of the first size; `--host` with `--world-size` hosts any size,
  and a joining player builds it from the snapshot's generation settings.
- **Alerts, history and the end screen** word each victory by its kind ("Vael has conquered the
  world", "The Cultural Triumph of Seren").
- **HUD:** the contest line shows a sandbox, a conquest or cultural victory under way, or, when
  ascendancy is off, what the player must do.
- **Debug:** F1 shows the island's width and the world's victories beside the seed.

## Consequences

- **Cost:** a Huge world costs 0.59 ms a tick, a Large 0.20 ms and a Small 0.08 ms (seed 42, ten
  simulated minutes); a Vast one 0.56 ms. The AI step's worst tick fell from 110 to 18 ms.
- **Length:** larger worlds are long games. In ten one-hour games with a passive player, Large
  found a winner four times and Huge once; their gods hold each other in check. Populations
  reach 10,000–26,000 in an hour, as §6 asks.
- **Frame rate:** on the Mac a Huge world runs at about 106 FPS at the start and 120 FPS seen from
  2.6 km after ten minutes, and a Vast one at 125 and 118, on Terrain3D. A Large world on the
  fallback renderer also runs at 120 FPS.
- **Balance:**
  - Without turn-taking the 40-game probe repeats part 6's results exactly, so the other changes
    alter no outcome.
  - With it, seeds 30–69 found a winner in 33 games (38 before) and seeds 70–109 in 39 (37
    without it): the games' variance, not a trend.
- **Not modelled:**
  - Terrain streamed in pieces: the whole heightfield is built at once, in 1.1 s for a Huge or
    Vast world.
  - Worlds beyond 12 km, and more than eight gods.
  - A history record names two winners, so a larger pantheon's victory is told by its first two.
  - A size chosen on the menu's host page.
  - Victories chosen per player.
