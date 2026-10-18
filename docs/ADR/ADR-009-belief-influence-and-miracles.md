# ADR-009: Belief, influence and miracles

- Status: Accepted
- Date: 2026-10-18

## Context

Milestone 3 (First God, spec §118) delivers Divine Power, influence, belief and basic miracles,
so that the player can gain and spend belief. The specification describes what these are
(§7–15, §73, §89, §107, §109–110) but leaves open:

- how belief is stored, and how it turns into followers and Divine Power;
- whether influence is stored state or derived from other state;
- how miracles are defined, checked and applied, so that Milestones 12–13 can send them as
  intents to a host (§73, ADR-003);
- how the player casts. §55 says the player "primarily interacts using a Divine Hand", but no
  milestone delivers the hand, and physical interaction needs the Jolt work of Milestone 4;
- how settlements report what happened to alerts and effects (§91), without saving transient data.

## Decision

- **Belief per god per settlement.** `SettlementState.Beliefs` holds one `GodBelief` per god the
  people know of: `Adherence` (the share of the people who follow the god; the shares of all
  gods add up to at most 1, and the rest follow no god) and `Faith`, `Love`, `Fear` and `Doubt`
  (§7, §109), all in 0..1. Dependency and Resentment wait (§109 allows it). Followers are
  `Population × Adherence`; they are derived, never stored.
- **The belief step.** `BeliefSystem` runs every `DivineRules.StepIntervalTicks` ticks (10, 1 Hz
  at ×1, §89) through `IntervalSystem`, after the settlement step. Each belief drifts from its
  values before the step: Faith grows with worship, erodes with Doubt and is forgotten by those
  who do not follow; Love grows among comfortable followers, fades and wears down in hardship;
  Fear fades in safety; Doubt drifts towards a "silence" level and rises among followers in
  hardship (hunger or ill health). Adherence converts the people who follow no god with a pull
  of `Faith × (base + Love + Fear) × (1 − Doubt)` and loses followers with a push of
  `Doubt × (base + Fear) × (1 − Love)`. Fear converts fastest and holds worst; Love converts
  slower and holds best (§8). Every rate is data in `game/Data/divine_rules.json`.
- **Worship is work.** `SettlementEconomy.AllocateLabour` takes `WorshipWorkforceShare` of the
  workers, scaled by the settlement's devotion (Σ adherence × Faith), out of the fields, so
  belief costs food (§112).
- **Divine Power** (§9): per belief step, each settlement raises `worshippers × intensity ×
  infrastructure × PowerPerWorshipperPerStep` for each god, where worshippers are workers ×
  share × adherence × Faith, and intensity rises with Love and more with Fear. The shrine is the
  only infrastructure. Power has no cap.
- **Rank** (§13) is the highest threshold the god's followers reach, from data, and never falls.
  Higher ranks unlock miracles.
- **Influence is derived** (§10). `InfluenceField.Build` turns each settlement with followers
  into a source: strength `min(1, k × adherence × Faith)` at its centre, fading linearly to zero at
  a reach that grows with the square root of its followers. Sources combine as
  `1 − Π(1 − influence)`. Nothing about influence is saved, so it can never disagree with belief.
  Several gods can hold influence over the same place.
- **Miracles are data and are checked by an authority.** `game/Data/miracles.json` defines each
  miracle (§107): id, name, domain, scale, cost, cooldown, radius, influence requirement, minimum
  rank, effects (`AddFood`, `Heal`, `Bless`, `Smite`), the change in the witnesses' belief and
  the need that scales their gratitude. A `CastMiracleRequest` (god, miracle, target) goes
  through `MiracleCaster`, which checks, in §73's order, that the god exists, the miracle exists,
  the issuer controls the god, the rank suffices, the cooldown is over, the target is in the
  world, the influence there meets the requirement, and the god has the power. Costs rise where
  influence is weak: `cost × (1 + factor × (1 − influence))`. Only an accepted request changes
  the world. `GameSession` is the local authority; a host will be the same code (ADR-003).
- **Miracles change belief.** The settlements a miracle reaches get its effects and its full
  belief change; others within `WitnessRadius` get a share that falls with distance, and people
  who did not know the god start to (with `NewBelieverDoubt`). A miracle that answers a need
  (food for the hungry, healing for the sick) earns more Love than one nobody needed.
- **Click-to-cast first.** The player selects a miracle (keys 1–9 or the miracle bar) and clicks
  the ground. The Divine Hand (§55), with its tactile physics, comes with the Creature in
  Milestone 4, which brings Jolt; casting through the hand will send the same requests.
- **Domain events are transient** (§91). Systems and the caster append `WorldEvent` records
  (`MiracleCast`, `FollowingChanged`, `RankAchieved`, `FamineStarted`) to the tick's list. The
  session hands them to the client, which turns them into alerts (§59) and effects. Events are
  not saved: the world state already holds their consequences. History (§92) will select the
  significant ones later.
- **Several settlements.** World generation founds up to `StartingSettlement.Count` settlements
  (8, as §108's MVP world) on the best habitable regions at least `MinimumSpacing` regions apart,
  so their farmland never overlaps. Each god in `game/Data/gods.json` starts as the main god of
  its own home settlement, best site first.
- **Saves** move to version 3, with a migration from version 2 that adds the Milestone 3 player
  god followed in the first settlement.

## Consequences

- Positive: belief, power and influence are plain data and pure functions, tested headless, and
  a rival god (Milestone 5) uses the same rules: competing gods share the free share of each
  settlement and convert it.
- Positive: influence and followers cannot go stale, and saves stay small.
- Positive: every cast already goes through the check a host will make, so multiplayer adds
  transport, not rules.
- Negative: influence is recomputed on demand: cheap now (8 settlements), but its cost grows
  with settlements × gods × queries. Cache it per belief step if profiling shows a cost.
- Negative: without the Divine Hand, §55's tactile interaction is missing until Milestone 4.
- Negative: transient events are lost on save and load, so an alert raised just before a save
  does not repeat after loading. History, when it comes, must store what it keeps.
- Negative: belief rates are per step, like the settlement rates (ADR-008): changing
  `StepIntervalTicks` changes the dynamics unless they are rescaled.

## Alternatives considered

- Store influence per region in `WorldState`: faster lookups, but it duplicates belief, needs
  its own update order and migration, and can disagree with belief after a load or a debug edit.
- One belief record per settlement with a "main god": simpler, but it cannot express several
  religions in one settlement (§32) or conversion between gods.
- Build the Divine Hand now with ray-cast picking and no physics: it would be redone with Jolt in
  Milestone 4, and §55's value is the tactile physics.
- Save events in the world for a history log: §92 asks history to select significant events, not
  to store every event; that selection belongs to the history milestone.
