# ADR-008: Settlement step cadence and representative citizens

- Status: Accepted; amended by [ADR-032](ADR-032-prophets-elders-and-temple-influence.md) and [ADR-049](ADR-049-people-that-look-and-move-like-people.md)
- Date: 2026-10-18
- Amends: [ADR-004](ADR-004-statistical-population-simulation-with-tiering.md)

## Context

ADR-004 simulates population statistically everywhere and adds detail near the camera in three
tiers: Statistical, Regional ("cohorts and work groups with simple schedules") and Full
(individual agents promoted from and folded back into the aggregates). Aggregates are
authoritative, tier changes conserve totals, and Milestone 1 implemented only the statistical
tier, stepping the settlement every 10 Hz tick. The roadmap schedules the Regional and Full tiers,
with promotion and demotion, for Milestone 12.

Milestone 2's brief asks for:

- a statistical settlement model that steps at about 1 Hz, decoupled from rendering;
- visible representative citizens near the camera, a few hundred standing for the larger
  population, spawned from the statistical state and never canonical.

Stepping every tick spreads each rule over tiny per-tick rates and costs ten times more than the
brief needs. Representative citizens are a form of the Regional tier, but ADR-004 does not say
where that tier lives, whether it is saved, or how it relates to promotion and demotion. ADR-004
also rejected "pure statistics with cosmetic agents" as the overall model, because the player's
interactions would not feed back into the world. These points need a decision rather than a
silent reinterpretation.

## Decision

- **Tier numbering.** Tier 1 is Statistical, Tier 2 is Regional and Tier 3 is Full, as in
  ADR-004's table. Readouts and documents use both the number and the name.
- **Settlement step cadence.** The statistical settlement model steps once every
  `StepIntervalTicks` ticks (`settlement_rules.json`, 10 by default): once per simulated second at
  ×1. `IntervalSystem` runs it on the last tick of each interval, so `10 × n` ticks run exactly
  `n` steps. Every rate in `settlement_rules.json` is per step. The scheduler keeps ticking at
  10 Hz, so ADR-006 is unchanged, and other systems can choose their own interval later.
- **Statistical model.** The aggregates gain `Health` (0..1) and `ConstructionProgress` (work on
  the one house being built). Labour splits into builders and farmers, farmland is the land
  around the settlement with diminishing returns, starvation lowers health, and health changes
  births and deaths. `Housing` stays the canonical capacity in people.
- **Tier 2 in Milestone 2: representative citizens.** Near the camera (promotion within
  `RegionalTierEnterDistance`, demotion beyond the larger `RegionalTierExitDistance`), a
  settlement shows up to `MaxRepresentatives` figures, each standing for `Population / N` people.
  Their roles follow the aggregate labour split, and each follows a simple schedule (home, work,
  gathering at the shrine). They are:
  - derived by pure functions of `WorldState`, the world seed and simulation time, in
    `Pantheon.Game.Population`;
  - never saved and never written back; a load or a new world rebuilds them;
  - free of effects of their own: they show the work the aggregates already account for, so
    demotion has nothing to fold back and totals are conserved trivially;
  - selected per client from the camera. The tier is presentation state, not `WorldState`.
- **Not the final model.** This is an interim presentation of the Regional tier, not the
  "pure statistics with cosmetic agents" alternative that ADR-004 rejected. Milestone 12 adds
  regional and full tiers whose agents have effects, with promotion and demotion that write back
  to the aggregates; that milestone amends or supersedes this ADR.
- The rest of ADR-004 stands.

## Consequences

- Positive: the settlement model runs ten times less often, and its per-step rates read as
  per-second quantities in the data file and the overlay.
- Positive: representatives cost no save space, no random stream and no conservation logic, and
  they cannot corrupt the simulation. A few hundred of them render with one MultiMesh per
  settlement.
- Positive: what the player sees near the camera reflects the aggregates: more builders appear
  while a house is going up, and fewer people work during a famine.
- Negative: `StepIntervalTicks` and the per-step rates are coupled. Changing the interval
  changes the dynamics unless the rates are rescaled with it.
- Negative: representatives are cosmetic until Milestone 12. Interactions with individuals (the
  hand in Milestone 4, for example) need either that milestone's promotion or a decision of
  their own.
- Negative: a client that zooms in and out promotes and demotes settlements; the hysteresis gap
  and in-place promotion keep this from popping, but rosters are rebuilt on every promotion.

## Alternatives considered

- Simulate Tier 2 agents in `Pantheon.Simulation` with their state in `WorldState`: canonical
  copies of the population would need conservation and promotion logic now, which is
  Milestone 12's scope, and saves would grow with the camera's history.
- Keep stepping the settlement every tick: it contradicts the brief, and rates of a few
  hundred-thousandths per tick are hard to tune and read.
- Ambient figures unrelated to the aggregates (a fixed crowd per settlement): cheaper still, but
  they would not show growth, construction or famine, so the presentation would lie about the
  state.
