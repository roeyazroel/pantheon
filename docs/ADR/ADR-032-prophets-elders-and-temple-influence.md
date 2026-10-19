# ADR-032: Prophets and elders as Tier 3 individuals, and temples and prophets as sources of influence

- Status: Accepted
- Date: 2026-10-19
- Amends: [ADR-004](ADR-004-statistical-population-simulation-with-tiering.md) and
  [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (what Tier 3 is)
- Builds on: [ADR-009](ADR-009-belief-influence-and-miracles.md) (influence derived,
  never stored), [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature as a
  saved individual), [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md)
  (temples) and [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md) (what the hand
  holds)

## Context

- **§80:** Tier 3 gives important entities full behaviour, with physics, AI, animation,
  pathfinding and individual memory. Its examples are the Creature, a selected villager, a
  prophet, a combatant, an object being held and an important leader.
  - The Creature, the Creatures in combat and what the hand holds were already individuals saved
    in `WorldState` (ADR-010, ADR-017, ADR-030).
  - There were no prophets and no leaders.
- **ADR-004 and ADR-008:** foresaw Tier 3 as ordinary villagers promoted near the camera. Their
  effects would be folded back into the aggregates on demotion, with totals conserved.
- **§10:** lists the sources of influence: followers, shrines, temples, holy sites, prophets, the
  Creature's presence and allied religious access.
  - Followers, the Creature, holy sites and allied access were built (ADR-009, ADR-021, ADR-025).
  - Every village has a shrine, which its followers' influence already stands for.
  - Temples and prophets were missing.

## Decision

### Tier 3 is individuals who matter, kept in the world everywhere

Tier 3 entities are standing individuals in `WorldState`. They are simulated wherever they are,
saved and shared, and never promoted or demoted by the camera:

- the Creature;
- the Creatures in combat;
- what the hand holds;
- now prophets and elders (`NotableState` in `WorldState.Notables`).

Every example in §80 is such an individual. Promoting ordinary villagers into agents near the
camera, and folding their effects back, is not built. Tier 2 stays the cosmetic presentation of
ADR-008, and the rest of ADR-004 and ADR-008 stands.

A notable is one of their village's people. They are not taken out of its population, so nothing
needs conserving. Their effects act on the aggregates (belief) directly, at every step.

### Notables

- **State:**

  | Field | What it holds |
  | --- | --- |
  | Kind | Prophet or Elder |
  | Name | Their name |
  | God | The god they serve |
  | Village | Their village, or the one they last left |
  | Position, destination and path | Where they are and where they walk |
  | Time left to preach | How long they stay |
  | Born and death ticks | Their lifespan |
  | Memories | Their last twelve deeds |

  Their name and lifespan come from `SeedMixer.Derive(seed, "notable", id)`, so raising one draws
  from no random stream.

- **`NotableSystem`:**
  - steps with the divine step;
  - draws from a stream derived from the seed and the tick, like the disasters (ADR-013);
  - comes after `BeliefSystem`.

  The numbers are in `divine_rules.json` (`Notables`):

  - **Elders (the important leaders):**
    - Every village of 40 people or more chooses an elder of the god most of it follows.
    - The elder keeps that faith: 0.0005 faith a second, and as much doubt taken away.
    - When the village's resentment of their god reaches 0.5, the elder turns to the next god it
      follows. This leaves 0.15 doubt in the god they left, and the alerts and the history tell
      of it.
    - When the village has left their god, the elder follows it to the god that now leads it,
      quietly. "Left" means the god that leads it has more than twice their god's following. The
      village's own turning is told already.
    - An elder lives 20 to 40 minutes, and the village then chooses another.
  - **Prophets:**
    - A village devout to a god calls one at 0.6 an hour. Devout means at least 0.6 of it follows
      the god, with faith of at least 0.6.
    - A god may also anoint one with **Anoint a Prophet**:
      - a Knowledge miracle for a Lesser God;
      - 300 power, with a ten-minute cooldown;
      - it acts on a village within reach of which at least 40% follow the god.
    - A god has at most two prophets.
    - A prophet preaches for 40 seconds. Each sermon:
      - wins 2.5% of the godless and half as much of each rival's followers;
      - adds 0.05 faith.
    - The prophet then walks at 4 m/s, by the region pathfinder, to the next village within
      1500 m. That is the village with the most people not yet their god's, nearer ones first.
    - A prophet lives 15 to 30 minutes.
  - **Martyrs:**
    - Danger can take a prophet: the village's danger, or fire or flood on the road, at a chance of
      0.01 × danger a second.
    - A prophet who dies so is a martyr. The god's followers within 400 m take the martyrdom
      impulse: faith 0.12, love 0.08 and doubt −0.1, fading with distance.
    - Danger lived through is remembered once for each place.

### Temples and prophets carry influence (§10)

`InfluenceField` adds two sources, both in `divine_rules.json`:

| Source | Strength | Reach |
| --- | --- | --- |
| A temple raised to the god | 0.4 | 250 m |
| Each of the god's prophets, where they stand | 0.4 | 250 m |

A god may therefore act, at the full price, where a prophet walks or a temple stands, even
beyond its followers.

### What the player sees and does

- **In the world:** a robed figure in the god's colour, taller than the villagers.
  - A prophet carries a staff under a halo.
  - An elder stands by the village's centre.
  - The label reads, for example, "Prophet Asha of Seren", and up close what they are doing.
- **Alerts:**
  - the player's prophets rising;
  - rivals' prophets rising, or winning people, among the player's villages;
  - martyrs;
  - elders turning to or from the player out of resentment.
- **History:** a prophet arising, a martyrdom and an elder turning from a resented god.
- **The Book of the World:**
  - "Prophets and elders" on the World page;
  - "Your prophets and elders", with their memories, on the Religion page.
- **F1:** every prophet's walk, last deed and time left, and the selected village's elder and
  preachers.
- **`--prophet=on`:** raises a prophet at the focused village, for captures.
- **Saves and sharing:**
  - saves move to version 23;
  - the protocol moves to version 11, with a Notables section sent every second.

## Consequences

- **§10:** complete.
- **§80:** complete except the selected villager. Following one representative citizen as an
  individual would need the camera-driven promotion this ADR leaves out.
- **Balance:** AI gods anoint whenever a slot is free.
  - Over 40 seeds (30–69), 38 games are won within the hour, at a median of 28.3 minutes and a
    mean of 30.9.
  - Part 10 gave 39 of 40, at a median of 27.6 and a mean of 29.3.
  - With a sermon winning 4% of the godless, the median fell to 21.7 minutes, faster than any
    part before, so a sermon wins 2.5%.
- **Rates:** over ten one-hour games (seeds 100–109), each game sees:
  - about 21 elders chosen, and 30 turns of elders, nearly all following their village;
  - 60 sermons, winning about 340 people;
  - nine prophets anointed and one called;
  - a martyr every two or three games.

  Notables cost 4 ms over 3000 ticks of a Huge world.
- Notables cost one list in the save and one section on the wire. The step walks them once a
  second.
