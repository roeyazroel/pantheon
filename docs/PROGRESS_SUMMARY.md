# PANTHEON — Progress Summary

This document sums up the work, Milestones 1 to 15 and the parts that complete 15, against the owner's complete
specification, [PANTHEON_SPEC.md](PANTHEON_SPEC.md), which is the source of truth. Section numbers
(§) refer to it. It describes `main` on 2026-10-20.

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

- All fifteen of the specification's milestones are implemented: 1 Foundation (§116),
  2 Living Settlement (§117), 3 First God (§118), 4 Creature (§119), 5 Rival God (§120),
  6 Diplomacy (§121), 7 Weather (§122), 8 Disaster Simulation (§123), 9 Four Gods (§124),
  10 Population Movement (§125), 11 Creature Relationships (§126), 12 Multiplayer Foundation
  (§127), 13 Multiplayer Simulation (§128), 14 Complete Vertical Slice (§129) and 15 Polish and
  Expansion (§130). Their playable outcomes hold (§126–130 name deliverables rather than an
  outcome):
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
  - rival Creatures befriend, fight and remember each other;
  - four players share a world over ENet;
  - the vertical slice plays from start screen to victory or defeat;
  - gods specialize in domains, contest holy sites and suffer schisms, with sound and settings.
- The world is an island of four sizes (part 7): Small (2 km, eight settlements, four gods),
  Large (4 km, twenty, six), Huge (8 km, forty, eight) or Vast (12 km, forty-eight, eight). Each settlement is simulated
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
  people with a strategic utility AI that plays by the player's rules; larger worlds add Aurel
  the Zealot, Maren the Nature Keeper, Thorn the Warlord and Ysolde the Isolationist. Each has its
  own Creature. People convert between gods. By default the first god, or alliance of gods, to
  hold 60% of all followers for ten minutes wins (§115, §53); a world may instead be won by
  conquest or culture, by any of them, or not at all (part 7).
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
- Up to four players can share a world. One player hosts; the others join over the network and
  each takes over a god the AI played. The host's world is the only real one: joined players'
  commands are intents that the host checks and answers. Each part of the world reaches players at
  its own rate, and every event at once:
  - Creatures near a player's view five times a second;
  - gods four times a second;
  - settlements once a second;
  - diplomacy when it changes.

  That is about 13 KB/s per player.
- The game plays as a game:
  - a start screen, with new world, continue, load, host and join;
  - a guide for the first minutes;
  - a pause menu, with named saves and an autosave;
  - the world's history (H), "Year 12 — The Shaking of Elar";
  - an end screen when a god, or a god and its strongest ally, wins.
- Gods choose what they are gods of (Milestone 15):
  - all thirteen of §12's domains, taken up one per rank tier up to three;
  - each with cheaper miracles, a boon that changes a rule of the world, and one of thirteen
    signature miracles.
- Rivers run from the highlands to the sea and lakes fill the hollows (Milestone 15, part 2).
  Villages drink from them and from wells that dry up in a drought. Rain and wet soil swell the
  rivers until they flood the land downstream. The Water miracle completes §110's nine.
- Every village has a society (part 3): safety, happiness, order, wealth, learning and eight
  fields of technology. Gods set its priorities, it raises temples and preparations against
  disaster, and crowded villages found new ones.
- Villages have a culture (part 4):
  - they hold §33's eight doctrines more or less firmly, and may turn from the gods: agnostic,
    atheist, then set against every god;
  - people depend on gods that help them and resent gods they fear, and may revolt;
  - temples send missionaries, and charitable villages feed their hungry neighbours;
  - gods earn traits (compassionate, cruel, vengeful and others) from what they do.
- Gods make treaties and sit in council (part 5):
  - eleven kinds of treaty for a term, public or secret, whose breaking is a betrayal;
  - favours owed for rescues, called in for power, war, votes, territory or help;
  - a Divine Council, summoned by betrayals, dominance and disasters, that condemns, protects,
    prohibits, coordinates, wars together and declares neutral ground;
  - absorbing an ally.
- Gods fight for something and in secret (part 6):
  - wars have goals (conquest, a holy site, tribute, a humbled Creature) and end in dictated
    peace;
  - gods know rivals only through allies, treaties, spies and scouts;
  - miracles may be veiled, and rebels incited and propaganda spread, at the risk of being traced;
  - militarist villages wage holy wars of their own will.
- The year turns and the land lives (part 8):
  - optional seasons: summer's harvests and fires, autumn's rain and hurricanes, winter's snow,
    hunger and cold;
  - snow, fog, hurricanes, and heat and cold waves;
  - wild animals that villages hunt, harm that heals or scars the land for good, and pollution; a
    god of Nature heals scars.
- The rest of nature's hazards strike, and villages prepare (part 9):
  - tsunamis from coastal quakes, landslides that block the way, a volcano that rumbles and
    erupts, tornadoes, falling stars, blight in the fields and locust swarms;
  - sea walls, dams, irrigation and evacuation routes;
  - cracks, slide scars and craters left on the land.
- Worlds come in four sizes and are won in more ways (part 7):
  - the start screen offers the size and the victories beside the Creature;
  - conquest brings every rival below 5% of belief, and culture leads three villages in four
    while rivals live on; a sandbox has no victory; larger pantheons may win together;
  - the AI gods cost a seventh of what they did and take turns, so a Huge world runs at about
    120 FPS; labels give way to bigger villages when seen from afar.
- Four holy sites are found in every world and kept by the god whose influence holds them;
  Creatures claim and defend them.
- Doubting followers break away in schisms to keep their god's Old Faith, and return or fade.
- Creatures come in four species.
- Every sound and the music are synthesized by the game, and the settings are kept between runs.
- Creatures walk paths, the hand lifts and throws, and gods do Divine Acts (part 10):
  - at levels 3, 6 and 9 a Creature takes up the path its deeds and rewards have called it to:
    Guardian, Warrior, Destroyer, Healer, Nature Keeper, Missionary, Trickster or Commander;
  - the hand (R) lifts villagers out of danger to safety, carries them, plants trees, throws rocks
    and holds the Creature;
  - a Greater God may hallow the land forever, wake the volcano or still every disaster at once.
- Not built: Steam networking, streaming the terrain in pieces, worlds beyond 12 km, and Divine
  Acts that reshape the terrain.
- The Book of the World (Tab) gathers the world, religion, every miracle and the Creature; the map
  overlays add population, disaster risk and political control; backtick shows the whole island;
  and the game exports for Windows, macOS and Linux (part 11).
- Prophets and elders live in the world by name, as Tier 3 individuals (part 12):
  - every village of 40 or more has an elder who keeps its faith, and turns from a god it
    resents;
  - devout villages call prophets, and a Lesser God may anoint one; prophets walk from village to
    village preaching, and one who dies in danger is a martyr;
  - temples and prophets carry their god's influence.
- The land has forests and stone, villages gather them and build roads (part 13):
  - trees stand on the island, thinning where villages cut them and growing back;
  - houses take wood, and buildings wood and stone;
  - roads join neighbouring villages, speed walkers, carry trade and food, and are cut by
    floods, fires and landslides until mended.
- The world lives through great events (part 14):
  - the plague breaks out in crowded, sick villages and spreads along roads until healed or burnt
    out;
  - the sun goes dark at seeded times;
  - doubting, learned faiths reform;
  - world wars, great droughts and great migrations are named, told and remembered;
  - the plague and droughts bring the gods' council to coordinate help.
- An ancient beast wakes in the grown world and ravages the villages until the gods bring it down
  together, and a magical anomaly makes the gods vie for its power (part 15).
- Gods know only the gods they have met, and the AI gods plan: every 30 seconds each sets itself
  goals such as protecting its home, converting a village or weakening the leader (part 16).
- Every Creature has a character: brave or cowardly, protective, jealous, playful and more, which
  drives what it does and changes with how it is treated and what befalls it (part 17).
- The island has every biome of §5, by climate and by place (part 18).
- The hand takes offerings from your villages and gives them where they are needed (part 19).
- AI gods weigh every choice of the alliance endgame, absorbing their allies included; a god's
  lightning sets dry woods alight, and hurricanes are announced as they form (part 20).
- Villages look like what they worship: gardens, monuments or columns (part 21).
- The player can follow any villager as an individual with a life of their own (part 22).
- A stress scenario of 100,000 people measures the game, and every debug overlay of §106 exists
  (part 23).
- Divine Acts raise islands, split mountains and turn rivers for good (part 24).
- Village figures find their way round huts, the shrine and the temple (part 25).
- The Creature runs on a hierarchical state machine and behaviour trees, and gives up pointless
  errands (part 26).
- Houses crack and mend, resist floods, and fall as physical debris; a thrown rock lands and rolls
  (part 27).
- Villagers are people who walk, sit, hoe, hammer and pray at a human pace (part 28).
- The world streams: forests, terrain detail and fires are loaded near the camera only (part 29).
- The ground is textured by biome, slope and height under softer, richer light (part 30).
- Villages build by their land, size and god: huts, longhouses, stone, adobe, stilts, temples
  in their god's style, palisades and stone walls with towers (part 31).
- Seven tree species by biome and grass, flowers, bushes, stones and reeds near the camera, all
  swaying in the weather's wind (part 32).
- Herds of deer and flocks of birds live where the land's wildlife does, and roads cross rivers
  on bridges (part 33).
- The Creature's face shows how it feels and its body its nature, and every miracle has a look
  of its own (part 34).
- The simulation keeps to a CPU budget, vegetation, particles and terrain quality scale, and a
  shared game sends the land only when it changes visibly (part 35).
- 1529 xUnit tests pass headless (1523 at the end of part 34, 853 at the end of Milestone 15).
  The solution builds with no warnings.
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
| 12 | Multiplayer Foundation (§127) | Implemented, on `main` |
| 13 | Multiplayer Simulation (§128) | Implemented, on `main` |
| 14 | Complete Vertical Slice (§129) | Implemented, on `main` |
| 15 | Polish and Expansion (§130) | Implemented, on `main` (domains, holy sites, schisms, species, more miracles, settings, audio; larger worlds came in part 7; not Steam) |
| 15.2 | Water and Law (§130, with §5, §12, §15, §19, §30, §110) | Implemented, on `main`: rivers, lakes, floods, drinking water, the Water miracle, and the Ocean and Justice domains |
| 15.3 | Society (§21, §29, §30, §36, with §6 and §9) | Implemented, on `main`: safety, happiness, order, wealth, learning, priorities, buildings, technology and expansion |
| 15.4 | Culture (§7, §8, §11, §32–35, with §3.2) | Implemented, on `main`: doctrines, secularism, dependency, resentment, missionaries, charity, revolts and gods' traits |
| 15.5 | Treaties and the Divine Council (§37–42, §54) | Implemented, on `main`: eleven treaties, secret agreements, betrayal of treaties, favours, requests and promises, the council and its decrees, absorbing an ally |
| 15.6 | War and intrigue (§43–46) | Implemented, on `main`: war goals and dictated peace, intelligence, veiled miracles, proxy acts, mortal holy wars |
| 15.7 | Larger worlds and victories (§6, §53, with §84) | Implemented, on `main`: Small, Large, Huge and Vast worlds with up to eight gods, conquest, cultural and sandbox victories, larger pantheons, the AI's cost cut and labels that give way |
| 15.8 | Seasons and the land's life (§16–18) | Implemented, on `main`: optional seasons, snow, fog, hurricanes, heat and cold waves, wildlife and hunting, harm and scars, pollution, and Nature's healing |
| 15.9 | Hazards (§19–21, §64) | Implemented, on `main`: tsunamis, landslides, the volcano, tornadoes, meteors, blight and locusts; sea walls, dams, irrigation and evacuation routes; marks on the land |
| 15.10 | Paths, the hand and Divine Acts (§14, §23–24, §55) | Implemented, on `main`: eight Creature paths and three attributes, the Divine Hand that lifts, carries, rescues and throws, and three Divine Acts |
| 15.11 | The Book of the World, overlays, overview and exports (§56–58, §62) | Implemented, on `main`: the World, Religion, Miracles and Creature pages, population, risk and politics overlays, the strategic overview, and Windows, macOS and Linux export presets |
| 15.12 | Prophets, elders and temples (§10, §80) | Implemented, on `main`: elders who keep and turn their villages' faith, prophets who walk and preach, Anoint a Prophet, martyrs, and temples and prophets as sources of influence |
| 15.13 | Wood, stone, forests and roads (§5, §29, §93) | Implemented, on `main`: forests and stone from the seed, gathering and stores, houses and buildings that take wood and stone, roads that speed walkers and carry trade and food, and the trees and roads drawn |
| 15.14 | Great events (§52) | Implemented, on `main`: the plague, eclipses, reformations, world wars, great droughts and great migrations, with the council answering plague and drought |
| 15.15 | The ancient beast and the magical anomaly (§52) | Implemented, on `main`: the beast that wakes, ravages, is fought by Creatures and miracles and shares its glory when it falls, and the anomaly that makes the gods vie for its power |
| 15.16 | Known gods and the gods' long-term plans (§11, §47, §87) | Implemented, on `main`: gods who must meet before they deal, and AI gods who plan every 30 seconds to protect, convert, befriend, weaken, prepare, slay the beast or win back their doubters |
| 15.17 | Creature temperament (§26) | Implemented, on `main`: courage, protectiveness, jealousy and playfulness, §26's twelve named traits, Creatures that guard their god's people, envy the strong and defend their villages from the beast by themselves |
| 15.18 | The biomes of the world (§5) | Implemented, on `main`: tundra, desert and swamp by climate, and islands, coasts, river valleys and volcanic land by place, each with its fertility, growth, resources and colour |
| 15.19 | Offerings (§55) | Implemented, on `main`: the hand takes offerings from its villages' stores and gives them to villages in need or to the Creature |
| 15.20 | The alliance endgame, lightning's fires and hurricane warnings (§15, §54, §59) | Implemented, on `main`: AI gods absorb their allies' believers when they would betray them, a god's lightning sets dry woods alight, hurricanes are announced as they form, and the progress summary is brought up to date |
| 15.21 | Religion's look on villages (§61) | Implemented, on `main`: gardens, monuments or columns by the village's god, its traits, doctrines and people's feelings |
| 15.22 | The selected villager (§80) | Implemented, on `main`: / follows someone of a village as an individual with needs, work, faith, flight, memory and death; the Book's Villager page tells their life |
| 15.23 | The stress scenario and the last debug overlays (§104, §106) | Implemented, on `main`: `--stress` makes 50 villages of 100,000 people with weather and disasters; F1 measures frames, ticks, memory and saves; the Navigation and Authority overlays |
| 15.24 | Divine Acts that reshape the land (§14) | Implemented, on `main`: Raise an Island, Split the Mountain and Turn the River, kept in the world and made into the terrain, its rivers, biomes and regions following |
| 15.25 | Local navigation (§85) | Implemented, on `main`: villages shown up close bake a navigation grid, and their figures walk round huts, the shrine and the temple; nobody works inside a house |
| 15.26 | The Creature's state machine and behaviour trees (§86) | Implemented, on `main`: a hierarchical state machine and behaviour trees with guards; Creatures give up errands whose reason is gone |
| 15.27 | Structural integrity, flood resistance and debris (§94, §55) | Implemented, on `main`: cracked houses fall more easily and are mended; houses resist floods; fallen houses and strikes throw Jolt debris; local navigation's searches are 2.5–5 times faster |
| 15.28 | People that look and move like people (§61, §80, §81) | Implemented, on `main`: procedural people of varied looks, animated on the GPU (walking, sitting, hoeing, hammering, chores, prayer) and kept to a human pace by the crowds' own clock |
| 15.29 | World streaming (§84) | Implemented, on `main`: forests planted and fallback terrain detailed near the camera only, fires drawn near it, with an F1 line; the vast island builds faster in less memory |
| 15.30 | Ground textures and light (§61) | Implemented, on `main`: nine ground textures made in code, painted by biome, slope, height and coast with soft borders; ACES tonemapping, ambient occlusion, glow and a richer sky |
| 15.31 | Village architecture (§61) | Implemented, on `main`: five house styles made in code, chosen by land and size, coloured by a strongly felt god; temples in their god's style; palisades and stone walls with towers; a building shader that paints and raises them |
| 15.32 | Trees, ground cover and wind (§61, §93) | Implemented, on `main`: seven tree species made in code and grown by biome; grass, flowers, bushes, stones and reeds streamed near the camera; everything sways in the weather's wind |
| 15.33 | Wildlife and bridges (§61) | Implemented, on `main`: herds of deer and flocks of birds near the camera, as many as each region's wildlife, animated in the crowds' time; plank bridges where roads cross water; `--wildlife` |
| 15.34 | Expressive Creatures and miracle looks (§61) | Implemented, on `main`: the Creature's face and tail show what it does, needs and feels and its god's last word, and its body its nature; eleven signature miracle looks on the GPU; looking at the Creature turns the camera to its face |
| 15.35 | The CPU budget, graphics scaling and regions within a tolerance (§105, §95–96) | Implemented, on `main`: the simulation keeps to the player's budget a frame; vegetation, particles and terrain quality settings; regions sent past a tolerance (amends ADR-019); the client reacts to a busy world at a pace |

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
| Interaction | Implemented | Reward, punish and send, as commands checked by an authority (§73), with a visual Divine Hand that pats, swats and points. Picking up, carrying and throwing (§55) came in Milestone 15, part 10 (ADR-030), and offerings in part 19 (ADR-039). |
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

