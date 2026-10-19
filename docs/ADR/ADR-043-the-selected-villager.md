# ADR-043: The selected villager

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the Tier 2
  representative citizens) and [ADR-032](ADR-032-prophets-elders-and-temple-influence.md) (Tier 3 is
  individuals kept in the world state, never camera promotion)

## Context

§80's Tier 3 is full simulation for the entities that matter. It lists:

- the Creature, a prophet, a combatant, an object being held and an important leader;
- a "selected villager".

Tier 3 includes physics, AI, animation, pathfinding and individual memory. ADR-032 gave Tier 3 to
prophets and elders. It decided that Tier 3 individuals are standing members of `WorldState`, never
figures promoted because the camera came near, so the simulation does not depend on where anyone
looks. The selected villager is the one example of §80 still missing.

The crowd the player sees is Tier 2. Its figures are derived from a village's numbers and never saved
(ADR-008), so none of them has a life of its own to follow.

## Decision

Selecting a villager is a player's command. The figure the player points at becomes an individual
of the world.

### Following

- **Choosing someone (/):** the player follows the crowd figure nearest the cursor, within 6 m.
- **The command:** it carries the figure's home, work place and spot by the shrine
  (`FollowVillager`), and `VillagerAuthority` checks them (within 150 m of the village).
- **Who they are:** the same person each time for the same figure:
  - a name and an age drawn from the seed, the village and the figure;
  - a lifespan, and the work of the figure (farming, building or chores);
  - a god drawn by the village's adherence, with faith near the village's.
- **One at a time:** a god follows one villager. Following another, or Shift+/, lets the first go
  back to the crowd. The followed figure's crowd double is hidden.
- **Saving:** villagers live in `WorldState.Villagers`, so they are saved and shared.

### Their life (`VillagerSystem`, once a second)

- **Needs:**
  - hunger rises and is eaten away at home while the village has food;
  - work tires them and rest restores them;
  - starving or the village's plague costs them health, and they heal when well.
- **Choosing (`VillagerMind`):** when an activity ends they choose the next by score:
  - eat, by hunger, but only with food in store;
  - rest, by fatigue;
  - work, by strength and hunger;
  - pray, by faith, less soon after praying, and more for a hardship remembered.

  They walk to where each is done. The scores and their reasons are pure, so F1 and the Book show
  the same choice.
- **Danger:** their village's fire, flood or shaking, or the land's underfoot.
  - At 0.25 they flee to the nearest safe village within 800 m, else out of the village.
  - They shelter there, eating from its stores, and walk home when the danger falls to half.
  - Remembering danger makes them flee sooner (0.15).
- **Homes lost:** a villager whose village is emptied seeks the nearest village of their god (else
  any), and makes a home there.
- **Faith:**
  - their faith follows their village's and grows in prayer;
  - a miracle or a prophet of their own god strengthens it, and another god's shakes it;
  - below 0.3, or with no god, they turn to the god most of their village follows (at least half of
    it).
- **Death:** age, hunger, illness or danger (with a chance by the danger) ends their life.
- **Memory (`VillagerWitness`):** they remember up to 24 things, including:
  - miracles within 150 m;
  - their village's disasters, storms, famines and thirst;
  - the Creatures that feed, build, raid or attack it;
  - its buildings, preachers and offerings;
  - their own flight, return, moves, turning and illness.

  The engine lets them witness the events of each tick and of each command.
- **What changes for the world:** nothing. A villager's eating, faith and death are their own. The
  village's numbers already count them, so balance is unchanged.

### What the player sees and does

- **In the world:**
  - a larger figure in their work's colour, under a turning marker in their follower's colour;
  - it walks, stoops to work, kneels to pray and sits to rest;
  - a label says who they are and what they do.
- **Camera:** / with nobody at the cursor turns the camera on them and keeps them in view until the
  player moves it.
- **The Book's Villager page (Tab):**
  - who they are, what they do, their faith and needs;
  - what they want and why;
  - their life, newest first, each memory with the age they were.
- **F1 and alerts:**
  - F1 shows every followed villager, and the player's villager's needs, options, danger and last
    memory;
  - alerts tell of their flight, their turning and their death.
- **Words:** every text names them or says "they", never assuming pronouns.
- **Launch options:** `--villager=on` follows someone of the focused village at the start, the
  camera on them, and `--book=villager` opens their page.
- **Saves and sharing:**
  - saves move to version 31, and no god follows anyone in an older save;
  - the protocol moves to version 19, for `VillagerIntent` and the villagers sent with the
    notables.

## Consequences

- **§80:** every Tier 3 example exists.
- **Play:** a window onto the systems from below. A famine is one person going hungry, a fire their
  flight, and a conversion their turning.
- **Not modelled:**
  - physics for the villager (§94);
  - local navigation around houses (§85);
  - families and relationships;
  - villagers chosen by AI gods.
