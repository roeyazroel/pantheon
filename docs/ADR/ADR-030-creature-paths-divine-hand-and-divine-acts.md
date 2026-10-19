# ADR-030: Creatures' paths, the Divine Hand and the Divine Acts

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-009](ADR-009-belief-influence-and-miracles.md) (belief and miracles),
  [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature and the hand),
  [ADR-017](ADR-017-creature-relationships-and-combat.md) (combat) and
  [ADR-029](ADR-029-hazards-preparation-and-marks-on-the-land.md) (the volcano)

## Context

- **§23:** a Creature's attributes include intelligence, resilience and divine affinity, which
  were not modelled.
- **§24:** Creatures walk paths: Guardian, Warrior, Destroyer, Healer, Nature Keeper, Missionary,
  Trickster and Commander, and no player gets every path in one game.
- **§55:** the Divine Hand picks things up, carries villagers, rescues people, throws rocks and
  moves trees. Until now it was a visual that pressed, patted and swatted.
- **§14:** the Divine Acts are extremely expensive miracles that become history. Examples are
  redirecting a river, permanently blessing land, creating an island, splitting a mountain,
  awakening a dormant volcano and stopping a continent-scale disaster. Power had little to spend
  on late in a game.

## Decision

### Attributes (§23)

They are derived, never stored (`CreatureAttributes`):

- **Intelligence:** the species' learning, growing by `IntelligencePerLevel` per level. It scales
  what rewards and punishments teach.
- **Resilience:** the species' toughness, growing by `ResiliencePerLevel` per level. It divides the
  harm of fights, starvation and wounding miracles.
- **Divine affinity:** 1, growing by `DivineRules.CreatureAffinityPerLevel` per level. It widens the
  influence the Creature carries for its god.

The card and F1 show them with strength and speed.

### Paths (§24)

