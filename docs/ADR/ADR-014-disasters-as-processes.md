# ADR-014: Disasters as processes: wildfires in regions, earthquakes that shake, ruins to rebuild

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-013](ADR-013-weather-cells-and-soil-water.md) (weather and soil water)

## Context

Milestone 8 (spec §123) delivers earthquakes, wildfires, destruction and settlement response, with
the playable outcome "Disasters produce emergent consequences". The specification asks that:

- disasters be processes, not "deal 500 damage" (§19);
- an earthquake run as a chain: shaking, structural checks, collapse, fire, panic, migration,
  religious interpretation and god intervention (§20);
- physics be used selectively, and buildings carry structural abstractions such as integrity,
  earthquake, fire and flood resistance, the result staying simulation-driven (§94);
- god actions alter ecology, and repeated destruction can damage a region (§18);
- the MVP nature include wildfire and earthquake (§114), and the MVP miracles Fire, Earthquake and
  Shield (§110);
- the map have a danger overlay (§58).

Migration (§20, §31) is Milestone 10's.

## Decision

- **Wildfires live in the regions they burn.** Each region holds `Fire` (how fiercely it burns) and
  `Scorch` (how much of its fuel fire has eaten).
  - A region's flammability is its vegetation × (1 − scorch) × dryness², where dryness falls to 0
    at `WetSoil`. Damp land hardly burns; parched land burns readily. Fire therefore follows
    droughts and dry spells (ADR-013).
  - A fire grows towards `FuelHeat` × flammability and scorches the land. It spreads to each
    neighbour with a chance set by its intensity, the neighbour's flammability and the wind
    (more downwind).
  - Rain and the firefighting of the settlement in the region put it out. It dies when its fuel
    is gone, and scorched land regrows.
  - Fires are lit by:
    - thunderstorms over dry land;
    - sparks from villages' hearths into the dry fields around them;
    - earthquakes;
    - the Fire miracle.
- **Earthquakes are processes.** An `Earthquake` in `WorldState.Disasters` has an epicentre, a
  magnitude, a peak intensity, a radius that grows with the magnitude, and an end.
  - For its few seconds it shakes every settlement in its radius, fading with the square of the
    distance.
  - Each step does a share of its damage: houses collapse into ruins, people die, stores spill,
    building work is lost, and fire may break out.
  - Natural earthquakes strike at a seeded rate, usually small.
- **Structural abstraction** (§94): houses resist a share of earthquake and of fire damage
  (`HouseQuakeResistance`, `HouseFireResistance`). Losses are Poisson draws, so small settlements
  lose whole houses now and then rather than fractions.
- **Destruction leaves ruins.** A destroyed house's housing is lost, and the house stays as a ruin
  (`SettlementState.Ruins`), drawn as rubble on its plot.
- **The settlements respond:**
  - firefighting in their own region;
  - rebuilding: a crowded settlement builds as before, and a ruin takes `RuinWorkShare` of a new
    house's work, each finished house clearing one;
  - the homeless fall ill (`HomelessHealthLossPerStep`);
  - scorched farmland yields less until it regrows.
- **Religious interpretation** (§20, simplest form): a disaster raises fear of every god the
  people know and doubt that the gods care, in proportion to its severity. Danger (fire on the
  farmland or shaking ground) counts as hardship and is a need (`MiracleNeed.Danger`), so a god
  who shields people in danger earns their love.
- **God intervention:**
  - Fire sets the target alight.
  - Earthquake shakes the ground at the target in the god's name.
  - Shield protects the settlements it covers for three minutes; they take `ShieldedDamage` of
    any disaster.
  - Rain still quenches fires.
  - Fire and Earthquake on another god's followers count as harm (ADR-012).
  - AI gods value Fire and Earthquake as intimidation of rivals' followers, minus the harm to
    their own, and never strike an ally. They value Shield by their followers' danger.
- **Order and randomness.** The disaster step runs at 1 Hz after the weather step and before the
  settlement step. Like the weather, it draws a stream seeded by the world seed and the tick, so
  disasters never shift the shared stream.
- **Tools:**
  - alerts for earthquakes, fires near your people and the damage done;
  - the HUD names settlements threatened by fire or shaken;
  - labels mark "burning", "fire nearby", ruins and shields;
  - a Danger overlay (fire, ash, shields), and F3 layers for fire and scorch;
  - F1 lists quakes, the fiercest fires and a settlement's dangers;
  - F11 and F12 start an earthquake or a fire at the cursor, and `--disaster=quake|fire` does so
    at start.

  The client draws flames, smoke and a glow per burning region, ash on scorched land, domes over
  shielded settlements, a shock wave for each quake, and shakes the camera while the ground under
  its focus shakes.
- **Saves** move to version 8. The migration from version 7 adds no earthquakes, no fire or
  scorch, and no ruins or shields.

## Consequences

- Positive: disasters chain through the existing systems. Drought dries the land, so a spark or a
  dry storm starts a wildfire. Houses burn, the homeless fall ill, the harvest from scorched land
  falls, and people fear and doubt. A god can shield, rain or rebuild trust. With seed 1234 and a
  passive player, a dry spell's wildfire reached 33 burning regions and scorched 86, burning 10
  houses and killing 11.
- Positive: earthquakes are rare and local unless a god causes one; the rival Tyrant uses them as
  a weapon of fear, and the player can shield against them.
- Positive: deterministic, isolated from the shared stream, saved whole and fully explained in the
  debug tools.
- Negative: fire spread is a percolation process and is sensitive to dryness. The shipped numbers
  were tuned between runs where nothing spread and runs where the island burned.
- Negative: no road blockage, migration, temples or disaster preparation (§20–21), and no floods,
  volcanoes or other disasters (§19).
- Negative: buildings have one shared resistance per disaster rather than per-house integrity.

## Alternatives considered

- Fire as entities (fronts with positions): finer, but a grid of regions already carries the
  fuel, soil water and settlements fire needs.
- Deterministic heat accumulation for spread: no randomness, but a neighbour then always catches
  at the same moment, so fires grow as squares.
- Instant earthquake damage: §19 asks for processes, and a few seconds of shaking lets the player
  see it coming in the HUD and alerts.
- Destroyed houses simply vanishing: ruins make destruction visible and give rebuilding a target.
