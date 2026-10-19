# ADR-017: Creature relationships, meetings and combat

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature's simulation,
  learned utility AI and authority), [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md)
  (gods' memory of acts) and [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) (four
  gods)

## Context

Milestone 11 (spec §126) asks for rival Creatures, friendship, rivalry, combat and memories. The
specification also asks:

- **Relationships (§27):** Creatures remember each other, tracking friendship, fear, rivalry,
  respect and trust. Two allied Creatures may become friends, and if their gods later fight, this
  creates interesting behaviour.
- **Defeat (§28):** defeated Creatures are banished, not killed, with a temporary absence, scars,
  a reputation impact, a loyalty effect and an experience consequence.
- **Attributes (§23, §90):** CombatSkill and Relationships belong to a Creature's state.
- **MVP Creature (§111):** combat and fighting Creatures.
- **Personality (§26):** traits evolve through experience, trauma and relationships.
- **AI (§86):** relationship memory.

Every god already has a Creature (Milestones 4, 5 and 9), but they never dealt with each other.

## Decision

- **Each Creature keeps its own view of every Creature it has met** (`CreatureRelation` in
  `CreatureState.Relations`): friendship, fear, rivalry, respect and trust (0..1), the tick of
  their last meeting, and the latest encounters it remembers (`EncounterKind`: met, greeted,
  played, won, lost, spared, was spared, drew, fled, chased off, betrayed, saw it attack our
  people). The two sides of a pair can differ. Fear, rivalry and friendship fade; trust drifts back
  to where it started; respect stays.
- **Meetings.** Two free Creatures (not banished, fighting or playing) within `MeetRadius` meet, at
  most once every `MeetCooldownSeconds` per pair. Each answers with the brain it already has
  (ADR-010): learned preference × drive × personality, plus jitter, over four answers: fight,
  play (`Befriend`), flee or walk on.
  - The situation is `MeetsEnemyCreature` when the gods are at war, otherwise `MeetsCreature`, so a
    god can teach its Creature to fight enemies and befriend everyone else.
  - Drives come from the views and the gods' standing. War and rivalry raise the urge to fight;
    friendship and fear lower it; being weaker (power and health) lowers it further. Friendship,
    trust, alliance and boredom raise the urge to play. Fear and weakness raise the urge to flee
    from a threatening Creature.
  - The outcome:
    - one attacking starts a fight, unless the other flees;
    - two wanting to play play together for a while, unless one is on its god's errand;
    - otherwise one wanting to play greets the other;
    - one wanting to flee runs;
    - otherwise they walk on.
  - A Creature attacked by a friend remembers a betrayal.
  - Friends whose gods go to war rarely fight: friendship outweighs the war's urge. The player is
    told when the player's Creature will not fight a friend.
- **Seeking.** Creatures within `SeekRadius` are options in the Creature's ordinary decisions: go
  after a rival, go to play with a friend, or flee a threat within `FleeRadius`.
- **Combat** (`CreatureCombat`, every tick while two fight):
  - Each tick the two trade blows by power, which is strength raised by `CombatSkill`. The jitter
    comes from a stream derived from the seed and the tick, as weather and disasters do, so a fight
    never shifts the shared stream.
  - The first worn down to `YieldHealth` is beaten. A fight that runs `Fight.Seconds` is a draw.
  - A fight one fighter leaves (punished away, starved) is broken off, and the one that stayed has
    chased it off.
  - Every fight raises both fighters' combat skill.
- **Mercy and defeat (§28).** A winner whose mercy is high enough lets the loser go. Mercy is
  kindness tempered by aggression, plus friendship, less at war. Otherwise the loser:
  - is banished for `BanishedSeconds` (temporary absence);
  - gains a scar (`Scars`), drawn on its flanks;
  - loses `LoyaltyPerDefeat` loyalty;
  - loses `DefeatExperienceLoss` of its progress towards the next level, never a level.

  A defeat also affects others:
  - **Reputation.** Followers of the winner's god who see it, within `WitnessRadius`, gain faith
    and fear; the loser's god's followers gain doubt.
  - **Gods' memory.** The loser's god remembers `CreatureDefeated`, a hostile act.
