# ADR-041: Lightning's fires and hurricane warnings

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-014](ADR-014-disasters-as-processes.md) (wildfires) and
  [ADR-028](ADR-028-seasons-extremes-and-ecology.md) (hurricanes)

## Context

Two of the specification's own examples were not true of the game:

- **§15:** gives a chain from a miracle: lightning hits a tree, a forest fire, wildlife migrates,
  nearby settlements lose hunting, food decreases, migration begins. From the fire on, the chain
  held (ADR-014, ADR-028), but a god's Lightning killed and frightened without setting anything
  alight; only storms' lightning lit fires.
- **§59:** gives the alert "A hurricane is approaching the western coast". Hurricanes were told of
  only when they struck a village.

## Decision

- **Lightning's fires:** a god's bolt of lightning (any smiting miracle) sets the struck region
  alight with `LightningMiracleIgnition` (1.0) times the region's flammability (`disasters.json`).
  - Flammability is the same as for storms' lightning: its growth, times its dryness squared, less
    scorch and snow.
  - The roll comes from a stream of the seed and the tick, so no shared stream shifts.
  - The fire is a lightning fire (`FireCause.Lightning`) and spreads, scorches and drives off
    wildlife like any other.
- **Hurricane warnings:** the weather raises `HurricaneApproaching` when a hurricane forms at the
  upwind edge of the world. Every player is warned of the coast it comes towards ("A hurricane is
  approaching the western coast: its winds tear down houses and ruin stores."), from the island's
  centre on an eight-point compass (`WeatherText.Coast`).

## Consequences

- **§15:** both of its chains now run from the miracle to the migration.
- **§59:** its examples are all alerts.
- **Balance:** AI gods cast lightning, so their bolts now start fires in dry woods. Over 160 seeds
  (30–189), 141 games are won within the hour; 139 were won before.
