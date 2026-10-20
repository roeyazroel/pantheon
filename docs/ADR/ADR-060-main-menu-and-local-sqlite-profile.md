# ADR-060: A main menu and the player's profile in local SQLite

- Status: Accepted (amends ADR-020: saves and settings live in the profile)
- Date: 2026-10-20
- Builds on: [ADR-006](ADR-006-simulation-state-separate-from-scenes.md) (versioned JSON saves of
  `WorldState`), [ADR-020](ADR-020-game-flow-and-history.md) (the game's flow) and
  [ADR-059](ADR-059-interface-look-scaling-and-hud.md) (the interface's look)

## Context

The owner asked for "a local SQLite and a game menu to start a new game, settings, etc." instead of
the command line. A start screen existed, but it had problems:

- **Hidden by options:** any launch option hid it, and it was a small panel over a paused world
  with the HUD showing behind it.
- **Saves:** they were loose JSON files named after the god, year and seed. Listing them parsed
  every file in full, and nothing recorded a world's size, god, rank or time played. A save could
  not be deleted.
- **Settings:** they were a JSON file of their own.

§65 asks that the game work fully offline with no database *server*. A database file inside the
game's process is local, as §65 requires.

## Decision

### The profile (`Pantheon.Persistence.Profile`)

`IProfileStore` keeps the player's profile:

- **Values:** settings, and progress such as the tutorial's later.
- **Worlds:** each world started (`WorldRecord`): its name, god, seed, size, victory, seasons and
  Creature species; when it was made and last played; time played; its newest tick, the player's
  followers and rank; and how it ended.
- **Saves:** each world's saves (`SaveRecord`). A save is still the versioned JSON of `WorldState`
  (ADR-006), compact and gzipped, with its save version. A world keeps every save the player
  makes, and only its newest autosave and quicksave.

**The SQLite store (`SqliteProfileStore`):** Microsoft.Data.Sqlite 8.0.11, with SQLite's native
library from SQLitePCLRaw for Windows, macOS and Linux. The database is one file,
`user://pantheon.db`, in WAL mode with cascading deletes and a schema version.

**The fallback:** a native dependency sits behind an adapter with a fallback (AGENTS.md).
`ProfileStores.Open` falls back to `FileProfileStore` (an index and one file per save) if SQLite
cannot be loaded, and says why.

**The core never reads the clock:** the client passes the time in.

### The library (`Pantheon.Game.Flow.WorldLibrary`)

- **Starting a world:** records it, named by the player or "The isle of" its first village.
- **Saving:**
  - writes the world's save and brings its record up to date, adding the real time played;
  - a world not yet in the library is recorded first;
  - joined players do not save.
- **Loading:** takes a save back into the session through `GameSession.LoadWorld`, which readies a
  saved `WorldState` as a file load does.
- **Listing:** `Cards` lists the worlds for the menu, and `Latest` finds the newest save to
  continue from.
- **Old saves and settings:** save files from before the profile are brought in once, each as a
  world, and `settings.json` once into the profile's values.

### The menus

**The title screen** lies over the living island:

- **The view:** the camera circles the island lower than in play, the HUD and the labels are hidden,
  and the left is shaded.
- **The left:** "PANTHEON" in Cinzel, with Continue (the newest save, with its world's name), New
  World, Your Worlds, Play Together, Settings and Quit.
- **The right:** the chosen page.
  - **New World:** name, seed with a button for another, Creature, size, victory and climate.
  - **Your Worlds:** a card per world, with its god, rank, followers, size, seed, year, time played
    and when it was last played. Continue, its saves to load each, and Forget, which deletes it
    after a second click.
  - **Play Together:** host or join.
  - **Settings:** in four tabs (graphics, sound, controls, interface), each slider showing its value.

**The pause menu:**

- the world's name and god;
- Resume, Save, Load, Settings;
- "Save and leave to the title" and "Save and quit", which autosave first.

**Saves in play:**

- F5 and F9 save to and load from the world's quicksave.
- The autosave runs every five minutes for a world in the library.
- F2 starts a new world in the library.

**Tools:** `--title-page=new|worlds|together|settings` opens a page of the title screen, for
captures. The command line remains for development and captures. `--load` still reads a save file.

## Consequences

- **No command line needed:** a player starts, continues, loads, deletes and configures from the
  menu. The command line is a developer's tool.
- **Dependencies:** Microsoft.Data.Sqlite is the first NuGet package outside the tests. Each export
  includes its native library: `libe_sqlite3.dylib` for both Mac architectures, `e_sqlite3.dll` and
  `libe_sqlite3.so`. The exported Mac app was run and kept its profile in SQLite 3.41.2.
- **Saves:** the save format and its migrations are unchanged; only where saves are kept changed.
  A host's world is still written to `hosted.json` when it ends a shared session.
- **Not done:** cloud sync (excluded by §65 and §70), renaming worlds, and thumbnails of a world.
