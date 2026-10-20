# ADR-068: Every command in view: the god's bar, the key map, and the debug tools behind a setting

- Status: Accepted (amends ADR-059)
- Date: 2026-10-20
- Amends: [ADR-059](ADR-059-interface-look-scaling-and-hud.md) (the HUD gains the god's bar and the
  Veil toggle)

## Context

The owner, playing, asked how to meet a god so as to talk to it. The answer was two debug keys,
J and K. The owner then asked: "Do we have shortcuts key maps? And all those settings should be in
the settings menu UI", "there should be a button for J and K", and "rethink the buttons we show
the user".

An inventory of all 67 input actions and every control in play found five problems:

- **Hidden panels.** Apart from pause, the speeds, the Creature's orders and the miracles, nothing
  on screen opened anything. These were keys only, shown nowhere: the Book (Tab), the villages (N),
  diplomacy (P), the council (U), the domains (G), the history (H), the fourteen overlays (O, in
  turn), the overview (`), veiled casting (T) and the menu (Esc). The only list of keys was the
  README.
- **Cheats always on.** Twenty debug keys worked in every game, including an exported one, and none
  of them cost anything. F4 gave 500 Divine Power, J and K skipped the rules for the council and
  for meeting, F11 and F12 started disasters, and F2 replaced the world at a touch.
- **A wrong Esc.** In a Send, Fight or Work order, Esc opened the menu, though the hint said it
  cancelled the order.
- **Launch-only options.** The gods' first acquaintance (`--gods-met`) and the terrain renderer
  (`--terrain`) were player choices with no place in the menus.
- **No fair way to meet.** The only quick way to meet a god was the J and K cheats.

Spec §57 asks for panels for the world, the settlements, the Creature, diplomacy, religion, the
history, the domains and the miracles, and warns: "Avoid excessive permanent RTS UI."

## Decision

### The god's bar

The HUD gains one small row of drawn icons under the season, top right. Each button shows its key
on a badge, and its name and key in its tooltip. The buttons are:

- the Book;
- the village at the view's centre;
- the gods (diplomacy);
- the council;
- the domains;
- the history;
- the map;
- the overview;
- the keys;
- the debug tools, while they are on;
- the menu.

A button lights while its panel is open.

**A button presses its key.** It feeds its input action to Godot (`Input.ParseInputEvent`), so the
same handler runs for a button and a key, and the two cannot drift apart.

**The map button opens a chooser.** It lists the overlays by name, with the shown one marked. O
still steps through them.

**Where a command points.** A command pressed with the pointer over the interface acts at the
view's centre (`GodCameraController.CommandGroundPoint`), not at the ground hidden behind the panel.

The bar is one row of eleven small icons, about 460 pixels, under the clock, so §57's warning
against permanent RTS UI still holds.

**Elsewhere in the HUD:**

- **Veil:** a toggle at the end of the miracle bar, lit while the god's miracles are cast veiled.
- **The rivals:** a rival under the plaque opens the gods' panel.

### The key map

`Pantheon.Game.Controls.KeyGuide` is the key map: every key and gesture of play in seven groups (the
view, miracles, the Creature, the hand, the panels, time, debug), in words. The client reads each
action's keys from the bindings, with the keyboard layout's letters (`KeyText`). The map shows in
two places:

- the settings' new Keys tab;
- the bar's Keys button.

A test holds `KeyGuide` to `project.godot`: every action is in the map, and the map names no other.

Rebinding is left for later.

### The debug tools behind a setting

The debug group of `KeyGuide` names the debug tools:

- every `debug_*` action;
- F1, F3 and Shift+B (the debug views);
- the single steps (`.` and `,`);
- F2 (a new random world).

`GameSettings.DebugTools` turns them on or off. While it is null, the build decides: on when the
game runs from the project (`OS.IsDebugBuild()`), as the owner plays it, and off in an exported
game.

**While they are off:**

- their keys do nothing (`GameRoot` and `PoliticsController` check);
- the debug overlays (navigation and authority, ADR-044) leave the chooser and O's cycle;
- the bar has no Debug button.

**While they are on:**

- the bar's Debug button opens a palette with a button for every tool and its key;
- the council panel offers "Summon now (J)";
- an unmet god's card offers "Bring its Creature (K)".

So the owner's J and K are buttons, and a player of an exported game is not handed them.

### A fair way to meet a god

An unmet god's card offers "Seek them out". It sends the player's Creature to the other god's: the
same order Send gives (`CreatureCommandKind.GoTo`, through the authority, so a joined player can use
it too), aimed where their Creature is. When the two come within the meeting radius (40 m) they
meet, and so do their gods (ADR-036). No rule is skipped.

### Choices in the menus

- **The New World page** gains "The gods": strangers who meet as their lands grow (ADR-036), or
  known to one another from the start (`NewWorldOptions.GodsMet`). `WorldMaker` introduces every
  pair before the history begins, so no meetings are recorded and the save is unchanged.
- **Graphics** gains "Simple terrain" (`GameSettings.SimpleTerrain`): the fallback renderer from the
  next start, for a computer the detailed land is slow on. `--terrain` still overrides it.

### Esc

Esc cancels a chosen miracle or a waiting order before it opens the menu (`DivineController.Aiming`).
It also puts away the key map, the palette and the chooser.

### Tooling

`--bar=<button>` presses one of the bar's buttons at the start, for captures (for example
`--bar=keys`, `--bar=map` or `--bar=debug`). `--title-page=keys` opens the settings' Keys tab.

## Consequences

- **Nothing in play hides behind a key.** Every panel and choice has a control with its key shown,
  and every key is in the map.
- **One source for the keys.** The key map, the badges, the palette and the debug gate all read
  `KeyGuide`, and the tests tie it to `project.godot`.
- **A changed default.** An exported game has no cheats unless the player turns them on. Captures
  and tests are unaffected, because launch options are not keys.
- **Code moves.** The diplomacy panel moves from `GameRoot` to `PoliticsController`, keeping
  `GameRoot` under 500 lines. `GodCameraController` is at 499 lines.
- **No save or protocol change.**

## Alternatives considered

- **A text toolbar.** Words are clearer than icons, but the eleven names would take about 700 pixels,
  against §57's warning. Each icon's name and key are in its tooltip, and the key map names them
  all.
- **J and K as plain buttons for everyone.** Summoning the council off its timetable and teleporting
  a Creature skip the council's cooldown and the meeting rules (§42: "Periodic major events may
  summon a Divine Council"). Seek is the fair way to meet. J and K stay as debug tools, with
  buttons.
- **A popup window for the map's chooser.** Godot's `PopupMenu` is a window, placed in screen
  coordinates that the interface's scaling shifts. A panel in the HUD's own column sits where it
  should at any scale.
