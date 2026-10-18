# ADR-013: Weather: moving cells, spells and the water in the soil

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the settlement step)
  and [ADR-009](ADR-009-belief-influence-and-miracles.md) (belief and miracles)

## Context

Milestone 7 (spec §122) delivers weather cells, moving storms, rain and drought, with the playable
outcome "World produces environmental pressure". The specification asks that:

- weather be simulated as cells that move across regions, each with a position, velocity,
  pressure, temperature, humidity, precipitation, wind and electrical activity (§16);
- the states include clear, rain, thunderstorm and windstorm, among others (§16);
- weather update one or two times per second, never at rendering frequency (§16, §89);
- seasons be optional (§17), and climate modelling stay simple at first (§114);
- the MVP nature have rain, storm and drought (§114; wildfire and earthquake belong to Milestone 8);
- a Rain miracle exist (§110), and miracles have systemic consequences (§15);
- the map have a weather overlay (§58) and the debug tools show weather cells (§106).

Until now a region's moisture was a fixed climate value, and harvests depended only on fertility,
blessing and labour.

## Decision

- **Weather is state.** `WorldState.Weather` holds:
  - the cells (`WeatherCell`, with §16's fields plus a radius and the god who called it);
  - the spell;
  - when the spell ends;
  - the prevailing wind.

  Each region gains `SoilMoisture`, the water in its soil now, distinct from its climate
  `Moisture`.
- **The weather step** (`WeatherSystem`, every 5 ticks, 2 Hz, first in the pipeline):
  - every cell rains (rain needs humidity above a threshold and grows with the depth of the low),
    which spends its humidity;
  - over the sea a cell takes up water; over land its low fills and the cloud thins;
  - cells take on the warmth beneath them and move with their velocity;
  - a cell vanishes when it has thinned away or left the map;
  - new cells blow in at the upwind edge, and a few are scattered over the map when the weather
    first starts.

  Wind follows the depth of the low, lightning follows depth × rain × warmth, and
  `WeatherModel.Classify` names the state (clear, rain, thunderstorm, windstorm).
- **Spells instead of seasons.**
  - The island is always in a normal, dry or wet spell of seeded length.
  - A dry spell brings a tenth of the cells and dries the soil faster. A wet spell brings twice as
    many, with more storms.
  - The prevailing wind turns when a spell changes.

  Spells give droughts and stormy periods a beginning and an end the player can see and plan
  around, with no calendar.
- **Soil water.**
  - Each land region's soil gains rain where there is room for it, loses water to evaporation
    (faster when warm or in a dry spell), and springs refill it in proportion to the climate's
    moisture.
  - A settlement's drought is `1 − mean soil of its farmland / WellWateredSoil` (0 when well
    watered).
  - Its harvest is multiplied by a water yield that falls from 1 to `DroughtYieldFloor`.
  - A settlement is "in drought" from `DroughtThreshold`. These are farming rules, so they live in
    `settlement_rules.json`.
- **Weather reaches belief through hardship.** Drought counts as hardship
  (`DivineRules.DroughtWorry`), so people worry and doubt before their stores run out. The drought
  is also a need (`MiracleNeed.Water`) that scales the love a Rain miracle earns.
- **Storms do damage.** Where wind passes `StormWind`, a settlement loses a share of its stored
  food and of the work on its house under construction every second. Lightning, fire and
  destroyed buildings belong to Milestone 8.
- **The Rain miracle** (`MiracleEffectKind.Rain`) calls a gentle, slowly drifting rain cell over
  the target. The weather step then waters the land.
  - AI gods value Rain only where drought is past `GodAiRules.RainDrought`, and more the drier the
    land.
  - Rain on another god's parched followers is remembered as help (ADR-012).
- **Randomness.** The weather draws from a stream seeded each step by
  `SeedMixer.Derive(seed, "weather", tick)`.
  - The weather never shifts the settlements' births and deaths in the shared stream.
  - The save needs no second random state.
  - A run is still a pure function of the seed and the inputs, across a save and load.
- **Events and tools.** The weather step raises:
  - `WeatherSpellChanged`;
  - `StormStruck` when storm winds reach a settlement;
  - `DroughtChanged` when its farmland falls into drought or recovers.

  Alerts tell the player about those that concern their people. The other tools:
  - the HUD shows the spell during a dry or wet spell and names parched and storm-struck
    villages;
  - labels show "drought";
  - the O key has a Weather overlay of soil water, and F3 has a soil layer;
  - F1 lists the spell, the wind and the strongest cells, and a settlement's water;
  - F8 cycles the spell, F10 calls a storm at the cursor, and `--weather=` sets either up at start.
- **Presentation** (`CloudLayout`, `weather_view.json`, `WeatherPresenter`):
  - clouds are soft camera-facing puffs laid out by `PresentationHash`, darker and denser for
    storms and fainter as they dry. They fade out near the camera, so they never hide the ground
    up close;
  - rain falls as streaks seen only nearby;
  - thunderstorms strike lightning in hashed time slots;
  - the sun dims under storms over the camera's focus.

  None of it reads or writes simulation state.
- **Saves** move to version 7. The migration from version 6 gives every region its climate's
  moisture as soil water, and the world clear skies, a normal spell not yet scheduled and a west
  wind.

## Consequences

- Positive: environmental pressure. With seeds 1234, 42 and 7 and a passive player, 40 simulated
  minutes see 11–13 droughts and 4–7 storm strikes on villages. A dry spell takes several villages'
  farmland to about 0.2 soil, their harvests fall, hardship rises and the rival god answers with
  rain.
- Positive: rain, drought and storms work through systems already there (harvest, stores, hardship,
  need-scaled love, diplomacy's help), so they combine with miracles, Creatures and rivals without
  new special cases.
- Positive: weather is deterministic, isolated from the shared random stream and fully shown in
  the debug tools.
- Negative: weather affects farming, stores and building work only. Snow, heat and cold waves,
  fog, hurricanes, floods and fire are not modelled; Milestone 8 adds disasters.
- Negative: no seasons (§17 makes them optional). Spells stand in for longer rhythms.
- Negative: clouds are hidden near the camera; up close the weather shows as rain, dimmed light
  and lightning only.
- Negative: the soil is sampled at region centres, and cells are at most a few hundred metres
  wide, so weather is coarse on a 2 km island.

## Alternatives considered

- A per-region weather grid (diffusing humidity and pressure between neighbours): closer to
  climate modelling than §114 wants, and storms would not travel as recognisable bodies.
- Global rainfall with no cells: no moving storms (§122) and nothing to see coming.
- A seasonal calendar: optional in §17, and a fixed cycle would make droughts predictable rather
  than a pressure to answer.
- Saving a weather random state in `WeatherState`: equivalent results, but one more state to
  migrate and validate for no gain.
