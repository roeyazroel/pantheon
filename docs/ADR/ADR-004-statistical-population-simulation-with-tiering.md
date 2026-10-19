# ADR-004: Statistical population simulation with tiering

- Status: Accepted; amended by [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) and [ADR-032](ADR-032-prophets-elders-and-temple-influence.md)
- Date: 2026-10-18

## Context

A god game needs many settlements and thousands of villagers whose collective mood (belief)
matters more than any individual. Simulating every villager as an agent everywhere would
dominate the CPU budget and complicate saves and networking.

## Decision

Population is simulated **statistically by default**, with detail promoted only where the player
(or a rival god's attention) needs it:

| Tier | Scope | Representation |
| --- | --- | --- |
| Statistical | Every settlement, always | Aggregates: population, food, housing, belief components, with stochastic rates driven by `SimulationRandom`. |
| Regional | Settlements and regions near the camera or influence | Cohorts and work groups (farmers, builders, worshippers) with simple schedules. |
| Full | Small area under the camera or hand | Individual villager agents spawned from the aggregates and folded back into them on demotion. |

- Aggregates in `WorldState` are always authoritative. Higher tiers are derived, and their net
  effects are written back to the aggregates when they demote.
- Tier changes must conserve totals (population, food, belief) so promotion and demotion
  cannot create or destroy resources.
- Milestone 1 implements **only the statistical tier**, for the single settlement: food
  production from region fertility, consumption, and Poisson-sampled births and deaths
  (`SettlementSystem`).

## Consequences

- Positive: cost scales with the number of settlements, not villagers. Saves stay compact and
  network replication stays cheap (ADR-003).
- Positive: belief (Faith, Love, Fear, Dependency, Doubt, Resentment) can be modelled per god per
  settlement as aggregates from the start.
- Negative: promotion and demotion logic must be designed carefully (Milestone 12) so it does
  not cause visible popping or exploits.

## Alternatives considered

- Full agent simulation everywhere: rich, but too expensive and hard to network.
- Pure statistics with cosmetic agents: cheap, but the player's direct interactions would not
  feed back into the world.
