# ADR-036: Known gods and the gods' long-term plans

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md) (the gods' utility AI),
  [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) (diplomacy),
  [ADR-025](ADR-025-treaties-secrets-favours-and-the-divine-council.md) (treaties and the council)
  and [ADR-035](ADR-035-the-ancient-beast-and-the-magical-anomaly.md) (the beast)

## Context

Three parts of the specification were still partial:

- **§11 (god attributes):** lists `KnownGods`. Every god knew every other from the first tick, so
  it could ally with or declare war on a god whose people it had never met.
- **§47 (AI gods):** gives each AI short-term and long-term goals. The utility AI (ADR-011) had only
  short-term ones: each evaluation it scored the actions open now.
- **§87 (god AI architecture):** asks for major planning every 15–60 seconds, and for the AI to
  maintain goals such as "Protect capital", "Convert northern settlement", "Increase alliance with
  Nara", "Prepare for drought" and "Weaken Vael".

§48 still holds: the AI scores actions continuously and does not follow a behaviour tree. So goals
should bend that scoring rather than replace it.

## Decision

### Known gods

- **The state:** `GodState.KnownGods` holds, sorted, the ids of the gods a god has met.
- **Meeting:** the diplomacy step (`Acquaintance.Step`, each second) brings two gods to know each
  other for good (`GodsMet`) when:
  - villages they lead lie within 500 m (`Neighbours`);
  - each is followed by 5% of one village (`SharedVillage`);
  - a whole road joins their villages (`Road`);
  - their Creatures have met (`Creatures`);
  - one's deeds touched the other's people, so the other remembers them (`Deeds`);
  - they have dealings already: a stance, a treaty or a proposal (`Diplomacy`);
  - they sit together in the Divine Council (`Council`), which seats every god, so all have met
    by its first session.
- **What waits on meeting:** with `MeetFirst`, a god may not offer an alliance, declare war,
  propose a treaty, ask a favour, demand surrender or absorb a god it has not met
  (`DiplomacyRejection.NotMet`). Answering a proposal, asking for peace, and breaking an alliance or
  a treaty need no check, since they need dealings already.
- **The rules:** `DiplomacyRules.Acquaintance` in `diplomacy.json`. Absent (as in the pinned test
  rules), every god knows every other.
- **The AI:** an AI god weighs dealings and goals only with the gods it knows.

### Major planning

`GodPlanner` plans on an AI god's turn once every 30 seconds (`PlanSeconds`), as `NextPlanTick`
falls due. It weighs one best target for each kind of goal (`GodGoalKind`):

| Goal | When | Weight |
| --- | --- | --- |
| Protect its home (the village where most of its followers live) | The beast hunts it, an enemy's Creature is within 300 m, or the plague (danger at least 0.3) | Protect × danger |
| Convert a village within 700 m of its own that neither it nor an ally leads | Always | Convert × the share not its own × size |
| Befriend a god it knows, not allied with it nor at war with it | Always | Cooperation × (0.5 + half its feeling, + 0.3 against a shared enemy, − 0.3 if hostile) |
| Weaken a god it knows and is not allied with | Always | Aggression × (that god's lead + hate + 0.4 at war) |
| Prepare its people | The plague, or the land's dryness past 0.65 | Protect × severity |
| Slay the ancient beast | While it is awake | Protect × its followers where the beast hunts + 0.4 × Aggression + 0.3 × Opportunism |
| Win back its doubters | Its followers' mean doubt reaches 0.3 | The larger of Protect and Bless × twice the doubt |

- **Feelings:** they are measured from rest. A fresh relation is cool, from its starting rivalry
  and suspicion, and that counts as neither friendship nor hate.
- **What it holds:** the god keeps up to three goals (`MaxGoals`), one of each kind, each of weight
  0.35 or more (`Threshold`).
- **Hysteresis:** a goal younger than 90 seconds (`MinGoalSeconds`) is kept unless it is moot. A
  goal is moot when its village is gone, the danger has passed or the beast has fallen. Older goals
  compete with the new options.
- **Announcing:** each new goal is announced (`GodGoalTaken`).
- **The rules:** `GodAiRules.Planning` in `god_ai.json`. Absent, nobody plans.

### What goals change (`GoalFocus`)

- **Casting and sending:** an action that serves a goal is worth up to `Boost` (2) times as much,
  by the goal's weight: `1 + (Boost − 1) × weight`. Its reason says which goal it serves.
  - Protecting: kind casts on the home, and sending the Creature there.
  - Converting: any cast on the village, and sending the Creature there.
  - Weakening: casts on the villages the god leads.
  - Preparing or winning back: kind casts on its own villages.
- **Slaying the beast:** the god sends its Creature against the beast and smites it with every
  smiting miracle it may cast there. A bolt also strikes the village where the beast stands, so the
  god values it less, by half, the more of its own people live there.
- **Diplomacy:** a goal adds up to 0.1 (`DiplomacyBias`) by its weight, and takes as much from the
  opposite move.
  - Befriending favours an alliance and peace with the god, and weighs against war on it or
    betraying it.
  - Weakening favours war on the god, and weighs against an alliance or peace with it.
- **Priorities:** a god preparing asks its villages for health against the plague and food against
  a drought, after hunger, illness, danger and war.

### What the player sees and does

- **The P panel:** a god not yet met says so, with how gods meet, and offers nothing.
- **The top bar:** reads "not yet met" for such a god.
- **Meetings:** an alert announces each god the player meets and how. The history records every
  meeting but the council's (it would list every pair at once).
- **Intelligence:** a well-informed player (intelligence 0.6 or more, ADR-026) sees a rival's goals:
  "it means to weaken you and convert Brightwater".
- **F1:** each AI god's plans with their weights and ages, when it plans next, the gods it knows
  and the best options it is weighing.
- **Debug driver:** `--gods-met=on` introduces every god at the start. The debug drivers that sign
  a treaty or declare war introduce the two gods first.
- **Saves and sharing:**
  - saves move to version 27; a version 26 save gives every god every other god as known (they
    dealt freely), no goals, and a plan on its next turn;
  - the protocol moves to version 15, as the Gods section carries the new state.

## Consequences

- **Completed:** §11's `KnownGods`, §47's long-term goals and §87's major planning.
- **Meetings:** over ten one-hour games, gods first meet a median 2.3 minutes in, and all have met
  by a median of 6.1 minutes, mostly as neighbours or in the first council.
- **Goals per game:**
  - about 18 to convert, 8 to weaken, 6 to befriend, 6 to win back doubters and 5 to protect a home;
  - 3 to slay the beast, and rarely one to prepare.
- **Balance (passive player):** over 40 seeds (30–69), 34 games are won within the hour, at a
  median of 24.5 minutes and a mean of 28.5. Part 15 gave 38, at 25.7 and 27.8.
  - Over 160 seeds (30–189), 150 games are won; part 15 won 155.
  - The gods' plans to weaken the leader, and the leaders' to win back their doubters, draw out a
    few games.
- **Found on the way:** a fixed `Boost` for every goal, however slight, drew out the AI's games
  badly: 65 of 80 won, against 77. The boost is now scaled by the goal's weight, and only goals of
  weight 0.35 count.
- **Not modelled:**
  - gods forgetting each other;
  - unknown gods' names hidden;
  - goals for a player's god, whose player sets their own.