### Milestone 12: Multiplayer Foundation (§127)

| §127 deliverable | Status | What exists |
| --- | --- | --- |
| ENet host | Implemented | `--host` listens on UDP 7777 (or a chosen port) through Godot's ENet behind `INetworkTransport` (ADR-018). `--upnp=on` asks the router to forward it (§68). The protocol is tagged JSON in `Pantheon.Networking`, gzipped from 1 KB. |
| Join | Implemented | `--join=<address>[:<port>]`, with `--name` and `--god`. Players with other game data (content fingerprint), another protocol, or no free god are refused. A player that cannot join plays alone. |
| Player assignment | Implemented | A joining player takes the god it asks for, or the first god the AI plays. Its controller becomes `Remote`, and the AI leaves it alone. Leaving gives it back to the AI. |
| Action authority | Implemented | Joined players' miracles, Creature commands, diplomatic moves and refugee policies are intents. The host checks them as the player's god through the same authorities as its own commands (§73) and answers with the status line. Only the host saves, loads, keeps the clock or uses debug tools. |
| Synchronized god state | Implemented | Divine Power, cooldowns, rank, followers, relations and stances arrive with the whole world every second, and every event as it is raised. Between snapshots, a player's replica predicts by running the same simulation. |

First target, two players: played by two instances on one Mac over ENet. The joined player's
miracle was checked and cast by the host, with its cost and cooldown showing on the player's HUD.
A cast outside the player's influence was refused with its reason. The host's quit ended the
session and saved its world (§71). The plan is
[.docs/milestone-12-multiplayer-foundation.md](../.docs/milestone-12-multiplayer-foundation.md).

### Milestone 13: Multiplayer Simulation (§128)

