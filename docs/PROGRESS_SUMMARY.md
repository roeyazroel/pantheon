# PANTHEON — Progress Summary

This document sums up the work so far, Milestones 1 to 11, against the owner's complete
specification, [PANTHEON_SPEC.md](PANTHEON_SPEC.md), which is the source of truth. Section numbers
(§) refer to it. It describes `main` on 2026-10-19.

Milestones 1 and 2 were built in pull requests #1 and #2. Pull request #2 had been merged into the
Milestone 1 branch rather than `main`, so `main` merged that branch at the start of Milestone 3.
From Milestone 3 on, work goes straight to `main`, with one push per milestone.

Status words used below:

- **Implemented**: built as the section asks, and verified.
- **Partial**: some of it is built; the notes say what is missing.
- **Not started**: nothing of it is built yet.

Sections that set a rule or a target rather than a feature say instead whether the rule is
followed or the target has been measured.

## In short

- Eleven of the specification's fifteen milestones are implemented: 1 Foundation (§116),
  2 Living Settlement (§117), 3 First God (§118), 4 Creature (§119), 5 Rival God (§120),
  6 Diplomacy (§121), 7 Weather (§122), 8 Disaster Simulation (§123), 9 Four Gods (§124),
  10 Population Movement (§125) and 11 Creature Relationships (§126). Their playable outcomes
  hold (§126 names deliverables rather than an outcome):
  - the player can explore a seeded island;
  - its settlements visibly live and change;
  - the player can gain and spend belief;
  - the Creature behaves independently;
  - religious competition exists;
  - the rival can cooperate or attack;
  - the world produces environmental pressure;
  - disasters produce emergent consequences;
  - basic politics emerges;
  - populations can abandon gods;
  - rival Creatures befriend, fight and remember each other.
- The world is one 2 km island with eight settlements. Each settlement is simulated
  statistically (Tier 1) and, near the camera, shown by up to 300 representative citizens
  (Tier 2).
- The player is a god, Ilyra:
  - Followers worship and raise Divine Power, and the god spends it on five data-driven
    miracles, cast with a click where its influence reaches.
  - Faith, Love, Fear and Doubt change with what the god does and with how the people fare;
    people convert or fall away.
  - A HUD, alerts and map overlays show all of it.
- The god has a Creature, Bramble the Mossback, which lives on its own. It forages, rests, plays,
  helps build, feeds the hungry, steals or attacks, as its needs, personality and learned
  preferences decide. The player teaches it by reward and punishment with a (visual) Divine Hand.
- Three AI gods, Vael the Tyrant, Seren the Guardian and Kaio the Trickster, compete for the same
  people with a strategic utility AI that plays by the player's rules. Each has its own Creature.
  People convert between gods. The first god, or alliance of gods, to hold 60% of all followers
  for ten minutes wins (§115, §53).
- Gods keep a view of each other (trust, fear, respect, rivalry, gratitude, suspicion) and
  remember each other's acts. They can ally, go to war, make peace and betray, through the same
  kind of checked commands as miracles; each AI god weighs these moves by its personality and
  explains them. Politics emerges among all four:
  - a leader draws wars and coalitions;
  - lesser gods make peace to face it;
  - gods remember wrongs done to their allies;
  - allies win together, unless one betrays the other near the end.
- Weather cells drift in from the sea, rain and thin away; dry and wet spells come and go; the
  soil's water sets harvests, so droughts starve villages and worry their people, and storms ruin
  stores. A Rain miracle answers drought, for the player and the rival alike.
- Dry land burns: wildfires start from storms, hearths, earthquakes and gods, spread with the wind
  and scorch the land. Earthquakes shake villages into ruins. Villages fight fires and rebuild,
  the homeless fall ill, and survivors fear and doubt their gods. Fire, Shield and Earthquake
  complete nine miracles.
- People migrate. Famine, ruins, danger, war, and fear or doubt of their god build a village's
  wish to leave. Groups walk to better places, giving up gods they fear or doubt. Where they
  arrive, the host god welcomes them, makes them convert or turns them away, and their own god
  remembers it.
- The gods' Creatures meet, play, run and fight. Each remembers the others with friendship, fear,
  rivalry, respect and trust. Allies' Creatures become friends, friends refuse to fight when their
  gods go to war, and a beaten Creature is banished with a scar, shaken loyalty and its people's
  doubt.
- Not built yet: history, audio, multiplayer, and picking things up with the hand.
- 724 xUnit tests pass headless (683 at the end of Milestone 10). The solution builds with no
  warnings.
- Milestone 3 was built and played on macOS 26 (Apple M3 Pro, Metal 4), at about 120 FPS at
  1600 × 900 with eight grown settlements and 300 citizens shown. Milestones 1 and 2 were built
  on a Linux virtual machine without a GPU. Windows has not been run.

## Milestones (§116–130)

