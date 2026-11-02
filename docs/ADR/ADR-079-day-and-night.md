# ADR-079: Day and night, the sky, and figures with some life

- Status: Accepted
- Date: 2026-11-02
- Amends: [ADR-055](ADR-055-expressive-creatures-and-miracle-looks.md) (miracle looks) and
  [ADR-077](ADR-077-land-water-and-villages.md) (a fixed sun)

## Context

The owner asked for day and night, and to finish the realism pass: the sky, people, Creatures and
miracles. The specification does not mention day and night, so it is a new decision.

The sun was fixed, and the sky a stock gradient. Close up, people had tube arms, a Creature's skin was
smooth with jagged edges between its markings, and miracles were flat translucent shapes.

## Decision

### The hour comes from the world's clock (`DayNight`, pure)

`DayNight.HourAt` follows from the tick, as a season does, so every player of a shared world sees the
same hour, a save brings it back, and there is no save or protocol change. A day lasts 240 seconds of
the world's time. A new world begins at 9 in the morning. The hours, colours and sun's path are
`day_night.json`.

### It changes the look, and never the rules

The day is presentation only. Nothing in the simulation reads it. Villages' food, belief and
miracles are balanced over 160 seeds, and rules that hang on the hour would change them all. If nights
are to matter to play, that is a later and separate decision.

### One light, sun by day and moon by night

The light swings east to west across the south of the sky, and changes from moon to sun at the horizon,
where its energy is 0, so the swap never shows and there is no second shadow-casting light to pay for.
It is drawn no lower than 7 degrees, so shadows stay on the land.

The weather composes over the hour: storms and eclipses dim whatever the hour gives. Dawn and dusk need
far more `Ambient` than noon, as the sky's dim colours are also the light it gives the land.

### The sky (`sky.gdshader`)

A custom sky replaces the stock one: a gradient the hour sets, a glow and a disc for the sun or moon,
and high cloud and stars in the sky itself but not in the light it gives. A sky that reads the scene's
light is refiltered every frame the light moves, which cost up to 0.6 ms in the near view. So it is
given the light as plain uniforms, like its colours, twice a second, and refiltered incrementally at
128 pixels.

### Night in the villages

- Windows light, house by house as the dark comes on, each lamp a little unsteady.
- A warm lamp burns by each shrine.
- Most people go indoors, each at their own hour. They are hidden, not simulated: the crowds are
  derived, and go on as they were.
- Clouds and rain, drawn unlit, dim with the sky.

### Controls

A "Day and night" setting (off holds a fine afternoon), the part of the day beside the season, `\` for
six hours on with the debug tools, `--hour` to hold an hour, and the hour in F1.

### Figures and miracles

- **People:** tapered sleeves, hands, a tunic that flares below the belt, broader shoulders; they darken
  towards the feet and take the clouds' shadow.
- **Creatures:** fine relief over the coat, by a triplanar normal map on the existing material, a stronger
  soft rim, and soft edges between markings, where a hard one showed the mesh's cells.
- **Miracles:** threads of brighter light rise through pillars and domes, which melt into the ground at
  their foot; the ring of a wave is half as strong.

## Consequences

- **Performance**, uncapped at 2720x1530 on an M3 Pro, the build before part 55 against this one, with
  the sun or moon moving. "Before" had a fixed afternoon sun; noon is the dearest hour of the day:

  | View | Before | Noon | Night |
  | --- | --- | --- | --- |
  | Close (40 m) | 15.6 ms | 15.6 ms | 15.6 ms |
  | Near (110 m) | 17.2 ms | 16.7 ms | 16.9 ms |
  | Wide (420 m) | 21.9 ms | 19.4 ms | 17.9 ms |
  | High (1400 m) | 17.8 ms | 16.4 ms | 16.0 ms |
  | Stress world, morning | 21.7 ms | 19.5 ms | |

  The commit of this part gave better daytime numbers, measured by mistake at tick 9000, which is
  3 in the morning. These are the true ones.

- At ×16 a day passes in 15 seconds, and shadows sweep visibly. It is the price of a clock every
  player shares; the setting holds the hour for those who mind.
- A year of ten minutes holds two and a half days. The day and the year are each paced for play, not
  for each other.
- `HutPlacement` and the save are unchanged, and `PresentationContent` gains `day_night.json`.

## Alternatives considered

- **A clock of its own, slowed at high speed:** players of a shared world would see different hours.
- **Rules that follow the hour:** see above.
- **A separate moon light:** a second shadow pass, for a change that never shows.
- **Physical sky:** it gave less control over the colours of dawn and dusk, and no stars or moon.
