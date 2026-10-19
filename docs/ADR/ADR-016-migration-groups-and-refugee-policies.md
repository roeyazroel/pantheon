# ADR-016: Migration as groups on the road, and gods' refugee policies

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the statistical
  settlement), [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) (gods' memory of acts)
  and [ADR-014](ADR-014-disasters-as-processes.md) (disasters)

## Context

Milestone 10 (spec §125) asks for migration, refugees and conversion through migration, with the
playable outcome "Populations can abandon gods". The specification also asks:

- **Who leaves (§31):** people may leave for famine, persecution, war, disaster, poverty, fear, low
  religious confidence or opportunity elsewhere.
- **Refugees (§31):** refugees create political consequences. A neighbouring god may accept them,
  reject them, demand conversion or negotiate their return.
- **Fear and love (§7–8):** fear drives people to migrate; love draws them.
- **Representation (§82):** a `MigrationGroup` with population, origin, destination, route,
  health and food, not 900 individual refugees, with visible agents near the camera.
- **Systems and events (§79, §91):** a `MigrationSimulation`, and events such as `RefugeesCreated`.
- **Navigation (§85):** migration routes over the region graph.
- **Tools (§20, §58, §106):** the earthquake chain ends in migration, and there are a migration
  overlay and migration-route debug tools.

Settlements are statistical (ADR-008): a population and shares of belief per god, with no
individuals to move.

## Decision

- **The wish to leave builds up.** Each migration step (1 Hz) measures a settlement's pressure as
  a weighted sum, capped at 1, of six causes:
  - hunger and danger, from its conditions (ADR-013, ADR-014);
  - the share of its people without a roof (ruins or crowding);
  - war: two gods at war, each followed by at least `WarPresence`;
  - its followers' fear of their gods;
  - their doubt in their gods (low religious confidence).

  Love keeps people home: the pressure is scaled by `1 - LoveLoyalty × love`. The largest term is
  the cause. Above `PressureThreshold`, `SettlementState.Emigration` (people wanting to leave)
  grows with the pressure, and it fades when life eases. Leaving is a gradual, saved state, not a
  dice roll.
- **Groups set out together** once enough people want to go: at least `MinGroupSize`, or
  `GroupShare` of the village. At most `MaxGroupShare` leave at once, and `MinRemaining` always
  stay.
  - They take their share of the stores, up to `FoodTakenPerPerson` each.
  - They head for the most appealing settlement within `MaxDistance` whose pressure is lower by
    `BetterBy`. Appeal weighs room, food, safety, finding their own gods followed there, and
    distance.
  - People fleeing fear or doubt of a god do not go where that god is followed most.
- **Populations abandon gods.** People leaving give up following each god by
  `AbandonShare × max(fear, doubt)` of it, so they arrive following no god. A group is reported as
  abandoning a god when at least a tenth of that god's followers among them give it up.
- **Groups on the road** (`MigrationGroup`, §82, in `WorldState.Migration`):
  - each carries its people, beliefs, health, food, cause and the way still to walk;
  - routes come from the region pathfinder;
  - groups walk at `Speed` and eat as they go;
  - without food they sicken and die, with deaths drawn from a stream derived from the seed and
    the tick, as weather and disasters do.
- **Arrival and refugee policies** (§31). Each god has a `RefugeePolicy`: `Welcome`,
  `DemandConversion` or `TurnAway`. The policy of the god followed most in the settlement decides
  what happens, unless that god is the migrants' own:
  - *Welcome*: the migrants join as they are. The settlement's love for the host god rises by
    their share (`WelcomeLove`), and the migrants' god remembers `Sheltered`, a friendly act.
  - *Demand conversion*: `ConversionDemand` of the migrants who follow other gods or none turn to
    the host god. The converts share its followers' faith there but fear and doubt it, and the
    migrants' god remembers `Converted`.
  - *Turn away*: the group goes on to the next best place that has not refused it, or home. The
    migrants lose love for the refusing god, and their god remembers `TurnedAway`, a hostile act.

  Joining a settlement mixes people, stores, health and beliefs by population, so conversion
  through migration continues through the belief model (§32).
- **Who sets the policy.** The player sets theirs in the diplomacy panel. AI gods take their
  personality's policy from `god_ai.json`:
  - the Guardian, Opportunist, Nature Keeper and Trickster welcome;
  - the Tyrant, Zealot and Warlord demand conversion;
  - the Isolationist turns migrants away.

  A welcoming AI god turns migrants away while more than `OverwhelmedHomeless` of its own followers
  lack a roof. Policies are commands checked by a `MigrationAuthority` (§73), the fourth authority.
  Negotiating refugees' return (§31) is not built.
- **Seeing it:**
  - walkers in the migrants' god's colour (two abreast, about one per three people) with a label,
    which fades out at strategic zoom;
  - a Migration overlay: villages from content to wanting to leave, and the way of every group in
    its god's colour;
  - label marks "people leaving" and "migrants coming";
  - alerts when the player's followers leave or give the player up, migrants settle among the
    player's people, the player's followers are converted, turned away or die on the road, and a
    rival changes its policy;
  - the F1 lines: groups, policies with the AI's reasons, and the selected village's pressure;
  - the M hotkey and `--migrate=<n>` send people out of a village; `--refugees` sets the player's
    policy.
- **Saves** move to version 10. The migration from version 9 adds no groups, no wish to leave and
  `Welcome` for every god; AI gods choose their own at their next evaluation.

## Consequences

- Positive: migration chains through the existing systems.
  - War, famine, danger, homelessness, fear and doubt push people out.
  - Love and room draw them in.
  - Fear of the Tyrant makes its people abandon it.
  - A Tyrant forces conversions, a Guardian's welcome earns love, and an Isolationist's refusals
    sour its neighbours.
- Positive: in 60-minute headless runs with a passive player (seeds 1234, 42, 7), 22–57 groups of
  8–34 people moved 480–1,030 people, mostly away from war. The contest's pace is about what it was
  in Milestone 9: Vael and Seren won together at 27.5 minutes and Seren alone at 32.
- Positive: deterministic, isolated from the shared stream, saved whole and explained in the debug
  tools.
- Negative: migration slows a runaway leader, because people leave villages where war and doubt
  are high. With a higher emigration rate no god won within 40 minutes, so the shipped rate is a
  compromise.
- Negative: groups do not found new settlements, travel by sea, fall into danger on the road or
  negotiate their return, and the route ignores fires.

## Alternatives considered

- Moving people instantly between settlements. It is simpler, but §82 asks for groups with routes,
  health and food, and walkers on the road show the player what is happening.
- Random departures with no accumulated wish. These are noisier, harder to explain and harder to
  test. The saved `Emigration` shows how close a village is to losing people.
- Migrants keeping their beliefs unchanged. That leaves out §31's abandonment and §7's "followers
  abandon weak gods quickly".
- One global refugee rule instead of per-god policies. That leaves out §31's accept, reject and
  demand-conversion choices and their diplomatic consequences.
