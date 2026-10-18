# Population

Reserved for canonical regional and full-tier population data (cohorts, promoted villager agents) — a later milestone
(ADR-004, ADR-008). Population is a statistical aggregate on `Settlements/SettlementState`. Milestone 2's representative
citizens are derived presentation data in `Pantheon.Game.Population` and are never stored in `WorldState` (ADR-008).
