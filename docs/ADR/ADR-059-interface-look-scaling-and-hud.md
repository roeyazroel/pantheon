# ADR-059: The interface's look, its scaling, and a HUD of gauges

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (the
  settings) and [ADR-058](ADR-058-camera-controls-for-mouse-and-trackpad.md) (the controls)

## Context

The owner found the miracle buttons and every text on screen too small, and the interface
unpolished: "it looks like Minecraft F1 help". Investigating found three causes:

- **The window:** the project's 1600 × 900 window is in physical pixels. On a 2× Retina screen it
  opens at a quarter of the screen. The display had no stretch mode, so fullscreen drew the
  interface at half its size.
- **No theme:** there was no font and no theme. Each panel set its own sizes (11–17 pixels for most
  text), colours and boxes in Godot's default font, and two copies of the palette disagreed.
- **The HUD:** it was text. It showed lines of figures for the god, the rivals and the Creature's
  needs, text buttons for the miracles, no pause or speed controls, and status messages ("Saved")
  only in the debug panel.

## Decision

### Scaling

- **Stretch:** the display stretches 2D content (`canvas_items`, aspect `expand`), with 1600 × 900
  as the design size. The interface grows with the window, and the 3D view is still drawn at the
  window's own resolution.
- **The window:** a windowed game opens at nine tenths of its screen at 16:9, centred
  (`SettingsApplier.FitWindow`). On the owner's MacBook that is 2720 × 1530 pixels, and the
  interface is drawn at 1.7×.
- **Interface size:** a new setting (0.75–1.5) scales the interface further.

### The look (`UiTheme`)

- **Fonts:** two fonts under the SIL Open Font License, with their licences beside them in
  `game/Fonts/`. Cinzel, a Roman capital, is for titles. Alegreya Sans is for text, at 18 pixels
  for body, 16 and 14 for secondary text and 24 for titles, at the design size.
- **Applied once:** `UiTheme.Apply` writes the fonts and styles into the engine's default theme
  before any interface is built. Every panel, button, slider, field, popup and tooltip shares them
  without overrides of its own.
- **Style:** deep slate panels with a gilded edge, gold buttons when hovered and pressed, and one
  palette (`UiTheme.Ink`, `Gold`, `Muted`, `Good`, `Bad`, `Warning`, `Calm`).

### The HUD

| Place | What it shows |
| --- | --- |
| Top left | The god's plaque: its name in Cinzel by a stripe of its colour, its rank, Divine Power, its income, its followers and a meter to the next rank. Below it a chip for each rival: name in its colour, standing, and following in the tooltip. |
| Top centre | The race for victory with a meter of the player's share and a mark at the share that wins, then the weather and the domains. |
| Top right | The season and year, pause, ×1, ×4 and ×16, and below them the alerts, edged in their severity's colour. |
| Bottom | The miracle bar of drawn tiles, with the target hint and a status toast above it. |
| Bottom left | The Creature's card. |

**The miracle tiles:**

- **Glyph:** each miracle has a vector glyph (wheat, heart, sprout, bolt, rain, flame, shield, quake,
  drop, sun, moon, spiral, wave, mountain or sparkle), chosen by its look (ADR-055) or its id.
- **Colour:** each tile is tinted by its miracle's colour.
- **Marks:** its key in a badge, its cost (red when it cannot be afforded), a dark sweep while it
  cools, a padlock while the god's rank is too low, and a gold ring when it is aimed.
- **Name and description:** the name is under the tile and the description in its tooltip.

**The Creature's card:**

- **Header:** its name in Cinzel, its level, its species and character, and what it is doing.
- **Meters:** health, hunger, tiredness, boredom and loyalty, and a meter from cruel to kind.
- **Buttons:** what a reward or punishment would judge, then Reward, Punish, Follow and Send, each
  with its key.
- **More:** opens its paths, attributes and bonds.

**The model:** `HudModel` gains the numbers behind these: power, income, followers, the next rank,
belief and winning shares, calendar, pause, speed and rival chips. `CreatureCard` gains its name,
species, level, needs, loyalty and morality. Both are built by `HudReadout` and tested.

**Status messages:** `GameRoot.SetStatus` also shows each message in the status toast.

## Consequences

- **Size:** text is about three times the size it had on a Retina screen. That is twice for the
  window, times 1.7 for the stretch, over 1.0 to 1.3 for the larger base sizes.
- **Panels:** every panel uses the new fonts. The panels that set their own sizes (the Book,
  diplomacy, domains, council, settlements, history and the menus) stretch with the window, but
  keep their layouts.
- **Frame time:** a larger window costs GPU time. The grown world at 160 m drew in 7.3 ms at
  1600 × 900 and 16.8 ms at 2720 × 1530, which is 2.9 times the pixels (60 FPS uncapped on the
  M3 Pro). The render scale setting trades sharpness for speed, as before.
- **Not done:** the start screen is still the panel of before (the next part builds a main menu),
  the panels' own layouts are unchanged, and there are no icons outside the miracle bar.
