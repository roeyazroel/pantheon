# ADR-010: The Creature: core simulation, learned utility AI and the hand

- Status: Accepted
- Date: 2026-10-18

## Context

Milestone 4 (spec §119) delivers the Creature with movement, interaction, needs, simple AI and
reward and punishment. The playable outcome is "Creature behaves independently". The
specification asks for:

- a Tier 3 entity with physics, AI, animation, pathfinding and memory (§80);
- needs, personality and learned preference weights that rewards and punishments change (§23–26,
  §111);
- AI that combines a behaviour tree and a hierarchical state machine with utility values,
  learned weights and memory (§86). §86 says "LimboAI **may** implement BT/HSM. Pantheon
  implements learning, reinforcement, personality and relationship memory";
- Jolt physics for Creature physics and thrown objects (§63);
- Godot navigation for visible agents and a region graph for long-distance travel (§85);
- interaction "using a Divine Hand", which should feel tactile (§55).

ADR-006 makes `WorldState` the only canonical state, and ADR-003 runs the canonical simulation on
a host. That rules out canonical state that lives in the scene tree.

## Decision

- **The Creature lives in the core.** `CreatureState` is part of `WorldState`: position and
  heading, level, experience, health, loyalty, morality, needs, personality, the current
  activity, learned preferences and memories. It is saved whole (save version 4) and runs
  headless on the host. Presentation only reads it.
- **The brain is our own small state machine and utility AI, not LimboAI.**
  - The machine has four states: deciding, travelling, performing and banished.
  - When deciding (at most at 5 Hz, §89), `CreatureBrain` lists options from the Creature's needs
    (hungry, tired, bored) and from each settlement it perceives (in need, building, thriving).
  - Each option scores learned preference × drive × personality × closeness. A little seeded
    jitter is added, and the best reachable option wins.
  - Its state is plain data in `WorldState`, so it is saved, deterministic and testable.
  - LimboAI's trees and state machines are nodes and resources in the scene tree. Using them for
    the canonical brain would break ADR-006, tie the host simulation to the scene tree, and add a
    native dependency for something about a hundred lines of C# do. LimboAI stays an option for
    presentation-side animation logic later. This uses §86's "may" and leaves its list of what
    Pantheon implements unchanged.
- **Learning** (§25): preferences are kept per situation and action, from data
  (`creatures.json`), starting with §25's example values. A reward closes a share of the gap to 1
  and a punishment removes a share of the value. Both nudge the personality trait behind the
  action (kindness, aggression, laziness, curiosity), change loyalty, and are recorded in the
  memory they judge. They judge what the Creature is doing now, or what it finished within a
  short window.
- **Commands are intents checked by an authority** (§73), as miracles are (ADR-009).
  `CreatureCommand` (reward, punish, go to) goes through `CreatureAuthority`. It checks that the
  god and Creature exist, that the issuer controls the god and the god owns the Creature, that the
  Creature is not banished, that there is something to judge, and that the target is in the world
  and reachable.
- **Movement is kinematic over the region graph** (§85's strategic navigation).
  - `RegionPathfinder` runs A* over land regions with eight neighbours, then cuts every corner
    that a straight line over land allows. The Creature walks the waypoints at its speed, every
    tick.
  - Local navigation meshes come with Tier 3 citizens and obstacles; there are none to avoid yet.
- **Physics: Jolt is the physics engine** (`project.godot`), as §63 asks. No gameplay depends on
  physics yet.
  - When thrown objects and debris arrive, physics will run in the client as presentation.
  - Gameplay outcomes, such as where a thrown rock lands or what it hits, will be computed in the
    core or sent back as intents that the authority checks. Physics state will never be
    canonical. That later milestone records the details in its own ADR.
- **The Divine Hand is presentation for now.** A glowing hand follows the cursor, presses down
  when a miracle is cast or the Creature is sent somewhere, pats the Creature for a reward and
  swats it for a punishment. Picking up and throwing (§55) come with physics gameplay.
- **The Creature's deeds reach the world through the same state as everything else:**
  - feeding fills a settlement's stores;
  - eating from them empties them;
  - helping build adds work to a house;
  - attacking kills villagers;
  - the settlement's belief in the Creature's god changes with each deed, and the Creature's
    morality follows its deeds;
  - its presence carries its god's influence (§10).
- **Defeat is banishment** (§28): a Creature that starves leaves for a while and returns home
  weakened.
- **Content:** `game/Data/creatures.json` holds speeds, needs, thresholds, learning rates, levels
  1–10 (§23's MVP) and each action's duration, effects, kindness and the belief change it causes.
  `gods.json` names each god's Creature.

## Consequences

- Positive: the Creature is saved, deterministic for a given world, and covered by headless
  tests. A host can run it without rendering, and a client only needs `CreatureState` to draw it.
- Positive: learning is legible. The debug view shows the options and their scores, the learned
  weights and the memories (§106), so the player's teaching can be checked.
- Positive: no native dependency was added; Jolt ships with Godot.
- Negative: no behaviour-tree editor. Designers change behaviour through data (preferences,
  drives through the actions' rules, personality) and code, not a visual tree.
- Negative: no local avoidance. The Creature walks through houses and citizens, and paths are
  only as fine as the 64 m regions.
- Negative: the hand cannot pick things up yet, so §55's tactile interaction is only partly met.

## Alternatives considered

- LimboAI behaviour trees in the client: canonical decisions would live in nodes, which breaks
  ADR-006 and means a host needs the scene tree to simulate.
- A rigid body for the Creature driven by Jolt: physics results would have to be fed back into
  the world state each tick, and they differ between platforms (§74), with no gameplay gain while
  nothing collides.
- Godot's NavigationServer for the Creature's path: it needs baked navigation meshes in the
  client and gives paths only there. The region graph is enough for a single large walker.
