# Milestone 15, part 49 — Gods answer Creatures in their lands, a steady hand, and fights you can watch

## Overview

The owner, playing, reported three things.

- **The hand.** "The pick and drop of the creature is not stable and not always working."
- **The rivals.** "When I move my creature to another village, I don't expect the other creature
  to just walk around: the AI god should be triggered, react, and tell its creature to do something
  based on my relationship with this god."
- **Fights.** "Fight between my creature and another was very fast, and after my creature killed
  the other, my creature died a couple of seconds later."

The decisions are in [ADR-070](../docs/ADR/ADR-070-gods-answer-creatures.md).

### What was found

- **The hand.**
  - A held Creature was drawn in the hand only while the pointer touched the land. Over a panel or
    the sky it dropped back, standing, to where it was lifted, then jumped back to the hand.
  - The Creature was lifted only when nearer the hand than the nearest village's centre. As
    Creatures spend their time in villages, the hand often took the villagers instead.
  - The hand took the ground under the pointer, so pointing at a tall Creature's body found the
    ground well behind it.
  - The hand needed the god's influence where it set something down, so a Creature could not be set
    down in a rival's lands.
- **The rivals.** An AI god set its Creature on another only when at war, and only as one candidate
  among its miracles and errands, which it often did not choose. A hostile god, as most are, did
  nothing.
- **Fights.**
  - Two equal Creatures lost 5% health a second, so a fight was over in 11–13 seconds, and in two
    or three at ×4.
  - A replayed fight's winner rested and recovered. A winner left wounded, though, is the easiest
    prey for a third Creature, and gods at war send theirs against any.

### What changes

1. **A steady, precise hand:**
   - a held Creature stays in the hand wherever the pointer goes, and the hand stays in sight
     while it holds anything;
   - pointing at your Creature's body (a capsule to its head) lifts it, not what lies behind it;
   - your Creature within half the hand's reach is lifted before a village's people;
   - your own Creature may be set down or thrown anywhere on land, as it may walk anywhere.
2. **The gods' watch.** Every second each god looks for other gods' Creatures within 220 m of a
   village it leads. Its hostility to the visitor's god decides the answer:
   - **Welcome:** its Creature comes to greet the visitor, and it remembers a friend's visit.
   - **Watch:** its Creature comes to follow the visitor, and it holds the trespass against its god.
   - **Repel:** its Creature is set on the visitor, if it is a match for it; if not, it guards the
     village.

   A player's god is told and chooses for itself. The two new memories, trespass and visit, change
   trust and rivalry and show in the diplomacy panel. Alerts tell the player each god's decision.
3. **Fights you can watch:**
   - half the damage, so a fight between equals lasts about half a minute;
   - a respite of 45 seconds after a fight, when a Creature is drawn into no other;
   - when your Creature starts a fight while the world runs faster than ×1, it slows to ×1.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Domain` | • `WatchRules` (in `GodAiRules.Watch`)<br>• `CreatureWatched` and `WatchAnswer`<br>• `DiplomaticAct.Trespassed` and `Visited`<br>• `CreatureCombatRules.RespiteSeconds` |
| `Pantheon.Simulation` | • `Gods/CreatureWatch` (`Hostility`, `AnswerFor`; run by `GodAiSystem` after the AI's own turns)<br>• the ledger, grudge and war score know the new acts<br>• `GodAi` does not call a hunting Creature off for an errand<br>• `CreatureCombat.Recovering`, which `CreatureMeetings` honours<br>• `HandAuthority` lets a god set its own Creature down beyond its influence<br>• `HandCreatures` lifts it within half the reach first |
| `Pantheon.Game` | • `CreatureCommands.SlowForFight` and `Visit`<br>• the watch's alerts (`GodAlerts`)<br>• the memories' words (`DiplomacyText`)<br>• `--visit=<n>` |
| client | • `DivineController.HeldCreatureAt` keeps the grip<br>• `DivineHand` stays in sight while holding<br>• `CreaturePresenter.Pointed` and `GameRoot.PointedCreature` (the hand lifts the Creature pointed at)<br>• `GameRoot` slows to ×1 for your Creature's fights |
| content | • `god_ai.json`'s `Watch`<br>• `creatures.json`'s `DamagePerSecond` 0.025 and `RespiteSeconds` 45 |

## Test plan

- **`CreatureWatchTests`:**
  - each standing's answer;
  - an aggressive god drives out what a calm one only watches;
  - an enemy sets its Creature on yours, remembers the trespass, and you are told, once in the
    cooldown;
  - an ally's Creature comes to greet yours, and it remembers a friend's visit;
  - you are told when another god's Creature comes into your village;
  - a winner gets its breath back before another fight;
  - your Creature's fight slows the world to ×1;
  - `--visit` sets your Creature down beside a village.
- **`HandTests`:** your Creature is set down and thrown beyond your influence, and lifted before a
  village's people within half the hand's reach.

## Tasks

### Completed

- [x] The hand, the watch, the slower fights and the respite, the slowing to ×1, alerts, tests,
  captures and docs.

### Verification results

- **Build and tests:** `dotnet build` has no warnings, and 1793 tests pass (1781 before).
- **Fights replayed** with the shipped tuning before the change (seeds 1234, 77 and 5): each lasted
  11–13 seconds, and the winner rested and recovered afterwards.
- **Ten minutes of play, three seeds, counted:**

  | Rule | Creature fights |
  | --- | --- |
  | no watch | 0–1 |
  | hostile gods driving visitors out (repel from 0.6) | 1–4, all banishments; repel answers 26–52 |
  | repel from 0.75, as shipped | 0–4; aggressive gods drive out and calm ones watch; 8–14 watch answers |

  The visit counts were also high at first, because a guard re-sent later was told of each time.
  Now only a guard set on a foe later is told of.
- **A visit replayed under each standing:**
  - an ally welcomed;
  - a neutral god was wary;
  - hostile and warring gods were angered.

  The god's Creature was first refused when hungry, and was then found fighting another god's.
  A god's own errands also overrode its hunt for a tick, until the watch was made to look last.
- **In the game:** `--visit=6` set Bramble down in Mossgate. Belrays, hostile but calm, sent Pip to
  watch it, and the alert said Belrays held the trespass against you. On meeting, Pip attacked.
- **The hand:** Bramble held in the hand above Duskwater (`--hand=creature`), on Terrain3D and the
  fallback renderer.
- **Not verified in a capture:** the hand keeping a Creature as the pointer crosses a panel, since
  scripted runs aim at the screen's centre.
