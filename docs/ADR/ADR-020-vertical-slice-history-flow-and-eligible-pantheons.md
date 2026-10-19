# ADR-020: The vertical slice: world history, the game's flow, save slots and eligible pantheons

- Status: Accepted (amends ADR-015's pantheon victory)
- Date: 2026-10-19
- Builds on: [ADR-006](ADR-006-simulation-state-separate-from-scenes.md) (state and saves),
  [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) (pantheon victory) and
  [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) (shared worlds)

## Context

Milestone 14 (spec §129) is the complete vertical slice and "the first meaningful validation
build": four gods, eight settlements, Creatures, belief, politics, weather, disaster, local saves,
multiplayer and victory. Every system exists, but the game was played through launch options:

- it opened straight into a world;
- it had no menus and no end screen;
- the player had to know the keys;
- it had only one quicksave.

The specification also asks for:

- **History (§50, §92):** the world records important events ("Year 44 — Pact of Three") and
  evaluates significance; it does not store every event.
- **Panels (§57):** among them a History panel.
- **Victory (§53):** "a god or eligible alliance" wins; a pantheon victory is "an alliance jointly
  achieving dominance".
- **Alliance endgame (§54):** allied victory should create tension; an ally may betray or seek
  personal Ascendancy.
- **Performance testing (§104–105):** repeatable stress scenarios, and 60 FPS at 1080p on a
  modern Apple Silicon Mac.

A bot playing the player's god found a flaw in ADR-015. Because a bloc reached allies of allies,
three or all four gods sometimes "won" together. When everyone is allied, nobody is dominated.

## Decision

- **History is part of the world** (`WorldState.History`, saved; version 12):
  - **What it records** (`HistoryRecorder`): each event is weighed as it is raised, during a tick
    or by a command, and only significant ones are kept as `HistoryRecord`s:
    - pacts, wars, peace and betrayals; dominion, victory and ranks;
    - deadly earthquakes and fires; famines and great exoduses;
    - large settlements turning to a god; settlements falling and being settled again;
    - Creatures' battles and legends.
  - **Rules** (`history.json`): the thresholds, a year of history (60 simulated seconds), a
    capacity, and a time before the same kind of moment for the same place or pair counts again.
  - **Words** (`HistoryText`): "Year 12 — The Shaking of Elar (12 dead)". The History panel (H)
    lists the chronicle with the player's moments in gold. Shared worlds send it as a section.
- **The game's flow** (`GameFlow` in the client; `EndSummary`, `Guide` and `SaveCatalog` in the
  core):
  - **Start screen**, shown when the game starts without options: new world (seed), continue the
    newest save, load, host a shared world, join one, quit.
  - **Pause menu (Esc):** resume, save, load, main menu, quit.
  - **End screen**, at a victory: Victory or Defeat, who won, how the player's god and Creature
    ended, and the world's history. It offers keep watching, new world or main menu.
  - **Guide:** a first-minutes guide that teaches by doing. It advances when the player casts,
    judges the Creature and opens diplomacy, and can be hidden.
- **Saves:** named save slots in `user://saves` (god, year, seed), an autosave every five real
  minutes, the quicksave and the host's save at the end of a shared session. The load page lists
  them, newest first, from their seed and tick, without loading the worlds.
- **Joining from the menu** starts the game over in the same process: the main scene reloads, and
  its next start reads the host's address instead of the command line (`PendingLaunch`), because a
  joined world replaces the whole session. Leaving a shared world for the main menu does the same.
  An operating-system restart was tried first: on macOS the relaunched app stalled before it could
  join.
- **Eligible pantheons** (amends ADR-015). A god counts towards victory with its strongest direct
  allies only, up to `PantheonMaxGods` gods in all (2, in `divine_rules.json`; ties go to the lower
  id). Allies of allies no longer count. A god allied with two others must leave one out of its
  victory, which is §54's tension.
- **Measuring** (§104):
  - unattended runs print frame times (average, 95th percentile, worst);
  - `--speed` sets the simulation's speed;
  - `--menu`, `--guide`, `--history-panel` and `--pause-menu` open the flow's screens for captures;
  - headless probes measure ticks, saves and loads.

## Consequences

- Positive: the slice can be played from start to end without launch options: start, guide, play,
  pause, save, victory or defeat, again.
- Positive: games tell their stories. A 40-minute game records about 45 moments, a bit over one a
  year: wars, conversions, quakes, battles, legends. The end screen shows those that concern the
  player first.
- Positive: with a bot playing the player's god, over 20 seeds and 60 minutes each:

  | Player's god played as | Games with a winner | Player among the winners |
  | --- | --- | --- |
  | Guardian | 9 | 7 |
  | Tyrant | 12 | 8 |
  | Opportunist | 8 | 0 |
  | Passive (no player) | 16 | 0 |

  No three- or four-god victories remain (with allies of allies, there had been nine).
- Positive (§105): on an M3 Pro at 1920 × 1080, 30 minutes into a game at x16:
  - 150 FPS uncapped (6.7 ms a frame), 120 FPS with vsync;
  - simulation 0.076 ms a tick on the shipped world, and 0.198 ms with 21 settlements;
  - save 43 ms (408 KB), load 1.4 s with the terrain.
- Negative: when four active gods play, more games last beyond an hour without a winner. The
  contest is close; a sandbox mode or a shorter victory can be offered later.
- Negative: there are no settings (graphics, audio, keys), no tutorial beyond the guide, and no
  in-game lobby: joining reloads the game's scene.

## Alternatives considered

- **Keeping every event as history.** §92 forbids it: thousands of events an hour would bury the
  story.
- **History as a presentation log in the client.** Saves, shared worlds and the end screen need it
  on the host, and §50 lets civilizations refer to it, so it is world state.
- **Forbidding alliances of more than two gods.** That limits diplomacy itself. Limiting who wins
  together keeps alliances free and only bounds the victory.
- **A scene for each menu.** Menus are presentation over the same session; one flow node over the
  running world keeps the world visible behind them and needs no scene changes.
