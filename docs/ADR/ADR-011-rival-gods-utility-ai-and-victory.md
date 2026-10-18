# ADR-011: Rival gods: utility AI, conversion between gods and victory

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-009](ADR-009-belief-influence-and-miracles.md) (belief and miracles) and
  [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature)

## Context

Milestone 5 (spec §120) delivers a second god, utility AI, competing belief and conversion. Its
playable outcome is "Religious competition exists". The specification asks that:

- AI gods behave like strategic actors with personalities (§47);
- they use utility AI rather than rigid behaviour trees, scoring every possible action (§48);
- their choices can be explained (§49);
- they evaluate every 2–5 seconds (§87);
- conversion is gradual and several religions can coexist (§32);
- weaker gods pay more where influence is contested (§10);
- the first god to hold 60% of the world's belief for ten minutes wins (§115);
- neither love nor fear dominates (§8).

ADR-009 already lets several gods share a settlement, but people could only join a god from the
share that followed none. ADR-003 and §73 require an AI god to have no shortcut past the rules that
bind a player.

## Decision

- **A rival is a god like the player's.** `gods.json` lists Vael, controlled by the AI, with a
  `Personality` and a Creature (Gristle). Each later god's home is the settlement furthest from
  the homes already taken, so rivals start apart. `GodState` stores the personality; a player's
  god has `None`.
- **Strategic utility AI in the core** (`GodAi`, run by `GodAiSystem` every
  `EvaluationIntervalTicks`: 30 ticks, 3 s). Each evaluation scores:
  - every miracle the god may cast (checked by `MiracleCaster.Check`) on every settlement;
  - sending its Creature to the settlement it courts most;
  - waiting (`Patience`).

  A cast's value is the sum over the settlements it reaches of the personality's goals, each
  weighted by the settlement's size:
  - protect its followers in need;
  - defend followers a rival is courting;
  - convert people who do not follow it, more where they suffer or doubt (opportunism);
  - bless the land;
  - intimidate rivals' followers.

  Utility is value × (1 − thrift × cost / power). The best candidate, plus a little seeded
  jitter, is carried out through the same `MiracleCaster` and `CreatureAuthority` a player uses,
  as the god itself. The god also teaches its Creature: it rewards or punishes the Creature's
  latest unjudged deed by how that deed's kindness suits its personality. Every candidate keeps
  its factors as text, which the debug view shows (§49, §106).
- **Personalities are data** (`god_ai.json`): Guardian, Tyrant, Opportunist, Zealot and Nature
  Keeper. Each weighs protect, convert, intimidate, bless, opportunism, thrift, sending the
  Creature and Creature kindness. The rest of §47's personalities (Trickster, Isolationist,
  Warlord) need diplomacy and war (Milestones 6 and 9).
- **Conversion between gods.** The people who leave a god follow the other gods in the settlement
  in proportion to their pull, or no god in proportion to `ApostateFreedom`. A rival therefore
  gains what a neglected or doubted god loses, and the shares still never pass 1.
- **Contest.** A god's miracle adds `RivalMiracleDoubt` to the doubt of every other god's
  followers who see it: another god can work wonders here. Casting where a rival's influence
  exceeds one's own costs more by `ContestedCostFactor × (rival − own)`.
- **Love against fear** (§8):
  - fear converts faster than love but fades twice as fast as it did in Milestone 3;
  - frightened followers fall away more easily;
  - a killing raises doubt and costs love rather than reassuring anyone.

  A tyrant must keep striking, which costs power and people, while a loving god's followers stay.
- **Victory** (`VictorySystem`, on the belief step's cadence). A god holding `VictoryShare` (60%)
  of all followers dominates, recorded as `DominantSinceTick` and announced by `DominanceChanged`.
  Holding it for `VictorySeconds` (600) wins: `WorldState.WinnerGodId` is set and
  `VictoryAchieved` is raised. The world goes on afterwards. A world with fewer than two gods
  has no contest.
- **Saves** move to version 5. The migration from version 4 gives every god no personality and
  no dominance, and the world no winner; old single-god worlds stay single-god.

## Consequences

- Positive: the AI follows the player's rules exactly, so multiplayer will add no new paths, and
  every AI decision can be explained from its factors.
- Positive: personalities are tuning, not code; a new one is a data entry until it needs a new
  goal.
- Positive: headless contests make balance measurable. With seed 1234 a passive player loses to
  the Tyrant in about 19 simulated minutes, and a player who keeps casting Food on the frontier
  wins in about 13. Vael then fights back.
- Negative: the AI scores every miracle on every settlement each evaluation. That is cheap for 8
  settlements and 2 gods, but grows with both (Milestone 9: four gods; later: 20–50
  settlements). Profile it then.
- Negative: the AI plans one action at a time. It has no long-term goals or memory of the player
  yet (§47 "short-term and long-term goals", "diplomatic memory"); diplomacy (Milestone 6) adds
  them.

## Alternatives considered

- Behaviour trees for the god AI: §48 asks for utility AI, and trees would hard-code priorities
  that personalities should weigh.
- A separate "conversion pressure" between gods: the free-share pull already models attraction,
  and sending apostates to the strongest pull adds competition with no new state.
- Victory by settlements held rather than followers: §53 and §115 speak of "global active
  belief", which followers measure directly.
