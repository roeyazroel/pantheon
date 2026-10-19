# PANTHEON

An original 3D god game for Windows, macOS and Linux, built with Godot 4.7.2 (.NET) and C#. It
runs fully offline.

The owner's complete game design and technical architecture specification,
[docs/PANTHEON_SPEC.md](docs/PANTHEON_SPEC.md), is the source of truth for what PANTHEON is and
how it is built. Where any other document disagrees with it, the specification wins.
[docs/PROGRESS_SUMMARY.md](docs/PROGRESS_SUMMARY.md) sums up what is built so far against the
specification, section by section, with the known gaps and the recommended next steps.

**Milestone 1 (Foundation)** generates a seeded island with Grassland, Forest and Highland
biomes and the surrounding sea. A grid of 32 × 32 regions covers it, with one settlement.
You can explore it with a free god camera and save or load the world. A debug overlay shows
what the simulation is doing.

**Milestone 2 (Living Settlement)** makes the settlement live on its own. Its villagers farm
the land around it, eat, fall ill when food runs short, are born and die, and build new houses
when they get crowded. You never place houses. Near the camera, a few hundred representative
citizens walk between their homes, the fields, the building site and the shrine. A new house
goes up as housing grows and fields spread around the village.

**Milestone 3 (First God)** makes you a god. You are Ilyra, a Minor Spirit followed by most of
one village on an island of eight. Your followers worship at their shrines, and their worship
gives you Divine Power. Spend it on miracles: Food, Heal, Bless Crops, Lightning and, once you
are a Lesser God, Mass Healing. You can only work miracles where your influence reaches, and
your influence spreads with your followers. People who see a miracle change their minds about
you. Help them when they need it and they love you. Strike them and they fear you; fear converts
quickly but does not last. Neglect your followers in a famine and they doubt you and leave.

**Milestone 4 (Creature)** gives your god a Creature: Bramble the Mossback, a mossy giant born
beside your home village. It lives on its own. It forages, rests and wanders, plays with
villagers, helps them build, feeds them when they go hungry, or raids their stores and attacks
them. What it does depends on its needs, its personality and what it has learned. Teach it with
your hand: reward what it does (Z) and it does more of it, punish it (X) and it does less. What it
does changes what the villagers think of you. It grows to level 10.

**Milestone 5 (Rival God)** brings a rival: Vael the Tyrant, a god with its own village at the far
end of the island and its own Creature, Gristle. Vael plays by your rules. It casts the same
miracles, needs influence where it casts and pays more where yours is stronger, and it courts,
defends and frightens villages as its personality decides. People who lose faith in one god
turn to another. The first god to hold 60% of all followers for ten minutes wins.

**Milestone 6 (Diplomacy)** lets gods deal with each other. Vael remembers what you do: help for
its people in need, courting them, striking them, converting its villages, your Creature's
attacks. It grows to trust, fear, respect or despise you. Offer an alliance, declare war, ask for
peace or break your word. Vael weighs the same moves by its personality, answers your proposals,
and never forgets a betrayal.

**Milestone 7 (Weather)** brings weather to the island. Clouds blow in from the sea, rain and drift
away; storms bring wind and lightning. Dry spells parch the farmland until harvests fail, and storms
ruin stores and building work. A new miracle, Rain, calls a rain cloud wherever you need it, and
your rival uses it too.

**Milestone 8 (Disasters)** lets the land fight back. Dry fields catch fire and the flames spread
with the wind; earthquakes bring houses down. Villages fight the fires, rebuild their ruins and
fear their gods. Three new miracles let a god start a fire, shake the earth or shield a village.

**Milestone 9 (Four Gods)** fills the island with gods. Vael the Tyrant, Seren the Guardian and
Kaio the Trickster all compete for the same people, and deal with each other as they deal with
you. They gang up on whoever pulls ahead, make peace to face a common threat, remember who
wronged their allies, and betray when it pays. Allies win together: a bloc that holds 60% of all
belief for ten minutes shares the victory.

**Milestone 10 (Population Movement)** sets people on the move. Famine, ruins, fire, war, and fear
or doubt of their god drive villagers to pack up and walk to a better place, and many give up the
god they fled. Where they arrive, the god most followed there welcomes them, makes them convert or
turns them away, and their own god remembers it. You choose what your people do with newcomers.

**Milestone 11 (Creature Relationships)** lets the Creatures deal with each other. They meet,
play, greet, run and fight. Each remembers the others: who it plays with, who beat it, who
betrayed it. Allies' Creatures become friends, and friends refuse to fight when their gods go to
war. A beaten Creature is banished for a while with a scar, its loyalty shaken and its people's
faith dented, and its god holds a grudge.

**Milestone 12 (Multiplayer Foundation)** lets friends share a world. One player hosts; others join
over the network and each takes over a god the AI was playing. The host's computer runs the only
real world. Everything a joined player does goes to the host, which checks it and answers.

**Milestone 14 (Complete Vertical Slice)** makes it a game you can play from start to end: a start
screen, a guide for the first minutes, a pause menu with named saves and an autosave, the world's
history (H), and an end screen when a god or two allies win.

**Milestone 13 (Multiplayer Simulation)** lets four players share a world. Each part of the world
travels at its own pace: Creatures near your camera often, settlements and weather once a second,
diplomacy when something happens. Your own miracles show their cost and effect as soon as the host
agrees.

**Milestone 15 (Polish and Expansion)** deepens and finishes the game:

- **Domains.** Gods choose what they are gods of, from Storm, Life, War, Death, Trickery and six
  more. Each domain changes how a god plays and opens a miracle of its own: a Storm god carries
  clouds where it likes, and a Trickster turns rivals against each other unseen.
- **Holy sites.** A sacred peak, an ancient grove, a holy spring and a ring of standing stones
  go to the god whose influence holds them.
- **Schisms.** Followers who come to doubt their god break away to keep its Old Faith.
- **Creature species.** Four species, so your Creature can be tough, quick or clever.
- **Settings, music and sound.** All synthesized by the game itself.

**Milestone 15, part 2 (Water and Law)** gives the world fresh water and its gods the last two
domains:

- **Rivers and lakes.** Rivers run from the highlands to the sea and lakes fill the hollows.
  Villages settle beside them and draw water from them.
- **Floods.** Rain and wet soil swell the rivers. A river over its banks floods the land
  downstream: houses fall and people drown, but the silt enriches the fields.
- **Thirst.** In a long drought away from water the wells run dry. The Water miracle (key 0)
  fills them.