- **Callings:** every deed, fight and reward calls the Creature towards the paths it belongs to
  (`CreatureState.Callings`, from `creatures.json`'s `Callings.Deeds`):
  - feeding → Healer;
  - building → Guardian;
  - playing and befriending → Missionary;
  - attacking → Destroyer;
  - stealing food and fleeing → Trickster;
  - foraging and wandering → Nature Keeper;
  - going where it is sent → Commander;
  - a won fight → Warrior;
  - sparing a beaten foe → Guardian;
  - a rewarded deed calls `RewardCalling` further.

  The player shapes the path by what it rewards.
- **Taking up paths:** at each of `Callings.Levels` (3, 6 and 9) the Creature takes up the path it
  has been called to most and has not yet taken (`CreatureSpecialised`). With no calling at all it
  follows its strongest trait. Three of eight paths in a ten-level game: never all of them.
- **What each path does:**

  | Path | Effect |
  | --- | --- |
  | Guardian | Its god's villages within `GuardRadius` take `GuardianShield` less of every disaster's harm |
  | Warrior | `WarriorPower` more combat power |
  | Destroyer | Its attacks kill `DestroyerAttack` more |
  | Healer | Its deeds in a village give `HealerHealth` |
  | Nature Keeper | Its deeds heal `KeeperHealing` of the harm and blight around it |
  | Missionary | Its deeds win `MissionaryConversion` of a village's godless to its god |
  | Trickster | Its deeds sow `TricksterDoubt` in the village's main rival god |
  | Commander | Its god's villages within `GuardRadius` have `CommanderDefence` against Creatures' attacks |

### The Divine Hand (§55)

The hand is now an authority (`HandAuthority`), with a command and a player intent (`HandIntent`).
What it holds is saved on the god (`GodState.Held`, `HeldObject`). It works where the god's
influence reaches at least `MinInfluence`, as a miracle does.

- **Picking up (R):** whatever lies within `Reach` nearest the cursor:
  - **The god's Creature:** it waits in the hand, doing nothing, until set down. It cannot be lifted
    while it fights.
  - **Villagers:** up to `VillagersPerGrab`, at most `MaxShare` of a village. Lifted out of danger
    (a fire, shaking ground or a flood) they are being rescued. Taken from a safe home they and
    those left behind feel `Abduction`, and the god's conduct notes harm.
  - **A tree** from land at least `TreeVegetation` green (taking `TreeCover` of it), or **a rock**
    from barer land.
- **Setting down (R again):**
  - **Villagers in a village** join it at once. Rescued (carried from danger to safety), they and
    everyone within `WitnessRadius` feel `Rescue`, and the god's conduct notes help.
  - **Villagers on open land** walk to the nearest village as a migration group
    (`MigrationCause.Carried`).
  - **Villagers in the sea** drown.
  - **A tree** is planted (`Planting`). **The Creature** stands where it is set and decides afresh.
- **Throwing (Shift+R):**
  - **A rock** strikes within `ThrowRadius`, wrecking `ThrowHousingLoss` of the houses and killing
    `ThrowDeaths` of the people at the centre, harming the land and leaving a crater. A tree or a
    body does `TreeThrowFactor` as much, and thrown villagers die.
  - Those struck feel `Smiting`; striking the god's own followers is cruelty.
  - A thrown Creature loses `ThrownCreatureHealth` and a little loyalty.
- **AI gods** do not use the hand: it is the player's tool, as §55 describes it.

### Divine Acts (§14)

- **Where they sit:** three miracles of `MiracleScale.DivineAct`, for a Greater God and above,
  costing 1,500–2,500 power with cooldowns of 10–20 minutes. They are cast from the Domains panel
  (G), not the miracle bar, whose keys are full.
- **The acts:**
  - **Hallow the Land:** blesses the land within reach forever. `RegionState.Hallowed` is a floor
    blessing never fades below, as a scar is a floor under harm.
  - **Wake the Volcano:** wakes the island's sleeping volcano wherever it stands (ADR-029).
  - **Still the World:** ends every disaster on the island at once: quakes, fires, floods, blight,
    storms, tornadoes, locusts, drought and heat or cold waves, and puts the volcano to sleep.
- **History:** each raises `DivineActDone` and enters the history (`Hallowing`, `Awakening`,
  `Stillness`).
- **The AI:** it values hallowing its own hungry lands and stilling the dangers to its people. It
  never wakes the volcano, since it aims only at villages.
- **Left out:** acts that reshape the terrain (a new island, a split mountain, a river's new course).
  §64 postpones changing the terrain, and a river's land is fixed with the terrain at generation.

### Saves, multiplayer and presentation

- **Saves:** version 22. Migrating from 21 gives Creatures no paths or callings, every hand empty
  and no hallowed land.
- **Multiplayer:** protocol 10, with `HandIntent`. The regions section carries hallowed land; the
  Creatures and gods sections carry paths and what hands hold, as whole lists.
- **Alerts and history:**
  - Alerts tell of a rival's hand in the player's villages, rescues, drownings, things thrown,
    Creatures' new paths and every Divine Act.
  - The history keeps paths taken, rescues, stonings and acts.
- **The card and F1:**
  - The Creature card shows its paths, the next calling level and its attributes.
  - F1 adds the callings.
- **The look:**
  - The hand shows the rock, tree or villagers it holds, and flings them in an arc when it throws.
  - A held Creature hangs under the hand.
  - Labels sit on their own render layer, which ground decals leave alone (part 9).
- **Options:** `--hand=villagers|creature` lifts at the start for captures. R and Shift+R in play.

## Consequences

- Creatures differ by how they were raised: a rewarded feeder becomes a Healer and a Guardian, a
  fighter a Warrior and a Destroyer.
- Late-game power has a sink. Divine Acts are rare: they need a Greater God.
- **Balance:** 40 one-hour passive games found a winner 39 times, with a median of 27.6 and a mean
  of 29.3 minutes (part 9: 39, 24.9 and 28.3).
- **Tuning found:**
  - The AI first stilled the world 35 times in ten hours, since the leading AI gods hold hundreds
    of thousands of power in a passive game. It now stills only a real disaster among its own
    people and hallows only land not yet hallowed, and the acts' cooldowns are 20 and 30 minutes.
  - The hand first lifted a Creature standing in a village rather than its people; it now lifts
    whichever is nearer the cursor.
- **Not modelled:**
  - Acts that change the terrain.
  - Carrying offerings.
  - Creatures above level 10 (§23's target of 50).
  - The AI using the hand.