| # | Milestone | Status |
| --- | --- | --- |
| 1 | Foundation (§116) | Implemented ([#1](https://github.com/roeyazroel/pantheon/pull/1)) |
| 2 | Living Settlement (§117) | Implemented ([#2](https://github.com/roeyazroel/pantheon/pull/2), merged into `main` at the start of Milestone 3) |
| 3 | First God (§118) | Implemented, on `main` |
| 4 | Creature (§119) | Implemented, on `main` |
| 5 | Rival God (§120) | Implemented, on `main` |
| 6 | Diplomacy (§121) | Implemented, on `main` |
| 7 | Weather (§122) | Implemented, on `main` |
| 8 | Disaster Simulation (§123) | Implemented, on `main` |
| 9 | Four Gods (§124) | Implemented, on `main` |
| 10 | Population Movement (§125) | Implemented, on `main` |
| 11 | Creature Relationships (§126) | Implemented, on `main` |
| 12 | Multiplayer Foundation (§127) | Not started; next. Decided in ADR-003. Miracles, Creature commands, diplomatic moves and refugee policies are already requests that an authority checks (§73). |
| 13 | Multiplayer Simulation (§128) | Not started |
| 14 | Complete Vertical Slice (§129) | Not started |
| 15 | Polish and Expansion (§130) | Not started |

ADR-004 and ADR-008 cite an earlier working roadmap with different milestone numbers;
[GAME_SPEC.md](GAME_SPEC.md#earlier-working-roadmap-superseded) maps them. Every other document
uses the specification's numbers.

### Milestone 1: Foundation (§116)

| §116 deliverable | Status | What exists |
| --- | --- | --- |
| Godot project | Implemented | `game/`: Godot 4.7.2-stable .NET, pinned by ADR-001, with the Forward+ renderer. |
| C# solution | Implemented | `Pantheon.sln` with Domain, Simulation, Persistence, Game, Networking (reserved, empty) and Tests, plus the Godot client project `game/Pantheon.csproj` (ADR-002). Warnings are errors. |
| Camera | Implemented | A free god camera with Close, Regional and Strategic zoom bands (§56). |
| Terrain | Implemented | A seeded island rendered with Terrain3D 1.0.2 (ADR-005), patched at runtime (ADR-007), with a fallback mesh renderer. |
| One settlement | Implemented (eight since Milestone 3) | Founded on fertile land near the island centre. |
| Saveable WorldState | Implemented | `WorldState` is the only canonical state (ADR-006) and is saved as versioned JSON. |
| Debug UI | Implemented | The F1 debug panel, the B region grid, the F3 region debug map and the `.` single-tick step. |

The plan, with its verification results, is
[.docs/milestone-1-foundation.md](../.docs/milestone-1-foundation.md).

### Milestone 2: Living Settlement (§117)

| §117 deliverable | Status | What exists |
| --- | --- | --- |
| Aggregate population | Implemented | A statistical (Tier 1) settlement step once per simulated second (ADR-008), with births and deaths drawn from the saved random stream. |
| Food | Implemented | Farmers farm the land within two regions with diminishing returns; everyone eats; the stores are capped by housing. |
| Housing | Implemented | Crowded settlements with food in reserve build one house at a time. The player never places houses. |
| Health | Implemented | Falls with hunger, recovers when everyone is fed, and changes births and deaths. |
| Visible representative citizens | Implemented | Within 450 m of the camera, up to 300 figures per settlement follow the labour split and a daily round (ADR-008). |

The plan is [.docs/milestone-2-living-settlement.md](../.docs/milestone-2-living-settlement.md).

### Milestone 3: First God (§118)

| §118 deliverable | Status | What exists |
| --- | --- | --- |
| Divine Power | Implemented | Followers spend part of their working time worshipping. Each belief step, a settlement raises worshippers × worship intensity × the shrine × a rate for each god (§9). Worship intensity rises with Love, and more with Fear. The HUD shows the power, its income per second and the cost of each miracle. |
| Influence | Implemented | Derived from followers, never stored (ADR-009). Each settlement with followers reaches further the more followers it has. Influence overlaps and adds up, and several gods can hold it over one place. Miracles need influence at the target and cost more where it is weak (§10). The O overlay and the F3 layer show it. |
| Belief | Implemented | Faith, Love, Fear and Doubt per god per settlement (§7, §109), plus the share of the people who follow each god. Belief drifts with worship, comfort, hardship and time. Fear converts fast and holds badly; love converts slower and holds (§8). Miracles change the belief of everyone who sees them, and help that was needed earns more love. |
| Basic miracles | Implemented | Food, Heal, Bless Crops, Lightning and Mass Healing, defined in `game/Data/miracles.json` (§107). Each cast is checked by an authority, in §73's order, before anything changes. Click-to-cast with a targeting ring and a hint. |

Playable outcome, "Player can gain and spend belief": met.

- Worship raises Divine Power from the first second.
- Food, Heal or Bless Crops on a neighbouring village converts it in about three minutes with
  the shipped numbers.
- Followers bring influence, which reaches further villages, and ranks, which unlock Mass Healing
  at 100 followers.
- A famine at home without help costs about half of the god's followers there; with help they
  rise to 95%.

The plan, with its verification results, is
[.docs/milestone-3-first-god.md](../.docs/milestone-3-first-god.md), and
[ADR-009](ADR/ADR-009-belief-influence-and-miracles.md) records the decisions.

Also built in Milestone 3:

- Eight settlements founded on the best land, at least five regions apart, with varied
  populations and names drawn from the seed (§108).
- Ranks from Minor Spirit to Ascendant by followers (§13), never lost.
- A player HUD (§57), alerts (§59) with de-duplication and urgency, map overlays for influence,
  faith and food (§58), and labels that show and colour each settlement's main god.
- Transient domain events (§91): `MiracleCast`, `FollowingChanged`, `RankAchieved` and
  `FamineStarted`.
- Save version 3 with a migration from version 2, tested against a save written by the
  Milestone 2 build.
- Debug tools: god and belief lines in F1, the blessing and influence layers in F3, F4 for Divine
  Power, and launch options for scripted captures of casting and overlays.
- The toolchain on macOS, and captures on both terrain renderers with Metal.

### Milestone 4: Creature (§119)

| §119 deliverable | Status | What exists |
| --- | --- | --- |
| Creature | Implemented | Each god's Creature (one per god, §22), saved whole in `WorldState` (ADR-010): level 1–10, experience, health, loyalty, morality, needs, personality, learned preferences and memories. Drawn as an original Mossback that grows with its level. |
| Movement | Implemented | Kinematic walking over land at its level's speed, along A* paths over the region graph that go around the sea (§85's strategic navigation). |
| Interaction | Partial | Reward, punish and send, as commands checked by an authority (§73), with a visual Divine Hand that pats, swats and points. Picking up and throwing (§55) are not built. |
| Needs | Implemented | Hunger, fatigue and boredom grow over time and are eased by foraging or eating, resting, and playing or wandering. Starvation wears health down until it is banished (§28). |
| Simple AI | Implemented | A state machine (deciding, travelling, performing, banished) and a utility choice: learned preference × drive × personality × closeness over the options its needs and the settlements it sees offer, at 5 Hz (§89). |
| Reward/punishment | Implemented | Judge the current action, or one finished within 15 s. This moves the learned preference for that situation and action (§25), the trait behind it (§26) and loyalty; a punishment stops the action. |

Playable outcome, "Creature behaves independently": met. Untouched for 15 simulated minutes,
Bramble plays, wanders, forages, rests, eats from stores and helps villages build, and its deeds
change the villages' food, houses and belief in its god. Five rewards for feeding raise its
feeding preference from §25's 0.40 to about 0.86, close to the spec's 0.85. The plan is
[.docs/milestone-4-creature.md](../.docs/milestone-4-creature.md).

### Milestone 5: Rival God (§120)

| §120 deliverable | Status | What exists |
| --- | --- | --- |
| Second god | Implemented | Vael the Tyrant (`gods.json`), an AI god founded in the settlement furthest from the player's home, with its own Creature, Gristle. |
| Utility AI | Implemented | `GodAi` (ADR-011) scores every allowed miracle on every settlement, sending its Creature, and waiting, every 3 s (§87). Personality-weighted goals: protect, defend, convert (with opportunism), bless and intimidate. It acts through the same authorities as the player, teaches its Creature, and keeps reasons for every score (§49, §106). Five personalities in `god_ai.json`. |
| Competing belief | Implemented | Several gods per settlement, a miracle plants doubt about the other gods its witnesses follow, contested influence makes the weaker god pay more (§10), and dominance and victory (§115). |
| Conversion | Implemented | People leaving a god follow the others by their pull (§32); miracles, prosperity, intimidation and the Creature's deeds drive it. |

Playable outcome, "Religious competition exists": met. With seed 1234, a passive player leads
for nine minutes, then loses the island and the game to Vael at about 19 minutes. A player who
keeps casting Food at the frontier wins at about 13 minutes, after which Vael retakes villages
the player does not defend. The plan is
[.docs/milestone-5-rival-god.md](../.docs/milestone-5-rival-god.md).

### Milestone 6: Diplomacy (§121)

| §121 deliverable | Status | What exists |
| --- | --- | --- |
| Relationships | Implemented | Each god's view of each other god (ADR-012): trust, fear, respect, rivalry, gratitude and suspicion (§37), with a fading memory of acts: help to followers in need, courting, harm, conversions, Creature attacks, betrayal, war, alliance and peace. Pairs turn Friendly or Hostile by themselves as their views warm or sour (§113). |
| Alliance | Implemented | Offered and accepted or declined through the diplomacy authority (§73). Allies plant no doubt in each other's followers, do not raise each other's costs, and their AI and Creatures spare each other's people. |
| War | Implemented | Declared and ended by requested peace, with cooldowns. At war, villages resist the enemy where their god leads, and AI gods and Creatures target the enemy more. |
| Betrayal memory | Implemented | Breaking an alliance, or declaring war on an ally, collapses the victim's trust, lowers every god's trust in the betrayer (§40) and is remembered about eight times longer than other acts (§113). |

Playable outcome, "Rival can cooperate or attack": met. With seed 1234 and a player who casts Food
at the frontier, offers alliance and accepts every proposal, over 25 minutes:

- the Tyrant declines the alliance, declares war at 3 minutes, and twice accepts peace and later
  declares war again;
- an Opportunist declines every alliance while it is ahead;
- a Guardian accepts the alliance at 3 minutes and keeps it.

The diplomacy panel (P) shows each rival's status, attitude, memories and the moves allowed, and
the F1 AI view lists the rival's diplomatic candidates with their factors. The plan is
[.docs/milestone-6-diplomacy.md](../.docs/milestone-6-diplomacy.md).

### Milestone 7: Weather (§122)

| §122 deliverable | Status | What exists |
| --- | --- | --- |
| Weather cells | Implemented | Cells in `WorldState` with §16's fields (ADR-013), stepped at 2 Hz: they take up water over the sea, rain it out over land, fill, thin and drift on the prevailing wind. Rain, wind and lightning follow humidity, the depth of the low and warmth. |
| Moving storms | Implemented | Deep lows are windstorms or thunderstorms that cross the island; their winds ruin stores and building work where they pass, and alerts follow them. |
| Rain | Implemented | Rain soaks each region's soil; the Rain miracle (§110) calls a cloud over its target. |
| Drought | Implemented | Soil dries with warmth and faster in dry spells; dry farmland yields less and its people worry (hardship), and alerts, labels and the HUD name villages in drought. |

Playable outcome, "World produces environmental pressure": met. With seeds 1234, 42 and 7 and a
passive player, 40 simulated minutes bring 11–13 droughts and 4–7 storm strikes on villages. A
dry spell takes several villages' farmland to about 0.2 soil, where harvests fall short and
stores drain, and the rival god calls rain over its own parched villages. The plan is
[.docs/milestone-7-weather.md](../.docs/milestone-7-weather.md).

### Milestone 8: Disaster Simulation (§123)

| §123 deliverable | Status | What exists |
| --- | --- | --- |
| Earthquake | Implemented | Earthquakes as processes (ADR-014): a few seconds of shaking that fades from the epicentre, natural ones at a seeded rate and the Earthquake miracle. |
| Wildfire | Implemented | Fire in regions: lit by dry storms, villages' hearths, earthquakes and the Fire miracle; it grows on its fuel, spreads with the wind, scorches the land and dies in the rain, under firefighting or for want of fuel. |
| Destruction | Implemented | Houses collapse or burn into ruins (with houses' quake and fire resistance, §94), people die, stores and building work are lost, and scorched farmland yields less. |
| Settlement response | Implemented | Firefighting, rebuilding ruins at half the work, the homeless falling ill, and fear and doubt of the gods; a Shield miracle protects. |

Playable outcome, "Disasters produce emergent consequences": met. With seed 1234 and a passive
player, a dry spell's hearth fire grew to 33 burning regions, scorching 86 and burning 10 houses;
the Tyrant shook the player's home twice and shielded its own villages; ruins were rebuilt within
about five minutes. The plan is [.docs/milestone-8-disasters.md](../.docs/milestone-8-disasters.md).

### Milestone 9: Four Gods (§124)

| §124 deliverable | Status | What exists |
| --- | --- | --- |
| 1 player, 3 AI | Implemented | Ilyra (the player), Vael the Tyrant, Seren the Guardian and Kaio the Trickster (`gods.json`), each with a home far from the others and a Creature (§108). |
| Diplomacy between all gods | Implemented | Every pair of gods has relations, status, proposals and memory (ADR-012). Allies remember wrongs done to each other (`WrongedAlly`). The AI weighs third gods: coalitions against a leader, peace between lesser gods, war on the leader, the goodwill of a fresh peace, and betrayal near a joint victory (ADR-015). |
| Multiple strategic personalities | Implemented | All eight of §47: Guardian, Tyrant, Opportunist, Zealot, Nature Keeper, and now Trickster, Isolationist and Warlord, as data in `god_ai.json`. |

Playable outcome, "Basic politics emerges": met. In nine headless runs of 40 simulated minutes
(seeds 1234, 42 and 7; a passive player, a player who casts Food, and the personalities moved to
other gods):

- wars were declared 4–8 times and peace made 1–6 times per run;
- alliances formed in six runs;
- three alliances won together (Vael and Seren once, Seren and Kaio twice) and one god won
  alone;
- five contests were still open.

At first the Guardian won every run and the Tyrant made war and peace every few minutes. The
balance-of-power and goodwill terms and a lower Guardian `Convert` weight changed that. The
diplomacy panel shows each god's alliances and wars, alerts announce alliances between other
gods, and the HUD names a bloc and its time to victory. The plan is
[.docs/milestone-9-four-gods.md](../.docs/milestone-9-four-gods.md).

### Milestone 10: Population Movement (§125)

| §125 deliverable | Status | What exists |
| --- | --- | --- |
| Migration | Implemented | Famine, homelessness, danger, war, fear and doubt build a settlement's wish to leave (ADR-016); love keeps people home. Groups (§82) set out with their share of the stores and walk the region graph to the most appealing settlement they can reach, eating as they go and dying without food. |
| Refugees | Implemented | The god followed most where migrants arrive welcomes them, demands they convert or turns them away (§31), by its refugee policy: the player's from the diplomacy panel, the AI's from its personality, refusing while its own people lack room. The migrants' god remembers it as `Sheltered`, `Converted` or `TurnedAway`. |
| Conversion through migration | Implemented | Migrants give up gods they fear or doubt on leaving, are converted by gods that demand it, and mix into their new home's beliefs, whose gods convert them over time (§32). |

Playable outcome, "Populations can abandon gods": met. In 60-minute headless runs with a passive
player (seeds 1234, 42, 7):

- 22–57 groups of 8–34 people moved 480–1,030 people, mostly away from war, then famine, danger,
  fear and doubt;
- most groups that gave up a god gave up Vael, the feared Tyrant;
- Vael's demand for conversion converted 29 newcomers on two seeds;
- with an Isolationist, migrants were turned away and walked on.

Victories came at 27.5 and 32 minutes, as in Milestone 9. Walkers in their god's colour, a
Migration overlay, label marks, alerts, F1 lines, M and `--migrate` show and drive it. The plan is
[.docs/milestone-10-population-movement.md](../.docs/milestone-10-population-movement.md).

### Milestone 11: Creature Relationships (§126)

| §126 deliverable | Status | What exists |
| --- | --- | --- |
| Rival Creatures | Implemented | The four gods' Creatures meet when they come within 40 m and answer by learned preference, urge and personality: fight, play, run or walk on (ADR-017). Rivals seek each other, and an AI god at war sends its Creature against an enemy Creature threatening its people. |
| Friendship | Implemented | Play and greetings build friendship and trust. Alliance draws Creatures together. Friends whose gods go to war will not fight, and the player is told. A friend that attacks is remembered as a betrayal. |
| Rivalry | Implemented | War, fights and attacks on a god's people build rivalry. Winning halves it, and it fades. |
| Combat | Implemented | Blows traded by strength and combat skill, on a stream derived from the seed and the tick. The fight ends in banishment (§28: absence, a scar, lost loyalty and progress, witnesses' doubt, the god's grudge), mercy, a draw or a broken-off fight. Gods can reward or punish a fight like any deed. |
| Memories | Implemented | Each Creature keeps its own view of every other (friendship, fear, rivalry, respect, trust) and its latest encounters: met, greeted, played, won, lost, spared, was spared, drew, fled, chased off, betrayed, saw it attack our people. |

In 60-minute headless runs with a passive player (seeds 1234, 42, 7), the four Creatures:

- met 25–35 times;
- fought 8–9 times, nearly all between gods at war;
- befriended each other up to 0.6, including allies' Creatures at 0.48.

Vael's Creature, the Tyrant's, fought and lost most: 6–7 scars an hour. Over 40 seeds the contest
kept its pace: 24 games won within an hour against Milestone 10's 22, median 37.6 minutes against
35.2.

Poses, scars, labels, the card's bonds, the panel's line, alerts, F1 lines, K and `--meet` show
and drive it. The plan is
[.docs/milestone-11-creature-relationships.md](../.docs/milestone-11-creature-relationships.md).

## Coverage by specification section

Sections that only state intent are summarised at the end of this part.

### World and generation

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 5 | The world | Partial | One continuous world divided into regions. Regions have a biome, elevation, temperature, moisture, fertility, vegetation, soil water and a divine blessing, and religious influence and weather are derived for any point. Missing: wildlife, rivers and lakes, resources, ecological health and disaster risk. Grassland, forest and mountain (as Highland) exist, plus the sea. |
| 6 | World scale | Partial | A 2 km island against 10–20 km. 8 settlements (the MVP number) and one god against 4. A few thousand people after half an hour against 10,000–100,000+. Hundreds of visible citizens, and no heavyweight object per citizen. |
| 84 | World streaming | Not started | The whole island is loaded. |
| 93 | Procedural generation | Partial | Elevation, moisture, temperature, fertility, vegetation, biome and settlement sites come from the seed (tested). Missing: trees, resources, river candidates and sacred sites. |

### Settlements and population

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 29 | Settlements | Partial | Housing, job allocation (farm, build, worship) and construction are autonomous. Missing: roads, gathering, expansion and priorities set by a god. |
| 30, 90 | Settlement state | Partial | Population, food, housing, health, construction, and belief, love, fear and doubt per god. Missing: water, safety, happiness, education, wealth, political stability and resentment. |
| 31 | Population migration | Implemented | People leave for famine, homelessness, danger (disaster), war, fear and low religious confidence, and go where life is better (opportunity); persecution and poverty count through fear and hunger. Neighbouring gods accept them, reject them or demand conversion (ADR-016). Negotiating their return is not built. |
| 33–36 | Culture, schisms, atheism, technology | Not started | |
| 32 | Religious conversion | Implemented | Gradual conversion between gods and from people who follow none, driven by miracles, prosperity, intimidation, rival doubt, the Creatures' deeds and migration (migrants give up feared or doubted gods, are converted on demand and mix into their new home's beliefs). Several religions coexist. Missionaries, propaganda, temples and military victory are not modelled. |
| 80 | Simulation tiering | Partial | Tier 1 simulates the economy, demographics, health and belief. Tier 2 shows walking, working and gathering figures (presentation only). Tier 3 is not started. |
| 81 | Population representation | Implemented | Up to 300 figures stand for each settlement's population. |
| 82 | Migration representation | Implemented | `MigrationGroup` with population, origin, destination, route, health and food; near the camera a few walkers stand for the group. |
| 83 | Rendering large crowds | Implemented | One MultiMesh per settlement for citizens, and one per house part and for fields. |
| 112 | MVP settlements | Implemented | Settlements farm, build housing, grow, worship, respond to miracles, fight fires, rebuild, and send people away from hardship, war and danger. |

### Gods, belief and miracles

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 7–8 | Belief, love versus fear | Partial | Faith, Love, Fear and Doubt with their trade-offs. Dependency and Resentment are left out, as §109 allows. |
| 9 | Divine Power | Implemented | Followers × Faith × worship intensity × religious infrastructure (the shrine) × a rate, with Love and Fear raising the intensity differently. |
| 10 | Divine influence | Partial | From followers and the Creature's presence; overlapping; weaker influence costs more, except where the stronger god is an ally. Missing sources: shrines and temples as their own sources, holy sites and prophets. |
| 11 | God attributes | Partial | Id, Name, Rank, Divine Power, followers (derived), controller, personality, Creature, and relationships (in `WorldState.Diplomacy`). Missing: domains, reputation as its own value, known gods, holy sites, historical actions and traits. |
| 12 | God domains | Partial (data only) | Every miracle has a domain; gods do not specialise yet. |
| 13 | Divine rank | Implemented (followers only) | Six ranks by followers, never lost, unlocking miracles. Achievements, holy places and the Creature do not count yet. |
| 14 | Miracles | Partial | Minor: Food, Heal, Bless Crops and Lightning. Regional: Mass Healing. No Great miracles or Divine Acts yet. |
| 15 | Systemic consequences | Partial | Bless Crops raises the yield of the land; Food and Heal change hunger and health, which drive doubt and love; Lightning kills and frightens; Rain calls a cloud that waters the land as it drifts on; Fire spreads across dry land and scorches it; Earthquake starts fires and ruins. Ecology beyond scorch is not modelled. |
| 47 | AI gods | Partial | All eight of §47's personalities, with risk, aggression, cooperation, treachery and grudge as profile weights, and diplomatic memory. Long-term goals and preferred domains are not modelled. |
| 48 | God AI model | Implemented | Utility AI that continuously scores candidate actions, miracles and diplomatic moves alike, with explainable factors. |
| 49 | AI betrayal | Implemented | An AI god breaks an alliance when it is far ahead, its ally dominates alone, its view has soured, or, near a joint victory, it outweighs its ally, weighted by its `Treachery` and held back by the goodwill of the alliance. The clues are visible beforehand: its attitude and grievances in the diplomacy panel and its weighed moves in the F1 AI view. |
| 87 | God AI architecture | Partial | Strategic and diplomatic evaluation every 3 s. No major planning yet. |
| 109 | MVP belief | Implemented | Faith, Love, Fear and Doubt. |
| 110 | MVP miracles | Partial | Heal, Food, Lightning, Bless Crops, Rain, Fire, Earthquake and Shield. Missing: Water, which needs water bodies. |
| 115 | MVP victory | Implemented | 60% of all followers for 10 minutes, both configurable in `divine_rules.json`; the world goes on afterwards. |

### The Creature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 22 | The Creature | Implemented (one per god) | Companion, physical avatar and champion: its deeds act for its god, and it fights other gods' Creatures (ADR-017). Missionary work and political roles come later. |
| 23 | Progression | Partial | Level, experience, health, strength and speed (from level), combat skill, loyalty, morality and relationships. Intelligence, resilience and divine affinity are not modelled. |
| 24 | Specialisations | Not started | |
| 25 | Learning | Implemented | Preferences per situation and action, from §25's starting values, moved by reward and punishment. |
| 26 | Personality | Partial | Kindness, aggression, laziness and curiosity, born from the seed and shaped by feedback, by experience (victories embolden) and by trauma (defeats cow). The other §26 traits are not modelled. |
| 27 | Relationships | Implemented | Friendship, fear, rivalry, respect and trust, one view per Creature met, with remembered encounters (ADR-017). Allied Creatures become friends; friends whose gods fight refuse to fight. |
| 28 | Defeat | Implemented | Banishment (temporary absence) on starvation or defeat. A defeat also brings a scar, lost loyalty, lost progress towards the next level and reputation: witnesses' belief and the god's grudge. Permanent death is not modelled. |
| 86 | Creature AI architecture | Partial | Our own state machine, utility values, learned weights, memory and relationship memory in the core (ADR-010, ADR-017). No behaviour tree yet, and LimboAI is not used. |
| 111 | MVP Creature | Implemented | Movement, health, needs, combat, XP, levels 1–10, basic personality, helping and attacking villagers, fighting Creatures, reward, punishment and learned preferences. |

### Nature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 16 | Weather | Partial | Cells with §16's fields that move across regions at 2 Hz; clear, rain, thunderstorm and windstorm. Snow, heat and cold waves, fog and hurricanes are not modelled. |
| 17 | Seasons | Not started (optional) | Dry, wet and normal spells of seeded length stand in for seasons. |
| 18 | Ecology | Partial | Regions carry fixed temperature, climate moisture, fertility and vegetation, soil water that the weather changes, scorch that fires leave and that regrows, and a blessing that miracles set and that fades. No vegetation, wildlife or pollution processes yet. |
| 19 | Natural disasters | Partial | Earthquakes and wildfires as processes; drought (Milestone 7) and storms. Floods, volcanoes, landslides, disease and locusts are not modelled. |
| 20 | Earthquake example | Partial | Shaking, structural checks, collapse, fire, panic (fear and doubt), religious interpretation in its simplest form, god intervention, and survivors leaving their ruins (migration). Road blockage is not modelled. |
| 21 | Disaster preparation | Not started | |
| 94 | Physics and disasters | Partial | Houses carry earthquake and fire resistance; destruction is simulated and shown as rubble. No debris physics. |
| 114 | MVP nature | Implemented | Rain, storm, drought, wildfire and earthquake. |

### Politics and history

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 37 | Divine politics | Implemented | Directional, multi-dimensional relations: a god may respect, fear and resent another while allied with it. |
| 38 | Diplomacy | Partial | Alliance, peace and war. Non-aggression, trade, shared territory, tribute and the other treaties are not built. |
| 39 | Secret agreements | Not started | |
| 40 | Betrayal | Implemented | Any alliance can be broken; it costs the victim's trust, every god's trust and future goodwill, and is long remembered. |
| 41 | Favours | Partial | Help to another god's followers in need is remembered as gratitude. Asking for and owing favours are not built. |
| 42–43, 45–46 | Divine Council, information and deception, proxy wars, mortal holy wars | Not started | |
| 44 | War between gods | Partial | Declared war makes the enemy's followers resist conversion, draws Creatures and AI miracles to the enemy, sets the gods' Creatures against each other and lasts until peace is agreed. War goals and their outcomes are not modelled. |
| 113 | MVP diplomacy | Implemented | The five statuses and the four actions, plus accepting and declining; the AI remembers betrayal. |
| 50–52 | History, sacred locations, world events | Not started | |
| 53 | Victory conditions | Partial | Divine Ascendancy (§115's MVP form) and Pantheon Victory: allied gods, and allies of allies, count their followers together and win together (ADR-015). Conquest and cultural victories and a sandbox switch are not built. |
| 54 | Alliance endgame | Partial | Near a joint victory each AI ally weighs staying loyal to share the win against breaking away to seek personal ascendancy, by its `Treachery` and its lead over its ally. Absorbing an ally's believers is not a move of its own. |
| 91 | Event architecture | Partial | Systems and the authorities raise transient domain events (among them §91's refugees created, as `MigrationStarted`); alerts and effects read them. |
| 92 | History system | Not started | |

### Player experience

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 55 | Player interaction | Partial | Miracles are cast with a click (ADR-009). A visual Divine Hand follows the cursor, presses when casting or sending, and pats or swats the Creature (ADR-010). Picking up, carrying and throwing are not built. |
| 56 | Camera | Partial | WASD, edge movement, drag, zoom, rotate, focus on each settlement in turn and on the Creature, three zoom bands. Missing: a strategic overview. |
| 57 | User interface | Partial | HUD with Divine Power, income, followers, rank, the rivals with their status, the race for victory (a bloc's when allied), the miracle bar and hint, the Creature card (with its bonds, combat skill and scars) and alerts, and the Diplomacy panel (P) with each god's alliances and wars. Missing: the other panels (World, Settlements, Religion, History, Domains, Miracles). |
| 58 | Map overlays | Partial | Influence (strongest god per region), religion (main god per settlement), faith, food, weather (soil water), danger (fires, ash, shields) and migration (pressure to leave, groups' ways) for the player; the F3 debug map adds the region statistics, blessing, influence, soil water, fire and scorch. Missing: population, ecology, disaster risk and political control. |
| 59 | Alert system | Partial | Starving followers, settlements gained or lost, ranks, Creature deeds, rival miracles on the player's followers, dominance and victory (a bloc's included), diplomacy (proposals, war, betrayal, answers, and alliances between other gods), weather (dry spells, droughts and storms among the player's people), disasters (earthquakes, fires near the player's people and the damage done), migration, and Creatures' meetings and fights. Alerts are filtered to what concerns the player, not repeated within 30 simulated seconds, and the most urgent are kept. |
| 60 | Audio | Not started | |
| 61 | Visual direction | Partial | Stylised terrain, sea, huts, fields, figures, glowing miracle effects, soft clouds, rain, lightning and storm-dimmed light, flames, smoke, ash, rubble and shield domes, and god colours on labels and overlays. No original art assets, and religion does not yet change how settlements look. |

### Platforms, technology and performance

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 62 | Supported platforms | Partial | Built, tested and played on macOS (Apple Silicon, Metal) and Linux x64. Not run on Windows. No export presets. |
| 63 | Core technology stack | Partial | In use: Godot .NET 4.7.2, C#, Terrain3D, FastNoiseLite and MultiMesh. Jolt is chosen as the physics engine but nothing physical exists yet. Navigation is a region graph in the core rather than Godot's navigation meshes (ADR-010). LimboAI is not used. Utility AI drives the Creature and the gods. No Blender assets. |
| 64 | Terrain destruction | Not started | Earthquakes damage settlements, not the terrain. |
| 88 | Threading model | Followed | Everything runs on the main thread. |
| 85 | Navigation | Partial | Strategic: A* over land regions, smoothed (the Creature and migration groups). Local navigation meshes do not exist; citizens walk in straight lines. |
| 89 | Update frequencies | Partial | Weather steps at 2 Hz; disasters, settlements, belief and diplomacy at 1 Hz; the god AI evaluates every 3 s; the Creature decides at 5 Hz and moves every tick. Citizen poses are evaluated every frame from simulation time. |
| 104 | Performance testing | Not started | No stress scenarios. |
| 105 | Target performance | Partly measured | About 120 FPS at 1600 × 900 on an Apple M3 Pro with 8 settlements and 300 citizens shown. Not measured at 1080p on the other platforms; no graphics options. |

### Architecture, saves and process

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 65 | Local-first architecture | Implemented | Fully offline. |
| 66 | Single-player architecture | Implemented | The local host (`GameSession`) runs the world with one player and three AI gods. |
| 67–69, 71–72, 95–98 | Multiplayer, ENet, Steam, host disconnect and migration, replication, prediction, cheating | Not started | ADR-003 decides host-authoritative ENet without lockstep. |
| 73 | Network authority | Partial (local) | Miracle, Creature and diplomatic commands and refugee policies are intents. `MiracleCaster`, `CreatureAuthority`, `DiplomacyAuthority` and `MigrationAuthority` check them (power, location, influence, cooldown, rank, authorisation, reachability, diplomatic status and proposals) before anything changes. AI gods use the same path. There is no network yet. |
| 70 | No required server infrastructure | Implemented | |
| 75 | Save architecture | Partial | One quicksave holds the seed, generation settings, tick, random state, regions (with soil water, fire and scorch), settlements (with ruins, shields and the wish to leave), gods (with refugee policies), beliefs, Creatures (with their views of each other), diplomacy, weather, disasters and migration groups. No GameTime; the other listed state does not exist yet. |
| 76, 99 | Save principle, persistence format | Implemented | `WorldState` is serialized as human-readable JSON with `SaveVersion` 10 and migrations from versions 1 to 9, each tested against a save written by the previous build. |
| 77–78 | Core rule, layered architecture | Implemented | The core projects never reference Godot (tested). |
| 79 | Simulation systems | Partial | Weather, disaster, settlement, population, belief, victory, diplomacy, migration, Creature and god AI systems, and the miracle, Creature, diplomacy and migration authorities. |
| 90 | Core data models | Partial | `GodState` (subset), `SettlementState` with belief, love, fear and doubt per god, `CreatureState` (with combat skill, scars and relationships), `RegionState` (soil water, fire and scorch; no wildlife or pollution), `WeatherState` with §90's weather cells, `DisasterState` with earthquakes, `MigrationState` with §82's groups, and `GodRelation` and `GodStance` for §90's DiplomacyRelation (no treaties). No WorldEvent records with duration. |
| 100 | Repository structure | Implemented, with differences | As recommended, plus `World/` and `Miracles/` in `Pantheon.Domain`, `game/Data/`, `game/addons/`, `.docs/` and `AGENTS.md`. |
| 101 | Architecture decision records | Implemented | ADR-001 to ADR-016. |
| 102–103 | Testing, deterministic testing | Implemented | Every rule has tests, including §102's "Fear decreases after prolonged safety" and "God cannot cast miracle without enough power". Randomness is seeded and checked. |
| 106 | Debug tools | Partial | Influence, belief, population, region boundaries, simulation tier, the frame rate, the Creature view (current behaviour, personality, needs, learned weights, memories and its options with their scores), and the god AI view (current goal, top candidate actions, utility scores and their reasons, and relationship reasoning: the diplomatic moves weighed with their factors), weather cells (spell, wind, the strongest cells, soil and rain under the cursor, a settlement's water), disasters (earthquakes, the fiercest fires, a settlement's dangers), migration routes (groups on the road, refugee policies with the AI's reasons, a settlement's pressure to leave, the Migration overlay), and Creatures' relationships (views, encounters, combat skill, scars, power and how the player's Creature would meet the nearest one; K and `--meet` bring two together). Missing: navmesh, network authority and other performance metrics. |
| 107 | Content data | Partial | World generation, settlement and divine balance, miracles, the gods of a new world, Creature rules, AI personalities, diplomacy rules, weather and disaster rules and presentation are data files, parsed strictly. No domain trees yet. |
| 108 | MVP world | Implemented | One map, three land biomes, 8 settlements, four gods (1 human, 3 AI) and four Creatures. |
| 131 | Non-goals | Respected | |
| 132 | Validation priorities | Partly testable | "Is converting populations fun?", "Is raising the Creature fun?", "Are rival gods interesting?" and "Does diplomacy generate stories?" and "Do disasters create meaningful decisions?" can be tried now: rain or shield, rebuild trust after a quake, shake a rival, side with one rival against another, take in or turn away a rival's refugees. |
| 134 | Coding agent rules | Followed | `AGENTS.md`; `ArchitectureRulesTests` checks the mechanical ones. |

### Sections that state intent

- §1–4 (vision, originality, pillars, core loop): names, art and interface are original. The core
  loop runs from "Observe world" through "Expand influence", with the Creature, rival gods
  (cooperating or competing), the weather and disasters in it. Mortals have agency (§3.3): they
  worship, doubt, convert, ignore gods and migrate. World events and history are still to come.
- §133, §135 and §136: the split between simulation and presentation holds for gods, belief,
  miracles, the Creature, diplomacy, weather, disasters and migration, with `GameSession` as the
  local authority. Mortals remember what you did (belief remembers help and harm, and migrants
  leave gods they fear or doubt), the Creature remembers how it was treated, and "Gods remember
  what you did" holds through diplomatic memory, including what you did with their refugees.
  "Nature reacts" through weather and disasters. History is still to come.

## Architecture decision records

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR/ADR-001-godot-dotnet-pinned-version.md) | Godot 4.x .NET, pinned to 4.7.2-stable | Accepted |
| [ADR-002](ADR/ADR-002-csharp-primary-language.md) | C# as the primary language | Accepted |
| [ADR-003](ADR/ADR-003-host-authoritative-enet-networking.md) | Host-authoritative networking over ENet, no lockstep | Accepted; implementation in Milestones 12 and 13 |
| [ADR-004](ADR/ADR-004-statistical-population-simulation-with-tiering.md) | Statistical population simulation with tiering | Accepted; amended by ADR-008 |
| [ADR-005](ADR/ADR-005-terrain3d-for-terrain.md) | Terrain3D for terrain rendering | Accepted; amended by ADR-007 |
| [ADR-006](ADR/ADR-006-simulation-state-separate-from-scenes.md) | Simulation state is separate from scenes | Accepted |
| [ADR-007](ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md) | Patch Terrain3D's generated shader and report why the fallback is used | Accepted; amends ADR-005 |
| [ADR-008](ADR/ADR-008-settlement-step-and-representative-citizens.md) | Settlement step cadence and representative citizens | Accepted; amends ADR-004 |
| [ADR-009](ADR/ADR-009-belief-influence-and-miracles.md) | Belief, influence and miracles (Milestone 3) | Accepted |
| [ADR-010](ADR/ADR-010-creature-simulation-and-learning.md) | The Creature: core simulation, learned utility AI and the hand (Milestone 4) | Accepted |
| [ADR-011](ADR/ADR-011-rival-gods-utility-ai-and-victory.md) | Rival gods: utility AI, conversion between gods and victory (Milestone 5) | Accepted; amended by ADR-015 |
| [ADR-012](ADR/ADR-012-diplomacy-relations-status-and-memory.md) | Diplomacy: directional relations, shared status and remembered acts (Milestone 6) | Accepted |
| [ADR-013](ADR/ADR-013-weather-cells-and-soil-water.md) | Weather: moving cells, spells and the water in the soil (Milestone 7) | Accepted |
| [ADR-014](ADR/ADR-014-disasters-as-processes.md) | Disasters as processes: wildfires in regions, earthquakes that shake, ruins to rebuild (Milestone 8) | Accepted |
| [ADR-015](ADR/ADR-015-four-gods-coalitions-and-pantheon-victory.md) | Four gods: coalitions, allies' memory and pantheon victory (Milestone 9) | Accepted; amends ADR-011 |
| [ADR-016](ADR/ADR-016-migration-groups-and-refugee-policies.md) | Migration as groups on the road, and gods' refugee policies (Milestone 10) | Accepted |
| [ADR-017](ADR/ADR-017-creature-relationships-and-combat.md) | Creature relationships, meetings and combat (Milestone 11) | Accepted |

No decision has been reversed.

## Tests

The xUnit suite in `src/Pantheon.Tests` has 724 tests, and they all pass headless in about ten
seconds, without Godot (`dotnet test Pantheon.sln`).

| Area | Test classes | M2 | M3 | M4 | M5 | M6 | M7 | M8 | M9 | M10 | Now |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| World generation and determinism | `WorldGenerationTests`, `BiomeClassifierTests`, `WorldSeedTests`, `DeterminismTests`, `SimulationRandomTests`, `TerrainRaycasterTests` | 47 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 |
| Settlement rules and step (Tier 1) | `SettlementEconomyTests`, `SettlementSystemTests`, `IntervalSystemTests` | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 |
| Belief, influence, miracles and rivals | `BeliefModelTests`, `BeliefSystemTests`, `InfluenceFieldTests`, `MiracleCasterTests`, `RivalGodTests` | 0 | 45 | 45 | 55 | 55 | 55 | 55 | 55 | 55 | 55 |
| Pantheon and politics | `PantheonTests`, `PoliticsTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 18 | 18 | 18 |
| Diplomacy | `DiplomacyTests` | 0 | 0 | 0 | 0 | 14 | 14 | 14 | 14 | 14 | 14 |
| Weather and water | `WeatherModelTests`, `WeatherSystemTests`, `WaterTests` | 0 | 0 | 0 | 0 | 0 | 32 | 32 | 32 | 32 | 32 |
| Disasters | `DisasterModelTests`, `DisasterSystemTests`, `ResponseTests` | 0 | 0 | 0 | 0 | 0 | 0 | 27 | 27 | 27 | 27 |
| Migration | `MigrationModelTests`, `MigrationSystemTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 23 | 23 |
| Creature relationships | `CreatureBondsTests`, `CreatureEncounterTests`, `CreatureRivalryAiTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 28 |
| The Creature and navigation | `CreatureModelTests`, `CreatureBehaviourTests`, `CreatureAuthorityTests`, `RegionPathfinderTests` | 0 | 0 | 27 | 29 | 29 | 29 | 29 | 29 | 29 | 29 |
| Representative citizens (Tier 2) | `RepresentativeRosterTests`, `CitizenRoutineTests`, `SettlementCrowdTests`, `TierSelectorTests` | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 |
| Saves and migration | `SaveLoadTests`, `SaveMigrationTests`, `SaveMigrationV2Tests` to `V10Tests`, `DivineSaveTests` | 34 | 47 | 52 | 57 | 62 | 68 | 73 | 80 | 86 | 92 |
| Session, scheduler, commands, launch options | `GameSessionTests`, `SimulationSchedulerTests`, `SessionCommandsTests`, `LaunchOptionsTests`, `DivineSessionTests`, `CreatureSessionTests` | 38 | 51 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 |
| Camera | `GodCameraModelTests`, `EdgeScrollTests` | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 |
| Presentation, HUD, alerts and layout | `PresentationTests`, `SettlementLayoutTests`, `Terrain3DShaderPatchTests`, `DivinePresentationTests`, `RivalPresentationTests`, `DiplomacyPresentationTests`, `WeatherPresentationTests`, `DisasterPresentationTests`, `PantheonPresentationTests`, `MigrationPresentationTests`, `CreatureRelationsPresentationTests` | 30 | 45 | 45 | 51 | 57 | 74 | 83 | 89 | 97 | 104 |
| Debug readouts and region map | `DebugReadoutTests`, `RegionDebugMapTests` | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 |
| Content parsing | `ContentTests` | 11 | 18 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 |
| Architecture rules | `ArchitectureRulesTests` | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 |
| **Total** | | **331** | **426** | **471** | **494** | **519** | **574** | **615** | **646** | **683** | **724** |

Not covered by automated tests: the Godot client (`game/Scripts`), which is checked by building
it and by scripted runs with captures, and Windows.

## How to run

You need Godot 4.7.2-stable, .NET edition, the .NET SDK 8.0 or newer, and a GPU with Vulkan
support or Metal on macOS. On macOS, `brew install --cask dotnet-sdk@8 godot-mono` installs both,
and the Godot executable is `godot-mono`.

```bash
godot --headless --path game --import    # once after cloning: registers Terrain3D
dotnet build Pantheon.sln                # warnings are errors
dotnet test Pantheon.sln                 # headless, no Godot needed
godot --path game                        # play
```

`godot` stands for the Godot 4.7.2 .NET executable. Other useful runs:

```bash
godot --path game -- --seed=42                                     # another world
godot --path game -- --advance-ticks=9000 --camera-distance=300    # grown settlements
godot --path game -- --divine-power=900 --overlay=influence        # power to spare, influence shown
godot --path game -- --terrain=fallback                            # the fallback terrain renderer
```

Main controls:

- Camera: W A S D, the arrow keys or the screen edge to pan, the right mouse button to grab and
  drag the ground, the mouse wheel to zoom, Q and E to rotate, and F to look at each settlement
  in turn.
- Miracles: keys 1–9 or the miracle bar select one; left click casts it; Esc cancels.
- Overlays and time: O cycles the map overlays. Space pauses, `[` and `]` change the speed.
- Creature: C looks at it, Z rewards and X punishes what it is doing, V sends it to the cursor.
- Diplomacy: P shows the diplomacy panel.
- Weather and disasters: O cycles to the Weather and Danger overlays; F8 starts the next spell,
  F10 calls a storm, F11 starts an earthquake and F12 a fire (debug).
- Debug: F1 shows the debug panel, F3 the region debug map and F4 grants 500 Divine Power.
- Saves: F5 and F9 quicksave and quickload, and F2 makes a new world.

The [README](../README.md) lists every control and option.

## Known gaps and deviations from the specification

### Scope and scale

- **Four gods on a small island.** The MVP's four gods (§108) crowd eight villages: a player who
  does nothing keeps only a few followers after ten minutes. There is no setup screen to choose
  the rivals; `gods.json` sets them.
- **A small world.** One 2 km island instead of 10–20 km (§6). No trees or other vegetation are
  drawn.
- **Miracles.** Eight of §110's nine kinds (Rain since Milestone 7; Fire, Earthquake and Shield
  since Milestone 8); Water waits for water bodies.

### Belief and power

- Dependency and Resentment are not modelled (§109 allows it); fear's long-term costs are only
  faster apostasy until resentment, migration and rebellion exist.
- Religious infrastructure is only the shrine; there are no temples or holy sites.
- Divine Power has no upkeep or cap, and a god with a large following earns far more than five
  minor miracles can spend. Rival gods and more expensive miracles are the intended sinks.
- Settlements' stores grow large in good times, so food miracles mostly earn faith rather than
  love until droughts, storms or disasters bring hardship.
- The contest: with three AI gods, a player who only casts Food at the frontier holds 5–13% of the
  world after 36 minutes (seeds 1234, 42 and 7). A human has more tools (Lightning, Mass Healing,
  Rain, Shield, the Creature and alliances), but no human-like bot has measured them yet.
- The Guardian is the strongest AI personality and leads from any seat. Kindness converts better
  than intimidation under the belief rules (ADR-009). Its `Convert` weight was lowered so that it
  no longer wins every game (ADR-015), but love against fear deserves a broader look.

### The population tiers

- Tier 2 citizens are presentation only (ADR-008). Worshippers are shown as villagers, and
  citizens walk in straight lines (no navigation, §85).

### Saves

- One quicksave slot and no GameTime.
- Events are not saved, so an alert raised just before a save does not repeat after a load.

### Platforms, performance and tooling

- Not run on Windows. No export presets, so no packaged builds; an export must include
  `Data/*.json`.
- macOS may quarantine the unsigned Terrain3D libraries.
- No stress scenarios (§104) or graphics options (§105).
- The Terrain3D shader patch (ADR-007) is still needed, and every start logs a harmless
  deprecation warning.

### The Creature

- Creatures fight one on one: no group fights, no defending a settlement as such (rivalry from
  attacks stands in for it), no escorting migrants, no permanent death, and no command to attack a
  chosen Creature; the player sends it and teaches it.
- The player's untaught Creature, never sent away by a god, levels fastest and wins most of its
  fights. The Tyrant's Creature, nearly always at war, collects 6–7 scars an hour.
- The hand is visual: it cannot pick up, carry or throw anything. Jolt is chosen but no physics
  gameplay exists.
- The Creature walks through houses and people: there is no local avoidance, and paths follow
  64 m regions.
- Four personality traits and no specialisations (§24, §26).

### Weather

- Clear, rain, thunderstorm and windstorm only; no snow, heat or cold waves, fog or hurricanes,
  and spells instead of seasons (§16–17).
- Weather touches harvests, stores and building work, and dry storms start fires; nothing floods.
- Clouds fade out near the camera, so up close the weather shows as rain, dim light and
  lightning.

### Disasters

- Earthquakes and wildfires only; no floods, volcanoes, disease or locusts, and no disaster
  preparation (§19, §21). Survivors leave their ruins (Milestone 10), but roads are not blocked.
- Fire spread is sensitive to dryness: the shipped numbers sit between runs where nothing spread
  and runs where the island burned (see the plan).
- Smoke is faint and fires read mostly as flames and glow; visuals wait for Milestone 15.

### Migration

- Groups never found new settlements, cross water, meet danger on the road or avoid fires on
  their route, and nobody negotiates refugees' return or migration agreements (§31, §38).
- Migration slows a runaway leader by emptying war-torn and doubting villages. The shipped rate
  keeps victories near Milestone 9's (27.5 and 32 minutes); faster migration delayed them to
  44–52 minutes.
- Walkers stand for their group; they do not interact with citizens or Creatures.

### Diplomacy

- Statuses only: no treaties beyond alliance and peace, no secret agreements, favours, tribute or
  council (§38–42).
- A god forms no view of another until the first act between them; the panel says so.
- Alliances are pairwise. A bloc of allies of allies wins together, but there are no shared
  treaties, shared enemies or council votes (§38, §42), and absorbing an ally's believers is not a
  move of its own (§54).

### Interface

- One panel (Diplomacy); no audio, menu or settings screen.

### Documents written before the specification

- [GAME_SPEC.md](GAME_SPEC.md) differs from the specification in places, and the specification
  wins: its roadmap numbers, a cap on Divine Power by total Faith (§9 has none, and none is
  implemented), contested influence weakening every god (§10 makes weaker gods pay more, as
  implemented), its own example miracles, and a campaign milestone that §131 rules out.

## Recommended next steps

### Milestone 12: Multiplayer Foundation (§127)

Deliverables: an ENet host, joining, player assignment, action authority and synchronized god
state, for two players first. ADR-003 decided a host-authoritative model over ENet, without
lockstep (§67–74).

1. Message contracts in the core (`Pantheon.Networking`, no Godot): joining, the god assigned, the
   player's intents (miracles, Creature commands, diplomatic moves, refugee policy), their
   verdicts, and state snapshots.
2. A transport behind an interface: Godot's ENet in the client, and an in-memory loopback for
   headless tests.
3. The host runs the only simulation. It assigns a joining player a god the AI was playing, and
   checks every intent with the existing authorities, issued as that player's god (§73).
4. A client receives the world once, then the god state (power, rank, followers, cooldowns,
   diplomacy) and the changes it needs at the rates of §96. It shows them with the same
   presenters, and predicts nothing that matters (§97).
5. `--host` and `--join=<address>`, a status line for the connection, and debug lines for peers,
   latency and messages.
6. Tests over the loopback: assignment, refused and accepted intents, the same god state on both
   sides, a client leaving and joining again.

### Alongside

- Run on Windows and measure at 1080p; add export presets.
- Report the Terrain3D shader defects upstream.
