# ADR-026: War goals, intelligence, proxy wars and mortal holy wars

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) (relations and war),
  [ADR-024](ADR-024-doctrines-secularism-dependency-resentment-and-traits.md) (doctrines, resentment
  and revolts) and [ADR-025](ADR-025-treaties-secrets-favours-and-the-divine-council.md) (treaties
  and secrets)

## Context

The specification asks for wars, knowledge and mortals that are richer than a war status:

- **§43:** players should not know everything: secret treaties, exact enemy belief, enemy
  resources, planned betrayal. Intelligence comes from spies, Creature scouting, allies,
  miracles and defectors.
- **§44:** victory in war may involve conversion, Creature defeat, holy-site occupation,
  rebellion and the rest. Direct destruction should not always be optimal.
- **§45:** gods may act indirectly: support rebels, secretly create disasters, send disguised
  missionaries, anonymously provide miracles, spread propaganda.
- **§46:** a militaristic religion may decide on its own that another faith is hostile. Its god
  may encourage, discourage or ignore, for mortals are not puppets.

## Decision

### War goals and dictated peace (§44)

- **Records:** every declared war has a `WarRecord` in `DiplomacyState.Wars`: its aggressor and
  defender, its goal (`WarGoal`: Conquest, HolySite, Tribute or Humiliation), the holy site for a
  site war, its start, and each side's score.
  - A declaration carries a `WarAim`; a site war on a god that keeps no site becomes a conquest.
  - Wars a treaty or the council starts are conquests.
- **Scoring:** the ledger scores every act between gods at war (`WarBook.Score`), so the
  diplomatic memory and the war score never disagree.

  | Act | Score |
  | --- | --- |
  | A village converted | `ConversionScore` (3) |
  | The enemy's Creature defeated | `CreatureScore` (4) |
  | The enemy's holy site taken | `SiteScore` (5) |
  | A holy war's raids | `RaidScore` (1) |
  | The enemy's people sheltered | `RefugeeScore` (0.5) |
  | A strike on its followers | `HarmScore` (0.5): destruction is not the best road |

- **When a side may dictate peace:**
  - The aggressor, when its goal is met with a lead: `GoalScore` and half as much lead for a
    conquest or tribute; keeping the site for a site war; having defeated the enemy's Creature
    to humble it.
  - The defender, when it has repelled the attack with `GoalScore` and that lead.
  - `WarGoalAchieved` tells the aggressor once.
- **Surrender:** the victor's `DemandSurrender` is a proposal (`ProposalKind.Surrender`).
  Accepting it brings peace on terms set by the goal (`PeaceDictated`):

  | Goal | Terms |
  | --- | --- |
  | Conquest | `CededShare` of the loser's believers, in its village where the victor is followed most, turn to the victor. |
  | Holy site | The site changes keeper. |
  | Tribute (and a defender's victory) | The loser pays `DictatedTribute` a minute for `DictatedSeconds`. |
  | Humiliation | A territorial agreement for `DictatedSeconds`. |

  The loser remembers the dictated peace.

### Intelligence (§43)

- **What a god knows:** `IntelModel.Level(observer, subject)` is derived from the world and never
  stored, from 0 to 1:
  - an alliance (`AllyIntel`) and any treaty (`TreatyIntel`);
  - its own followers among the subject's people, its spies and defectors (`PresenceIntel` per
    share);
  - its Creature's visits to the subject's villages in the last `ScoutSeconds` (`ScoutIntel`);
  - a Knowledge domain (`KnowledgeIntel`).
- **What it shows the player:**
  - On the HUD a rival's following is exact from 0.6, rounded ("about 350 followers (~30%)")
    from 0.3, and unknown below.
  - Well informed, the diplomacy panel shows a rival's Divine Power and whether an ally is
    weighing betrayal.
- **What it changes:**
  - Secret treaties leak faster when others know much of a party.
  - A covert act is traced with `ExposureChance` × (0.25 + 0.75 × what the wronged god knows of
    its author), from a draw derived from the seed and the tick.
- **The AI:** it still reads the world fully; intelligence limits what players see.

### Proxy wars (§45)

- **Veiled miracles:** a cast may be veiled (`CastMiracleRequest.Veiled`), at `VeilCostFactor`
  times the cost.
  - It does what it does, but no witness credits the caster: a kindness earns no faith or love,
    and a strike plants `VeiledDoubt` in the people's own god (why did it not protect them?)
    instead of fear of the caster.
  - Nothing is noted: no rival memory, no conduct, no defiance.
  - If the god whose people it reached traces it, it is attributed after all, and the victim
    remembers `CovertAct` (`CovertActExposed`).
  - `MiracleCast.Veiled` tells other players a miracle fell, not who cast it.