| §128 item | Status | What exists |
| --- | --- | --- |
| Miracles | Implemented | Every cast reaches players as an event the moment the host raises it, so its effect plays at once. After a player's intent the gods' and settlements' sections go out with the verdict, so cost, cooldown and effect show within one round trip (ADR-019). |
| Creatures | Implemented | Motion (position, heading, health, activity) five times a second near a player's view and once a second elsewhere (§95's interest), unreliable; whole Creatures every two seconds. |
| Settlements | Implemented | Every second, when changed (§96's "low frequency"). |
| Disasters | Implemented | Earthquakes every second, and regions' fire, scorch, soil water, blessing, vegetation and fertility as changes every two seconds; fires and quakes also arrive as events. |
| Diplomacy | Implemented | On a change of status, proposal or remembered act (§96's "event based"), with the relations' fading values refreshed every five seconds; moves arrive as events. |
| Four players | Implemented | The host and three players, one per god the AI plays. Played as a Terrain3D host with three headless players over ENet: each received about 14 KB/s with 13 ms round trips. A player who returns under the same name gets its god back. |

On the shipped world with three players, each received 12.5 KB/s, a third of what whole snapshots
cost. The host spent 1.5 ms per second of play on replication. A whole snapshot still corrects
everything every 20 seconds. The plan is
[.docs/milestone-13-multiplayer-simulation.md](../.docs/milestone-13-multiplayer-simulation.md).

### Milestone 14: Complete Vertical Slice (§129)

| §129 item | Status | What exists |
| --- | --- | --- |
| 4 gods | Implemented | Ilyra and three AI gods (Milestone 9), any of whom players can take over (Milestones 12–13). |
| 8 settlements | Implemented | The shipped island founds eight. |
| Creatures | Implemented | One per god, learning, meeting, fighting (Milestones 4 and 11). |
| Belief | Implemented | Faith, love, fear and doubt, conversion, worship, miracles (Milestones 3 and 5). |
| Politics | Implemented | Relations, statuses, coalitions, betrayal; pantheons now a god and its strongest ally (ADR-020). |
| Weather | Implemented | Milestone 7. |
| Disaster | Implemented | Milestone 8. |
| Local saves | Implemented | Named save slots, an autosave every five minutes, the quicksave and a host's save; the start screen and pause menu list them (ADR-020). |
| Multiplayer | Implemented | Hosting and joining from the start screen as well as launch options (Milestones 12–13). |
| Victory | Implemented | An end screen with Victory or Defeat, who won, how the player's god and Creature ended, and the world's history. |

Beyond §129's list, the slice adds what a first validation needs:
- **Start screen**, shown without options.
- **Guide** that teaches by doing.
- **Pause menu** (Esc).
- **The world's history** (§50, §92): saved, replicated, shown with H and on the end screen.
- **Frame-time statistics and stress probes** (§104).

Measured:
- **Performance:** 150 FPS uncapped at 1080p on an M3 Pro (§105). The simulation takes 0.076 ms a
  tick on the shipped world and 0.198 ms with 21 settlements.
- **Balance:** with a bot playing the player's god, Guardian and Tyrant play won 7 and 8 of 20
  hour-long games, and no longer any three- or four-god victories. The plan is
  [.docs/milestone-14-vertical-slice.md](../.docs/milestone-14-vertical-slice.md).

### Milestone 15: Polish and Expansion (§130)

| §130 item | Status | What exists |
| --- | --- | --- |
| Domains | Implemented (all 13 of §12) | Nature, Life, Storm, Fire, War, Death, Prosperity, Protection, Knowledge, Trickery and Chaos (ADR-021), and Ocean and Justice since part 2 (ADR-022). One domain per rank tier, up to three: Minor Spirit, Lesser God, God. Each gives cheaper miracles, a boon and a signature miracle. |
| Doctrines | Implemented (part 4) | §33's eight doctrines held by every village, drawn from what it lives through and the god it follows, each with its effect (ADR-024). The Old Faith answers §34's "behaviour contradicts history", and a god known for mercy that harms its own shocks them more. |
| Holy sites | Implemented | The Sacred Peak, the Ancient Grove, the Holy Spring and the Standing Stones. They are claimed by influence, kept for power, influence and a gift, and fought over by Creatures; gods remember who took them. |
| Advanced politics | Implemented | Eleven treaties, secret agreements, favours and the Divine Council (part 5, ADR-025); holy sites are grounds for grievance, and Sow Discord sets two rivals against each other. War goals and dictated peace, intelligence, proxy wars and mortal holy wars since part 6 (ADR-026); gods who must meet before they deal (ADR-036); and AI gods who absorb their allies near the endgame (ADR-040). |
| Schisms | Implemented | Doubting followers keep the Old Faith, which cannot be converted, returns when the god is loved again and otherwise fades. Sanctuary prevents a schism for a minute. |
| Larger worlds | Implemented (part 7) | Small (2 km), Large (4 km), Huge (8 km) and Vast (12 km) islands with 8, 20, 40 or 48 villages and 4, 6 or 8 gods, chosen on the start screen (ADR-027). The terrain is not yet streamed in pieces (§84). |
| More miracles | Implemented | Thirteen signature miracles, and with the Water miracle (part 2) twenty-three miracles in all. |
| More Creatures | Implemented | Four species with their own toughness, strength, speed, learning, temperament and look; the player chooses for a new world. |
| Steam networking | Not started (decision) | Optional; it needs the Steamworks SDK. `INetworkTransport` is the seam for it beside ENet (ADR-021). |

Beyond §130's list, the milestone adds:
- **Settings (§105):** fullscreen, vertical sync, shadows, render scale, citizens shown and four
  volumes, kept in `user://settings.json`.
- **Audio (§60):** synthesized sounds heard where they happen, and music in three moods.

Measured:
- **Balance:** a passive player's games end as in Milestone 14: 8 of 10 with a winner, in 32 minutes
  on average. That took tuning, because domains first made Seren win every game in 22 minutes.
  With a bot playing the player's god, a Guardian was among the winners in 3 of 10 games and a
  Tyrant in 5 of 10.
- **Simulation:** 0.106 ms a tick on the shipped world.
- **Frames:** the frame rate stays within Milestone 14's on the same machine and day.

The plan is
[.docs/milestone-15-polish-and-expansion.md](../.docs/milestone-15-polish-and-expansion.md).

### Milestone 15, part 2: Water and Law

- **Rivers and lakes (§5, §93):** cut into the land when a world is made (ADR-022).
  - A priority flood routes the water. Hollows hold lakes, and the longest rivers run to the sea
    with their tributaries.
  - On the 2 km island they make 4 main rivers of 200–800 m and 3–4 lakes, and 4–5 of the 8
    villages stand beside water.
- **Floods (§15, §19):** rain and wet soil swell the rivers. A river over its banks floods the low
  land downstream, drowning houses, people and stores, putting out fires and leaving silt that
  blesses the fields. There are 1–10 floods in an hour's game.
- **Drinking water (§30):** villages draw water from rivers, lakes and wells, and the shallow
  wells dry up in a drought. Thirst lowers health and counts as a need that Rain and Water
  answer.
- **The Water miracle (§110):** completes the MVP's nine miracles.
- **Ocean and Justice (§12):** with Tidal Wave and Judgement.
- **Balance:** 36 of 40 passive games found a winner, with a mean of 29.7 minutes. Milestone 15
  had 37 and 29.3 minutes over the same seeds.

The plan is
[.docs/milestone-15-part-2-water-and-law.md](../.docs/milestone-15-part-2-water-and-law.md).

### Milestone 15, part 3: Society

- **A village's life (§30):** safety, happiness, order, wealth and learning, with unhappiness and
  unrest as reasons to leave (ADR-023).
- **Priorities (§29):** set by a god followed by 40% of a village, through the Settlements panel
  (N), or by the AI.
- **Buildings (§21, §9):**
  - Temples, granaries, hospitals and walls.
  - Levees, firebreaks, shelters and cisterns against disaster.
- **Technology (§36):** eight fields, capped at a fantasy level 5, that grow with learning and
  spread between neighbours.
- **Expansion (§6, §29):** worlds grow to about 16 villages and 10,000 people in an hour.
- **Balance:** 32 of 40 passive games found a winner, with a median of 23.9 and a mean of 26.4
  minutes (part 2: 36, 27.8 and 29.7).

The plan is [.docs/milestone-15-part-3-society.md](../.docs/milestone-15-part-3-society.md).

### Milestone 15, part 4: Culture

- **Doctrines (§33):** every village holds pacifism, militarism, sacrifice, charity, nature
  worship, knowledge seeking, expansionism and isolationism more or less firmly (ADR-024).
  - They follow what it lives through and the traits and domains of the god it follows.
  - They change worship, outrage, gratitude, learning, founding, conversion and migration.
- **Secularism (§35):** doubt, the godless, an Old Faith, resentment and science turn a village
  agnostic, atheist, then anti-divine.
  - Secular villages yield less power and resist miracles and conversion, and learn faster.
  - Anti-divine ones turn people from every god and spread to their neighbours.
- **Dependency and resentment (§7–8):**
  - Help in need breeds dependency, which raises worship and doubt in hard times.
  - Fear and harm to one's own followers breed resentment, which love dissolves.
  - Feared, resented gods face revolts in unsettled villages, which tear down their temples.
- **Missionaries and charity (§32, §33):** temples win the godless of nearby villages, and
  charitable villages feed starving neighbours.
- **Traits (§11, §3.2, §34):** the world remembers each god's help, gifts, harm, cruelty,
  vengeance, upheaval, deceit, faithfulness and wars.
  - Traits emerge from that conduct and fade if it changes.
  - A god known for mercy that strikes its own shocks them twice as hard.
- **Balance:** 37 of 40 passive games found a winner, with a median of 28.1 and a mean of 27.5
  minutes (part 3: 32, 23.9 and 26.4).

The plan is [.docs/milestone-15-part-4-culture.md](../.docs/milestone-15-part-4-culture.md).

### Milestone 15, part 5: Treaties and the Divine Council

- **Treaties (§38):** eleven kinds besides alliance, each for a term or until broken, each with
  its effect (ADR-025):
  - war forbidden or joined;
  - shared influence, religious access, Creature truces, disaster assistance;
  - territory kept, tribute paid, migrants welcomed.
- **Secret agreements (§39):** known only to their parties (also in multiplayer) until invoked or
  leaked; the god they were aimed at remembers the plot.
- **Betrayal (§40):** breaking any treaty costs the partner's and the world's trust and the
  breaker's word.
- **Favours (§41):** rescues put gods in debt; favours are called in for power, war support,
  backing, territory or assistance, and refusals and broken promises are remembered.
- **The Divine Council (§42):** summoned by betrayals, new dominance and disasters, or a tabled
  motion. It votes in public or secret ballots, with votes traded by promises, on six kinds of
  motion. Decrees last eight minutes, and defiance costs trust.
- **Alliance endgame (§54):** absorbing an ally's believers is a move of its own.
- **Balance:** 37 of 40 passive games found a winner, with a median of 28.6 and a mean of 31.2
  minutes (part 4: 37, 28.1 and 27.5).

The plan is [.docs/milestone-15-part-5-politics.md](../.docs/milestone-15-part-5-politics.md).

### Milestone 15, part 6: War and intrigue

- **War goals (§44):** wars declared for conquest, a holy site, tribute or to humble a Creature
  (ADR-026).
  - Scores come from what is won: conversions, a defeated Creature and a taken site most,
    strikes least.
  - A met goal lets the victor dictate peace by its terms, and a defender that repelled the
    attack exacts tribute.
- **Intelligence (§43):** derived from alliances, treaties, the god's followers among the other's
  people, its Creature's visits and the Knowledge domain.
  - It blurs rivals' followings on the HUD, and reveals power and plotted betrayal.
  - It speeds leaks of secret treaties and the tracing of covert acts.
- **Proxy wars (§45):** veiled miracles (unattributed; a strike makes the people doubt their own
  god), inciting rebels and spreading propaganda, each possibly traced.
- **Mortal holy wars (§46):** militarist villages attack neighbours of another faith of their own
  will. Their god may encourage or discourage, which costs their love.
- **Balance:** 38 of 40 passive games found a winner, with a median of 29.0 and a mean of 31.3
  minutes (part 5: 37, 28.6 and 31.2).

The plan is [.docs/milestone-15-part-6-conflict.md](../.docs/milestone-15-part-6-conflict.md).

### Milestone 15, part 7: Larger worlds and victories

- **World sizes (§6):** Small (2 km, 8 villages, 4 gods), Large (4 km, 20, 6), Huge (8 km, 40, 8)
  and Vast (12 km, 48, 8), from `world_sizes.json` (ADR-027).
  - Four new rivals with their Creatures: Aurel the Zealot, Maren the Nature Keeper, Thorn the
    Warlord and Ysolde the Isolationist.
  - The start screen offers the size and the victories beside the Creature; `--world-size` and
    `--victory` do the same.
- **Victories (§53):**
  - conquest (every rival outside the god's alliances below 5% of belief for ten minutes);
  - cultural victory (leading three villages in four while a rival keeps 5%);
  - every victory, or a sandbox.
  - Larger pantheons may win together: three gods of six, four of eight.
  - Alerts, history, the end screen and the HUD say how a world was won.
- **Large worlds (§84 in part):**
  - **Simulation:** a Huge world's tick fell from 4.14 to 0.59 ms. The AI weighs only villages in
    its influence, caches influence fields, counts followers once and takes turns.
  - **Labels:** they give way to more important ones on screen.
  - **View:** the camera reaches over the whole island, and the fog thins with it.
  - **Frame rate:** Huge and Vast run at about 120 FPS on the Mac.
- **Balance:**
  - 33 of 40 passive Small games found a winner, with a median of 25.9 and a mean of 30.8 minutes
    (part 6: 38, 29.0 and 31.3). Without turn-taking the probe repeats part 6 exactly, and on
    seeds 70–109 turn-taking won more games (39 against 37), so the difference is the games'
    variance.
  - Larger worlds are long: in ten hour-long games, four Large worlds and one Huge world found a
    winner, with 10,000–26,000 people.

The plan is [.docs/milestone-15-part-7-worlds.md](../.docs/milestone-15-part-7-worlds.md).

### Milestone 15, part 8: Seasons and the land's life

- **Seasons (§17):** an optional ten-minute year, chosen with the world (ADR-028).
  - Summer grows crops, dries the land, spoils stores and brings heat waves and fire.
  - Autumn brings the harvest, rain and hurricanes.
  - Winter halves the harvest, lays snow on the hills, sickens the homeless and keeps people home.
- **Weather (§16):** snow that lies until it melts into the soil and the rivers, fog, hurricanes that
  tear houses down, and heat and cold waves.
- **The land's life (§18):**
  - Fires, floods, earthquakes and lightning harm the land; harm heals, but repeated harm scars it
    for good.
  - A god of Nature heals scars, and Regrowth heals both.
  - Wildlife returns as the land allows and feeds the villages that hunt it.
  - Crowded villages pollute.
- **Balance:**
  - 38 of 40 passive games with seasons found a winner, with a median of 24.9 and a mean of 27.0
    minutes (part 7: 33, 25.9 and 30.8).
  - Winters bring famine (74 begun in six 40-minute games, against 2 without seasons) and more
    migration, and populations end as large.

The plan is [.docs/milestone-15-part-8-nature.md](../.docs/milestone-15-part-8-nature.md).

### Milestone 15, part 9: Hazards

- **The rest of §19's disasters**, each a process (ADR-029):
  - **Tsunamis:** a strong earthquake near the coast floods the low land around the shore.
  - **Landslides:** soaked or shaken slopes slide and bury the land below in rubble that blocks
    the way until it clears (§20's road blockage).
  - **The volcano:** the island's highest peak wakes now and then, rumbles with tremors and
    erupts; lava burns down its slope and ash sickens and spoils around it.
  - **Tornadoes** spin down from thunderstorms over warm land, and **falling stars** leave craters,
    fire and a quake.
  - **Blight** spreads through warm, wet fields until dry weather, fire or Bless Crops ends it;
    **locusts** drift with the wind, eating stores, until rain drowns them.
- **Preparation (§21):** sea walls by the sea, dams and irrigation beside water, and evacuation
  routes, raised under the existing priorities.
- **Marks on the land (§64):** cracks, slide scars and craters that fade over ten minutes; the
  terrain is not remeshed.
- **Rates:** about one tsunami, three landslides, half a tornado, half a blight and half a swarm
  at a village per hour of play; an eruption or a falling star every five hours.
- **Balance:** 39 of 40 passive games found a winner, with a median of 24.9 and a mean of 28.3
  minutes (part 8: 38, 24.9 and 27.0).

The plan is [.docs/milestone-15-part-9-hazards.md](../.docs/milestone-15-part-9-hazards.md).

### Milestone 15, part 10: Paths, the hand and the Divine Acts

- **Paths (§24):** every deed, fight and reward calls a Creature towards paths. At levels 3, 6 and
  9 it takes up the one it has walked most: never more than three of the eight (ADR-030).
  - Healers heal, Guardians shield villages from disasters, Missionaries win the godless,
    Nature Keepers heal the land, Warriors fight harder, Destroyers kill more, Tricksters sow
    doubt and Commanders make villages harder to attack.
  - The player decides what its Creature becomes by what it rewards.
- **Attributes (§23):** intelligence, resilience and divine affinity grow with level, beside strength
  and speed, and show on the card.
- **The hand (§55):**
  - R lifts villagers, a tree, a rock or the Creature where the god's influence reaches; R sets it
    down and Shift+R throws it.
  - Villagers carried out of danger to safety love the god for it. Taken from safety they are
    afraid; set down in the sea they drown. A thrown rock wrecks, kills and leaves a crater.
- **Divine Acts (§14):** a Greater God may hallow the land (its blessing never fades), wake the
  volcano or still every disaster on the island, from the Domains panel. Each costs 1,500–2,500
  power and enters the history.
- **Balance:** 39 of 40 passive games found a winner, with a median of 27.6 and a mean of 29.3
  minutes (part 9: 39, 24.9 and 28.3).

The plan is [.docs/milestone-15-part-10-powers.md](../.docs/milestone-15-part-10-powers.md).

### Milestone 15, part 11: The Book of the World, overlays, overview and exports

- **Panels (§57):** the Book of the World (Tab), with four pages (ADR-031):
  - **World:** every god's standing, ties and Creature, the race, the dangers and the holy sites.
  - **Religion:** the player's followers village by village, every god's faith, the Old Faiths and
    the godless.
  - **Miracles:** every miracle with what it costs and still needs, and Aim.
  - **Creature:** the player's Creature in full.
- **Overlays (§58):** population, disaster risk and political control.
- **Camera (§56):** backtick shows the whole island and goes back.
- **Platforms (§62):** export presets for Windows x64, macOS (universal) and Linux x64. The macOS
  app was exported and played; it is signed ad hoc with Apple's `codesign`, since macOS 26 kills an
  ad-hoc app with the hardened runtime.

The plan is [.docs/milestone-15-part-11-interface.md](../.docs/milestone-15-part-11-interface.md).

### Milestone 15, part 12: Prophets, elders and temples

- **Tier 3 (§80):** prophets and elders are individuals in the world state, with names,
  lifespans, paths and memories, simulated everywhere and saved (ADR-032, which amends ADR-004 and
  ADR-008).
  - **Elders (the important leaders):** every village of 40 or more chooses one of the god it
    follows most. The elder keeps its faith, turns from a god it resents (leaving doubt) and
    follows the village when it leaves their god.
  - **Prophets:** a devout village calls one, or a Lesser God anoints one with Anoint a Prophet.
    They preach, winning the godless and some of every rival's followers, then walk to the next
    village by the pathfinder. A god has two at most.
  - **Martyrs:** a prophet who dies in danger strengthens the faithful within 400 m.
- **Influence (§10):** temples and prophets are sources of their god's influence.
- **Seen and told:** robed figures in the god's colour, alerts, the history, the book's World
  and Religion pages, F1 and `--prophet`.
- **Balance:** 38 of 40 seeds won within the hour, at a median of 28.3 minutes.
- **Saves:** version 23. **Protocol:** version 11.

The plan is [.docs/milestone-15-part-12-notables.md](../.docs/milestone-15-part-12-notables.md).

### Milestone 15, part 13: Wood, stone, forests and roads

- **The land (§5, §93):** each region has a forest, its standing timber and its stone, laid in
  seeded patches by biome (ADR-033). Timber grows back and burns. A cleared forest holds fewer
  animals, and a cleared wet slope slides sooner.
- **Gathering (§29):** villages keep stores of wood and stone, and send gatherers out of the fields
  while they run short. Houses take wood, and buildings wood and stone. Settlers carry their share.
- **Roads (§29):** villages build roads to their neighbours with stone, round the lakes. Walkers go
  faster on them, both villages trade, and food goes to the hungry. Floods, fires and rubble cut
  them until they are mended.
- **Seen and told:** trees and roads in the world, the Settlements panel, the book, alerts, F1, F3,
  `--roads` and `--stores`.
- **Balance:** 38 of 40 seeds won within the hour, at a median of 25.7 minutes.
- **Saves:** version 24. **Protocol:** version 12.

The plan is [.docs/milestone-15-part-13-resources.md](../.docs/milestone-15-part-13-resources.md).

### Milestone 15, part 14: Great events

- **The plague (§52):** villages break out when crowded, sick and polluted. The sickness spreads
  to neighbours and along roads, the sick lose health, and those who recover are immune for about
  an hour. Hospitals and medicine slow it, and healing miracles cure it. Three villages sick at
  once make a great plague, and the council meets to coordinate the response (ADR-034).
- **Eclipses:** the sun goes dark at seeded times. Everyone is afraid, and a miracle worked in the
  dark awes twice as much.
- **Reformations:** a god's doubting faith, in villages that have learned, reforms. Doubt falls
  and faith rises, but some leave.
- **The watch:** world wars, great droughts and great migrations begin and end with the world's
  state.
- **Seen and told:** alerts, the top bar, the book, the history, labels, the danger overlay, F1,
  `;` and `--great-event`.
- **Balance:** 37 of 40 seeds won within the hour, at a median of 25.0 minutes.
- **Saves:** version 25. **Protocol:** version 13.

The plan is [.docs/milestone-15-part-14-great-events.md](../.docs/milestone-15-part-14-great-events.md).

### Milestone 15, part 15: The ancient beast and the magical anomaly

- **The ancient beast (§52):** asleep on the island's heights, it wakes once the world has grown,
  by a great quake, the volcano or time. It walks to village after village and ravages them, and
  walls spare half its harm. Creatures near it fight it, and AI gods send theirs. Smiting miracles
  and fire harm it, and it heals when nothing does. Tired, it goes home to sleep. When it falls,
  every god that harmed it shares the glory (ADR-035).
- **The magical anomaly:** at seeded times and places, miracles cost half within its reach, and each
  god draws power there by its influence.
- **Seen and told:** the beast's body and the anomaly's column of light, alerts, the history, the top
  bar, F1, `;` and `--great-event=beast|anomaly`.
- **Balance:** 38 of 40 seeds won within the hour, at a median of 25.7 minutes.
- **Saves:** version 26. **Protocol:** version 14.

The plan is [.docs/milestone-15-part-15-beast-and-anomaly.md](../.docs/milestone-15-part-15-beast-and-anomaly.md).

### Milestone 15, part 16: Known gods and the gods' long-term plans

- **Known gods (§11):** a god knows only the gods it has met. They meet as neighbours, through
  shared believers or a road, when their Creatures meet, by their deeds, or in the Divine Council,
  which brings everyone together. Until then two gods cannot ally, war, sign or ask (ADR-036).
- **Long-term goals and major planning (§47, §87):** every 30 seconds an AI god sets itself up to
  three goals: protect its home, convert a village within reach, befriend or weaken a god it
  knows, prepare for the plague or a drought, slay the ancient beast, or win back its doubters.
  - Its miracles, Creature, diplomacy and villages' priorities lean towards them, by how much each
    goal matters.
  - It holds a goal at least 90 seconds unless the goal is moot.
- **Seen and told:** "not yet met" on the P panel and the top bar, an alert for each god met, the
  history, a rival's goals for a well-informed player, F1's plans and `--gods-met`.
- **Balance:** 34 of 40 seeds won within the hour, at a median of 24.5 minutes; 150 of 160 over
  seeds 30–189 (part 15: 155).
- **Saves:** version 27. **Protocol:** version 15.

The plan is [.docs/milestone-15-part-16-known-gods-and-plans.md](../.docs/milestone-15-part-16-known-gods-and-plans.md).

### Milestone 15, part 17: Creature temperament

- **Character (§26):** a Creature is born with courage, protectiveness, jealousy and playfulness
  beside its kindness, aggression, laziness and curiosity, shaded by its species. Its card names it
  in §26's words: brave or cowardly, kind or sadistic, protective, jealous, playful, curious, lazy,
  aggressive, loyal or independent (ADR-037).
- **In what it does:**
  - a brave Creature fights where a coward runs;
  - a protective one stands its ground near its god's people, and defends them from the beast by
    itself;
  - a jealous one makes rivals of stronger Creatures;
  - a playful one plays.
- **How it changes:** rewards and punishments move the traits behind the deed, rewards ease
  jealousy, victories embolden, defeats, flight and wounds cow and embitter, and play makes it
  playful.
- **Seen and told:** the card, the book, F1, an alert when it defends by itself, and
  `--temperament`.
- **Balance:** 33 of 40 seeds won within the hour, at a median of 22.3 minutes; 141 of 160 over
  seeds 30–189 (part 16: 150).
- **Saves:** version 28. **Protocol:** version 16.

The plan is [.docs/milestone-15-part-17-creature-temperament.md](../.docs/milestone-15-part-17-creature-temperament.md).

### Milestone 15, part 18: The biomes of the world

- **Every biome of §5:** tundra below the heights, desert where it is hot and dry, and swamp
  where it is wet and low, beside grassland, forest and highland; and by place, islands off the main
  one, coasts, river valleys, and the volcanic land around the volcano (ADR-038).
- **What they do:** each has its own fertility, growth, wood and stone. Villages crowd into the
  fertile river valleys, and the deserts stay empty.
- **Seen:** each biome has its own colour, blending into its neighbours, and the Book of the World
  counts the land by biome.
- **Balance:** 138 of 160 seeds won within the hour (part 17: 141); every shipped world is a new
  world.
- **Saves:** version 29. **Protocol:** version 17.

The plan is [.docs/milestone-15-part-18-biomes.md](../.docs/milestone-15-part-18-biomes.md).

### Milestone 15, part 19: Offerings

- **Offerings (§55):** Ctrl+R takes an offering from the stores of a village that follows you, and
  R gives it to the village or the Creature you point at (ADR-039).
  - A village's stores fill, and it is grateful by its need.
  - The Creature eats the food and grows more loyal.
  - Generous gods are seen as generous.
- **Seen:** a basket in the hand, the status line, and `--hand=offering`.
- **Saves:** version 30. **Protocol:** version 18.

The plan is [.docs/milestone-15-part-19-offerings.md](../.docs/milestone-15-part-19-offerings.md).

### Milestone 15, part 20: The alliance endgame, lightning's fires and hurricane warnings

- **Absorbing (§54):** an AI god that would betray its ally absorbs it instead when that takes at
  least 0.5% of all believers (ADR-040). Over 40 seeds, AI gods absorbed twice and broke away 7
  times.
- **Lightning's fires (§15):** a god's bolt sets dry woods alight by their flammability, so §15's
  chain from lightning to migration runs (ADR-041).
- **Hurricane warnings (§59):** a hurricane is announced as it forms, with the coast it comes
  towards.
- **The summary:** the rows for §15, §30, §33–36, §51–52, §54, §59, §75, §79, §89, §90, §91, §107,
  interaction and advanced politics now say what is built.
- **Balance:** 141 of 160 seeds won within the hour (part 27).

The plan is [.docs/milestone-15-part-20-alliance-endgame.md](../.docs/milestone-15-part-20-alliance-endgame.md).

### Milestone 15, part 21: Religion's look on villages

- **Religion's look (§61):** a village takes its god's style (ADR-042):
  - flower beds and bushes for a loved nature god;
  - dark obelisks and burning braziers for a feared war god;
  - white columns for a god of learning and order.

  The style grows with the god's hold, its traits, the village's doctrines and its people's love
  or fear.
- **Seen:** in the world on both renderers, and in F1.

The plan is [.docs/milestone-15-part-21-religions-look.md](../.docs/milestone-15-part-21-religions-look.md).

### Milestone 15, part 22: The selected villager

- **The selected villager (§80's last Tier 3 example):** / makes the figure at the cursor an
  individual of the world (ADR-043). They have a name, an age, their work and a god, and are the same
  person each time.
- **Their life:**
  - they eat from the village's stores, work, rest and pray as their needs and faith move them;
  - they flee danger to a safe village, and seek a new home if theirs is lost;
  - they turn to another god when their faith fails, and die of age, hunger, illness or danger.
- **Memory:** they remember miracles near them and what befalls their village. Hardship drives them
  to pray, and danger they have known makes them flee it sooner.
- **Seen:**
  - the camera keeps them in view, and the Book's Villager page tells their life;
  - F1 shows their needs, options and danger, and alerts tell of their flight, turning and death.
- **Saves and sharing:** saves move to version 31 and the protocol to 19.

The plan is [.docs/milestone-15-part-22-selected-villager.md](../.docs/milestone-15-part-22-selected-villager.md).

### Milestone 15, part 23: The stress scenario and the last debug overlays

- **Stress testing (§104):** `--stress=on` makes the vast island with 50 villages of 2,000 people,
  8 gods and their Creatures, 20 weather cells and three disasters at once, with thousands of
  citizens drawn (ADR-044).
- **Measurements:**
  - F1 and unattended runs report frame and simulation times, ticks per second, memory, the
    engine's objects and draw calls, the citizens drawn and a save's times;
  - on an M3 Pro the stress world holds 100 FPS with 7,000 citizens drawn, and a tick takes 1.5 ms.
- **Debug overlays (§106):**
  - Navigation shows walkable land, roads, rubble and every walker's way;
  - Authority shows who commands each village's god;
  - F1 says who decides in a shared game and how fresh each section is.
- **Found on the way:**
  - F1 now rebuilds four times a second (43 FPS to 92 on the stress world);
  - a joined player's bandwidth on the largest island is mostly the regions (144 KB/s).

The plan is [.docs/milestone-15-part-23-stress-and-debug-overlays.md](../.docs/milestone-15-part-23-stress-and-debug-overlays.md).

### Milestone 15, part 24: Divine Acts that reshape the land

- **Reshaping the land (§14):** three Divine Acts change the land's shape for good (ADR-045):
  - Raise an Island lifts new land from the open sea;
  - Split the Mountain opens a gorge down to the low ground on either side;
  - Turn the River runs the nearest river down to where the god points, damming its old course.
- **Kept and made:**
  - the reshapings are saved and shared;
  - the terrain is made with them before the rivers are found, so the water, biomes and regions
    follow;
  - an act takes about 250 ms on the default island, without sampling the noise again.
- **Seen:**
  - in the world on both renderers, in the history, the alerts and F1;
  - `--reshape` makes one for captures;
  - on seed 42 the split mountain captures the big river.

The plan is [.docs/milestone-15-part-24-reshaping-the-land.md](../.docs/milestone-15-part-24-reshaping-the-land.md).

### Milestone 15, part 25: Local navigation

- **Local navigation (§85):** each village shown up close bakes a navigation grid from its layout,
  and its figures walk round huts, ruins, the building site, the shrine, the temple and water on
  A* ways pulled taut. Each leg keeps its time (ADR-046).
- **Work:** work spots that fell inside a neighbour's hut now lie outside it.
- **Measured:** a village up close is unchanged at 113 FPS; the stress world dips to 87 FPS while
  its 21,000 ways are first found.

The plan is [.docs/milestone-15-part-25-local-navigation.md](../.docs/milestone-15-part-25-local-navigation.md).

### Milestone 15, part 26: The Creature's state machine and behaviour trees

- **§86's architecture:** a hierarchical state machine (free, engaged, held, banished) runs each
  Creature (ADR-047). Behaviour trees carry out what its utility brain chooses: an interrupt for
  commands, a guard that the reason still holds, and travel, perform and deed.
- **Giving up:** a Creature gives up an errand whose reason is gone and tells its god: the village
  fed, the house built, the stores empty, the village now an ally's.
- **Balance:** 148 of 160 seeds won within the hour.
- **Found on the way:** a village's navigation grid was baked again on every settlement step while
  it built (part 25). It is now baked again only when its obstacles change, which took a grown game
  at ×16 from 33 FPS to 91.

The plan is [.docs/milestone-15-part-26-creature-behaviour-trees.md](../.docs/milestone-15-part-26-creature-behaviour-trees.md).

### Milestone 15, part 27: Structural integrity, flood resistance and debris

- **§94's structures:** every blow that leaves a village's houses standing cracks them, cracked
  houses fall more easily in the next blow, and villages mend them over minutes. Houses resist
  floods as they resist quakes and fires.
- **Debris:** fallen houses, falling stars and the hand's throws throw planks, stones and thatch
  as Jolt rigid bodies onto the terrain; they fade after nine seconds and are never saved. A
  thrown rock or tree lands and rolls.
- **Tools:** F1 shows the houses' integrity and the debris; `--hand=throw`; the debug falling
  star now strikes villages.
- **Balance:** 141 of 160 seeds won within the hour.
- **Found on the way:** villages' figures searched for ways that do not exist across their whole
  grid. The grid now knows its connected areas, and its A* runs on arrays: a grown game at ×16
  went from 71 FPS to 113, and the stress world from 86 to 93.

The plan is [.docs/milestone-15-part-27-structures-and-debris.md](../.docs/milestone-15-part-27-structures-and-debris.md).

### Milestone 15, part 28: People that look and move like people

- **Why:** the owner found the villagers to be coloured dots running about like ants: cones
  coloured by work, whose rounds ran on simulation time, so at ×16 they walked over 20 m/s.
- **People:** each villager is a procedural person with their own skin, hair, height and clothes
  by work; farmers wear straw hats, some people a scarf or a long dress. Prophets and elders are
  robed in their god's colour with a staff, and migrants walk as people too.
- **Motion:** a shader moves the limbs. People walk, sit by their doors, hoe the fields, hammer at
  the building site, do chores, and raise their arms at the shrine.
- **Pace:** the crowds run on a clock of their own, never faster than real time, so people keep a
  human pace at any game speed and stop when it is paused (ADR-049, amending ADR-008).
- **Performance:** the stress world draws 7,000 people at 108 FPS (93 with the old cones).

The plan is [.docs/milestone-15-part-28-people.md](../.docs/milestone-15-part-28-people.md).

### Milestone 15, part 29: World streaming

- **§84:** the land near the camera carries its detail and the rest stays simulation data. Forests
  are planted within 2.1 km, the fallback terrain is drawn in 2 m detail within 650 m with its
  borders matched to the coarse ground, and fires are drawn within 2.5 km; citizens already
  appeared only near the camera. A shared, tested `ChunkStreamer` plans it, and F1 says what is
  loaded.
- **Measured:** on the vast island the visuals build in 1.38 s (1.58 before) with 194 MB in the
  engine (241), and half the objects and nodes. The frame rate was already above 200 FPS.

The plan is [.docs/milestone-15-part-29-world-streaming.md](../.docs/milestone-15-part-29-world-streaming.md).

### Milestone 15, part 30: Ground textures and light

- **The ground (§61):** Terrain3D now draws nine textures made in code: grass, forest floor,
  earth, rock, sand, snow, moss, mud and ash, each with its own pattern and relief. A control map
  painted in the core picks them by biome, blends them over a few metres at biome borders, and lays
  rock on steep slopes, snow on cold heights and sand near the sea. The biome colours only tint
  them gently.
- **The light:** ACES tonemapping, ambient occlusion, a soft glow, a deeper sky and softer
  shadows; "Lighting effects" switches the effects.
- **Cost:** visuals build in 1.2 s on the default island; a grown game at ×16 runs at 135 FPS.

The plan is [.docs/milestone-15-part-30-ground-and-light.md](../.docs/milestone-15-part-30-ground-and-light.md).

### Milestone 15, part 31: Village architecture

- **Houses (§61):** five styles made in code: round huts of wattle and thatch, timber longhouses,
  stone cottages with slate and chimneys, flat-roofed adobe, and plank houses on stilts. The land
  picks the style; villages of huts build longhouses as they grow, and big villages an upper
  storey. A house under construction rises from the ground.
- **Religion:** where a god is strongly felt, the houses take its colours: turf roofs for nature,
  dark timber and roofs for war, whitewash and blue for learning. The temple takes its god's
  style: a domed round temple, a dark stepped pyramid with a brazier, a columned hall, or a plain
  hall.
- **Fortifications:** walls are a palisade of pointed stakes with watchtowers by the gates, or
  crenellated stone with stone towers.
- **The building shader** paints daub, planks, coursed stone, thatch, slate and timber.

The plan is [.docs/milestone-15-part-31-village-architecture.md](../.docs/milestone-15-part-31-village-architecture.md).

### Milestone 15, part 32: Trees, ground cover and wind

- **Trees (§61, §93):** seven species made in code, grown by each biome in its shares: oaks and
  birches on grassland, pines in forests, firs on the heights, weeping willows by rivers and in
  swamps, palms on islands and in oases, bare snags on volcanic land.
- **Ground cover:** within 100 m of the camera, tufts of grass and flowers as thick as the land's
  vegetation, bushes, stones and reeds, never by houses, on fields or on a village's square.
- **Wind:** trees and grass sway in the weather's wind and bend hard in storms.

The plan is [.docs/milestone-15-part-32-trees-and-ground-cover.md](../.docs/milestone-15-part-32-trees-and-ground-cover.md).

### Milestone 15, part 33: Wildlife and bridges

- **Wildlife (§61):** near the camera, herds of deer graze and walk on to fresh pasture, stags
  among them, and flocks of birds wheel over forests, rivers and coasts. There are as many as each
  region's simulated wildlife, so hunted, burnt or cleared land is empty of them, and they keep a
  natural pace at any speed.
- **Bridges:** roads cross water on plank bridges from bank to bank.
- **Tools:** `--wildlife=on`, and the deer and birds in F1's streaming line.

The plan is [.docs/milestone-15-part-33-wildlife-and-bridges.md](../.docs/milestone-15-part-33-wildlife-and-bridges.md).

### Milestone 15, part 34: Expressive Creatures and miracle looks

- **Faces (§61):** the Creature scowls in a fight, widens its eyes in flight, grins at play,
  sleeps at rest, looks glum when hungry and bored, and beams after a reward or cowers after a
  slap. Its tail wags, lashes or tucks. Looking at it turns the camera to its face.
- **Nature:** a kind Creature grows warm and softly glowing, a cruel one dark and red-eyed, with
  longer horns.
- **Miracles:** each has its own look: rain from a gathering cloud, a golden harvest pillar, a
  healing spiral under a dome, flames and smoke, a quake's dust, a shield's dome, a fountain, a
  pillar of holy light, a dark swirl, a wheeling storm and a god's strike. They run on the GPU.
- **Tools:** F1's Creature lines show its face and nature.

The plan is [.docs/milestone-15-part-34-creatures-and-miracles.md](../.docs/milestone-15-part-34-creatures-and-miracles.md).

### Milestone 15, part 35: The CPU budget, graphics scaling and regions within a tolerance

- **The CPU budget (§105):** the simulation keeps to a budget a frame, the player's "Simulation
  budget" (6 ms by default). The ticks still due wait for the next frame, so a heavy world runs a
  little slower than asked instead of stuttering. F1 shows how it keeps to it.
- **Graphics scaling (§105):** vegetation, particles and terrain quality join the settings.
- **Shared games:** the land's regions are sent only when they change visibly, which more than
  halves the stress world's traffic (amends ADR-019).
- **At ×16:** floods, citizens, walkers' grids and forests react to the world at a pace and only
  where it changed. The stress world's visuals fell from 31 ms a frame to 4 ms, and its citizens
  from 42 ms to 11 ms.

The plan is [.docs/milestone-15-part-35-budget-and-scaling.md](../.docs/milestone-15-part-35-budget-and-scaling.md).

## Coverage by specification section

Sections that only state intent are summarised at the end of this part.

### World and generation

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 5 | The world | Implemented | One continuous world divided into regions. Regions have a biome, elevation, temperature, moisture, fertility, vegetation, soil water, fresh water from rivers and lakes (ADR-022), snow, wildlife, harm, scars and pollution (ADR-028), forest, timber and stone (ADR-033) and a divine blessing, and religious influence, weather and disaster risk (ADR-031) are derived for any point. Every biome of §5: grassland, forest, mountain (highland), tundra, desert and swamp by climate, and coast, island, river valley and volcanic land by place (ADR-038). |
| 6 | World scale | Implemented | Four sizes (ADR-027): 2, 4, 8 and 12 km across against 10–20 km, with 8, 20, 40 and 48 settlements at the start against 8 for the MVP and 20–50 in the full game, and four, six or eight gods against four, up to eight later. Villages found more as they grow. In an hour the larger worlds reach 10,000–26,000 people against 10,000–100,000+. Hundreds of visible citizens, and no heavyweight object per citizen. The small island's 8 settlements grow to about 16 (ADR-023), with up to about 10,000 people after an hour. |
| 84 | World streaming | Implemented | The island is cut into 256 m chunks that load near the camera (measured in three dimensions) and go when it leaves, by a shared `ChunkStreamer`: forests are planted within 2.1 km, the fallback terrain is drawn in 2 m detail within 650 m with matched borders (Terrain3D has its own clipmap), fires are drawn within 2.5 km, and citizens appear within 450 m (ADR-008, ADR-050). Distant regions remain simulation data; the simulation runs everywhere. |
| 93 | Procedural generation | Implemented | Elevation, moisture, temperature, fertility, vegetation, biome, forests and stone in seeded patches (ADR-033), rivers and lakes (ADR-022), holy sites and settlement sites come from the seed (tested). |

### Settlements and population

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 29 | Settlements | Implemented | Housing, roads, gathering, job allocation (farm, build, worship, gather), construction, buildings and expansion (founding new villages) are autonomous (ADR-023, ADR-033); a god sets the priorities §29 lists. |
| 30, 90 | Settlement state | Implemented | Every field §30 tracks: region, population, food, water (ADR-022), housing, health, safety, happiness, education, wealth and political stability (ADR-023), and belief, love, fear, doubt and resentment per god, with dependency (ADR-024). Besides: construction, the doctrines it holds, its plague and immunity (ADR-034), and wood and stone in store (ADR-033). |
| 31 | Population migration | Implemented | People leave for famine, homelessness, danger (disaster), war, fear and low religious confidence, and go where life is better (opportunity); persecution and poverty count through fear and hunger. Neighbouring gods accept them, reject them or demand conversion (ADR-016). Negotiating their return is not built. |
| 33–36 | Culture, schisms, atheism, technology | Implemented | Schisms (§34): doubting followers keep their god's Old Faith, unconvertible, returning when the god is loved again or fading into unbelief; a god that harms people who love it sows the doubt (ADR-021). People who follow no god exist (§35). Technology (§36): eight fields, capped at a fantasy level, that grow with learning and spread (ADR-023). Culture (§33): eight doctrines with their effects; secular societies (§35): agnostic, atheist and anti-divine stages, with less power, resistance to miracles, faster learning and anti-divine movements that spread (ADR-024). |
| 32 | Religious conversion | Implemented | Gradual conversion between gods and from people who follow none, driven by miracles, prosperity, intimidation, rival doubt, the Creatures' deeds and migration (migrants give up feared or doubted gods, are converted on demand and mix into their new home's beliefs). Several religions coexist. Temples send missionaries to the godless around them (ADR-024), and prophets walk from village to village preaching (ADR-032). Propaganda and military victory are not modelled. |
| 80 | Simulation tiering | Implemented | Tier 1 simulates the economy, demographics, health and belief. Tier 2 shows people walking, sitting, working and praying, at a human pace on the crowds' own clock (presentation only, ADR-049). Tier 3 is the individuals that matter, kept in the world state everywhere with their own behaviour, paths and memory: the Creatures, Creatures in combat, what the hand holds, prophets and elders, the important leaders (ADR-032), and the villager a player selects with /, who eats, works, prays, flees, turns, remembers and dies as an individual (ADR-043). Physics for individuals is §94's. |
| 81 | Population representation | Implemented | Up to 300 people stand for each settlement's population, each with their own look (skin, hair, height, clothes by work, headwear, dress) and animated motion (ADR-008, ADR-049). |
| 82 | Migration representation | Implemented | `MigrationGroup` with population, origin, destination, route, health and food; near the camera a few walkers stand for the group. |
| 83 | Rendering large crowds | Implemented | One MultiMesh per settlement for citizens, and one per house part and for fields. |
| 112 | MVP settlements | Implemented | Settlements farm, build housing, grow, worship, respond to miracles, fight fires, rebuild, and send people away from hardship, war and danger. |

### Gods, belief and miracles

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 7–8 | Belief, love versus fear | Implemented | Faith, Love, Fear, Doubt, Dependency and Resentment with their trade-offs: help in need breeds dependency (more worship, doubt in hardship), fear and harm breed resentment that love dissolves, and resentment ends in revolts (ADR-024). |
| 9 | Divine Power | Implemented | Followers × Faith × worship intensity × religious infrastructure (the shrine, a temple to the god and the people's religion technology, ADR-023) × a rate, with Love and Fear raising the intensity differently. |
| 10 | Divine influence | Implemented | From followers (whose villages' shrines it stands for), temples, the holy sites a god keeps, prophets, the Creature's presence and allied religious access (ADR-021, ADR-025, ADR-032); overlapping; weaker influence costs more, except where the stronger god is an ally. |
| 11 | God attributes | Implemented | Id, Name, Rank, Divine Power, followers (derived), controller, personality, Creature, domains, holy sites kept (derived), and relationships (in `WorldState.Diplomacy`); the world's history keeps its historical actions. Reputation is the god's remembered conduct, and its traits emerge from it (ADR-024). Known gods: the gods it has met, as neighbours, through shared believers or roads, Creatures, deeds or the council (ADR-036). |
| 12 | God domains | Implemented | All thirteen. One domain per rank tier up to three; each gives 0.8-cost miracles, a boon that changes a rule of the world and a signature miracle; AI gods choose by personality (ADR-021, ADR-022). |
| 13 | Divine rank | Implemented (followers only) | Six ranks by followers, never lost, unlocking miracles. Achievements, holy places and the Creature do not count yet. |
| 14 | Miracles | Implemented | Twenty-seven: Food, Heal, Bless Crops, Lightning, Rain, Fire, Shield, Mass Healing, Earthquake, Water and Anoint a Prophet (ADR-032), thirteen domain miracles (Regrowth, Gift of Life, Redirect Storm, Firestorm, Spear of Heaven, Dread, Abundance, Sanctuary, Enlighten, Sow Discord, Upheaval, Tidal Wave, Judgement), and six Divine Acts for a Greater God, which enter the history: Hallow the Land, Wake the Volcano and Still the World (ADR-030), and Raise an Island, Split the Mountain and Turn the River, which reshape the land for good, its rivers, biomes and regions following (ADR-045). |
| 15 | Systemic consequences | Implemented | Both of §15's chains run. Rain calls a cloud that waters the land as it drifts on and raises the rivers until they flood downstream (ADR-022). A god's lightning sets dry woods alight (ADR-041); the fire scorches the land and drives off its wildlife, the villages around lose their hunting, food falls and people leave (ADR-028, ADR-016). Bless Crops raises the yield of the land; Food and Heal change hunger and health, which drive doubt and love; Fire spreads across dry land; Earthquake starts fires and ruins; repeated harm scars the land, which a god of Nature or Regrowth heals. |
| 47 | AI gods | Implemented | All eight of §47's personalities, with risk, aggression, cooperation, treachery and grudge as profile weights, diplomatic memory, preferred domains taken up in order, and holy sites claimed and held by their Creatures (ADR-021). Short-term goals are the utility AI's choices each evaluation. Long-term goals come from the major plan: protect the home, convert a village, befriend or weaken a god, prepare, slay the beast or win back doubters (ADR-036). |
| 48 | God AI model | Implemented | Utility AI that continuously scores candidate actions, miracles and diplomatic moves alike, with explainable factors. |
| 49 | AI betrayal | Implemented | An AI god breaks an alliance when it is far ahead, its ally dominates alone, its view has soured, or, near a joint victory, it outweighs its ally, weighted by its `Treachery` and held back by the goodwill of the alliance. The clues are visible beforehand: its attitude and grievances in the diplomacy panel and its weighed moves in the F1 AI view. |
| 87 | God AI architecture | Implemented | Strategic and diplomatic evaluation every 3 s, the AI gods taking turns across the interval so eight gods never think on one tick (ADR-027). Major planning every 30 s keeps up to three goals, such as protecting the home, converting a village, befriending or weakening a god and preparing for drought, which bend the evaluation (ADR-036). Diplomatic reevaluation stays periodic, with proposals answered as they come. |
| 109 | MVP belief | Implemented | Faith, Love, Fear and Doubt. |
| 110 | MVP miracles | Implemented | Heal, Food, Lightning, Bless Crops, Rain, Fire, Earthquake, Shield and Water (ADR-022). |
| 115 | MVP victory | Implemented | 60% of all followers for 10 minutes, both configurable in `divine_rules.json`; the world goes on afterwards. |

### The Creature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 22 | The Creature | Implemented (one per god) | Companion, physical avatar and champion: its deeds act for its god, and it fights other gods' Creatures (ADR-017). As a Missionary it wins the godless, as a Commander it defends its god's villages (ADR-030). |
| 23 | Progression | Implemented (MVP maximum) | Level, experience, health, strength, speed, intelligence, resilience and divine affinity (from level and species, ADR-030), combat skill, loyalty, morality, personality and relationships. Levels run to §23's MVP maximum of 10, not the full game's 50. |
| 24 | Specialisations | Implemented | All eight paths, taken up at levels 3, 6 and 9 by what the Creature has done and been rewarded for, each with its effect; three at most, so never every path in one game (ADR-030). |
| 25 | Learning | Implemented | Preferences per situation and action, from §25's starting values, moved by reward and punishment. |
| 26 | Personality | Implemented | Eight traits: kindness, aggression, laziness, curiosity, courage, protectiveness, jealousy and playfulness, born from the seed and the species, with loyalty for loyal or independent. Together they give §26's twelve named traits. Personality drives every action and evolves through experience (victories embolden), reinforcement (rewards and punishments move the traits behind a deed), trauma (defeats, flight and wounds cow and embitter), relationships (play, envy) and divine treatment (ADR-037). |
| 27 | Relationships | Implemented | Friendship, fear, rivalry, respect and trust, one view per Creature met, with remembered encounters (ADR-017). Allied Creatures become friends; friends whose gods fight refuse to fight. |
| 28 | Defeat | Implemented | Banishment (temporary absence) on starvation or defeat. A defeat also brings a scar, lost loyalty, lost progress towards the next level and reputation: witnesses' belief and the god's grudge. Permanent death is not modelled. |
| 86 | Creature AI architecture | Implemented | A hierarchical state machine (Free › Deciding, Travelling, Performing; Engaged › Fighting, Playing; Held; Banished) and behaviour trees (interrupt, guard, travel › perform › deed) that carry out the utility brain's choices, with learned weights, memory, personality and relationship memory, all in the core (ADR-010, ADR-017, ADR-037, ADR-047). LimboAI's roles are played by our own code, as the core never references Godot. |
| 111 | MVP Creature | Implemented | Movement, health, needs, combat, XP, levels 1–10, basic personality, helping and attacking villagers, fighting Creatures, reward, punishment and learned preferences. |

### Nature

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 16 | Weather | Implemented | Cells with §16's fields that move across regions at 2 Hz; clear, rain, snow, thunderstorm, windstorm, fog and hurricanes, and heat and cold waves as spells (ADR-028). Snow lies on the land until it melts into the soil; hurricanes tear houses down. |
| 17 | Seasons | Implemented (optional) | A ten-minute year of four seasons, chosen with the world (ADR-028). They change temperature, rainfall, the harvest, fire risk, snow, the wish to leave, sickness (§17's disease) and stores spoiling, and bring heat waves, cold waves and hurricanes. Dry, wet and normal spells go on within them. |
| 18 | Ecology | Implemented | Regions carry temperature (shifted by the season), moisture, fertility (soil quality), vegetation, soil water, water availability from rivers and lakes, scorch and harm from fires (fire damage), wildlife, pollution and scars; biodiversity is derived (ADR-028). Fire, flood, quakes and lightning harm the land; repeated harm scars it for good; a god of Nature heals scars and Regrowth heals harm. Villages hunt the wildlife, and crowded villages pollute. Vegetation itself is fixed. |
| 19 | Natural disasters | Implemented | All thirteen, as processes: earthquakes, wildfires and floods (ADR-014, ADR-022); drought (Milestone 7); hurricanes and extreme winters as cold waves (ADR-028); and tsunamis, tornadoes, the volcano's eruptions, landslides, meteors, crop disease (blight) and locust swarms (ADR-029). |
| 20 | Earthquake example | Implemented | Shaking, structural checks, collapse, landslides whose rubble blocks the way (ADR-029), fire, panic (fear and doubt), religious interpretation in its simplest form, god intervention, and survivors leaving their ruins (migration). |
| 21 | Disaster preparation | Implemented | Granaries, cisterns (the reservoirs), firebreaks, shelters, levees, hospitals (ADR-023), and dams, sea walls, irrigation and evacuation routes (ADR-029), raised under a village's priorities where they help, and architecture and engineering, spare a village part of a disaster. |
| 94 | Physics and disasters | Implemented | Each village's houses have structural integrity, cracked by every blow that leaves them standing and mended over minutes, and earthquake, fire and flood resistance; the simulation decides what falls. Fallen houses and strikes throw planks, stones and thatch as Jolt rigid bodies onto the terrain, shown only (ADR-014, ADR-048). |
| 114 | MVP nature | Implemented | Rain, storm, drought, wildfire and earthquake, and floods since part 2. |

### Politics and history

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 37 | Divine politics | Implemented | Directional, multi-dimensional relations: a god may respect, fear and resent another while allied with it. |
| 38 | Diplomacy | Implemented | Alliance, peace and war, and all eleven of §38's treaties with terms and conditions (a target, a payer and an amount), each with its effect (ADR-025). |
| 39 | Secret agreements | Implemented | Treaties may be secret: known to their parties only, kept from other players by the host, revealed when invoked or by leaks; the god they were aimed at remembers the plot (ADR-025). |
| 40 | Betrayal | Implemented | Any alliance or treaty can be broken; it costs the victim's trust, every knowing god's trust and the breaker's word in future treaties, and is long remembered; secret plots against an ally count as betrayal. |
| 41 | Favours | Implemented | Rescuing another god's followers in great need makes it owe a favour, called in for Divine Power, war support, political backing, territory or miracle assistance (a promise to keep); refusals and broken promises are remembered (ADR-025). |
| 42 | Divine Council | Implemented | Summoned by betrayals, dominance and disasters, and every five minutes on a tabled motion; votes to condemn, protect sacred land, prohibit a practice, coordinate disaster response, declare pantheon-wide war or establish neutral territory, in public or secret ballots, traded through backing promises (ADR-025). |
| 43 | Information and deception | Implemented | What a god knows of another comes from allies, treaties, its followers among the other's people (spies and defectors), its Creature's scouting and the Knowledge domain; players see rivals' followings, power and plotted betrayal only as far as they know, secret treaties stay hidden, and knowledge traces covert acts (ADR-025, ADR-026). Prophets and the map's fog are not modelled. |
| 45 | Proxy wars | Implemented | Veiled miracles (secret disasters and anonymous gifts), inciting rebels and spreading propaganda, each traceable by the wronged god (ADR-026). |
| 46 | Mortal holy wars | Implemented | Militarist villages declare holy war on neighbours of another faith of their own will; raids kill and convert; their god may encourage, discourage (at a cost in love) or ignore (ADR-026). |
| 44 | War between gods | Implemented | Declared war makes the enemy's followers resist conversion and draws Creatures and AI miracles to it. Wars have goals (conquest, holy site, tribute, humiliation) and scores from conversions, Creature defeats, sites taken, holy war raids, refugees and (least) strikes, and end in peace dictated by the goal (ADR-026). |
| 113 | MVP diplomacy | Implemented | The five statuses and the four actions, plus accepting and declining; the AI remembers betrayal. |
| 50 | World history | Implemented | The world records its significant moments as "Year 12 — The Shaking of Elar" (ADR-020); a year is a simulated minute. Civilizations do not refer to them yet. |
| 51–52 | Sacred locations, world events | Implemented | Four holy sites found in every world (a peak, a grove, a spring, standing stones): kept by influence, giving power, influence and a gift, fought over and remembered (ADR-021); the sacred peak stands on the volcano when the island has one (ADR-038). Every world event of §52: the plague, global drought, the ancient beast, the volcano, meteors, mass migration, religious reformation, world war, eclipses and the magical anomaly, from the world's state and the seed (ADR-029, ADR-034, ADR-035), with the plague and droughts bringing the gods to cooperate. |
| 53 | Victory conditions | Implemented | Divine Ascendancy (§115's MVP form, at 60% rather than about 70%) and Pantheon Victory: a god counts its strongest direct allies' followers with its own and wins with them, at most `PantheonMaxGods` gods: two on a small island, three on a large one, four on a huge or vast one (ADR-015, ADR-020, ADR-027). Conquest (every rival outside its alliances below 5% of all followers), cultural victory (leading three villages in four while a rival keeps 5%) and a sandbox, chosen with the world on the start screen or with `--victory` (ADR-027). |
| 54 | Alliance endgame | Implemented | Near a joint victory each AI ally weighs staying loyal to share the win against breaking away to seek personal ascendancy, by its `Treachery` and its lead over its ally. Absorbing an ally's believers is a betrayal that turns a share of the ally's followers where the god leads (ADR-025); players and, since part 20, AI gods take it, the AI when it would betray anyway and absorbing gains at least 0.5% of all believers (ADR-040). |
| 91 | Event architecture | Implemented | Systems and authorities raise domain events, and alerts, the history, the council and effects read them, without calling each other. §91's examples have their events: `EarthquakeStruck`, `SettlementDamaged` (with its dead), `MigrationStarted` (refugees), `MiracleCast` and `OfferingGiven` (aid), `FollowingChanged` (belief) and `DiplomacyChanged` (opinion and status). |
| 92 | History system | Implemented | `HistoryRecorder` weighs every event and keeps only the significant ones by `history.json`'s thresholds, without repeats, up to a capacity; saved and replicated (ADR-020). |

### Player experience

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 55 | Player interaction | Partial | Miracles are cast with a click (ADR-009). The Divine Hand picks up villagers, rocks, trees and the Creature, carries villagers and rescues them from danger, plants trees and throws (ADR-030), pats or swats the Creature and presses when casting (ADR-010), and takes offerings from its villages' stores and gives them to villages in need or the Creature (ADR-039). A thrown rock or tree lands and rolls as a rigid body among the debris it throws up (ADR-048); lifting and carrying are shown without physics. |
| 56 | Camera | Implemented | WASD, edge movement, drag, zoom, rotate, focus on each settlement in turn and on the Creature, three zoom bands that reach further over a larger island (ADR-027), and a strategic overview of the whole island on backtick (ADR-031). |
| 57 | User interface | Implemented | HUD with Divine Power, income, followers, rank, the rivals with their status, the race for victory, the miracle bar and hint, the Creature card (with its bonds, paths and attributes) and alerts; panels for Settlements (N), Diplomacy (P), History (H) and Domains with the Divine Acts (G), and the Book of the World (Tab) with the World, Religion, Miracles and Creature pages (ADR-031); a start screen, pause menu, settings page and end screen. One book for four pages keeps the screen clear, as §57 asks. |
| 58 | Map overlays | Implemented | Influence, religion, faith, population, food, the land's life (ecology), weather, danger, disaster risk (fire, flood, landslide, the volcano), migration, culture and political control (blocs, wars darkened), cycled with O (ADR-031); the F3 debug map adds every region statistic. |
| 59 | Alert system | Implemented | Every example of §59: starving followers, proposals, the Creature's deeds (attacks on villages included), a hurricane approaching a coast (ADR-041) and rivals converting the player's followers. Besides: settlements gained or lost, ranks, dominance and victory, diplomacy, weather and disasters, hazards, great events, migration, Creatures' meetings and fights, gods met, offerings and more. Alerts are filtered to what concerns the player, not repeated within 30 simulated seconds, and the most urgent are kept. |
| 60 | Audio | Implemented | Music in three moods (calm, tension in war or danger, triumph in dominion) and the sounds of miracles, lightning, thunder, earthquakes, fire, Creatures and news, synthesized by the game; events are heard where they happen, distant thunder and roars faintly (ADR-021). |
| 61 | Visual direction | Implemented | Stylised terrain textured by biome, slope and height (grass, forest floor, earth, rock, sand, snow, moss, mud, ash) under ACES light with ambient occlusion and glow (ADR-051), sea, huts, fields, people of varied looks animated on the GPU (ADR-049), houses built by land, size and god with temples in their god's style and palisades or stone walls with towers (ADR-052), seven tree species by biome and grass, flowers, bushes, stones and reeds near the camera, swaying in the weather's wind (ADR-053), herds and flocks where the land's wildlife lives and bridges over rivers (ADR-054), Creatures whose faces and tails show what they feel and whose bodies show their nature, a signature look for every miracle (rain from a cloud, harvest and holy pillars, healing and shield domes, flames, a quake's dust, a fountain, a dark swirl, a wheeling storm, a god's strike) (ADR-055), soft clouds, rain, lightning and storm-dimmed light, flames, smoke, ash, rubble and shield domes, and god colours on labels and overlays. Villages take their god's style: gardens for a loved nature god, dark obelisks and braziers for a feared war god, white columns for a god of learning (ADR-042). Everything is built in code. Everything §61 lists is met: readable from high altitude, expressive Creatures, strong miracles, distinct biomes, recognisable civilisations and scalable performance (ADR-050). |

### Platforms, technology and performance

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 62 | Supported platforms | Partial | Export presets for Windows x64, macOS (one universal app for Apple Silicon and Intel) and Linux x64 (ADR-031). The macOS build was exported and played (114 FPS); the Windows and Linux builds export but have not been run, and Windows has never been run. Release signing and notarization need the owner's Apple account. |
| 63 | Core technology stack | Partial | In use: Godot .NET 4.7.2, C#, Terrain3D, FastNoiseLite and MultiMesh. Jolt runs the debris (ADR-048). Navigation is a region graph in the core rather than Godot's navigation meshes (ADR-010). LimboAI is not used. Utility AI drives the Creature and the gods. No Blender assets. |
| 64 | Terrain destruction | Implemented | As §64 asks for the initial game: earthquakes leave visible cracks and shake the camera, destroy buildings, set off landslides whose rubble blocks the way, and harm the land; meteors leave craters (ADR-029). The terrain is not remeshed. |
| 88 | Threading model | Followed | Everything runs on the main thread. |
| 85 | Navigation | Implemented | Strategic: A* over land regions, smoothed (the Creature, migration groups, prophets and followed villagers). Local: each village shown up close bakes a navigation grid from its layout (huts, ruins, the building site, the shrine, the temple and water blocked), and its figures walk round them on A* ways pulled taut (ADR-046). The core grid stands in for Recast, as ADR-010 chose core navigation. The followed villager, prophets and the Creature still walk straight within villages. |
| 89 | Update frequencies | Implemented | Weather and the seasons step at 2 Hz (§89: 1–2 Hz); settlements, belief and diplomacy at 1 Hz (1 Hz, 0.5–1 Hz); the god AI evaluates every 3 s (0.2–0.5 Hz) and plans every 30 s; the Creature decides at 5 Hz (5–10 Hz) and moves every tick; history is event-driven. Ecology steps at 1 Hz, above §89's 0.1–0.5 Hz, and citizens' poses follow the frame from simulation time, both cheap enough not to matter. |
| 104 | Performance testing | Implemented | A repeatable stress scenario (`--stress=on`, ADR-044): the vast island with 50 villages of 2,000 people (100,000), 8 gods and their 8 Creatures, 20 weather cells and an earthquake, a fire and a flood at once, with up to 1,000 figures a village drawn (7,000 seen). F1 and unattended runs report frame time, the simulation's share of a frame and a tick's time, ticks per second, memory, the engine's objects, nodes and draw calls, the citizens drawn and a save's write and read times; the host's F1 shows bandwidth. Measured on an M3 Pro: 100 FPS, 1.5 ms a tick, 1.3 GB resident at most, a 10 MB save in 130 ms, 144 KB/s to a joined player. Headless probes measure ticks, traffic, saves and loads. |
| 105 | Target performance | Measured (Mac) | 1080p on an Apple M3 Pro: 150 FPS uncapped (6.7 ms) at x16 thirty minutes into a game, Terrain3D or fallback. At 1600 × 900 (vsync), a Huge world holds about 120 FPS from 2.6 km up ten minutes in (part 7). The stress world holds 100 FPS on the Mac (ADR-044). The simulation keeps to a CPU budget a frame that the player sets (6 ms by default; ADR-056): the stress world at x16 spent 30 ms a frame on ticks before. Graphics options scale everything §105 lists: shadows, vegetation, particles, visible population (citizens shown) and terrain quality, besides render scale with FSR and the lighting effects. Not measured on Windows or Linux. |

### Architecture, saves and process

| § | Topic | Status | Notes |
| --- | --- | --- | --- |
| 65 | Local-first architecture | Implemented | Fully offline. |
| 66 | Single-player architecture | Implemented | The local host (`GameSession`) runs the world with one player and three, five or seven AI gods by the world's size. |
| 67 | Multiplayer architecture | Implemented | Host-authoritative (ADR-003, ADR-018): the host's computer runs the canonical world; players hold replicas that never own state. |
| 68 | Networking technology | Implemented | Godot ENet over UDP, LAN and direct IP, UPnP when asked. Our own messages travel as packets rather than Godot RPCs, so the protocol stays engine-agnostic and testable. |
| 69 | Optional Steam networking | Not started (decision) | Optional and needs the Steamworks SDK; `INetworkTransport` is the seam for it beside ENet (ADR-021). |
| 71 | Host disconnect | Implemented | The session ends for every player, whose replicas pause with the reason; the host's world is saved to `user://saves/hosted.json`. |
| 72 | Future host migration | Not started | Not for the MVP. |
| 73 | Network authority | Implemented | Miracle, Creature and diplomatic commands and refugee policies are intents, the host's and the AI's as well as joined players'. `MiracleCaster`, `CreatureAuthority`, `DiplomacyAuthority` and `MigrationAuthority` check them (power, location, influence, cooldown, rank, authorisation, reachability, diplomatic status and proposals) before anything changes. A joined player's intents act only for its own god, and it receives the verdict. |
| 95–96 | Network replication and frequency | Implemented | Events as they are raised; each section of the world at its own rate when it changed (ADR-019): Creature motion high and by interest, gods medium, settlements, weather and disasters low, diplomacy by event; whole snapshots on joining and every 20 s. Regions go out only when a value has moved past a tolerance, rounded to thousandths (ADR-056). About 13 KB/s per player; 60 KB/s on the stress world (144 before ADR-056). |
| 97 | Client prediction | Implemented | Camera, UI and the hand are local. Miracle results wait for the host. A replica predicts the world between snapshots by running the same simulation, and never applies a player's intent itself. |
| 98 | Cheating model | Implemented (as specified) | The host is trusted; no anti-cheat. |
| 70 | No required server infrastructure | Implemented | |
| 75 | Save architecture | Implemented | A save is versioned JSON of `WorldState` on the host: the seed and generation settings, the tick (the game's time, its years and seasons derived from it), the random state, regions with the land's life (ecology), settlements with their population, society and technology, gods, beliefs, Creatures, diplomacy, weather, disasters, migration, the great events (world events), the history and the holy sites (sacred locations). Named slots, an autosave and the quicksave. |
| 76, 99 | Save principle, persistence format | Implemented | `WorldState` is serialized as human-readable JSON with `SaveVersion` 20 and migrations from versions 1 to 19, each tested against a save written by the previous build. |
| 77–78 | Core rule, layered architecture | Implemented | The core projects never reference Godot (tested). |
| 79 | Simulation systems | Implemented | Weather, seasons, water, disasters, hazards, great events (world events), the beast, ecology (region), settlements and population, resources and roads, society and technology, culture, belief and notables, victory, diplomacy and politics, conflict, migration, Creatures, holy sites, domains and the god AI, with the history recorded from their events, and the miracle, Creature, diplomacy, migration, society, council, conflict and hand authorities. |
| 90 | Core data models | Implemented | `GodState` (rank, power, domains, personality, Creature, conduct and traits for reputation, known gods; followers derived), `SettlementState` (population, food, water, wealth, health, safety and belief, love, fear and doubt per god), `CreatureState` (species, level, experience, health, loyalty, morality, eight traits, relationships and learned preferences; strength and intelligence derived), `RegionState` (biome, elevation, temperature, moisture, fertility, vegetation, wildlife, pollution; weather from the cells), `GodRelation` and `GodStance` with treaties and memory for §90's DiplomacyRelation, and `GreatEventState` for §90's WorldEvent (kind, place, peak, start and end, the god concerned). |
| 100 | Repository structure | Implemented, with differences | As recommended, plus `World/` and `Miracles/` in `Pantheon.Domain`, `game/Data/`, `game/addons/`, `.docs/` and `AGENTS.md`. |
| 101 | Architecture decision records | Implemented | ADR-001 to ADR-028. |
| 102–103 | Testing, deterministic testing | Implemented | Every rule has tests, including §102's "Fear decreases after prolonged safety" and "God cannot cast miracle without enough power". Randomness is seeded and checked. |
| 106 | Debug tools | Implemented | Influence, belief, population, region boundaries, simulation tier, the frame rate, the Creature view (current behaviour, personality, needs, learned weights, memories and its options with their scores), and the god AI view (current goal, top candidate actions, utility scores and their reasons, and relationship reasoning: the diplomatic moves weighed with their factors), weather cells (spell, wind, the strongest cells, soil and rain under the cursor, a settlement's water), disasters (earthquakes, the fiercest fires, a settlement's dangers), migration routes (groups on the road, refugee policies with the AI's reasons, a settlement's pressure to leave, the Migration overlay), and Creatures' relationships (views, encounters, combat skill, scars, power and how the player's Creature would meet the nearest one; K and `--meet` bring two together). The navmesh as the Navigation overlay (walkable land, roads, rubble and every walker's way), network authority as the Authority overlay and F1's lines on who decides and how fresh each section is, and performance metrics in F1 (ADR-044). |
| 107 | Content data | Implemented | Every balance value is a data file parsed strictly: world generation and biomes, settlement, divine and society rules, miracles (with §107's id, name, domain, cost, cooldown, radius, influence and effects), the gods, Creatures and their species and temperament, the AI's personalities and planning, diplomacy, weather, disasters and great events, migration, history, domains and holy sites, water, ecology and world sizes, and the presentation's colours and views. |
| 108 | MVP world | Implemented | One map, three land biomes, 8 settlements, four gods (1 human, 3 AI) and four Creatures. |
| 131 | Non-goals | Respected | |
| 132 | Validation priorities | Partly testable | "Is converting populations fun?", "Is raising the Creature fun?", "Are rival gods interesting?" and "Does diplomacy generate stories?" and "Do disasters create meaningful decisions?" can be tried now: rain or shield, rebuild trust after a quake, shake a rival, side with one rival against another, take in or turn away a rival's refugees. |
| 134 | Coding agent rules | Followed | `AGENTS.md`; `ArchitectureRulesTests` checks the mechanical ones. |

### Sections that state intent

- §1–4 (vision, originality, pillars, core loop): names, art and interface are original. The core
  loop runs from "Observe world" through "Expand influence", with the Creature, rival gods
  (cooperating or competing), the weather and disasters in it. Mortals have agency (§3.3): they
  worship, doubt, convert, ignore gods, migrate and break away in schisms. The world keeps its
  history; scripted world events are not built.
- §133, §135 and §136: the split between simulation and presentation holds for gods, belief,
  miracles, the Creature, diplomacy, weather, disasters and migration, with `GameSession` as the
  local authority. Mortals remember what you did (belief remembers help and harm, and migrants
  leave gods they fear or doubt), the Creature remembers how it was treated, and "Gods remember
  what you did" holds through diplomatic memory, including what you did with their refugees.
  "Nature reacts" through weather and disasters, and the world keeps its history (ADR-020).

## Architecture decision records

| ADR | Decision | Status |
| --- | --- | --- |
| [ADR-001](ADR/ADR-001-godot-dotnet-pinned-version.md) | Godot 4.x .NET, pinned to 4.7.2-stable | Accepted |
| [ADR-002](ADR/ADR-002-csharp-primary-language.md) | C# as the primary language | Accepted |
| [ADR-003](ADR/ADR-003-host-authoritative-enet-networking.md) | Host-authoritative networking over ENet, no lockstep | Accepted; implemented from Milestone 12 (ADR-018) |
| [ADR-004](ADR/ADR-004-statistical-population-simulation-with-tiering.md) | Statistical population simulation with tiering | Accepted; amended by ADR-008 and ADR-032 |
| [ADR-005](ADR/ADR-005-terrain3d-for-terrain.md) | Terrain3D for terrain rendering | Accepted; amended by ADR-007 |
| [ADR-006](ADR/ADR-006-simulation-state-separate-from-scenes.md) | Simulation state is separate from scenes | Accepted |
| [ADR-007](ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md) | Patch Terrain3D's generated shader and report why the fallback is used | Accepted; amends ADR-005 |
| [ADR-008](ADR/ADR-008-settlement-step-and-representative-citizens.md) | Settlement step cadence and representative citizens | Accepted; amends ADR-004; amended by ADR-032 |
| [ADR-009](ADR/ADR-009-belief-influence-and-miracles.md) | Belief, influence and miracles (Milestone 3) | Accepted |
| [ADR-010](ADR/ADR-010-creature-simulation-and-learning.md) | The Creature: core simulation, learned utility AI and the hand (Milestone 4) | Accepted |
| [ADR-011](ADR/ADR-011-rival-gods-utility-ai-and-victory.md) | Rival gods: utility AI, conversion between gods and victory (Milestone 5) | Accepted; amended by ADR-015 |
| [ADR-012](ADR/ADR-012-diplomacy-relations-status-and-memory.md) | Diplomacy: directional relations, shared status and remembered acts (Milestone 6) | Accepted |
| [ADR-013](ADR/ADR-013-weather-cells-and-soil-water.md) | Weather: moving cells, spells and the water in the soil (Milestone 7) | Accepted |
| [ADR-014](ADR/ADR-014-disasters-as-processes.md) | Disasters as processes: wildfires in regions, earthquakes that shake, ruins to rebuild (Milestone 8) | Accepted |
| [ADR-015](ADR/ADR-015-four-gods-coalitions-and-pantheon-victory.md) | Four gods: coalitions, allies' memory and pantheon victory (Milestone 9) | Accepted; amends ADR-011 |
| [ADR-016](ADR/ADR-016-migration-groups-and-refugee-policies.md) | Migration as groups on the road, and gods' refugee policies (Milestone 10) | Accepted |
| [ADR-017](ADR/ADR-017-creature-relationships-and-combat.md) | Creature relationships, meetings and combat (Milestone 11) | Accepted |
| [ADR-018](ADR/ADR-018-multiplayer-host-snapshots-and-intents.md) | Multiplayer sessions: a host's world, players' intents and replicas kept by snapshots (Milestone 12) | Accepted; amended by ADR-019 |
| [ADR-019](ADR/ADR-019-replication-by-section-and-rate.md) | Replication by section and rate, with interest and event-based diplomacy (Milestone 13) | Accepted; amends ADR-018; amended by ADR-024 and ADR-056 |
| [ADR-020](ADR/ADR-020-vertical-slice-history-flow-and-eligible-pantheons.md) | The vertical slice: world history, the game's flow, save slots and eligible pantheons (Milestone 14) | Accepted; amends ADR-015 |
| [ADR-021](ADR/ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) | Polish and expansion: domains, holy sites, schisms, Creature species, settings and synthesized audio (Milestone 15) | Accepted |
| [ADR-022](ADR/ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) | Rivers, lakes, floods and drinking water, with the Ocean and Justice domains (Milestone 15, part 2) | Accepted |
| [ADR-023](ADR/ADR-023-settlement-society-priorities-buildings-and-technology.md) | Settlements' society: safety, happiness, order, wealth, learning, priorities, buildings, technology and expansion (Milestone 15, part 3) | Accepted |
| [ADR-024](ADR/ADR-024-doctrines-secularism-dependency-resentment-and-traits.md) | Culture: doctrines, secularism, dependency, resentment, missionaries, revolts and gods' traits (Milestone 15, part 4) | Accepted; amends ADR-019 |
| [ADR-025](ADR/ADR-025-treaties-secrets-favours-and-the-divine-council.md) | Treaties, secret agreements, favours and the Divine Council (Milestone 15, part 5) | Accepted |
| [ADR-026](ADR/ADR-026-war-goals-intelligence-proxy-wars-and-holy-wars.md) | War goals, intelligence, proxy wars and mortal holy wars (Milestone 15, part 6) | Accepted |
| [ADR-027](ADR/ADR-027-world-sizes-victories-and-large-worlds.md) | World sizes, conquest, cultural and sandbox victories, and large worlds (Milestone 15, part 7) | Accepted |
| [ADR-028](ADR/ADR-028-seasons-extremes-and-ecology.md) | Seasons, weather extremes and the land's life (Milestone 15, part 8) | Accepted |
| [ADR-029](ADR/ADR-029-hazards-preparation-and-marks-on-the-land.md) | Hazards, disaster preparation and marks on the land (Milestone 15, part 9) | Accepted |
| [ADR-030](ADR/ADR-030-creature-paths-divine-hand-and-divine-acts.md) | Creatures' paths, the Divine Hand and the Divine Acts (Milestone 15, part 10) | Accepted |
| [ADR-031](ADR/ADR-031-book-of-the-world-overlays-overview-and-exports.md) | The Book of the World, the last overlays, the strategic overview and export presets (Milestone 15, part 11) | Accepted |
| [ADR-032](ADR/ADR-032-prophets-elders-and-temple-influence.md) | Prophets and elders as Tier 3 individuals, and temples and prophets as sources of influence (Milestone 15, part 12) | Accepted (amends ADR-004 and ADR-008) |
| [ADR-033](ADR/ADR-033-resources-gathering-forests-and-roads.md) | Wood and stone, gathering, forests and roads (Milestone 15, part 13) | Accepted |
| [ADR-034](ADR/ADR-034-great-events-plague-eclipses-reformations-and-world-wars.md) | Great events: the plague, eclipses, reformations, world wars, great droughts and great migrations (Milestone 15, part 14) | Accepted |
| [ADR-035](ADR/ADR-035-the-ancient-beast-and-the-magical-anomaly.md) | The ancient beast and the magical anomaly (Milestone 15, part 15) | Accepted |
| [ADR-036](ADR/ADR-036-known-gods-and-the-gods-long-term-plans.md) | Known gods and the gods' long-term plans (Milestone 15, part 16) | Accepted |
| [ADR-037](ADR/ADR-037-creature-temperament.md) | Creature temperament: courage, protectiveness, jealousy and playfulness (Milestone 15, part 17) | Accepted |
| [ADR-038](ADR/ADR-038-the-biomes-of-the-world.md) | The biomes of the world (Milestone 15, part 18) | Accepted |
| [ADR-039](ADR/ADR-039-offerings.md) | Offerings (Milestone 15, part 19) | Accepted |
| [ADR-040](ADR/ADR-040-ai-gods-absorb-their-allies.md) | AI gods absorb their allies (Milestone 15, part 20) | Accepted |
| [ADR-041](ADR/ADR-041-lightning-fires-and-hurricane-warnings.md) | Lightning's fires and hurricane warnings (Milestone 15, part 20) | Accepted |
| [ADR-042](ADR/ADR-042-religions-look-on-villages.md) | Religion's look on villages (Milestone 15, part 21) | Accepted |
| [ADR-043](ADR/ADR-043-the-selected-villager.md) | The selected villager (Milestone 15, part 22) | Accepted |
| [ADR-044](ADR/ADR-044-stress-scenario-and-debug-overlays.md) | The stress scenario and the last debug overlays (Milestone 15, part 23) | Accepted |
| [ADR-045](ADR/ADR-045-divine-acts-that-reshape-the-land.md) | Divine Acts that reshape the land (Milestone 15, part 24) | Accepted |
| [ADR-046](ADR/ADR-046-local-navigation.md) | Local navigation (Milestone 15, part 25) | Accepted |
| [ADR-047](ADR/ADR-047-creature-state-machine-and-behaviour-trees.md) | The Creature's state machine and behaviour trees (Milestone 15, part 26) | Accepted |
| [ADR-048](ADR/ADR-048-structural-integrity-and-debris.md) | Structural integrity, flood resistance and debris (Milestone 15, part 27) | Accepted |
| [ADR-049](ADR/ADR-049-people-that-look-and-move-like-people.md) | People that look and move like people (Milestone 15, part 28) | Accepted |
| [ADR-050](ADR/ADR-050-world-streaming.md) | World streaming (Milestone 15, part 29) | Accepted |
| [ADR-051](ADR/ADR-051-ground-textures-and-light.md) | Ground textures and light (Milestone 15, part 30) | Accepted |
| [ADR-052](ADR/ADR-052-village-architecture.md) | Village architecture (Milestone 15, part 31) | Accepted |
| [ADR-053](ADR/ADR-053-trees-ground-cover-and-wind.md) | Trees, ground cover and wind (Milestone 15, part 32) | Accepted |
| [ADR-054](ADR/ADR-054-wildlife-and-bridges.md) | Wildlife and bridges (Milestone 15, part 33) | Accepted |
| [ADR-055](ADR/ADR-055-expressive-creatures-and-miracle-looks.md) | Expressive Creatures and miracle looks (Milestone 15, part 34) | Accepted |
| [ADR-056](ADR/ADR-056-cpu-budget-graphics-scaling-and-region-tolerance.md) | The simulation's CPU budget, graphics scaling and regions within a tolerance (Milestone 15, part 35) | Accepted (amends ADR-019) |

No decision has been reversed.

## Tests

The xUnit suite in `src/Pantheon.Tests` has 1529 tests, and they all pass headless in about twelve
seconds, without Godot (`dotnet test Pantheon.sln`).

| Area | Test classes | M2 | M3 | M4 | M5 | M6 | M7 | M8 | M9 | M10 | M11 | M12 | M13 | M14 | M15 | 15.2 | 15.3 | 15.4 | 15.5 | 15.6 | 15.7 | 15.8 | 15.9 | 15.10 | 15.11 | 15.12 | 15.13 | 15.14 | 15.15 | 15.16 | 15.17 | 15.18 | 15.19 | 15.20 | 15.21 | 15.22 | 15.23 | 15.24 | 15.25 | 15.26 | 15.27 | 15.28 | 15.29 | 15.30 | 15.31 | 15.32 | 15.33 | 15.34 | Now |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| World generation and determinism | `WorldGenerationTests`, `BiomeClassifierTests`, `WorldSeedTests`, `DeterminismTests`, `SimulationRandomTests`, `TerrainRaycasterTests` | 47 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 49 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 | 53 |
| Biomes and landforms | `LandformTests`, `BiomePresentationTests`, `GroundTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 12 | 12 | 12 | 12 | 12 | 12 |
| Settlement rules and step (Tier 1) | `SettlementEconomyTests`, `SettlementSystemTests`, `IntervalSystemTests` | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 | 43 |
| Belief, influence, miracles and rivals | `BeliefModelTests`, `BeliefSystemTests`, `InfluenceFieldTests`, `MiracleCasterTests`, `RivalGodTests` | 0 | 45 | 45 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 | 55 |
| Pantheon and politics | `PantheonTests`, `PoliticsTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 |
| Diplomacy | `DiplomacyTests` | 0 | 0 | 0 | 0 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 | 14 |
| Weather and water | `WeatherModelTests`, `WeatherSystemTests`, `WaterTests` | 0 | 0 | 0 | 0 | 0 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 |
| Disasters | `DisasterModelTests`, `DisasterSystemTests`, `ResponseTests`, `LightningFireTests` | 0 | 0 | 0 | 0 | 0 | 0 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 |
| Rivers, floods and drinking water | `HydrologyTests`, `WaterSystemTests`, `SettlementWaterTests`, `WaterMiracleTests`, `WaterPresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 |
| Society, priorities and technology | `SocietyModelTests`, `SocietySystemTests`, `PriorityTests`, `CivicTests`, `SocietyPresentationTests`, `SocietyMultiplayerTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 | 25 |
| Culture, doctrines and traits | `ConductTests`, `CultureModelTests`, `CultureSystemTests`, `WitnessCultureTests`, `CulturePresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 |
| Treaties, favours and the council | `TreatyTests`, `FavourTests`, `CouncilTests`, `TreatyAdvisorTests`, `PoliticsPresentationTests`, `PoliticsMultiplayerTests`, `AbsorbAiTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 41 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 44 |
| War and intrigue | `WarTests`, `CovertTests`, `HolyWarTests`, `ConflictPresentationTests`, `ConflictMultiplayerTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 |
| World sizes and victories | `VictoryKindTests`, `WorldSizeTests`, `VictoryPresentationTests`, `AiCostTests`, `LabelDeclutterTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 | 33 |
| Seasons, extremes and the land's life | `ClimateTests`, `EcologyTests`, `NaturePresentationTests`, `HurricaneWarningTests`, `WildlifeTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 30 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 36 | 36 | 36 |
| Hazards and preparation | `HazardModelTests`, `HazardSystemTests`, `LivingHazardTests`, `HazardPresentationTests`, `HazardPreparationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 | 37 |
| Structures and debris | `StructureTests`, `DebrisTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 |
| Paths, the hand and Divine Acts | `CallingTests`, `HandTests`, `OfferingTests`, `DivineActTests`, `PowerPresentationTests`, `LandTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 32 | 38 | 38 | 38 | 38 | 38 | 46 | 46 | 46 | 46 | 46 | 46 | 46 | 46 | 46 | 46 | 46 | 46 |
| The book, overlays, overview and exports | `BookTests`, `StateOverlayTests`, `ExportPresetTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 |
| Prophets, elders and temples | `NotableTests`, `NotablePresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 | 19 |
| Wood, stone and roads | `ResourceTests`, `RoadTests`, `ResourcePresentationTests`, `VegetationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 21 | 27 | 27 | 27 | 27 |
| Great events, the beast and the anomaly | `GreatEventTests`, `GreatEventPresentationTests`, `BeastTests`, `BeastPresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 13 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 | 24 |
| Known gods and plans | `AcquaintanceTests`, `PlannerTests`, `PlanPresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 |
| The selected villager | `VillagerTests`, `VillagerPresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 | 18 |
| Migration | `MigrationModelTests`, `MigrationSystemTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 | 23 |
| Creature relationships | `CreatureBondsTests`, `CreatureEncounterTests`, `CreatureRivalryAiTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 | 28 |
| Creature temperament | `TemperamentTests`, `TemperamentPresentationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 |
| Multiplayer | `ProtocolTests`, `MultiplayerSessionTests`, `MultiplayerPresentationTests`, `MultiplayerReplicationTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 31 | 39 | 39 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 41 |
| History and the game's flow | `HistoryTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 |
| Domains and signature miracles | `DomainTests`, `SignatureMiracleTests`, `DomainPresentationTests`, `ExpansionContentTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 36 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 | 40 |
| Holy sites and schisms | `HolySiteTests`, `SchismTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 | 15 |
| Settings and sound | `GameSettingsTests`, `AudioTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 13 | 14 |
| The Creature and navigation | `CreatureModelTests`, `CreatureBehaviourTests`, `CreatureAuthorityTests`, `RegionPathfinderTests`, `CreatureTreeTests` | 0 | 0 | 27 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 29 | 35 | 35 | 35 | 35 | 35 | 35 | 35 | 35 | 35 | 35 |
| Representative citizens (Tier 2) | `RepresentativeRosterTests`, `CitizenRoutineTests`, `SettlementCrowdTests`, `TierSelectorTests`, `LocalNavTests`, `PeopleTests` | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 68 | 72 | 73 | 74 | 83 | 83 | 83 | 83 | 83 | 83 | 83 | 85 |
| Saves and migration | `SaveLoadTests`, `SaveMigrationTests`, `SaveMigrationV2Tests` to `V32Tests`, `DivineSaveTests` | 34 | 47 | 52 | 57 | 62 | 68 | 73 | 80 | 86 | 92 | 92 | 92 | 98 | 105 | 112 | 117 | 122 | 127 | 132 | 138 | 144 | 150 | 156 | 156 | 162 | 168 | 174 | 180 | 186 | 192 | 198 | 204 | 204 | 204 | 210 | 210 | 216 | 216 | 216 | 222 | 222 | 222 | 222 | 222 | 222 | 222 | 222 | 222 |
| Session, scheduler, commands, launch options | `GameSessionTests`, `SimulationSchedulerTests`, `SessionCommandsTests`, `LaunchOptionsTests`, `DivineSessionTests`, `CreatureSessionTests` | 38 | 51 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 60 | 62 |
| Camera | `GodCameraModelTests`, `EdgeScrollTests` | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 27 | 28 | 28 |
| Presentation, HUD, alerts and layout | `PresentationTests`, `SettlementLayoutTests`, `Terrain3DShaderPatchTests`, `DivinePresentationTests`, `RivalPresentationTests`, `DiplomacyPresentationTests`, `WeatherPresentationTests`, `DisasterPresentationTests`, `PantheonPresentationTests`, `MigrationPresentationTests`, `CreatureRelationsPresentationTests`, `ReligiousStyleTests`, `ArchitectureTests`, `ExpressionTests` | 30 | 45 | 45 | 51 | 57 | 74 | 83 | 89 | 97 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 104 | 108 | 108 | 108 | 108 | 108 | 108 | 108 | 108 | 108 | 108 | 117 | 117 | 117 | 123 | 123 |
| Debug readouts and region map | `DebugReadoutTests`, `RegionDebugMapTests` | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 |
| Stress scenario and performance tools | `PerformanceToolTests`, `StreamingTests` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 6 | 6 | 6 | 6 | 6 | 6 | 10 | 10 | 10 | 10 | 10 | 10 | 10 |
| Content parsing | `ContentTests` | 11 | 18 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 22 | 27 | 30 | 30 | 30 | 30 | 30 | 34 | 37 | 37 | 41 | 44 | 47 | 47 | 47 | 47 | 50 | 50 | 50 | 50 | 50 | 52 | 52 | 52 | 52 | 52 | 52 | 52 | 52 | 52 |
| Architecture rules | `ArchitectureRulesTests` | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 11 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 | 12 |
| **Total** |  | **331** | **426** | **471** | **494** | **519** | **574** | **615** | **646** | **683** | **724** | **756** | **764** | **781** | **853** | **897** | **927** | **965** | **1011** | **1040** | **1084** | **1123** | **1166** | **1204** | **1215** | **1240** | **1271** | **1293** | **1310** | **1338** | **1359** | **1379** | **1391** | **1397** | **1401** | **1428** | **1434** | **1448** | **1452** | **1459** | **1479** | **1488** | **1492** | **1497** | **1506** | **1512** | **1516** | **1523** | **1529** |

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
  does nothing keeps only a few followers after ten minutes. The start screen chooses the size,
  which sets how many gods there are, but not which rivals; `gods.json` sets them.
- **Large worlds are long.** The largest island is 12 km across (§6 asks 10–20 km), with eight
  gods and forty-eight villages (ADR-027). Six or eight gods hold each other in check: in ten
  hour-long games with a passive player, four large worlds and one huge one found a winner. The
  terrain is built whole rather than streamed (§84), in 1.1 s for the 8 and 12 km islands. No
  trees or other vegetation are drawn.
- **Miracles.** All nine of §110's kinds (Rain since Milestone 7; Fire, Earthquake and Shield
  since Milestone 8; Water since Milestone 15, part 2) and thirteen domain miracles.
- **Domains.** All thirteen are offered, and a schism keeps the Old Faith rather than founding a
  new god.
- **Rivers.** On the 2 km island they run 200–800 m. River valleys are not a biome of their own,
  migrants walk through rivers, and thirst is rare (it needs a long drought away from water).

### Belief and power

- Fear's long-term cost is resentment, and revolts need a feared and resented god in an
  unsettled village, so they are rare among the AI gods and come to a cruel player.
- Religious infrastructure is the shrine, temples (since part 3) and the four holy sites.
- Divine Power has no upkeep or cap, and a god with a large following earns far more than five
  minor miracles can spend. Rival gods and more expensive miracles are the intended sinks.
- Settlements' stores grow large in good times, so food miracles mostly earn faith rather than
  love until droughts, storms or disasters bring hardship.
- The contest: with three AI gods, a player who only casts Food at the frontier holds 5–13% of the
  world after 36 minutes (seeds 1234, 42 and 7). A human has more tools (Lightning, Mass Healing,
  Rain, Shield, the Creature and alliances), but no human-like bot has measured them yet.
- The Guardian is the strongest AI personality and leads from any seat. Kindness converts better
  than intimidation under the belief rules (ADR-009). Its `Convert` weight was lowered so that it
  no longer wins every game (ADR-015), but love against fear deserves a broader look. Domains
  amplified it at first (ADR-021); Sanctuary and Gift of Life were tuned until the pace matched
  Milestone 14's again.
- Schisms are frequent, about 18 an hour across eight villages; nearly half return to their god.

### The population tiers

- Tier 2 citizens are presentation only (ADR-008). Worshippers are shown as villagers, and
  citizens walk in straight lines (no navigation, §85).

### Saves

- No GameTime: the world's clock is its tick.
- Events are not saved, so an alert raised just before a save does not repeat after a load.

### Platforms, performance and tooling

- Not run on Windows. The Windows and Linux builds export (ADR-031) but have not been run; the macOS
  build has.
- The macOS build is signed ad hoc: it runs where it was built, and Gatekeeper blocks it elsewhere
  until it is signed with a developer identity and notarized.
- No scripted stress scenarios beyond the headless probes (§104). Graphics options exist since
  Milestone 15.
- Frame times on the Mac were noisy during Milestone 15: Milestone 14's own build showed the same
  periodic 140 ms frames on the same day, so its 6.7 ms could not be reproduced for comparison.
- The Terrain3D shader patch (ADR-007) is still needed, and every start logs a harmless
  deprecation warning.

### The Creature

- Creatures fight one on one: no group fights, no defending a settlement as such (rivalry from
  attacks stands in for it), no escorting migrants, no permanent death, and no command to attack a
  chosen Creature; the player sends it and teaches it.
- The player's untaught Creature, never sent away by a god, levels fastest and wins most of its
  fights. The Tyrant's Creature, nearly always at war, collects 6–7 scars an hour.
- The hand lifts, carries and throws through the simulation; the flight of what it throws is drawn,
  not simulated by physics. Jolt is chosen but no physics gameplay exists.
- The Creature walks through houses and people: there is no local avoidance, and paths follow
  64 m regions.
- Four personality traits (§26); four species differ in toughness, strength, speed, learning and
  temperament; eight paths (§24) set by deeds and rewards.

### Weather

- Every state of §16, and optional seasons (§17, ADR-028). Snow does not slow travel, and grass
  and trees keep their colour through the year.
- Weather touches harvests, stores and building work, dry storms start fires, and rain swells the
  rivers until they flood (Milestone 15, part 2).
- Clouds fade out near the camera, so up close the weather shows as rain, dim light and
  lightning.

### Disasters

- Every disaster of §19 is modelled, but lava is drawn as the fire it leaves rather than its own
  flow, landslides do not change the terrain's height, and people do not catch diseases beyond the
  cold's and the ash's sickness.
- Fire spread is sensitive to dryness: the shipped numbers sit between runs where nothing spread
  and runs where the island burned (see the plan).
- Smoke is faint and fires read mostly as flames and glow.

### Migration

- Groups never found new settlements, cross water, meet danger on the road or avoid fires on
  their route, and nobody negotiates refugees' return or migration agreements (§31, §38).
- Migration slows a runaway leader by emptying war-torn and doubting villages. The shipped rate
  keeps victories near Milestone 9's (27.5 and 32 minutes); faster migration delayed them to
  44–52 minutes.
- Walkers stand for their group; they do not interact with citizens or Creatures.

### Diplomacy

- Treaties carry a term, a target, a payer and an amount; richer conditions are not modelled, and
  votes are traded only through promises of backing.
- AI gods seldom sign secret treaties among themselves, and favours are rare between them; both
  are open to the player.
- A god forms no view of another until the first act between them; the panel says so.
- Alliances are pairwise. A bloc of allies of allies wins together.

### Multiplayer

- Sections travel whole (a settlement's entry when one number changes); only regions go one by
  one, and only once they have changed past a tolerance (ADR-056). There are no field deltas,
  interest only covers Creature motion, and there are no per-player bandwidth limits (ADR-019).
- Sessions start from the start screen or launch options; there is no lobby or chat. Joining
  blocks the start for up to ten seconds.
- A player who drops cannot come back to the same god automatically, and there is no host
  migration (§72).
- Played on one Mac (two rendering instances, or a host and three headless players); not yet
  across computers, a router or Windows. Four rendering instances on one Mac starve each other.
- The start screen's host page makes a small world won by ascendancy; `--host` with
  `--world-size` and `--victory` hosts any other (part 7). Victories are the world's, not chosen
  per player.

### Interface

- Panels for diplomacy, history and domains, a start screen, a pause menu, a settings page and an
  end screen, and the Settlements panel since part 3; no World, Religion or Miracles panels
  (§57).
- The sound is synthesized: plain next to recorded or composed audio. The music has three moods.
- Joining a shared world from the menu reloads the game's scene with the host's address.
- With four active gods, about half of hour-long games end without a winner; the contest is close.

### Documents written before the specification

- [GAME_SPEC.md](GAME_SPEC.md) differs from the specification in places, and the specification
  wins: its roadmap numbers, a cap on Divine Power by total Faith (§9 has none, and none is
  implemented), contested influence weakening every god (§10 makes weaker gods pay more, as
  implemented), its own example miracles, and a campaign milestone that §131 rules out.

## Recommended next steps

Every milestone of the specification is implemented. What would make the game richer, in the
order of the validation priorities (§132) and what playing showed:

1. **Play the exported builds on Windows and Linux** at 1080p, and sign and notarize the macOS build
   with the owner's developer identity.
2. **Playtest with people.** The balance with active players depends on the domains they choose;
   the probes only cover bots.
3. **World streaming (§84)**: build the terrain in pieces near the camera, so islands of 10–20 km
   (§6) load quickly; offer the size on the host page.
4. **A followed villager (§80)**: the last of Tier 3's examples, which would need camera-driven
   promotion of a representative citizen (ADR-032 leaves it out).
5. **Steam networking (§69)** as a second `INetworkTransport`, only if the game ships on Steam.
6. **Recorded or composed audio** in place of the synthesized sounds, through the same director.
7. Report the Terrain3D shader defects upstream.
