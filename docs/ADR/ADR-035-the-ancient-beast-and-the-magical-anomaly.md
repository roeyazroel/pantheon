# ADR-035: The ancient beast and the magical anomaly

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-034](ADR-034-great-events-plague-eclipses-reformations-and-world-wars.md) (great
  events), [ADR-017](ADR-017-creature-relationships-and-combat.md) (Creatures fighting) and
  [ADR-029](ADR-029-hazards-preparation-and-marks-on-the-land.md) (the volcano and how disasters
  strike)

## Context

ADR-034 built most of §52's world events. It left out two:

- the ancient monster awakening;
- the magical anomaly.

§52 also says some events encourage rival gods to cooperate. A monster that ravages everyone's
villages is the clearest case.

## Decision

### The ancient beast

- **Its state:** `GreatEventsState.Beast` (`BeastState`) holds its name, lair, phase (sleeping,
  awake, returning, slain), position, health, the village it hunts, its way, and the harm each god
  has done it.
- **Its lair:** found the first time it is needed, on the highest land at least four regions from
  every village. Its name comes from the seed.
- **Waking:** after its first 25 minutes of sleep, it wakes by itself half a time an hour, or when a
  quake of magnitude 6 or more, or the volcano erupting, comes within 900 m of its lair. Its waking
  is a great event named after it.
- **Awake, for up to ten minutes (`BeastSystem`):**
  - it walks at 3 m/s to the nearest village it did not ravage last;
  - it ravages that village for a minute, striking every 10 seconds (6% of the houses and 3% of the
    people, before protection), then moves on;
  - walls spare half of its harm (`Preparedness.BeastWalls`);
  - tired, it walks home and sleeps whole again.
- **Fighting it:**
  - a Creature within 25 m harms it by its power (0.6 a second per unit), losing 0.025 health a
    second, and flies beaten and banished at 0.2;
  - a smiting miracle (Lightning, Spear of Heaven) harms it twice its smiting, and fire under it
    0.5 a second;
  - it heals 0.15 a second while nothing harms it;
  - AI gods send their Creatures within 700 m to fight it when it ravages one of their villages;
    the player sends theirs with V.
- **Cooperation:** no god alone easily brings down its 100 health, and its blows count as a
  disaster, so the council's coordinated response makes help cheaper where it struck.
- **Its fall:** slain, it is gone for good. Every god that harmed it shares the glory: its
  followers feel faith +0.12, love +0.15 and doubt −0.08 by its share of the harm (`BeastSlain`).

### The magical anomaly

- **When and where:** power surges at a land region drawn from the seed, first 15 to 25 minutes in,
  then every 25 to 40 minutes, for three minutes.
- **Within 250 m:** miracles cost half.
- **Power:** each god draws 4 Divine Power a second times its influence at the anomaly's heart, so
  the gods vie for influence there.
- It is a great event with a place (`GreatEventState.Position`).

### What the player sees and does

- **The beast:** a huge Creature-like body ("Beast" in `divine_view.json`: dark hide, glowing red
  ridges and great horns).
  - It lies half-buried in its lair, labelled "The lair of …".
  - Awake, it walks and its label tells what it does and its health.
  - Fallen, it lies on its side: "Here fell …".
- **The anomaly:** a column of violet light (`AnomalyColor`) with a pulsing glow and its name.
- **Alerts:**
  - the beast waking (danger), going back to sleep, and falling (the glory is the player's when
    they fought it);
  - the anomaly.
- **Elsewhere:** the history, the top bar and the book name them, and F1 gives the beast's
  health, position, lair and each god's harm.
- **Debug drivers:** the `;` cycle runs on from the reformation to the beast and the anomaly, and
  `--great-event=beast|anomaly` starts one and looks at it.
- **Saves and sharing:**
  - saves move to version 26;
  - the protocol moves to version 14, as the GreatEvents section now carries the beast.

## Consequences

- **§52:** every example is built.
- **Balance:** over 40 seeds (30–69), 38 games are won within the hour, at a median of 25.7
  minutes and a mean of 27.8. Part 14 gave 37, at 25.0 and 27.4.
- **Rates:** over ten one-hour games, the beast wakes about twice a game, from 25 minutes in. It is
  slain in about one game in three, and strikes about 34 blows a game. The anomaly comes 1.8 times a
  game.
- **Not modelled:**
  - a Creature that chooses for itself to fight the beast (Creatures fight it when near or sent);
  - the beast's harm to the land.