- **Experience, trauma and relationships (§26).** Winning adds experience, scaled by the levels'
  ratio, and a little aggression, and reinforces the liking for a fight. Losing removes some
  aggression and twice as much liking for a fight. The views change as each encounter says. The
  loser fears and respects the winner, and a spared loser trusts and likes it instead. The
  winner's rivalry halves. A Creature whose god's people another Creature attacks counts that
  Creature a rival.
- **Gods.**
  - Every AI god rewards its Creature for fighting an enemy's Creature and for playing with an
    ally's, and punishes the reverse. Other dealings are judged by kindness, as deeds were.
  - An AI god at war may send its Creature against the enemy's Creature. The value is
    `SendCreature × (Protect × the share of its people the enemy threatens + 0.5 × Aggression) ×
    confidence`, and it does so only when its Creature is a match.
  - AI gods never pull their Creature out of a fight or a game.
- **Tuning** is part of `creatures.json`:
  - `Bonds` and `Combat`;
  - the three new actions, whose rules give a fight's longest length and the distance a Creature
    runs;
  - the starting preferences for the two meeting situations.
- **Seeing it:**
  - Creatures fight face to face, trading blows, with their health on their labels. They hop
    together when they play.
  - Scars show on their flanks.
  - The Creature card lists its bonds, combat skill and scars.
  - Each rival's card in the diplomacy panel says how the two Creatures regard each other.
  - Alerts report the player's Creature's meetings and fights, and rivals' Creatures beaten by
    others.
  - The F1 lines show skill, scars, power, every view with its latest encounters, and how the
    player's Creature would answer the nearest one.
  - K brings the nearest Creature to the player's, and `--meet=on|<name>` does so at start.
- **Saves** move to version 11. The migration from version 10 gives every Creature no skill, no
  scars and no views, and marks nothing it did as concerning another Creature.

## Consequences

- Positive: Creatures have histories with each other that the player can read.
  - Rivalries grow from wars and from attacks on a god's people.
  - Friendships grow from play and greetings, most between allies' Creatures.
  - A defeat leaves a scar, fear and a grudge in the gods' memory.
- Positive: combat chains through the existing systems. It feeds belief, diplomacy and learning:
  reputation through witnesses, gods' memory through the ledger, and learning through rewards and
  experience. It stays deterministic, saved whole and separate from the shared stream.
- Positive: in 60-minute headless games with a passive player (seeds 1234, 42, 7), the four
  Creatures:
  - met 25–35 times;
  - fought 8–9 times, nearly all between gods at war;
  - formed friendships of up to 0.6.

  Over 40 seeds, games were won about as often and as soon as in Milestone 10: 24 against 22, with
  a median of 37.6 minutes against 35.2.
- Negative: the player's untaught Creature, never sent away by a god, levels fastest and wins most
  of its fights. The Tyrant's Creature, whose god is nearly always at war, fights and loses most:
  6–7 scars an hour, against 0–1 for the others. Snowballing is held back:
  - beaten Creatures flee;
  - victories pay little;
  - defeats cost little;
  - being stronger adds no wish to fight.

  It is not removed.
- Negative: there are no group fights, no fights over a settlement's defence beyond rivalry from
  attacks, no Creature death and no player command to attack another Creature. The player sends
  the Creature with the hand and teaches it through rewards and punishments.

## Alternatives considered

- **A fight resolved instantly by comparing power.** It is simpler, but nobody could watch, call
  off or reward a fight, and a draw would be impossible.
- **A shared, symmetric relation per pair**, as gods' stances are. Spec §27's fear and respect are
  one-sided: the beaten fears the winner, not the other way round.
- **Fights started only by the gods' command.** Creatures that only fight when told have no
  relationships of their own. Spec §27's friends at war need Creatures that choose.
- **Fleeing that fails against a faster attacker.** In headless runs the strongest Creature then
  caught and beat the same weak ones again and again. One reached 16 scars and no loyalty in an
  hour.
