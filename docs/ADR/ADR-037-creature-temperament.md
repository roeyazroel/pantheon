# ADR-037: Creature temperament: courage, protectiveness, jealousy and playfulness

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature's brain and
  learning), [ADR-017](ADR-017-creature-relationships-and-combat.md) (Creatures meeting and
  fighting) and [ADR-035](ADR-035-the-ancient-beast-and-the-magical-anomaly.md) (the beast)

## Context

§26 lists a Creature's possible traits:

- brave, cowardly, kind, sadistic, curious, lazy;
- protective, jealous, playful, aggressive, loyal, independent.

It says personality evolves through experience, reinforcement, trauma, relationships and divine
treatment.

What existed:

- **Traits:** kindness, aggression, laziness and curiosity.
- **Evolution:** rewards and punishments moved the trait behind the judged action; victories and
  defeats moved aggression.
- **Loyalty:** kept on the Creature, moved by rewards, punishments and defeats, and governing how
  long the Creature stays where it is sent.

## Decision

### Four new traits

`CreaturePersonality` gains `Courage`, `Protectiveness`, `Jealousy` and `Playfulness` (0..1).

- **At birth:** each is drawn from the seed, after the existing four so those keep their values,
  and shaded by the species.
  - A Stonehide is braver and less playful.
  - A Lumen is more playful and less brave.
  - A Swiftclaw is more jealous.
  - A Mossback is more protective and less jealous.
- **§26's twelve words:**
  - brave and cowardly come from courage;
  - kind comes from kindness, and sadistic from low kindness with high aggression;
  - protective, jealous, playful, curious, lazy and aggressive come from their own traits;
  - loyal and independent come from the Creature's loyalty.
  - A trait earns its name at 0.65 (its opposite at 0.35); loyalty at 0.8 and 0.4.

### Traits drive actions

`CreatureTemperament.Traits` names the traits behind each action. The brain multiplies an option's
utility by 0.5 plus their mean.

| Action | Traits |
| --- | --- |
| Feeding, building | Kindness |
| Playing, befriending | Kindness and playfulness |
| Attacking, raiding the stores | Aggression |
| Fighting | Aggression and courage |
| Fleeing | The lack of courage |
| Resting | Laziness |
| Wandering, walking away | Curiosity |

### How traits act (`TemperamentRules`, `creatures.json`)

- **Guarding:** within 250 m of a village its god leads, a Creature meeting any Creature but an
  ally's or a friend adds 0.2 × its protectiveness to its will to fight, and takes as much from its
  will to flee.
- **Envy:** meeting a stronger Creature (by power), not an ally's, adds 0.03 × its jealousy to its
  rivalry. Rivals seek each other at war, so the jealous make enemies.
- **Defending (a Creature's own choice):** a Creature whose protectiveness × courage reaches 0.3
  sets out by itself against the beast ravaging its god's village (`CreatureDefending`), unless its
  god has sent it elsewhere. AI gods already send theirs, so this is a player's Creature's own act.
- **Mercy:** a winner spares a rival it envies less (by half its jealousy times its rivalry).

### How traits evolve

| Cause | Change |
| --- | --- |
| Reinforcement | A reward moves the traits behind the judged action up, a punishment down (a rewarded flight makes a coward) |
| Divine treatment | A reward eases jealousy by 0.01 and a punishment feeds it as much; loyalty moves as before |
| Experience | A victory: courage +0.04 |
| Trauma | A defeat: courage −0.08 and jealousy +0.05; running from a Creature: courage −0.02; a wound from the beast or a miracle: courage −0.1 × the health lost |
| Relationships | Playing with another Creature: playfulness +0.02 |

### What the player sees and does

- **The Creature card:** a line with its character ("brave, protective, playful and loyal").
- **The book's Creature page:** the character and every trait's value.
- **F1:** every trait's value and the character.
- **Alert:** "Bramble goes by itself to defend Brightwater from Gharos".
- **Debug driver:** `--temperament=brave,jealous,...` gives the player's Creature the named traits
  strongly.
- **Saves and sharing:**
  - saves move to version 28; a version 27 save gives every Creature 0.5 of each new trait;
  - the protocol moves to version 16.

## Consequences

- **§26:** all twelve traits exist, and the five ways they evolve are modelled.
- **Creatures:** over ten one-hour games, 40 Creatures end with:
  - 13 playful, 11 kind, 11 brave, 9 cowardly, 9 aggressive, 9 lazy, 9 curious;
  - 8 protective, 8 jealous, 5 sadistic, 3 loyal;
  - 2 even-tempered.
- **Encounters:** Creatures fight more (14 fights in ten games, against 11) and play more (13
  against 8), since the protective stand their ground at home and the jealous make rivals.
- **Balance (passive player):** over 160 seeds (30–189), 141 games are won within the hour, against
  150 for part 16.
  - The difference is about two standard deviations.
  - With the temperament rules off but the new drives kept, 149 are won, so the cost comes from the
    extra fights, mostly guarding and envy.
  - Halving both left 141. Wins that come are no slower: over seeds 30–69, 33 games are won at a
    median of 22.3 minutes and a mean of 27.2 (part 16: 34, at 24.5 and 28.5).
- **Not modelled:**
  - jealousy of its god's attention to another Creature (a god has one);
  - named-trait milestones in the history.
