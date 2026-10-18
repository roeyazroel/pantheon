# PANTHEON — Game Specification (Vision Summary)

PANTHEON is an original single-player and (later) multiplayer 3D god game for Windows, macOS and
Linux. It is a spiritual successor to the classic "be a god, shape mortals and raise a creature"
genre. All names, lore, art and UI are original. It runs fully offline with no backend.

## Fantasy

You are a newly risen god in the **Veyrn Isles**, an archipelago of isolated island cultures
(working setting name). Several young gods compete for worship. Your power comes only from what
mortals believe about you. You cannot command mortals directly. You can only persuade, provide,
frighten or neglect them, and live with how they remember it.

## Pillars

1. **Belief is power.** Every act changes how each settlement feels about each god, and those
   feelings generate the power that makes further acts possible.
2. **Systemic consequences.** Miracles feed the same weather, ecology and population systems the
   world runs on. Rain grows crops but can also flood and spread disease.
3. **A creature that learns.** Each god raises one creature that learns from what it is taught
   and from what it sees its god do.
4. **A living world.** Settlements, rival gods and nature act on their own. The world moves
   whether or not the player intervenes.

## Belief model

Belief is tracked **per god, per settlement** with six components (0..1 aggregates):

| Component | Grows from | Effect |
| --- | --- | --- |
| Faith | Witnessed, answered prayers; consistent presence | Base multiplier for power generation and influence |
| Love | Benevolent acts: food, healing, protection | Stable worship; volunteers; low unrest |
| Fear | Destruction, punishment, displays of might | Fast, brittle worship; productivity loss; flight |
| Dependency | The god solving the settlement's problems | Extra worship now; helplessness and collapse if support stops |
| Doubt | Absence, failed requests, a rival outperforming | Erodes Faith; opens the settlement to conversion |
| Resentment | Harm, broken promises, exploitation | Unrest, desertion, conversion to rivals, sabotage |

A settlement's worship of a god is a function of these components (e.g. `Faith × (Love + Fear +
Dependency) − Doubt − Resentment`). The rules are data-driven and tuned in content files.

## Divine Power and influence

- **Divine Power** is the god's spendable resource. It is generated each tick by worship across
  settlements, capped by total Faith, and spent on miracles and creature actions.
- **Influence** is the area in which a god can act: overlapping fields around worshipping
  settlements, sized by belief. Contested areas weaken every god's miracles there.

## Miracles

Miracles are cast inside influence, cost Divine Power, and **act through world systems** rather
than as isolated effects. Examples:

- *Rainfall* raises region moisture and crop yield, and can cause floods and disease if overused.
- *Wildfire* clears forest for farmland, raises Fear and Resentment, and changes ecology for years.
- *Harvest Blessing* adds food now and raises Dependency.
- *Mending Light* heals and raises Love. *Storm Lance* destroys and raises Fear.

Observers remember what they saw, so each miracle changes belief in nearby settlements too,
including the settlements of rival gods.

## The Creature

- Each god raises exactly **one Creature**, a growing beast-like avatar (original designs).
- It learns through **reinforcement** (praise and punishment shape the weights of its utility
  considerations), **observation** (it imitates its god's recent acts) and **needs** (hunger,
  fatigue, curiosity).
- It acts autonomously inside influence, can be leashed to tasks, and can learn small miracles.
  Its behaviour changes the creature's alignment and how mortals see its god.

## Settlements

Settlements are autonomous. Villagers assign themselves to jobs (farm, gather, build, worship),
grow, expand, migrate, convert, pray and petition. They react to food, housing, safety, weather
and divine acts. Population is simulated statistically with tiered detail (see below).

## Rival gods

Rival gods use **utility AI**. Candidate actions (miracles, creature orders, diplomacy) are
scored by weighted considerations shaped by a personality (benevolent, tyrant, trickster, ...).
Rivals obey the same rules and costs as the player.

## Diplomacy and betrayal

Gods can form pacts (non-aggression, shared influence, tribute) and alliances. Breaking a pact is
possible but remembered: rival gods lower trust, and mortals who witness it gain Doubt or
Resentment. Reputation persists as long-term memory.

## Weather, ecology and disasters

The world simulates weather fronts, rainfall and temperature, vegetation growth and spread,
statistical fauna, and disasters (drought, flood, wildfire, storm, plague, earthquake). Disasters
can arise naturally or as consequences of miracles. Regions (the region grid) carry the
ecological state: biome, elevation, temperature, moisture, fertility and vegetation.

## Simulation tiers

| Tier | Where | Fidelity |
| --- | --- | --- |
| Statistical | Everywhere, always | Aggregates per settlement and region |
| Regional | Near the camera or influence | Cohorts, work groups, schedules |
| Full | Under the camera or hand | Individual agents, promoted from and folded back into aggregates |

`WorldState` aggregates are authoritative. Tier changes conserve totals (ADR-004).

## Technical summary

Godot 4.7.2 .NET, C#, Terrain3D. The simulation state is separate from scenes and saved as
versioned JSON. Multiplayer (later) is host-authoritative over ENet. See
[ARCHITECTURE.md](ARCHITECTURE.md) and [ADR/](ADR/README.md).

## Roadmap (15 milestones)

| # | Milestone | Outcome |
| --- | --- | --- |
| 1 | **Foundation** | Seeded world and terrain, god camera, region grid, one settlement, JSON save/load, debug UI, fixed-tick loop. |
| 2 | Living Settlements | Statistical economy (jobs, food, housing, construction), several settlements, growth and founding, visual villages. |
| 3 | Belief & Divine Power | Six-component belief per god per settlement, worship, Divine Power generation, belief readouts. |
| 4 | Influence & the Hand | Influence fields, the divine hand (pick up, throw, place), interaction limited by influence. |
| 5 | Miracles I | Miracle casting, core miracles wired into world systems, observer memory. |
| 6 | Weather & Ecology | Weather fronts, rainfall, seasons, vegetation growth and spread, statistical fauna. |
| 7 | Disasters & Consequences | Wildfire, flood, drought, plague, storms. Miracle side effects feed back into belief. |
| 8 | The Creature I | Creature body, needs, locomotion, reinforcement learning from praise and punishment. |
| 9 | The Creature II | Observation learning, alignment, leashes, creature miracles, growth. |
| 10 | Rival Gods | Utility-AI gods with personalities, competing for belief under the same rules. |
| 11 | Diplomacy & Betrayal | Pacts, tribute, alliances, betrayal, reputation memory. |
| 12 | Tiers at Scale | Regional and full tiers, promotion and demotion, large worlds, performance budgets. |
| 13 | Multiplayer | Host-authoritative ENet sessions (LAN/direct IP), snapshots and deltas, command validation. |
| 14 | Campaign & Content | Original islands, scenarios, events, tutorial, lore. |
| 15 | Polish & Release | Art and audio pass, accessibility, settings, localization, packaging for Windows, macOS and Linux. |
