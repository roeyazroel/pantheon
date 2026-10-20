# ADR-062: Creature fights on command, and fights you can see

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-017](ADR-017-creature-relationships-and-combat.md) (meetings, bonds and combat),
  [ADR-025](ADR-025-treaties-council-and-divine-favours.md) (treaties) and
  [ADR-061](ADR-061-animal-creatures.md) (the animal bodies)

## Context

The owner asked that "the creatures should be able to fight each other". They could, but only by
their own choice: two Creatures within 40 m met, at most every two minutes, and each answered by its
brain. Between gods not at war, a fight scored about a fortieth of a greeting.

- **The player:** had no way to set a Creature on another. Send (V) only walked it to a place to wait.
- **The AI gods:** at war, sent their Creatures to where an enemy's was, which rarely ended in a
  fight.
- **Seeing a fight:** a fight showed only as raised arms and a percentage on the label.

## Decision

### The command (`CreatureCommandKind.Fight`)

- **Choosing the rival:** the god points at the ground. The authority picks the rival Creature
  nearest the point, within `HuntReach` (80 m), of another god and not banished.
  - With none near it refuses (`NoRivalThere`).
  - It refuses if a Creature non-aggression treaty binds the two gods (`Sworn`).
  - The command carries a place, not a Creature, so it travels in the existing `CreatureIntent`
    message.
- **The hunt (`CreatureBrain.Hunt`):** the Creature sets out after the rival, `Commanded` and
  `Travelling`, for at most `HuntSeconds` (90 s). About once a second, and whenever its way runs out,
  it re-plans to where the rival is now (`CreatureTree`).
- **Giving up:** it gives up when its time runs out, the rival is banished or there is no way.
- **The fall:** when they meet, the hunter falls on the rival however lately they met, and the
  rival answers by its brain: it fights back, or runs if it can.
- **Diplomacy:** outside a war, the attacked god records the attack against the hunter's
  (`DiplomaticAct.CreatureAttacked`), as for an attack on its people.
- **The AI gods:** their confrontations at war use the same hunt (`AiActionKind.SetCreatureOn`).
  Over eight seeds and forty minutes, defeats rose from 22 to 24 and the worlds won stayed 2 of 8.

### Commanding it

- **Fight button:** the Creature card's Fight button aims. The next click near a rival sets the
  Creature on it. While aiming, a ring at the cursor shows the reach, valid when a rival is within it.
- **Shift+V:** sets it on the rival nearest the cursor at once, as V sends it there.
- **Launch option:** `--fight=on` sets it on the nearest rival at the start (with `--meet` for
  captures).

### Seeing it (`FightEffects`)

- **Health bars:** over each fighter's head, while it fights and four seconds after, green to red.
  The bar always faces the camera and draws over everything (`health_bar.gdshader`). The label
  rises above the bar.
- **Blows:** each blow lands with sparks and a puff of dust between the two, at the rival's chest.
  It is timed to the boxing pose's punches (`AnimalMotion.Blows`).
- **Defeat:** the loser falls flat on its back with a burst of dust and lies there "Defeated" for
  2.5 s before it is banished from sight (`AnimalMotion.Fallen`).

## Consequences

- **§27–28:** Creatures fight each other whenever their gods want them to, not only when they choose
  to, and a fight is seen blow by blow.
- **Diplomacy:** an unprovoked attack sours the attacked god towards the attacker, so starting
  fights has a cost.
- **Not modelled:** fights of more than two, the hand joining a fight, and blows that knock a
  Creature back.