- **Proxy acts** (`ConflictAuthority`), in a settlement where the acting god has
  `ProxyInfluence`, against a god followed there, one every `ProxyCooldownSeconds`:
  - **inciting rebels:** `IncitePower`; resentment, fear and unrest, which can tip a village into
    the revolt of ADR-024;
  - **propaganda:** `PropagandaPower`; doubt.
  - Both are covert and may be traced. `ProxyActed` reaches only the actor's player.

### Mortal holy wars (§46)

- **Who wages them:** a settlement whose main god holds at least 0.4 of it and whose militarism
  is at least `HolyWarMilitarism` may declare holy war (`HolyWarState` in its society):
  - on the most populous neighbour within trade range led by a god that is not its god's ally or
    non-aggression partner;
  - with `HolyWarPerSecond` chance a second (twice when the gods are at war, scaled by
    militarism), from a draw derived from the seed, the settlement and the tick.
- **Raids:** each second they kill and convert the attacked god's followers at a strength set by:
  - each side's people, militarism, warfare, safety and walls;
  - the god's stance: encouraged ×1.5, discouraged ×0.5.

  Casualties are rounded by chance, so small raids still count. The attacked god remembers the
  holy war, and raids score in a war between the gods.
- **The god's word:** its god may encourage, ignore or discourage (`HolyWarCommand`).
  Discouraging halves the war's length but costs the attackers `DiscourageLoveLoss` of their love.
- **The end** (`HolyWarEnded`): after `HolyWarSeconds`, when zeal fades, when the gods ally or
  sign a non-aggression pact, or when the attacked village turns to the attackers' god. The
  settlement then rests `HolyWarCooldownSeconds`.

### The AI

- **Declaring war:** `WarAims.For` chooses a site war for a god that cares for holy sites, to
  humble a weaker Creature for a warlike god, tribute from a much weaker enemy, and conquest
  otherwise.
- **Surrender:** a god demands surrender when it may. It accepts by how far behind it is, its
  weariness and fear, against its grudges.
- **`CovertAdvisor`:**
  - It urges its people's holy wars on when at war or warlike, and holds them back from allies
    and partners or when cooperative.
  - A treacherous god with deep enmity and power to spare incites or spreads propaganda.
- **Veiling:** the most treacherous gods veil harsh miracles on peoples they resent but are not at
  war with.

### Saves, multiplayer and presentation

- **Saves:** version 18. Migrating from 17 gives no war records (wars under way get none) and no
  holy wars. `ConflictIntegrity` checks them.
- **Multiplayer:** protocol 6. `CastIntent.Veiled`, `ProxyIntent` and `HolyWarIntent`;
  `DiplomacyIntent` carries a `WarAim` with the command. The host hides proxy acts and veiled
  casters from other players.
- **Diplomacy panel:** each god's war (goal and score), what the player knows of it, war buttons
  by goal, and Demand surrender.
- **Settlements panel:** holy wars waged and suffered, the player's word to its people's holy
  war, and proxy acts against the god that leads.
- **HUD:** rivals' followings blurred by intelligence; "Veiled (T)" on the cast preview.
- **Alerts:** goals won, peace dictated, covert acts exposed, holy wars declared and ended, and
  veiled strikes as "an unknown god".
- **History:** holy wars of note, dictated peaces and uncovered plots.
- **Debug:** F1 lists wars with scores, holy wars and every god's intelligence of the others.
  T toggles veiling. `--war-goal`, `--holy-war` and `--veil` drive conflict from the command line.

## Consequences

- **AI conflict:** in ten passive-player games AI gods declare wars mostly for conquest and
  sites. A few end in dictated peace. Trickster gods veil strikes and spread propaganda, often
  caught. Five holy wars broke out, three of which turned the attacked village.
- **Balance:** measured over 40 seeds against part 5's build (see the plan). The first tuning
  let covert acts slow victories (6 of 10 games won); ablation showed both veiling and proxy acts
  contributed, and both were made dearer and rarer.
- **Not modelled:**
  - Fog of war on the map itself: the overlays and the world view still show everything.
  - Prophets and divination miracles as intelligence sources.
  - Disguised missionaries, beyond veiled conversion miracles.
