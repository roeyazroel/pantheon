# ADR-070: Gods answer Creatures in their lands, a steady hand, and fights you can watch

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-017](ADR-017-creature-relationships-and-combat.md) (Creatures meeting and fighting),
  [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md) (the hand), [ADR-036](ADR-036-known-gods-and-the-gods-long-term-plans.md)
  (the gods' plans) and [ADR-062](ADR-062-creature-fights-on-command.md) (fights on command)

## Context

The owner, playing, reported three things:

- "The pick and drop of the creature is not stable and not always working."
- "When I move my creature to another village I don't expect the other creature to just walk
  around: the AI god should be triggered, react, and tell its creature to do something based on my
  relationship with this god."
- "Fight between my creature and another was very fast, and after my creature killed the other,
  my creature died a couple of seconds later."

The causes:

- **The hand.**
  - A held Creature hung from the hand only while the pointer touched the land, and jumped back to
    where it was lifted whenever the pointer crossed a panel or the sky.
  - The Creature was lifted only when nearer the hand than the nearest village's centre, so in a
    village the hand took the people instead.
  - The hand took the ground under the pointer, which lies well behind a tall Creature's body.
  - The hand needed the god's influence where it set the Creature down.
- **The rivals.**
  - An AI god set its Creature on another only at war, and only as one candidate among many.
  - Most gods are hostile but not at war, and did nothing.
- **Fights.**
  - Two equal Creatures lost 5% health a second, so a fight lasted 11–13 seconds, and two or three
    at ×4.
  - A wounded winner is the easiest prey for a third Creature.

## Decision

### A steady, precise hand

- **A steady hold.** A held Creature hangs from the hand's grip wherever the pointer goes, and the
  hand stays in sight while it holds anything.
- **Pointing at the Creature lifts it.** The client tests the pointer's ray against the Creature's
  body, a standing capsule from its feet to its head, as wide as two fifths of its height. A hit sends
  the Creature's own place as the hand's target.
- **The Creature before villagers.** The god's Creature within half the hand's reach is lifted before
  a village's people.
- **Anywhere on land.** A god may set down or throw its own Creature anywhere on land, without its
  influence there, as the Creature may walk anywhere. Lifting it still needs a little influence,
  which the Creature carries with it.

### The gods' watch

`CreatureWatch` runs every `IntervalTicks` (10), after the AI gods' own turns. A visitor in a god's
lands therefore comes before the errands it has just planned, and a god does not call its Creature
off a hunt for an errand.

**Noticing.** Every god looks for other gods' Creatures within `Radius` (220 m) of a village it
leads. Banished Creatures and those held in a hand are not noticed.

**Hostility.** A god's hostility to the visitor's god (0 to 1) is:

- its standing: war 1, hostile 0.72, neutral 0.42, friendly 0.15, alliance 0;
- plus 0.3 × (rivalry + half its suspicion − trust), from its view of that god;
- plus 0.25 × (aggression − 0.5), from its profile, its temper applied.

War is always 1. An alliance, or a treaty that spares their Creatures, keeps it at 0.2 at most.

**The answer:**

| Hostility | Answer | The god's Creature |
| --- | --- | --- |
| up to `WelcomeHostility` (0.25) | Welcome | comes once to greet the visitor |
| from `RepelHostility` (0.75) | Repel | is set on the visitor when at least `RepelConfidence` (0.4) sure of it and not recovering from a fight; otherwise guards the village |
| between | Watch | goes to the visitor and follows it while it stays |

At 0.75 an aggressive or embittered god drives a visitor out, and a calm hostile god watches it. In
ten minutes of play this gave 0–4 fights. Repelling from 0.6 made every hostile god attack.

- **One Creature, the most pressing visitor.** A god answers every visitor, but sends its Creature
  after one: a foe before a stranger before a friend, the nearest first. It sends it unless the
  Creature is fighting, held, banished, or below 40% health.
- **A player's god does not answer by itself.** Its player is told, and chooses.
- **Memory.** A god remembers a visit once in `CooldownSeconds` (60), as a new act:
  - `Trespassed`: trust down a quarter of a harm's, rivalry and suspicion up half; a light grievance;
  - `Visited`: trust up half a help's.

  The diplomacy panel shows these ("remembers that you let its Creature trespass in Mossgate"), and
  repeated trespass embitters a god towards repelling.
- **Telling.** A `CreatureWatched` event goes out with each memory, and again when the god sets its
  Creature on a foe later, once it can go. The alerts tell the visitor's player what the god decided
  ("Varak drives Bramble out of Emberfall: it sets Ember on it!"), and tell a village's player who
  came into it.

### Fights you can watch

- **Longer fights.** `DamagePerSecond` halves to 0.025, so a fight between equals lasts about half a
  minute.
- **A respite.** For `RespiteSeconds` (45) after a fight it fought or saw off, a Creature is drawn
  into no new fight. A meeting's call to fight becomes walking on, and a hunter waits.
- **×1 for your fights.** When the player's Creature starts a fight while the world runs faster than
  ×1, the host slows it to ×1 and says why.

## Consequences

- **Rivals' Creatures act on the gods' standing,** so the player's visits have consequences: a friend
  sends a greeting, a stranger a shadow, a foe a fighter. Memories of trespass build grudges, as
  courting does.
- **More fights between AI gods' Creatures** where they are hostile, though fewer than war alone
  would make. A test pins the answers, and the ten-minute counts are in the plan.
- **No change to saves.** The new acts are appended to the act list, and the new rules have defaults
  that leave the pinned test rules as they were.
- **No protocol bump.** Players with different `god_ai.json` or `creatures.json` already differ in
  their content's fingerprint, so mismatched builds cannot join each other.
- **Tooling:** `--visit=<n>` sets the player's Creature down beside village n, to see the watch at
  once.

## Alternatives considered

- **A candidate in the AI's turn.** This is where confronting lived before. It competes with
  miracles and errands every 3 seconds, so the god often failed to answer. An answer to a visitor in
  its own lands is a reflex, not a plan.
- **Letting the gods' diplomacy speak** (a message or demand at each trespass). The Creature's
  answer and the alert say it in the world, and a trespass grievance shows in the diplomacy panel.
  A spoken demand can follow through talk (ADR-066).
- **Healing a winner.** It would hide the risk a fight carries. A respite keeps the risk but takes
  away the unfairness.
