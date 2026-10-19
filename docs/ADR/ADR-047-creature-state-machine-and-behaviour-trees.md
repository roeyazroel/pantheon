# ADR-047: The Creature's state machine and behaviour trees

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature's brain and
  learning), [ADR-017](ADR-017-creature-relationships-and-combat.md) (relationships and combat) and
  [ADR-037](ADR-037-creature-temperament.md) (temperament)

## Context

§86 asks the Creature's AI to combine:

- a behaviour tree and a hierarchical state machine;
- utility values, learned preference weights and memory.

It says LimboAI may implement the tree and the machine, and that Pantheon implements learning,
reinforcement, personality and relationship memory.

The Creature had the utility values, learned weights, memory, personality and relationship memory
(ADR-010, ADR-017, ADR-037). Carrying out a chosen action was one switch over its phase: travel to
the target, perform for a fixed time, do the deed. So a Creature finished whatever it set out to do,
even when the reason had gone: the house it went to help build was finished, the village it went to
feed had been fed, the village it went to raid now followed an ally.

LimboAI is a Godot extension. The Creature is simulated in the core, which never references Godot
and is tested headless (ADR-006, ADR-010), so the tree and the machine are built in the core.

## Decision

### The hierarchical state machine (`CreatureModes`)

- **Its states:**
  - Free, with Deciding, Travelling and Performing within it;
  - Engaged, Fighting or Playing with another Creature;
  - Held in its god's hand;
  - Banished.
- **Stepping:** `CreatureSystem` steps each mode its own way. The combat plays out a fight, the hand
  holds a held Creature, and a banished one counts down to its return. In the free mode the brain
  decides, and the behaviour tree carries out what it chose; playing runs on the tree too.

### The behaviour trees (`CreatureTree`)

Every action's tree has the same shape, and it holds no state of its own: the sequence's place is the
activity's phase and the performance its ticks left. So saves and sharing are unchanged.

| Node | What it does |
| --- | --- |
| Interrupt | A pressing hunger or weariness ends a commanded walk, and counts a commanded wait as done. |
| Guard | The reason the action was chosen must still hold, or the Creature gives up. |
| Sequence | Travel, then perform, then the deed. |

The guards:

| Action | The Creature gives up when |
| --- | --- |
| Feed | The village's need falls below half the threshold that drew it |
| Help build | The village has nothing left to build |
| Eat from stores | The stores are empty |
| Attack | The village now follows an ally of its god |
| Any village action | The village is gone |
| Seeking a Creature, or playing with one | The other is gone or has stopped playing |

- **Giving up:** a Creature that gives up raises `CreatureGaveUp` with its reason and decides again.
  Its god is told: "Bramble gives up building in Elar: there is nothing left to build."
- **What stays:** the brain still chooses by utility and learned weights, the deed still teaches and
  is remembered, and personality and relationships still shape the choice.

### Why the interrupt is narrow

A first version interrupted any action when hunger or weariness became desperate. The brain already
weighs the Creature's needs when it chooses, so a Creature that chose to rest or wander while
starving was interrupted at once, chose the same again, and gave up thousands of times in a game. So
only the god's commands are interrupted, as before.

### Debug

- F1 shows the state's path and the tree: "mind: Free › Travelling; tree: guard: still in need ▸
  travel ▶ perform · deed".
- The alerts tell of the player's Creature giving up.

## Consequences

- **§86:** every part it names exists, in the core, with LimboAI's roles played by `CreatureModes`
  and `CreatureTree`.
- **Play:** a Creature no longer finishes a pointless errand. Over twenty minutes on three seeds,
  Creatures gave up 4–9 building errands that finished before they arrived, and a few commanded
  waits.
- **Balance:** 148 of 160 seeds won within the hour (141 before), within the spread between parts.
- **Not modelled:** trees that differ by action beyond their guards, and trees the Creature learns.