- **Ocean and Justice.** An Ocean god's villages fish the waters around them, and its Tidal Wave
  sends the sea over a low coast. A Justice god's villages keep its law, and its Judgement strikes
  only those who wronged it.

## Requirements

- [Godot 4.7.2-stable, .NET edition](https://godotengine.org/download/archive/4.7.2-stable/)
  (pinned by [ADR-001](docs/ADR/ADR-001-godot-dotnet-pinned-version.md)).
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
- A GPU with Vulkan support, or Metal on macOS (the project uses the Forward+ renderer).

On macOS with Homebrew: `brew install --cask dotnet-sdk@8 godot-mono` installs both; the Godot
executable is then `godot-mono`.

The Terrain3D GDExtension is vendored in `game/addons/terrain_3d`; nothing else needs to be
installed.

## Getting started

```bash
# Once after cloning: imports assets and registers the Terrain3D extension.
godot --headless --path game --import

dotnet build Pantheon.sln
godot --path game
```

`godot` stands for the Godot 4.7.2 .NET executable. You can also open `game/project.godot` in the
Godot .NET editor and press F5. Started without options, the game opens on its start screen: begin
a new world (its seed is shown, so you can share it), continue your newest save, load a world, or
host or join a shared world (joining reloads the game with the host's address). A short guide
walks you through your first minutes; Esc pauses. If you skip the import step, the first run cannot load
Terrain3D. In that case it uses the fallback terrain renderer and labels it in the debug overlay.

## Tests

```bash
dotnet test Pantheon.sln
```

The xUnit suite in `src/Pantheon.Tests` runs headless and does not need the Godot executable. It
covers:

- deterministic world generation, also across a save and load;
- save/load round trips, save versioning and the migration of Milestone 1 saves;
- the settlement rules: farming, eating, storage, starvation and recovery, births and deaths,
  growth up to housing, and building a house when crowded;
- the seeded RNG, the fixed-step tick scheduler, the settlement step cadence, the single-tick
  and settlement steps, and the famine and settler hotkeys;
- the representative citizens: tier selection, roles, homes, routines, smooth reroutes, and
  that showing them never changes the world;
- belief and miracles: drift, conversion, love against fear, worship and Divine Power, ranks,
  influence, every check on a cast (a god cannot cast without enough power), every miracle's
  effects and what witnesses make of them;
- the Creature: learning from rewards and punishments (spec §25's example), choices from needs,
  preferences and personality, walking around the sea, deeds and their effect on villages and
  belief, levels, banishment and the checks on commands;
- rival gods: conversion between gods, contested costs, the god AI's choices for each personality,
  dominance and victory;
- four gods: blocs of allies and their joint victory, allies' memory of wrongs, coalitions against
  a leader, fresh peace, betrayal near a joint victory and the new personalities;
- diplomacy: what each act does to a god's view of another and how long it is remembered,
  friendly and hostile relations, alliances, betrayal, war and peace through the authority's
  checks, what each status changes, and the AI's diplomatic choices with their reasons;
- weather: cells that rain, refill over the sea and drift on the wind, spells, soil water,
  droughts and storms, the Rain miracle and when the AI calls it, and that the weather is the same
  every time and across a save and load;
- disasters: earthquakes and their damage, wildfires that grow, spread with the wind and burn
  out, rain and firefighting, ruins and rebuilding, the homeless, fear of the gods, the Fire,
  Earthquake and Shield miracles and how the AI uses them;
- migration: what pushes people out and what keeps them, the wish to leave, groups setting out,
  the gods they give up, where they go, the road, welcome, conversion and refusal, and refugee
  policies for you and the AI;
- Creatures together: the views they hold of each other, meetings, play, greetings, flight,
  betrayal, fights and how they end (banishment, scars, loyalty, experience, mercy, draws), what
  gods teach their Creatures about others and when AI gods send them against an enemy's;
- the world's history (what is significant, how it reads), the end screen, the guide, the list of
  saves and that a pantheon wins as a god and its strongest ally;
- domains (slots by rank, the authority, cheaper miracles, every boon and every signature miracle,
  the AI's choices), holy sites (where they are found, claims, keepers' gains, the AI's
  Creatures), schisms (splits, the Old Faith's return or fading, betrayal doubt, sanctuaries),
  Creature species, the settings file, the synthesized sounds and music and what the world sounds
  like, and the migration of Milestone 14 saves;
- multiplayer: the messages and their encoding, a host and its players over an in-memory network
  (joining, gods, refusals, intents checked by the host, snapshots, events, leaving), each part of
  the world reaching players at its own rate, four players, coming back to one's god, and that only
  the host saves, keeps the clock or uses the debug tools;
- the HUD model, alerts, map overlays, the diplomacy panel, the weather's and disasters' looks and
  tools, migration's walkers, overlay and alerts, Creatures' bonds on the card, panel and debug
  panel, and the migration of Milestone 1 to 10 saves;
- camera maths, picking, content parsing, launch options, the settlement layout and other
  presentation helpers;
- the debug panel readouts and the F3 region debug map;
- the architecture rules: each core library references only the layers below it and never
  Godot, core code uses no unseeded randomness or clock, and every vendored GDExtension ships
  Windows, macOS and Linux libraries.

## Controls

| Action | Input |
| --- | --- |
| Pan | W A S D, arrow keys, or move the mouse to a screen edge |
| Grab and drag the ground | Hold the right mouse button and drag |
| Zoom (towards the cursor) | Mouse wheel, `=` / `-`, keypad `+` / `-` |
| Rotate | Q / E, or hold the middle mouse button and drag |
| Select a miracle (again to deselect) | 1–9 and 0, or click it in the miracle bar |
| Select your domains' miracles, after the tenth | Shift+1, Shift+2, Shift+3 |
| Show or hide the Domains panel: your domains, what each gives, the holy sites | G |
| Cast the selected miracle at the cursor | Left mouse button |
| Deselect the miracle | Esc |
| Next map overlay (influence, religion, faith, food, weather, danger, migration), then off | O |
| Look at the next settlement | F or Home |
| Look at your Creature | C, or Look on its card |
| Reward / punish your Creature for what it is doing (or just did) | Z / X, or the buttons on its card |
| Send your Creature to the cursor | V (or Send on its card, then click the ground) |
| Show or hide the diplomacy panel | P |
| Toggle region boundaries | B |
| Toggle the debug overlay | F1 |
| Next region debug map layer, then off | F3 |
| New world from a random seed | F2 |
| Quicksave / quickload | F5 / F9 |
| Pause / resume the simulation | Space |
| Simulation speed slower / faster (×1, ×4, ×16) | `[` / `]` |
| Pause and step the simulation by one tick | `.` |
| Pause and run to the next settlement step | `,` |
| Grant your god 500 Divine Power (debug) | F4 |
| Spoil every settlement's food stores (debug) | F6 |
| Settlers arrive: every settlement grows by half (debug) | F7 |
| Start the next weather spell: normal, dry, wet (debug) | F8 |
| Call a storm at the cursor (debug) | F10 |
| Start an earthquake at the cursor (debug) | F11 (fn+F11 on Mac laptops) |
| Start a fire at the cursor (debug) | F12 (fn+F12 on Mac laptops) |
| Flood the nearest river, or send a wave over the land at the cursor (debug) | L |
| Send 20 people out of the village under the cursor (debug) | M |
| Bring the nearest other Creature to yours, so they meet (debug) | K |
| Pause menu: resume, save, load, main menu, quit (when no miracle is selected) | Esc |
| The world's history | H |

The camera has three zoom bands, by distance from the point it looks at: **Close** (up to
120 m), **Regional** (up to 700 m) and **Strategic** (up to 2600 m). The simulation runs at a
fixed 10 ticks per second, independent of the frame rate. Choosing a speed also resumes a paused
simulation.

## The living settlements

Each settlement is simulated as a whole (Tier 1, statistical): population, food, housing,
health and the house being built. It steps once per simulated second, every tenth tick, so ×4
and ×16 speed it up. Each step:

- Most inhabitants work. They farm the land regions around the settlement, with diminishing
  returns, so each site has its own limit. When the settlement is crowded and has food in
  store, some of them build the next house instead, one house at a time. Followers of a god
  spend part of their time worshipping instead of farming.
- Everyone eats. Surplus food is stored up to what the houses can hold, and the rest spoils.
- When food runs short, health falls with the share of people left hungry, and recovers once
  everyone is fed again.
- Children are born while there is free housing, a food surplus and stocked stores; fewer when
  health is low. People die at a low baseline rate, faster when health is low.

The island has eight settlements, founded on the best land at least five regions (320 m) apart
with 26 to 54 people each. With seed 1234 your home village, Duskwater, grows from 30 people to
about 390 in 15 simulated minutes (about a minute at ×16). Press F7 and then F6 to start a
famine. Food runs out, your followers' doubt rises, and unless you help them some turn away;
and if the land cannot feed the newcomers, health falls and deaths rise until it can.

Near the camera (within 450 m of a settlement), the settlement switches to Tier 2 and shows
representative citizens: one figure per inhabitant, up to 300, each standing for several
people in a larger settlement. Farmers (red) work in the fields, builders (amber) at the
building site and villagers (blue) do chores near home. Each follows a repeating round: rest by
the door, work, gather at the shrine, go home. The citizens are derived from the settlement's
numbers and never saved. Beyond 550 m the settlement shows only its houses, building site and
fields (Tier 1); the simulation is the same in both tiers. Balance values are in
`game/Data/settlement_rules.json` and presentation values (layout, colours, tier distances,
routines) in `game/Data/settlement_view.json`.

## Being a god

The bar at the top shows your god and rank, your Divine Power with its income per second, your
followers and the next rank. The miracle bar at the bottom shows each miracle's key and cost (or
its cooldown); hover over a button to read what it does.

- **Followers and power.** In every settlement, a share of the people follow you. Followers
  spend part of their working time at the shrine instead of in the fields, and their worship
  raises Divine Power: more followers, stronger faith, and love or fear all raise it.
- **Influence.** Your influence spreads from the settlements that follow you, further the more
  followers they have; press O to see it. A miracle needs influence at the target and costs more
  where your influence is weak.
- **Casting.** Select a miracle and move the cursor: a ring shows its radius, green where you can
  cast it and red where you cannot, and the hint names the settlements it would reach, its cost,
  or what stands in the way. Click to cast.
- **What people think.** Faith (they believe you exist), Love, Fear and Doubt drift every
  second. Comfortable followers grow fond of you. Hunger and illness breed doubt and wear down
  love, and fear fades in safety. People who follow no god join you through faith, love and fear,
  and doubt drives them away; fear works fast, love holds. Miracles change the minds of everyone
  who sees them, and help that was needed earns more love. Strangers who see a miracle start to
  believe.
- **Ranks.** At 100 followers you become a Lesser God, then a God at 400, a Greater God at 1,200,
  a High God at 3,000 and Ascendant at 8,000. You never lose a rank. Higher ranks unlock
  miracles.
- **Alerts** on the right tell you when a settlement that follows you starves, when a settlement
  comes to follow you or turns away, and when you reach a rank. Click one to look there.

A good start: Food or Heal on the neighbours of your home village, then Bless Crops when your
followers grow hungry. Try Lightning to see how fear behaves.

## Your rivals

Three gods share the island with you, each with a village far from the others and a Creature of
its own: Vael the Tyrant (Gristle), Seren the Guardian (Thistle) and Kaio the Trickster (Quill).
The line under your HUD shows each rival's followers, its share of all belief and where you stand
with it. The line below that shows the race: who holds most of the world's belief and how long
until they win. Hold 60% of all followers for ten minutes and you win; let a rival do it and it
wins. Allies count together (see [Diplomacy](#diplomacy)). The world goes on either way.

The examples below use Vael.

- **Vael thinks like you.** Every few seconds it weighs every miracle it could cast on every
  village, plus sending its Creature or saving its power, and picks the best. As a Tyrant it
  strikes villages that follow you with Lightning, heals and feeds its own, and defends villages
  you court. It teaches its Creature, Gristle, cruelty.
- **Belief is contested.** People who lose faith in a god turn to whichever other god pulls them
  most. A miracle makes the people who see it doubt the other gods they follow. Working a miracle
  where Vael's influence is stronger than yours costs more.
- **Fear against love.** Fear converts quickly but fades quickly; killings breed doubt, and
  frightened followers leave at the first trouble. Loved followers stay.
- Labels take the colour of the god most of a village follows. The Influence overlay shows whose
  influence is strongest where, and the Religion overlay shows which god each village follows.
  Alerts warn you when Vael works a miracle on your followers, when its Creature attacks them, and
  when anyone starts to dominate.

## Diplomacy

Press P for the diplomacy panel. For each rival it shows where you stand (allied, friendly,
neutral, hostile or at war), how the rival feels about you and why ("remembers that you converted
Elar"), what your people hold against it, any proposal waiting for your answer, and a button for
each move you can make now. Your status also ends the rival's line on the HUD.

- **Every god keeps its own view of every other**: trust, fear, respect, rivalry, gratitude and
  suspicion. What you do shapes Vael's view of you:
  - feeding or healing its followers when they are in need earns gratitude;
  - working miracles on its comfortable followers is courting, and it resents it;
  - striking its followers, letting your Creature attack them and converting its villages make
    it a rival.

  Vael remembers these acts, and the memories fade with time.
- **Friendly and hostile** come by themselves as feelings warm or sour. **Alliance, war and
  peace** are decisions: offer an alliance and wait for an answer, declare war, or ask for peace
  once a war has lasted a while.
- **Allies** do not plant doubt in each other's followers or raise each other's costs. An allied
  AI god leaves your people alone, and Creatures seldom attack an ally's villages. **At war**, a
  village resists the enemy wherever its own god holds most of the people, and Creatures go after
  enemy villages.
- **Betrayal.** Breaking an alliance (or declaring war on an ally) collapses the victim's trust,
  makes every god trust you less, and is remembered far longer than anything else. Vael can
  betray you too.
- **Each god decides by its personality.** The Tyrant goes to war readily and makes peace only
  when a war wears it down. The Guardian welcomes an alliance and keeps it. The Trickster allies
  gladly and betrays whenever it pays. Change a god's `Personality` in `game/Data/gods.json` to meet
  the others: Opportunist, Zealot, Nature Keeper, Isolationist (tends its own and keeps out of
  wars) or Warlord (strikes and fights). The F1 panel lists the moves each AI god is weighing and
  why. Alerts tell you when a god proposes, declares war, betrays you, accepts or declines.
- **The gods deal with each other too** (ADR-015). Each rival's card in the panel shows its
  alliances and wars with the others ("Allied with Seren; at war with Kaio"), and alerts tell you
  when two other gods ally, one declares war on another or one betrays another.
  - A god that pulls ahead draws wars, and the others look for allies against it ("against
    Seren" in the F1 reasons).
  - Two lesser gods would rather make peace than fight while a third runs away with the world.
  - A fresh peace or a new alliance holds for a while.
  - A god remembers who wronged its allies.
- **Allies win together.** Allied gods (and allies of allies) count their followers together.
  A bloc that holds 60% of all belief for ten minutes wins together, and every member wins; the
  HUD then reads, for example, "Vael and Seren hold 76% of all belief together". Near such a
  victory, a treacherous god that holds more than its ally may break away to win alone. So an
  alliance with a strong god can be your road to victory, or theirs.

The rules are in `game/Data/diplomacy.json`, and each personality's diplomatic weights are in
`game/Data/god_ai.json`.

## Weather

Weather comes and goes on its own. Clouds blow in from the sea on the prevailing wind, rain on the
island and thin away as they drift on. Storms are dark, windy and full of lightning. From afar you
see the clouds; up close you see the rain, and the light dims under a storm.

- **Spells.** The island is in normal weather, a dry spell (little rain, and the land dries out) or
  a wet spell (frequent rain and storms). During a dry or wet spell the line under your HUD names
  it and how long it lasts; an alert announces a dry spell and its end.
- **Water in the soil.** Rain soaks the farmland, warmth dries it (faster in a dry spell), and
  springs refill it slowly. Press O until the Weather overlay shows: brown land is in drought,
  yellow has just enough water, blue is soaked.
- **Drought.** Dry farmland yields less, down to a quarter of its harvest. A village in drought
  eats through its stores and then goes hungry, and its people worry and doubt before that. Its
  label says "drought", and the HUD and an alert name it if it follows you.
- **Storms.** Where storm winds blow, a village loses part of its stored food and the work on its
  house under construction. An alert tells you when a storm strikes your people.
- **Rain** (key 6, available from the start). Calls a rain cloud over the target. It rains for a
  couple of minutes as it drifts slowly with the wind, and people in drought love you for it. Rival
  gods call rain on their own parched villages, and on yours to win them over.

The rules are in `game/Data/weather.json`; how dry the soil must be for drought is in
`game/Data/settlement_rules.json`, and how the clouds look is in `game/Data/weather_view.json`.

## Disasters

- **Wildfires.** Dry, green land burns. A spark from a village's hearths, a thunderstorm on parched
  land, an earthquake or a god sets the fields alight, and the fire spreads, faster downwind and
  where the land is driest. Rain and the villagers put fires out. Fire scorches the land, which
  yields less until it regrows, and a fire in a village burns its houses, people and food. Fires
  come with droughts and dry spells.
- **Earthquakes.** Now and then the ground shakes for a few seconds. Houses collapse, people die,
  stores spill and fires may break out, worst near the epicentre; the camera shakes with the
  ground.
- **Afterwards.** Fallen houses stay as rubble until the village rebuilds them, which takes half
  the work of a new house. People without a roof fall ill. Survivors fear their gods and doubt that
  the gods care; a god who shields or helps them in danger earns their love.
- **Miracles.** Fire (key 7, once you are a Lesser God) sets the land at the target alight.
  Shield (key 8, Lesser God) protects the villages under it from fire and earthquakes for three
  minutes. Earthquake (key 9, God) shakes the ground at the target. Vael uses all three.
- **Seeing danger.** Labels mark villages "burning", with "fire nearby", their ruins and a shield.
  The line under your HUD names your villages under threat, and alerts tell you of earthquakes,
  fires near your people and the damage done. The Danger overlay (O) shows fires in orange,
  scorched land in ash and shielded villages in blue.

The rules are in `game/Data/disasters.json`.

## Rivers, lakes and floods

- **Rivers and lakes.** Every new world has a few rivers running from the highlands to the sea,
  with tributaries, and lakes in its hollows. New villages prefer the land beside them, and their
  houses and fields keep off the banks. Rivers and lakes keep the soil beside them moist.
- **Floods.** A river rises with the rain falling on it and the water in the soil along it. Over
  its banks it floods the low land downstream, deepest at its mouth. A flood drowns houses, people
  and stores, puts out fires and leaves silt that makes the fields yield more once the water has
  gone. Shields and the Protection domain spare a village most of it. Call rain upstream and the
  river may flood a rival's village downstream.
- **Drinking water.** Villages draw water from a river or lake in or beside their region, from deep
  wells and from shallow wells that dry up in a drought. In a long drought away from water the
  wells run dry: people go thirsty and fall ill, and they are grateful to the god who brings water.
- **The Water miracle** (key 0) fills the wells of the village beneath it and soaks its fields.
- **Seeing it.** Labels mark villages "flooded" and "wells dry", the HUD's weather line names your
  flooded and thirsty villages, and alerts tell you when a river bursts its banks over your
  people or their wells run dry. The Weather overlay shows rivers and lakes and the Danger overlay
  floods. Big floods enter the world's history.

The rules are in `game/Data/water.json` and the rivers' shapes in `worldgen.json`'s `Hydrology`.

## Migration

People leave villages where life is hard and walk to better ones.

- **What drives them out.** Hunger, no roof (after a disaster, or too many people for the houses),
  fire or shaking, a village torn between gods at war, and fear of their god or doubt in it. Love
  keeps them home. The wish to leave builds while life is hard; once enough people want to go, a
  group sets out with its share of the food.
- **They give up gods.** Those who leave give up following a god in proportion to how much they
  fear or doubt it. People fleeing a god's terror never go where it rules.
- **The road.** A group heads for the best place it can walk to (room, food, safety, its own gods
  followed there) and eats as it goes; without food it sickens and dies. Near the camera you see
  its walkers in its god's colour, labelled "23 from Elar to Valen (war)".
- **Arrival.** The god followed most where they arrive decides:
  - *Welcome*: they settle as they are, and the village loves the god for it.
  - *Demand conversion*: many must turn to that god, and fear and doubt it.
  - *Turn away*: they walk on to another village, or home.

  Their own god remembers what was done with its people, so taking in a rival's refugees earns
  its gratitude and turning them away its resentment. The newcomers mix into the village's beliefs,
  and its gods convert them over time.
- **Your policy.** The row at the top of the diplomacy panel (P) sets what your people do with
  migrants. Each rival's card says what it does. Vael demands conversion, Seren and Kaio welcome
  them, an Isolationist turns them away, and a welcoming god turns them away while its own people
  lack a roof.
- **Seeing it.** Labels mark villages with "people leaving" and "migrants coming". Alerts tell you
  when your followers leave or give you up, when migrants settle among your people, and when your
  followers are converted, turned away or die on the road. The Migration overlay (O) colours each
  village from green (content) to red (people want to leave) and draws every group's way in its
  god's colour.

The rules are in `game/Data/migration.json`, and each personality's refugee policy is in
`game/Data/god_ai.json`.

## Your Creature

Bramble's card on the left shows its level, what it is doing, its health and needs, its loyalty
and whether it is turning out kind or cruel, and what a reward or punishment would judge right
now.

- **It decides for itself.** Its hunger, tiredness and boredom grow. It sees the villages within
  500 m and whether each is in need, building or doing well. It picks what it wants most, weighing
  what it has learned to like, its personality and how far it must walk.
- **What it does matters.**
  - Feeding a hungry village fills its stores and wins you love.
  - Helping a village build raises its houses.
  - Eating from a village's stores is theft.
  - Attacking a village kills villagers and makes the people fear you.

  The label above Bramble says what it is doing. Alerts report its notable deeds.
- **Teach it.** Press Z to reward or X to punish what Bramble is doing, or what it did in the last
  15 seconds. Your hand pats it (golden) or swats it (red).
  - A reward makes it like that action more in that kind of situation. A punishment makes it like
    it less and stops it.
  - Rewards and punishments also shape its personality and its loyalty.
  - Five rewards for feeding make a dutiful feeder of it.
- **Send it.** Press V to send it to the cursor; it waits there longer the more loyal it is.
- **It grows.** Its deeds earn experience. Each level, up to 10, makes it bigger, stronger and
  faster.
- **It can be lost for a while.** If it starves, or loses a fight, it is banished for a minute
  and comes home weakened.

## Creatures together

Every god's Creature roams the same island, and they remember each other.

- **Meetings.** When two Creatures come within about 40 m they meet. Each decides by what it has
  learned, how it feels about the other and its personality: fight, play, run or walk on.
  - Creatures of gods at war want to fight; allies' Creatures want to play.
  - A beaten or weaker Creature wants to run.
  - Rivals go looking for each other, and a Creature sent on its god's errand greets a friend in
    passing but does not stop to play.
- **Bonds.** Each Creature keeps its own view of every other: friendship, fear, rivalry, respect
  and trust, and its latest memories of it ("beat it", "was spared by it", "was betrayed by it").
  - Playing and greeting make friends.
  - A friend who attacks is a betrayer.
  - Friends whose gods go to war will not fight each other, and you are told when yours refuses.
  - A Creature that attacks your followers becomes your Creature's rival.
- **Fights.** Two Creatures fight face to face, their health on their labels, until one gives up.
  Strength (its level) and combat skill (it grows with every fight) decide it.
  - A kind winner may let the loser go.
  - Otherwise the loser is banished for a minute, scarred (the scars show on its flanks), less
    loyal and set back towards its next level, and its god holds the defeat against the winner's.
  - People of the winner's god who saw it believe more; the loser's doubt.
  - A fight that lasts 40 seconds is a draw.
  - Punishing your Creature (X) calls it out of a fight.
- **Teach it.** Reward or punish a fight or a game like any deed: Bramble learns whom to fight and
  whom to befriend. AI gods teach theirs to fight enemies and befriend allies, and at war send
  their Creature against yours when it threatens their people.
- **Seeing it.** Bramble's card lists its bonds, combat skill and scars. Each rival's card in the
  diplomacy panel (P) says how your Creature and theirs regard each other. Alerts report your
  Creature's games, flights, fights and refusals.

The rules are in the `Bonds` and `Combat` sections of `game/Data/creatures.json`.

## Domains

A god takes up a domain as a Minor Spirit, a second as a Lesser God and a third as a God (G
shows them all; the guide asks for your first). A domain is yours for good. It does three things:

- **Cheaper miracles:** its miracles cost you a fifth less.
- **A boon:** one rule of the world changes for you. Your villages heal (Life), stay blessed
  (Nature) or lose fewer houses to disasters (Protection); your Creature hits harder (War) or
  learns faster (Knowledge); storms over your people raise their faith (Storm); fires die down on
  your land (Fire); worship raises more power (Prosperity); the lives your miracles take give you
  power (Death); your miracles plant more doubt (Trickery); your earthquakes strike harder (Chaos);
  your villages fish the sea, rivers and lakes (Ocean); your villages keep your law, so fewer leave
  and doubt fades (Justice).
- **A signature miracle** only its gods can cast:
  - Regrowth heals burned land;
  - Gift of Life brings newcomers;
  - Redirect Storm carries the nearest storm to you;
  - Firestorm sets a wide country alight;
  - Spear of Heaven wounds rival Creatures;
  - Dread turns people from other gods;
  - Abundance fills a country's granaries;
  - Sanctuary keeps your followers from falling away;
  - Enlighten teaches your Creature;
  - Sow Discord makes two rivals blame each other;
  - Upheaval shakes the ground early;
  - Tidal Wave sends the sea over a low coast;
  - Judgement strikes only the followers of gods who wronged you (at war with you, or with a
    grudge you bear them).

The AI gods choose by personality: the Guardian takes Life, Protection and Prosperity, the
Tyrant Death, Storm and Chaos, the Trickster Trickery, Chaos and Knowledge. Domains and their
numbers are in `game/Data/domains.json`.

## Holy sites

Four sacred places are found in every world, away from the villages: the Sacred Peak on the
highest summit, the Ancient Grove in the oldest wood, the Holy Spring and the Standing Stones.

- **Taking one:** the god whose influence holds a site, clearly ahead of the others, takes it
  after 45 seconds.
- **Keeping one:** its keeper gains power and influence there, and a gift: cheaper miracles,
  blessed land, healing for its nearby villages, or a wiser Creature.
- **Fighting over them:** your Creature's presence counts, so send it (V) to claim a site or to
  hold yours when you are warned that a rival is taking it. The gods remember who took their
  sites.

The Domains panel lists the sites and who keeps them; Look takes the camera there.

## Schisms and the Old Faith

When the people who follow a village's god come to doubt it deeply, a part of them break away to
keep its Old Faith. They worship no god, and no other god can win them, until their god is loved
there again and they return, or they slowly lose their faith altogether. Hunger, disasters and
neglect sow that doubt. So does a god who strikes people who love it: its deeds no longer match
what they knew of it.

## The world's history

The world remembers what matters (press H): pacts, wars, peace and betrayals; a god's dominion,
victory and ranks; villages turning to a god; deadly earthquakes and fires; famines; great
exoduses; villages emptied and settled again; Creatures' battles and their rise to legend. Each
line reads like a chronicle, "Year 12 — The Shaking of Elar (12 dead)", a year being one minute.
Your own moments are in gold, and the end screen recalls the ones that led to the victory.

The rules for what is significant are in `game/Data/history.json`.

## Your Creature's species

On the start screen you choose your Creature's species for a new world (or pass `--species`):

- the **Mossback**, steady in all things;
- the **Stonehide**, slow but hard to hurt;
- the **Swiftclaw**, fast and fierce but fragile;
- the **Lumen**, gentle and quick to learn.

The rival gods' Creatures are a Stonehide (Vael), a Lumen (Seren) and a Swiftclaw (Kaio).

## Sound and settings

Every sound is made by the game itself:

- the music, which grows tense in war and triumphant when you dominate;
- bells for kind miracles and a crack for lightning;
- thunder and roars heard from across the island, where they happen;
- the earthquake's rumble, fire, and the wind and rain where you look.

The settings page (start screen or Esc) sets fullscreen, vertical sync, shadows, the render
scale, the citizens shown, and the loudness of everything, the music, the effects and the wind and
rain.

## Winning

A god wins by holding 60% of all belief for ten minutes. Two allies can win together: each counts
with its strongest ally only, so a god allied with two others must choose whom to share its
victory with. The end screen says who won, how your god and Creature ended up, and what the world
remembers; you can keep watching, start a new world or go back to the start screen.

## Debug tools

- **F1** shows the debug panel (hidden at the start): frame rate, tick, seed, terrain renderer,
  camera band, one line per god (power and income, rank, followers, the next rank), each AI god's
  goal now and its top candidate actions with their scores and reasons, its diplomatic moves
  with the factors behind them, the weather (the spell, the wind and the strongest cells),
  disasters (earthquakes and the fiercest fires), migration (the groups on the road and every
  god's refugee policy with the AI's reason), the Creature
  view (level, health, loyalty, morality, activity, needs, personality, its best options with
  their scores, how it would answer the nearest Creature, its combat skill, scars and power, its
  view of every Creature it has met with their latest encounters, its learned preferences by
  situation and its latest memories), the region
  under the cursor with its statistics, soil, rain and wind and every god's influence there, one
  line per settlement, and, for the settlement nearest the cursor:
  - its belief: the share that follows no god, its hardship and comfort, and for each god the
    followers, Faith, Love, Fear, Doubt, worshippers and the power they raise;
  - population / housing, houses and health;
  - food stored / storage, the net change per second, production and consumption;
  - the jobs (farming, building, not working) and the farmland;
  - expected births and deaths per second, the house under construction and the time to the
    next settlement step;
  - the simulation tier, the camera distance that changes it and, in Tier 2, the citizens
    shown and the people each stands for;
  - its water: its farmland's soil, the drought and the harvest it allows, and the weather over it;
  - its dangers: the fire threat, the shaking, its ruins, the homeless, scorched land and any shield;
  - its migration: the pressure to leave and its cause, the people wanting to go and those on
    their way there;
  - warnings when the settlement starves, construction waits for food, or births are blocked.
- **B** draws the region grid and outlines the region under the cursor.
- **F3** shows the region debug map in the bottom-left corner, one pixel per region, oriented
  like the camera's starting view. Each press shows the next layer: biome, elevation,
  temperature, moisture, fertility, vegetation, then off. Numeric layers run from dark (the
  lowest value) to bright (the highest), and the legend gives the range. The region under the
  cursor is white and settlement regions are red. The map stays up when F1 hides the panel. After
  vegetation come blessing, your god's influence, the soil's water, fire, scorch, fresh water and
  flood, all on a fixed 0 to 1 scale.
- **O** cycles the map overlays: influence, religion, faith, food, weather, danger and migration.
- **K** brings the nearest other Creature beside yours, so they meet at once and you can watch them
  play, run or fight.
- **F4** grants your god 500 Divine Power.
- **`.`** pauses the simulation and runs exactly one tick, so the panel shows what a single
  tick changed. Space resumes.
- **`,`** pauses and runs the ticks up to and including the next settlement step, so the panel
  shows what one step changed.
- **F6** spoils every settlement's food stores and **F7** brings settlers (every settlement
  grows by half, at least 10 people). Use them to drive famines and recoveries.
- **F8** starts the next weather spell (normal, dry, wet) for its longest length, and **F10** calls
  a storm at the cursor. Use them to drive droughts and storm damage.
- **F11** starts an earthquake (magnitude 6.5) and **F12** a fire at the cursor.
- **L** makes the nearest river within 400 m burst its banks, or sends a wave over the low land at
  the cursor. The debug panel lists the rivers' levels and the lakes, and a selected settlement's
  water, access to water and river; the region map gains fresh water and flood layers.
- **M** sends 20 people out of the village under the cursor to the best place they can reach.
- The debug panel also lists every god's domains, each holy site with its keeper, its claimant
  and who leads its influence by how much, and the selected settlement's Old Faith and sanctuary.
  `--rank` and `--domains` try out domains at once, and `--focus=siteN` looks at a holy site.
- The command-line options below reproduce a world (`--seed`, `--load`), force a renderer
  (`--terrain`), start from a grown settlement up close (`--advance-ticks`,
  `--camera-distance`) and script smoke runs (`--quit-after-frames`, `--screenshot`).
  Unattended runs end by logging one summary line per settlement, with its tier and the
  citizens shown.

## Command-line options

Pass options after `--`, for example `godot --path game -- --seed=42 --terrain=fallback`.

| Option | Effect |
| --- | --- |
| `--seed=<number or text>` | Seed of the generated world (text is hashed). Default: `1234`. |
| `--load=<path>` | Load a save at startup, for example `user://saves/quicksave.json`. If it fails, a new world is generated. |
| `--terrain=auto\|terrain3d\|fallback` | Terrain renderer. `auto` (the default) and `terrain3d` use Terrain3D when it is available; `fallback` forces the built-in mesh renderer. |
| `--advance-ticks=<n>` | Run `n` simulation ticks (10 per simulated second) before the first frame, for example `--advance-ticks=9000` for a settlement of about 425 people. |
| `--camera-distance=<m>` | Starting camera distance in metres (default 380, clamped to 25–2600). |
| `--quit-after-frames=<n>` | Quit after `n` frames (smoke tests). |
| `--screenshot=<file.png>` | Together with `--quit-after-frames`, save a screenshot just before quitting (not in headless mode). |
| `--divine-power=<n>` | Your god starts with `n` Divine Power. |
| `--cast=<id>[,<id>...]` | Cast these miracles (ids from `miracles.json`) on the focused settlement at the start. |
| `--select=<id>` | Start with a miracle selected; scripted runs show its ring at the screen centre. |
| `--overlay=influence\|religion\|faith\|food\|weather\|danger\|migration` | Start with a map overlay shown. |
| `--focus=<n>\|creature\|site<n>` | Start looking at settlement `n` (1 is your home; `--cast` targets it), at your Creature or at holy site `n`. |
| `--judge=reward\|punish` | Reward or punish your Creature on the first frame (for captures of the hand). |
| `--debug-panel=on\|off` | Show or hide the F1 debug panel at the start. |
| `--diplomacy-panel=on\|off` | Show or hide the diplomacy panel (P) at the start. |
| `--weather=normal\|dry\|wet\|storm` | Start in a weather spell, or with a storm over the focused settlement. |
| `--disaster=quake\|fire\|flood` | Start an earthquake, a fire or a flood at the focused settlement. |
| `--migrate=<n>` | Send `n` people out of the focused settlement at the start. |
| `--refugees=welcome\|convert\|turnaway` | Your refugee policy from the start. |
| `--menu=on\|off` | Start on the start screen (the default without options) or not. |
| `--guide=on\|off` | Show the first-minutes guide from the start. |
| `--history-panel=on\|off` | Open the history panel (H) at the start. |
| `--pause-menu=on\|off` | Open the pause menu at the start. |
| `--speed=1\|4\|16` | The simulation's speed from the start. |
| `--host=on\|off\|<port>` | Host this world for other players (port 7777 by default). |
| `--join=<address>[:<port>]` | Join a host's world instead of making one. |
| `--name=<player>` | Your name in a shared world. |
| `--god=<name>` | The god you ask for when joining. |
| `--upnp=on\|off` | Ask the router to forward the host's port. |
| `--meet=on\|off\|<name>` | Bring the nearest other Creature (or the one with that name) beside yours at the start, so they meet. |
| `--species=<name>` | The species of your Creature in a new world: Mossback, Stonehide, Swiftclaw or Lumen. |
| `--rank=<rank>` | Your god starts at this rank, such as LesserGod or God (debug). |
| `--domains=<domain>[,<domain>...]` | Take up these domains at the start, as your rank allows. |
| `--domains-panel=on\|off` | Open the Domains panel (G) at the start. |
| `--settings-page=on\|off` | Open the settings page at the start. |

Unknown options and invalid values produce a warning and are otherwise ignored.

## Playing together

One player hosts the world and up to three others join it, each playing a god the AI would
otherwise play. There is no server or account: the host's computer runs the world.

```bash
# The host (opens UDP port 7777; --upnp=on asks the router to forward it)
godot --path game -- --host=on --name=Roey
# Each other player, with the host's address
godot --path game -- --join=192.168.1.20 --name=Ana --god=Seren
```

- **Joining.** A player gets the god it asks for with `--god`, or the first god the AI plays. The
  host turns players away when its game data differs, when they run another version, or when no god
  is free. A player that cannot join plays alone after ten seconds.
- **Playing.** A joined player plays as usual: miracles, the Creature, diplomacy and the refugee
  policy. Each command goes to the host, which checks it as it checks the host's own and replies.
  The status line says "Asking the host..." and then what happened ("Food on Larkspur for 45
  Divine Power", or "Outside your influence").
  - The host sends its world every second and every event at once, so alerts and miracles show up
    for everyone.
  - The HUD names who plays the other gods: "Ilyra (Roey)".
- **The host decides** the speed and pausing, and only the host saves, loads, starts a new world
  or uses the debug keys.
- **Leaving.** When a player leaves, the AI takes its god back until the player returns under the
  same name. When the host quits, the session
  ends for everyone and the host's world is saved to `user://saves/hosted.json`.
- **Seeing it.** F1 shows who plays which god, the round trip to each and the traffic.

To play over the internet, the host forwards UDP port 7777 on its router (or uses `--upnp=on`)
and gives the others its public address. Each part of the world travels at its own pace: Creatures
near your camera five times a second, gods four times, settlements and weather once, diplomacy when
something happens, and the whole world every 20 seconds. That is about 13 KB/s per player.

## Saves

F5 writes the world to `user://saves/quicksave.json` and F9 loads it back. In a project run
(not an export), `user://` is:

- Linux: `~/.local/share/godot/app_userdata/PANTHEON/`
- Windows: `%APPDATA%\Godot\app_userdata\PANTHEON\`
- macOS: `~/Library/Application Support/Godot/app_userdata/PANTHEON/`

The pause menu saves the world under its god, year and seed in the same folder, an autosave is
written there every five minutes, and the start screen and pause menu list every save, newest
first. A host's world is saved there as `hosted.json` when it ends a shared session.

Saves are indented JSON of the simulation state, starting with `"SaveVersion": 13`. The scene tree
is never saved. The terrain is regenerated from the seed and loading rebuilds every visual
([ADR-006](docs/ADR/ADR-006-simulation-state-separate-from-scenes.md)). The representative
citizens are not saved either; they are derived again after a load. Saves from Milestone 1
(version 1) still load: each settlement starts healthy with no house under construction. Saves
from Milestones 1 and 2 (versions 1 and 2) get the Milestone 3 god, Ilyra, followed in their one
settlement. Saves from Milestones 1 to 3 get a newborn Creature, Bramble, for every god. Saves from
Milestones 1 to 4 stay single-god worlds with nobody to compete with. Saves from Milestones 1 to 5
start with every pair of gods neutral, with no memories of each other. Saves from Milestones 1 to 6
start under clear skies, with each region's soil as moist as its climate. Saves from Milestones 1
to 7 start with nothing burning, shaking or in ruins. Saves from Milestones 1 to 8 keep their one
winner, if any, as the list of winners. Saves from Milestones 1 to 9 start with nobody on the road
and every god welcoming migrants. Saves from Milestones 1 to 10 start with Creatures that have
never dealt with each other: no combat skill, no scars and no views of the others. Saves from
Milestones 1 to 13 start with an empty history: the world remembers from then on. Saves from
Milestones 1 to 14 start with gods who have chosen no domains and with no village split, and find
their holy sites when they are loaded.

Your settings are kept in `user://settings.json`; the start screen and the pause menu have a
settings page.

## Repository layout

```text
game/                 Godot project: scenes, client scripts (Pantheon.Client), data, materials, shaders
  Data/               worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                      creatures.json, god_ai.json, diplomacy.json, weather.json, disasters.json, migration.json,
                      history.json, domains.json, holy_sites.json,
                      settlement_view.json, biome_palette.json, divine_view.json, weather_view.json
  addons/terrain_3d/  Vendored Terrain3D 1.0.2 runtime (reproduce with tools/vendor-terrain3d.sh)
src/
  Pantheon.Domain/        World, region, settlement, god, belief, Creature, diplomacy, weather and disaster
                          state, events, and content contracts
  Pantheon.Simulation/    Seeded RNG, noise, terrain and world generation, the settlement and belief models,
                          influence, miracles, the Creature, navigation, the god AI, victory, diplomacy,
                          weather, disasters, tick systems
  Pantheon.Persistence/   Versioned JSON saves with migrations, and content parsing
  Pantheon.Game/          Engine-agnostic session (the local authority), scheduler, camera model, readouts,
                          HUD model, alerts, overlays, settlement layout and representative citizens
  Pantheon.Networking/    The multiplayer protocol, its codecs and the transport contract (ADR-003, ADR-018)
  Pantheon.Tests/         xUnit tests
docs/                 The owner's specification (PANTHEON_SPEC.md, the source of truth), the progress
                      summary (PROGRESS_SUMMARY.md), the vision summary (GAME_SPEC.md), architecture
                      and ADRs
.docs/                Milestone plans
```

Start with the specification, [docs/PANTHEON_SPEC.md](docs/PANTHEON_SPEC.md), and the progress
summary, [docs/PROGRESS_SUMMARY.md](docs/PROGRESS_SUMMARY.md), then
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/GAME_SPEC.md](docs/GAME_SPEC.md) (a short
vision summary) and the [ADRs](docs/ADR/README.md). The milestone plans and their status are in
[.docs/milestone-1-foundation.md](.docs/milestone-1-foundation.md),
[.docs/milestone-2-living-settlement.md](.docs/milestone-2-living-settlement.md),
[.docs/milestone-3-first-god.md](.docs/milestone-3-first-god.md),
[.docs/milestone-4-creature.md](.docs/milestone-4-creature.md),
[.docs/milestone-5-rival-god.md](.docs/milestone-5-rival-god.md),
[.docs/milestone-6-diplomacy.md](.docs/milestone-6-diplomacy.md),
[.docs/milestone-7-weather.md](.docs/milestone-7-weather.md),
[.docs/milestone-8-disasters.md](.docs/milestone-8-disasters.md),
[.docs/milestone-9-four-gods.md](.docs/milestone-9-four-gods.md) and
[.docs/milestone-10-population-movement.md](.docs/milestone-10-population-movement.md).

## Contributing

Read [AGENTS.md](AGENTS.md) before changing anything, whether you are a person or a coding
agent. It lists the owner's binding coding rules, where code goes, the build and test commands
and the definition of done.

## Known issues

- Every start logs `WARNING: instance_reset_physics_interpolation() is deprecated`. Terrain3D
  1.0.2 calls this deprecated Godot 4.7 function, and the warning is harmless.
- Without a GPU driver, Godot falls back to Mesa's software Vulkan driver (lavapipe), which
  manages only a few frames per second. Terrain3D 1.0.2's shader crashes that driver. PANTHEON
  patches the shader at runtime to prevent this
  ([ADR-007](docs/ADR/ADR-007-terrain3d-shader-patch-and-fallback-reason.md)). If a different Terrain3D version
  cannot be patched, a warning is logged; use `--terrain=fallback` on such machines.
- macOS may block the unsigned Terrain3D binaries until they are removed from quarantine. See
  Terrain3D's platform documentation.
- Export presets are not set up yet. The game reads `res://Data/*.json` at startup, so every
  export must include those files.
- Four gods crowd the island: a player who does nothing keeps only a few followers after ten
  minutes, and Seren the Guardian is the strongest AI god (kindness converts better than fear).
- The representative citizens are presentation only: they show what the settlement's numbers
  say, but nothing can interact with an individual citizen until a later milestone adds full
  (Tier 3) agents ([ADR-008](docs/ADR/ADR-008-settlement-step-and-representative-citizens.md)).

## Third-party code

- [Terrain3D](https://github.com/TokisanGames/Terrain3D) 1.0.2: MIT licence,
  `game/addons/terrain_3d/LICENSE.txt`.
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) 1.1.1: MIT licence, header of
  `src/Pantheon.Simulation/Noise/FastNoiseLite.cs`.
