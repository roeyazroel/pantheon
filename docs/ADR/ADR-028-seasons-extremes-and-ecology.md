# ADR-028: Seasons, weather extremes and the land's life

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-013](ADR-013-weather-cells-and-soil-water.md) (weather cells and soil
  water), [ADR-014](ADR-014-disasters-as-processes.md) (disasters as processes) and
  [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (the Nature domain)

## Context

The weather had spells (dry, wet, normal) but no year, and four kinds: clear, rain, thunderstorm
and windstorm. The land held fixed vegetation, a scorch that fire left and a blessing. The
specification asks for more:

- **§16:** snow, heat and cold waves, fog and hurricanes among the weather's states.
- **§17:** optional seasons that change temperature, rainfall, crop growth, fire risk, snow,
  migration, disease and food storage.
- **§18:** regions that track wildlife, soil, water, pollution, biodiversity and fire damage. God
  actions alter ecology; repeated destructive miracles may permanently damage a region, and nature
  gods may restore it.

## Decision

### Seasons (§17)

- **Optional per world:** `WorldState.Seasons` (`SeasonState`) is present when a world was made
  with them: the start screen's "Climate" (four seasons by default, or an even climate) and
  `--seasons`. A world without them keeps the year-round climate (`Seasons.Steady`).
- **The year:** four seasons of `SeasonSeconds` (150 s, a ten-minute year). The season follows
  from the tick and the year's origin (`ClimateModel`), so it needs no other state. The history's
  years (a minute each) are a different, faster count.
- **Effects:** each season's `ClimateEffects` in `weather.json`:

  | | Spring | Summer | Autumn | Winter |
  | --- | --- | --- | --- | --- |
  | Temperature | | +0.15 | | -0.25 |
  | Weather blowing in | x1.2 | x0.75 | x1.3 | |
  | Harvest | x0.9 | x1.25 | x1.3 | x0.5 |
  | Fire | x0.8 | x1.6 | x0.9 | x0.3 |
  | Stores spoiling, per second | 0.05% | 0.2% | 0.1% | |
  | Sickness, per second | | | | 0.06% |
  | Wish to leave | | x1.1 | | x0.4 |
  | Heat or cold wave after an ordinary spell | 2% / 4% | 50% heat | 3% / 5% | 50% cold |
  | Storms over warm sea that grow into hurricanes | | 25% | 50% | |

- **Where they act:**
  - The weather step warms or cools every region's air and soil, and scales the cells that blow
    in.
  - The settlement step scales the harvest.
  - The fire step scales how readily land catches.
  - The migration step scales the wish to leave.
  - `SeasonSystem` spoils stores and lets the cold sicken (the homeless twice as much), and raises
    `SeasonChanged`.

### Weather extremes (§16)

- **Snow:**
  - Rain on land colder than `SnowTemperature` (with the season's shift) lies as snow
    (`RegionState.Snow`). Snow on land warmer than `MeltTemperature` melts into the soil, so
    spring's melt waters the land and swells the rivers.
  - Nothing grows or burns under snow.
  - A snowing cell is `WeatherKind.Snow`.
- **Fog:** a calm, cool, humid cell is fog: mist and drizzle (`WeatherKind.Fog`).
- **Hurricanes:**
  - A storm born over sea at least `HurricaneSeaTemperature` warm grows into a hurricane by the
    season's share; every other storm stays below `HurricaneWind`.
  - A hurricane fills over land at `HurricaneLandFilling` of a storm's pace, so it reaches inland,
    and tears down `HurricaneHouseLossPerSecond` of a village's houses (`DisasterKind.Hurricane`).
  - `StormStruck.Hurricane` tells when one arrives.
- **Heat and cold waves:** two new spells, chosen after an ordinary spell by the season's chances.
  Each brings its own `ClimateEffects`: a heat wave warms, dries, burns, spoils and sickens; a cold
  wave freezes, snows and sickens. A season and a wave combine: shifts and losses add, factors
  multiply.

### The land's life (§18)

- **State:** regions gain `Wildlife`, `Damage`, `Scar` and `Pollution`. Rules are in
  `ecology.json` (`EcologyRules`), applied by `EcologySystem` and the pure `EcologyModel`.
- **Harm:** fires, floods, earthquakes and lightning (the miracles with a smiting effect) harm
  the land's life.
- **Healing and scars:**
  - Harm heals by `HealPerSecond`, down to the scar.
  - Harm above `ScarThreshold` slowly becomes scar, which never heals by itself: repeated
    destruction damages a region for good.
- **Nature:**
  - Land whose villages mostly follow a god of Nature heals `NatureHealFactor` faster and loses
    its scars.
  - The Regrowth miracle heals harm and scars.
- **Wildlife and hunting:**
  - Wild animals return towards what the land can hold: its cover, unburned, unharmed and
    unpolluted. Fire kills them.
  - Villages hunt the animals of their region and the regions beside it for food, in their own
    step, so a famine is still told once.
- **Pollution:** villages of more than `PollutionFreePeople` pollute their region; it clears slowly.
- **Yield:** harmed and polluted farmland counts for less: fertility times (1 - damage) times
  (1 - pollution).
- **Telling it:** a village whose land's scar passes `ScarNotice` raises `LandScarred`.
- **Biodiversity** is derived, not stored: health times the land's cover and animals.
- **Settling:** gaps under `EcologyModel.Settled` close at once, so settled land stops changing and
  is not sent to players again.

### Saves, multiplayer and presentation

- **Saves:** version 20. Migrating from 19 gives no snow, harm, scars or pollution, wildlife at
  the land's cover (as a new world starts) and no seasons: an old world keeps its even climate.
- **Multiplayer:** protocol 8. The regions section carries snow and the land's life.
- **HUD, alerts and history:**
  - The weather line starts with the season.
  - Alerts mark each season, heat and cold waves, hurricanes and scarred land.
  - The history keeps great storms and scarred land.
- **Debug:**
  - F1's climate line, and the land's life at the cursor.
  - F3's layers of snow, wildlife, damage, scar and pollution, and the O Nature overlay.
  - Y begins the next season; `--season` and the new `--weather` values set them at the start.
- **The look:**
  - Snow lying on the land is one island-wide layer, redrawn every second.
  - Snowing cells' streaks fall white and slow.
  - Clouds and labels are otherwise unchanged.

## Consequences

- **Balance:** 40 one-hour games with seasons found a winner 38 times, with a median of 24.9 and a
  mean of 27.0 minutes (part 7: 33, 25.9 and 30.8).
- **Tuning found:**
  - Hunting first fed villages between settlement steps, so a starving village's famine was told
    every step (3,501 times in six games). Hunting moved into the settlement step: 74 times now,
    against 2 without seasons.
  - A winter harvest of 0.35 killed a quarter of a village each winter; 0.5 leaves winters hard
    but survivable.
  - Snow at first rarely lay (0.3, a slow fall); fog never formed while it had to be dry; and
    hurricanes formed but weakened before reaching a village. Each was raised, allowed to drizzle
    or slowed over land.
- **Cost:** about a tenth more per tick: a Huge world takes 0.68 ms against 0.59.
- **Not modelled:**
  - The rest of §19's disasters (tsunamis, tornadoes, volcanoes, landslides, meteors, crop
    disease, locusts).
  - Snow slowing travel.
  - Grass and trees changing colour with the seasons.
  - Pollution that depends on a village's technology.
