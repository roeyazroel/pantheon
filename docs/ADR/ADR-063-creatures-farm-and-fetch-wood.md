# ADR-063: Creatures farm the fields and fetch wood for the villages

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature and its
  learning), [ADR-033](ADR-033-forests-wood-stone-and-roads.md) (wood, stone and forests) and
  [ADR-061](ADR-061-animal-creatures.md) (the animal bodies)

## Context

The owner asked that "the creatures should be able to help the villagers with farming and getting
trees". A Creature's only work for a village was Feed, which made food from nothing, and
HelpBuild, which raised houses without the wood a house costs. Nothing let it work a village's
fields or its forests, and its god could only send it to a place to wait.

## Decision

### Two new deeds (`CreatureAction.Farm`, `CreatureAction.GatherWood`)

- **Farming** a village's fields (14 s):
  - **The fields:** every farmland region within its `FarmlandRadius` is watered (+0.06 soil
    moisture), weeded (−0.15 blight) and tilled (+0.01 fertility, up to 1).
  - **The harvest:** `Amount` × its strength × the fields' mean fertility, as far as the stores
    hold it.
- **Fetching wood** (12 s): the Creature walks to the forest around the village richest in timber
  (`ResourceModel.RichestForest`) and uproots trees there.
  - **The wood:** `Amount` × its strength × the timber standing goes to the village's stores.
  - **The forest:** it thins by the village's own `TimberPerWood`, so the forests drawn near it
    clear.
  - **No guard:** fetching wood is never pointless, since stores have no ceiling.
- **Building spends wood:** HelpBuild's builder-steps each spend their share of a house's wood. A
  village without wood gets only what its stores allow.
- **Records:** both deeds are witnessed and remembered like the others; `CreatureActed.Wood`
  carries the wood brought.

### When a Creature chooses them (`CreatureBrain`)

| A village that is | Farm | Fetch wood |
| --- | --- | --- |
| In need | 0.7 × its food need | — |
| Building | — | 0.3–0.9 by its shortfall of wood |
| Thriving | 0.3 | half its shortfall of wood |

- **Help building:** pulls only 0.15 when the village lacks a house's wood.
- **Starting preferences:** 0.3 to farm in need, 0.4 to fetch wood when building, 0.3 and 0.25 when
  thriving.
- **Temperament:** both draw on kindness and against laziness.
- **Habits still win:** a Creature taught by rewards to attack still attacks a hungry village.

### Ordering it (`CreatureCommandKind.Work`)

- **Where you point:**
  - **A village** (within 120 m of its centre): the Creature does what that village needs most
    (`CreatureBrain.NeededWork`): its fields when it is hungry; wood when it is short or building
    without enough; building; else its fields.
  - **A wood** (at least 0.3 timber) away from a village's houses: it fetches that wood for the
    nearest village of its god (or the nearest village).
  - **Elsewhere:** the order is refused (`NoWorkThere`).
- **Teaching:** the order starts the work in the village's own situation, not `Commanded`, so a
  reward teaches the Creature to do it unasked.
- **The controls:** B at the cursor, or Work on the Creature card and a click. Region boundaries
  move to Shift+B. `--work=village|forest` sets it working at the start, for captures.

### Seeing it

- **Poses:** a farming Creature bends and brings both fists down to the soil, again and again
  (`AnimalMotion.Farm`). One fetching wood heaves at a tree before it, which lifts out of the
  ground with each tug (`AnimalMotion.Uproot`, `AnimalBody.ShowTree`).
- **Alerts:** "Bramble tended the fields of Duskwater: 32 food came in", "Bramble brought 18 wood
  to Duskwater".

## Consequences

- **§22, §29:** a Creature is a worker for the villages as well as a fighter. Its help comes from
  the land: food grown in fields it tends, wood felled from forests that thin.
- **Balance:** over eight seeds and forty minutes, villages grew a little more (people +7%, housing
  +10%), and the worlds won by then went from 2 to 3 of 8, within the seeds' scatter.
- **Not modelled:** the Creature carrying the timber back (the wood reaches the stores when it
  uproots), and working a field drawn by the village's layout rather than beside the village.
