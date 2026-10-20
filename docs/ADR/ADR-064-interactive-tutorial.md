# ADR-064: An interactive tutorial

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-020](ADR-020-game-flow-and-history.md) (the first-minutes guide),
  [ADR-059](ADR-059-interface-look-scaling-and-hud.md) (the HUD) and
  [ADR-060](ADR-060-main-menu-and-local-sqlite-profile.md) (the title screen and the profile)

## Context

The owner asked for "a tutorial on how to play the game". There was a first-minutes guide of six
text steps (domains, a miracle, teaching, diplomacy, winning). It pointed at nothing on screen and
said nothing of moving the camera, the hand, the Creature's work and fights, or time. It could not
be chosen again, and nothing remembered it.

## Decision

### The tutorial (`Pantheon.Game.Flow.Tutorial`, pure)

Fifteen steps teach the game by playing it:

1. Welcome.
2. Moving the view (60 m).
3. Zooming in on the home village (closer than 160 m).
4. Turning the view (0.6 rad).
5. The god's plaque.
6. Casting Food (a cast by the player's god).
7. Influence and the map overlays.
8. Following the Creature.
9. Rewarding or punishing it (a reward or punishment event).
10. Setting it to work (the client's signal that a work order was accepted).
11. Picking something up with the hand.
12. Speeding up time.
13. Opening diplomacy.
14. Setting the Creature on a rival's.
15. How to win, and where the rest is.

**How a step ends:**

- **Steps that ask for an action** end when the player does it, judged from the world's events and
  from a `TutorialView` of what the client sees: the camera's focus, distance and turn, following,
  the hand, the time scale and the diplomacy panel.
- **Only what happens within the step counts:** a step measures movement from the first view it
  sees, so what the player did before it does not count.
- **The other steps** end with Next. Any step may be skipped.
- **Wording:** each step names the player's god, Creature and home village.

**What a step points at:**

- **On the HUD:** a `TutorialTarget` (the plaque, the first miracle, the Follow, Reward and Punish
  or Work buttons, the time controls, the rivals or the race).
- **In the world:** the home village, marked by a beacon of light.

### Showing it (client)

- **`TutorialPanel`:** a card at the right with the step's number of fifteen, a progress meter,
  its title in Cinzel and its words, and buttons for Next (or "Skip this step") and "Leave
  tutorial". A pulsing gold frame surrounds the HUD element the step points at
  (`Hud.TargetRect`).
- **`TutorialController`:** shows the tutorial the player's actions each frame, passes on the
  events and the work orders (`DivineController.WorkOrdered`), and raises a pulsing column of
  light over the home village when a step points there.
- **Remembering it:** a finished tutorial is kept in the profile (`tutorial_done`).

### Starting it

- **From the title screen:** a Tutorial choice starts it on the first small island (seed 1234),
  as a sandbox no god can win so there is no hurry. That island is not kept among the player's
  worlds.
- **First run:** while the profile holds no world and no finished tutorial, the title screen says
  "New here? Learn to play in ten minutes."
- **From the command line:** `--tutorial=on|<step>` starts it, at a step, for captures.
- **The old guide:** the first-minutes guide still opens a new world, and hides during the
  tutorial.

## Consequences

- **A newcomer:** learns the camera, miracles, influence, the Creature (following, teaching, work,
  fights), the hand, time, diplomacy and winning in about ten minutes, each by doing it once.
- **Tests:** the tutorial's steps are pure and tested.
- **Not done:** voice-over, a tutorial for playing together, and steps for domains, holy sites
  and the Divine Council, which the Book of the World explains.
