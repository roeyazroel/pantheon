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

**Milestone 15, part 3 (Society)** gives every village a life of its own beyond food and houses:

- **Safety, happiness, order, wealth and learning** rise and fall with how its people fare.
- **Priorities.** A god followed by enough of a village asks it to increase food, improve health,
  grow, focus on worship, prepare for war or disaster, expand, or build a temple (the
  Settlements panel, N).
- **Buildings.** Temples, granaries, hospitals, walls, levees, firebreaks, shelters and cisterns.
- **Technology.** Eight fields, from agriculture to religion, that grow with learning and spread
  between neighbours, never past the world of fantasy.
- **Expansion.** Crowded villages found new ones, so worlds grow.

**Milestone 15, part 4 (Culture)** gives villages beliefs of their own and gods a reputation:

- **Doctrines.** Villages come to hold pacifism, militarism, sacrifice, charity, nature worship,
  the search for knowledge, expansion or isolation from what they live through and the god they
  follow, and each changes how they worship, learn, convert and welcome strangers.
- **Turning from the gods.** Doubt, the godless, schisms and resentment make villages agnostic,
  then atheist, then set against every god.
- **Dependency and resentment.** People you help in need come to depend on you and expect your
  protection. People you harm, or who fear you, resent you, and may revolt and tear down your
  temple.
- **Missionaries and charity.** Temples win the godless of nearby villages; charitable villages
  feed hungry neighbours.
- **Reputation.** What you do earns traits: compassionate, cruel, vengeful, merciful, generous,
  deceptive, honest, chaotic or ambitious. Villages read their god by them.

**Milestone 15, part 5 (Treaties and the Divine Council)** gives the gods politics beyond
alliance and war:

- **Treaties.** Non-aggression, defensive and mutual defence pacts, joint wars, shared influence,
  religious access, Creature truces, disaster assistance, territorial agreements, tributes and
  migration agreements, for a term or until broken. They may be signed in secret.
- **Betrayal.** Breaking a treaty costs the partner's trust and your reputation.
- **Favours.** Save another god's people in great need and it owes you. Call the favour in for
  power, war, its vote, its absence or its help.
- **The Divine Council (U).** Betrayals, a rising god and disasters summon it. It votes, in public
  or in secret, to condemn a god, protect a holy site, prohibit a practice, coordinate disaster
  response, make war together or declare a village neutral.

**Milestone 15, part 6 (War and intrigue)** gives wars a purpose and gods hidden hands:

- **War goals.** Go to war for conquest, a holy site, tribute or to humble a rival's Creature.
  Win your goal and dictate the peace.
- **Intelligence.** You know rivals only as far as your allies, treaties, spies among their
  people and your Creature's travels tell you.
- **Proxy wars.** Veil your miracles (T), incite rebels and spread propaganda; the god you wrong
  may trace it to you.
- **Holy wars.** Militarist villages attack neighbours of another faith of their own will. Urge
  them on or hold them back.

**Milestone 15, part 7 (Larger worlds and victories)** lets you choose the world you play:

- **World sizes.** A small island 2 km across with eight villages and four gods, a large one of
  4 km with twenty villages and six gods, a huge one of 8 km with forty villages and eight gods,
  or a vast one of 12 km with forty-eight villages and eight gods. Four new rivals join the
  larger worlds: Aurel the Zealot, Maren the Nature Keeper, Thorn the Warlord and Ysolde the
  Isolationist, and larger pantheons may win together.
- **Victories.** Win by Divine Ascendancy as before, by conquest (every rival a remnant) or by
  culture (most villages look to you while your rivals live on), by any of them, or play a
  sandbox that nobody wins.
- **Large worlds stay smooth.** The AI gods take turns to think, and village labels make way for
  each other.

**Milestone 15, part 8 (Seasons and the land's life)** gives the world a year and a living land:

- **Seasons.** A ten-minute year: summer's crops, heat waves and fires, autumn's harvest, rain and
  hurricanes, and winter's snow, hunger and cold. Choose an even climate if you would rather not.
- **Weather.** Snow lies on the hills and melts into the rivers in spring; fog, hurricanes that
  tear houses down, and heat and cold waves.
- **The land's life.** Fire, floods, earthquakes and lightning harm the land; it heals, but harm
  done again and again scars it for good, and only a god of Nature heals scars. Villages hunt the
  wild animals, and crowded villages pollute their land.

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
a new world (its seed is shown, so you can share it, and you choose its size, how it may be won,
its climate and your Creature's species), continue your newest save, load a world, or
host or join a shared world (joining reloads the game with the host's address). A short guide
walks you through your first minutes; Esc pauses. If you skip the import step, the first run cannot load
Terrain3D. In that case it uses the fallback terrain renderer and labels it in the debug overlay.

### Building releases

`game/export_presets.cfg` builds Windows x64, macOS (one universal app for Apple Silicon and Intel)
and Linux x64 into `build/`. It needs the Godot 4.7.2 .NET export templates: in the editor, Editor >
Manage Export Templates > Download, or unpack the official
`Godot_v4.7.2-stable_mono_export_templates.tpz` into Godot's `export_templates/4.7.2.stable.mono`
folder. Then:

```bash
godot --headless --path game --export-release "Windows Desktop" ../build/windows/Pantheon.exe
godot --headless --path game --export-release "macOS" ../build/macos/Pantheon.zip
godot --headless --path game --export-release "Linux" ../build/linux/Pantheon.x86_64
```

The macOS app is signed ad hoc, so it runs on the machine that built it. To share it, sign it with a
developer identity and notarize it (the preset's `codesign` and `notarization` options).

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
- seasons and the land's life: the season from the tick, seasons and waves combining, what each
  season does to harvests, stores, health and migration, snow lying and melting, fog and
  hurricanes, harm healing into scars and Nature's healing, wildlife and hunting, pollution, the
  words, alerts, history, readouts and overlay, and the migration of part 7's saves;
- the Book of the World's four pages, the population, disaster risk and political control
  overlays, the strategic overview, the book's options and the export presets;
- the Creature's paths and attributes, the Divine Hand (rescues, abductions, carrying, drowning,
  throwing, trees, the Creature in the hand, every refusal, through a host) and the Divine Acts
  (hallowing, waking the volcano, stilling every disaster, the rank and power they need), their
  alerts, history, card, panel and options, and the migration of part 9's saves;
- prophets and elders: elders chosen, keeping faith and turning, prophets called and anointed up to
  each god's limit, sermons, walking, martyrs, danger remembered, temples and prophets as
  influence, the same notables for the same world, their alerts, history, book, readout, option,
  save and section, the checks on their rules, and the migration of part 11's saves;
- wood, stone and roads: forests and stone from the seed, gathering and what it takes from the
  land, houses and buildings that wait for wood and stone, timber growing back and burning,
  wildlife leaving cleared forests, settlers' stores, roads planned, built with stone, sped along,
  carrying trade and food, cut by floods and mended, the trees and the road drawn, their alerts,
  panel, book, readouts, options, save and sections, the checks on their rules, and the migration of
  part 12's saves;
- great events: the plague's ripeness, course, spread, shields and cure, the great plague and the
  council it calls, eclipses and their dread and awe, reformations, world wars, great droughts and
  great migrations, the same world across a save, their alerts, history, bar, book, labels,
  readouts, commands, options and section, the checks on their rules, and the migration of part 13's
  saves;
- the beast and the anomaly: the beast's lair and name, its waking, its ravages, Creatures and
  miracles fighting it, its fall and the glory, its going home, the AI gods sending their Creatures,
  the anomaly's power and cheaper miracles, their alerts, history, commands and section, the checks
  on their rules, and the migration of part 14's saves;
- hazards: the volcano, its lava and ash, waves from coastal quakes and sea walls, landslides and
  the rubble that blocks the way, meteors, tornadoes, locusts and rain, blight and what cures it,
  evacuation routes, dams and irrigation, marks fading, the same hazards across a save and load,
  their words, alerts, history, readouts, overlay, cover and commands, and the migration of part
  8's saves;
- world sizes and victories: the shipped sizes and what a larger one adds, the same world for the
  same seed and size, the checks on `world_sizes.json`, conquest, culture and the sandbox, the
  first victory being the only one, how each victory is told (alerts, history, end screen, HUD),
  the AI's influence cache and turns, labels giving way, the camera's reach, the options and the
  migration of part 6's saves;
- war and intrigue: war aims, scores, goals and dictated peace, what gods know of each other,
  veiled miracles and their tracing, proxy acts, holy wars and their gods' word, the panels,
  alerts, history and secrets kept from other players;
- treaties and the council: each treaty's effect, terms, breaking, secrets and their leaks,
  defensive pacts, tributes, favours, requests and promises, absorbing an ally, what summons the
  council, public and secret ballots, decrees and defiance, the AI's offers, answers and votes,
  the panels, alerts, history, and secrets kept from other players;
- culture: doctrines and what they follow, secularism and its stages, dependency and resentment,
  missionaries, charity, revolts, the traits gods earn by their acts, what witnesses take from a
  miracle, and how culture is shown (panel, HUD, diplomacy panel, alerts, history, overlay);
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
| Select Anoint a Prophet, then your domains' miracles, after the tenth | Shift+1, then Shift+2, Shift+3, Shift+4 |
| Show or hide the Domains panel: your domains, what each gives, the holy sites | G |
| Show the Settlements panel for the village under the cursor (its society, technology, buildings and priority), or hide it | N |
| Cast the selected miracle at the cursor | Left mouse button |
| Deselect the miracle | Esc |
| Next map overlay (influence, religion, faith, food, weather, danger, migration, culture, nature, population, disaster risk, political control, navigation, authority), then off | O |
| The Book of the World: the world, religion, miracles, your Creature and your villager | Tab |
| Follow the villager at the cursor; with nobody there, keep the camera on the one you follow | / |
| Let the villager you follow go | Shift+/ |
| The whole island from afar, and back | ` (backtick) |
| Look at the next settlement | F or Home |
| Look at your Creature (the camera turns to its face) | C, or Look on its card |
| Reward / punish your Creature for what it is doing (or just did) | Z / X, or the buttons on its card |
| Send your Creature to the cursor | V (or Send on its card, then click the ground) |
| Your hand picks up what is at the cursor, or sets it down there; with Shift it throws it | R / Shift+R |
| Your hand takes an offering from a village that follows you (R gives it) | Ctrl+R |
| Show or hide the diplomacy panel | P |
| Show or hide the Divine Council: the vote in session, decrees, and motions to table | U |
| Toggle region boundaries | B |
| Toggle the debug overlay | F1 |
| Next region debug map layer, then off | F3 |
| New world from a random seed (small, won by Divine Ascendancy, with four seasons) | F2 |
| Quicksave / quickload | F5 / F9 |
| Pause / resume the simulation | Space |
| Simulation speed slower / faster (×1, ×4, ×16) | `[` / `]` |
| Pause and step the simulation by one tick | `.` |
| Pause and run to the next settlement step | `,` |
| Grant your god 500 Divine Power (debug) | F4 |
| Spoil every settlement's food stores (debug) | F6 |
| Settlers arrive: every settlement grows by half (debug) | F7 |
| Start the next weather spell: normal, dry, wet, heat wave, cold wave (debug) | F8 |
| Call a storm at the cursor, short of a hurricane (debug) | F10 |
| Begin the next season (debug) | Y |
| Start the next hazard at the cursor: tornado, landslide, meteor, blight, locusts, tsunami, eruption (debug) | I |
| Start the next great event: the plague at the village nearest the cursor, an eclipse, your faith's reformation, the beast's waking, the anomaly at the cursor (debug) | ; |
| Start an earthquake at the cursor (debug) | F11 (fn+F11 on Mac laptops) |
| Start a fire at the cursor (debug) | F12 (fn+F12 on Mac laptops) |
| Flood the nearest river, or send a wave over the land at the cursor (debug) | L |
| Send 20 people out of the village under the cursor (debug) | M |
| Bring the nearest other Creature to yours, so they meet (debug) | K |
| Summon the Divine Council now, on the oldest tabled motion or to condemn your strongest rival (debug) | J |
| Cast your miracles veiled (dearer, and unattributed unless traced), or in the open | T |
| Pause menu: resume, save, load, main menu, quit (when no miracle is selected) | Esc |
| The world's history | H |

The camera has three zoom bands, by distance from the point it looks at: **Close** (up to
120 m), **Regional** (up to 700 m) and **Strategic** (up to 2600 m, and further over a larger
island, in proportion to its width). The simulation runs at a fixed 10 ticks per second,
independent of the frame rate. Choosing a speed also resumes a paused simulation.

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
- **Wood and stone.** Each village keeps stores of wood and stone. While they run short, some of
  its workers gather instead of farming, from the land around: forests give wood and the heights
  stone. A new house takes wood, and every building takes wood and stone. Cut forests grow back
  slowly. Fire burns them, a cleared forest holds fewer animals, and a cleared wet slope slides
  more easily.
- **Trees and the ground (spec §61, ADR-053).** Each land grows its own trees: oaks and birches
  on grassland, pines, oaks and birches in forests, firs on the heights and in the tundra, willows
  by rivers and in swamps, palms on islands and in oases, and bare snags on volcanic land. Near
  the camera the ground is covered with tufts of grass and flowers as thick as the land's
  vegetation, bushes, stones and reeds in swamps, but not by houses, on fields or on a village's
  square. Trees and grass sway in the wind and bend hard in a storm. The species, colours and
  sway are in `Trees`, and the ground cover in `GroundCover`, in `game/Data/settlement_view.json`.
- **Expressive Creatures and miracle looks (spec §61, ADR-055).** Your Creature's face shows how
  it feels: it scowls in a fight, widens its eyes in flight, grins at play, shuts its eyes at rest,
  looks glum when hungry and bored, and beams after a reward or cowers after a slap. Its tail wags,
  lashes or tucks. Its nature shows in its body: a kind Creature grows warm and softly glowing, a
  cruel one dark and red-eyed, with longer horns. Looking at it (C) turns the camera to its face,
  and the debug panel's Creature lines show its face and nature. Every miracle has its own look:
  rain pours from a gathering cloud, Food raises a golden pillar with grain falling about it,
  healing rises in a spiral under a soft dome, fire bursts with embers and smoke, an earthquake
  throws up dust, a shield raises a dome, Water springs up in a fountain, Enlighten and Hallow
  bring down a pillar of light, Dread and Sow Discord swirl dark, and a god's lightning strikes
  with a shock ring and sparks. The looks are in `game/Data/divine_view.json`.
- **Wildlife (spec §61, ADR-054).** Near the camera, herds of deer graze and walk on to fresh
  pasture, stags among them, and flocks of birds wheel over forests, rivers and coasts. There are
  as many as the land's wildlife allows, so hunted, burnt or cleared land is empty of them. They
  keep a natural pace at any game speed and stop when it is paused. Roads cross rivers on plank
  bridges. `--wildlife=on` takes the camera to the herd nearest the focused village, and the debug
  panel's streaming line counts the deer and birds. The numbers are in `Wildlife` in
  `game/Data/settlement_view.json`.
- **How villages build (spec §61, ADR-052).** The land picks the houses: round huts of wattle and
  thatch on grassland and by rivers and coasts, timber longhouses in forests and the tundra, stone
  cottages with slate roofs and chimneys in the highlands and on volcanic land, flat-roofed adobe
  in the desert, and plank houses on stilts in the swamp. Villages of huts build longhouses as
  they grow, and big villages an upper storey. Houses rise from the ground as they are built.
  Where a god is strongly felt, the houses take its colours: turf roofs for a god of nature and
  growth, dark timber and roofs for a god of war and death, whitewash and blue roofs for a god of
  learning. A temple takes its god's style: a domed round temple, a dark stepped pyramid with a
  brazier, a columned hall, or a plain hall. Walls are a palisade of pointed stakes with timber
  watchtowers by the gates, or crenellated stone with stone towers where the village builds in
  stone or follows a god of learning. The colours are in `Architecture` in
  `game/Data/settlement_view.json`.
- **Roads.** A village of 50 or more builds a road, with stone, to its nearest neighbour within
  700 m, one at a time. On a whole road, walkers (migrants and prophets) go 1.6 times as fast,
  both villages grow richer, and food travels to a hungry village from one with plenty. Floods,
  fires and landslides on the way cut a road until its villages mend it.

A small island has eight settlements (a large one 20 and a huge one 40), founded on the best land
at least five regions (320 m) apart with 26 to 54 people each. With seed 1234 your home village, Duskwater, grows from 30 people to
about 390 in 15 simulated minutes (about a minute at ×16). Press F7 and then F6 to start a
famine. Food runs out, your followers' doubt rises, and unless you help them some turn away;
and if the land cannot feed the newcomers, health falls and deaths rise until it can.

Near the camera (within 450 m of a settlement), the settlement switches to Tier 2 and shows
representative citizens: one person per inhabitant, up to 300, each standing for several
people in a larger settlement (ADR-049). Each looks their own: skin, hair, height and clothes
vary, many farmers wear straw hats, some people a scarf or a long dress. Farmers hoe the fields,
builders hammer at the building site and villagers do chores near home. Each follows a repeating
round: sit by the door, work, pray at the shrine with raised arms, go home. They walk at a human
pace whatever the game's speed, on a clock of their own that stops when the game is paused, and
the F1 panel shows it. They find their way round the huts, the shrine,
the temple and water on the village's local navigation grid (ADR-046), so nobody walks through a
wall. The citizens are derived from the settlement's numbers and never saved. Beyond 550 m the settlement shows only its houses, building site and
fields (Tier 1); the simulation is the same in both tiers. Balance values are in
`game/Data/settlement_rules.json` and presentation values (layout, colours, tier distances,
routines) in `game/Data/settlement_view.json`.

**Following a villager (ADR-043).** Point at someone in a village and press **/**: that figure
becomes one of the world's individuals, simulated in full wherever the camera is.

- **Who they are:** they have a name, an age, their work and a god, and are the same person each
  time you choose that figure.
- **Their days:** they eat while the village has food, work, rest and pray as their hunger, weariness
  and faith move them, walking between their home, their work and the shrine.
- **Danger and loss:**
  - a fire, flood or earthquake sends them fleeing to a safe village, where they shelter until it
    is over;
  - if their village empties, they seek a new home among their god's people.
- **Faith:** their faith follows their village's. Your miracles and prophets near them strengthen it,
  and a rival's shake it; when it fails they turn to the god their village follows.
- **What they remember:** miracles, disasters, famines, Creatures, buildings and preachers. Hardship
  drives them to pray, and danger they have known makes them flee it sooner.
- **The end:** age, hunger, illness or disaster ends their life, and an alert tells you.
- **Seeing them:**
  - press **/** with nobody at the cursor to keep the camera on them until you move it;
  - **Tab** opens their page in the Book: what they do and want, and their life, each memory with
    the age they were;
  - **Shift+/** lets them go back to the crowd.

They change nothing the world counts. The numbers are in `settlement_rules.json` (`Villagers`).

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
- **Your hand.** R picks up what lies under the cursor where your influence reaches, and R again
  sets it down; Shift+R throws it.
  - **Villagers (ten at a time):** lift them out of a burning, shaking or flooded village and set
    them down in a safe one, and they and everyone who sees it love you for it. Taken from a safe
    home they are afraid. Set down on open land they walk to the nearest village; set down in the
    sea they drown.
  - **A tree** from green land, which you can plant elsewhere, or **a rock**: thrown, it wrecks
    houses and kills where it lands and leaves a crater. Striking your own followers is cruel, and
    the world will see you so.
  - **Your Creature:** it waits in your hand until you set it down; thrown, it is hurt and resents
    it.
  - **Offerings (Ctrl+R):** your hand takes an offering from the stores of a village that follows
    you: a quarter of its food, wood and stone, and its people are glad you accept it (ADR-039).
    - R gives it to the village or your Creature you point at.
    - A village's stores fill, and it is grateful by its need, so feed the starving from the rich.
    - Your Creature eats the food and is the more loyal for it, and the wood and stone go home.
    - Generous gods are seen as generous.
  - **It feels in your hand (spec §55, ADR-057):** what you hold hangs below your palm and swings
    as you move, lagging behind a quick sweep and swinging past when you stop; a held Creature
    dangles the same way. When your hand presses down, to cast or where a throw lands, the rubble
    and debris about it are knocked away.
- **Up close (ADR-057).** The camera keeps above the roofs as well as the ground, so on a steep
  village's edge it never looks out from inside a house. Villagers whose rounds cross a river walk
  over the bridge where their road has one.
- **Divine Acts.** A Greater God can do the dearest miracles of all, from the Domains panel (G):
  Hallow the Land (its blessing never fades), Wake the Volcano and Still the World (every quake,
  fire, flood, storm, swarm, blight and drought ends at once). The world's history remembers each.
- **Reshaping the land (ADR-045).** Three more Divine Acts change the land for good:
  - **Raise an Island:** point at the open sea, and new land rises for villages to settle.
  - **Split the Mountain:** point at a mountain, and a gorge opens through it, down to the low ground
    on either side. A river may find its way through.
  - **Turn the River:** point at land below a river within 900 m, and the river runs there instead,
    dammed below where it leaves. The land it now reaches has water, and its old course dries.

  The rivers, biomes and regions follow the new land, saves keep it, and a shared game shows it to
  everyone. The numbers are in `divine_rules.json` (`Land`).
- **The Book of the World (Tab).** Four pages: the **World** (every god's standing, ties and
  Creature, the race for victory, the dangers under way and the holy sites), **Religion** (your
  followers village by village, with the failing ones in red, every god's faith, the Old Faiths and
  the godless), **Miracles** (every miracle with what it costs you and what it still needs; Aim
  selects one) and your **Creature** (its growth, paths, what it has learned, remembers and thinks
  of the others). Backtick shows the whole island from afar, and again brings you back.
- **Prophets and elders.** Some people matter more than the rest, and the world follows each of
  them by name:
  - **Elders:** every village of 40 or more chooses an elder of the god most of it follows, who
    keeps its faith and calms its doubt. When the village comes to resent that god, the elder
    turns to another, and the village doubts the god they left. When the village leaves that god
    for another, the elder follows it.
  - **Prophets:** a village devout to you (most of it yours, in strong faith) may call a prophet.
    As a Lesser God you can also anoint one with **Anoint a Prophet** (Shift+1) over a village
    where four in ten follow you. A prophet preaches where they stand, then walks to the next
    village with the most people not yet yours, winning the godless and some of your rivals'
    people in every sermon. You may have two at a time.
  - **Martyrs:** a prophet caught by danger (a quake, fire or flood) may die a martyr, and your
    followers nearby hold to you the harder.
  - Prophets and temples carry your influence where they stand, so you can act beside a prophet
    on the road or a temple to you even far from your followers.
  - Look for the robed figures in each god's colour: a prophet carries a staff under a halo. The
    alerts, the history, the book's World and Religion pages and F1 follow them.

A good start: Food or Heal on the neighbours of your home village, then Bless Crops when your
followers grow hungry. Try Lightning to see how fear behaves; but beware, a bolt can set dry woods
alight, and a forest fire drives off the game the villages hunt (ADR-041).

## Your rivals

Three gods share the island with you, each with a village far from the others and a Creature of
its own: Vael the Tyrant (Gristle), Seren the Guardian (Thistle) and Kaio the Trickster (Quill).
A large world adds Aurel the Zealot (Ember) and Maren the Nature Keeper (Fern), and a huge one
Thorn the Warlord (Brute) and Ysolde the Isolationist (Wisp) as well (see
[World sizes](#world-sizes)).
The line under your HUD shows each rival's followers, its share of all belief and where you stand
with it. The line below that shows the race: who holds most of the world's belief and how long
until they win. Hold 60% of all followers for ten minutes and you win; let a rival do it and it
wins. Allies count together (see [Diplomacy](#diplomacy)). The world goes on either way. A world
may also be made to be won by conquest or culture, or by nobody (see [Winning](#winning)).

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

- **First you must meet a god** (ADR-036). You know only the gods your people have met. A god you
  have not met reads "Not yet met" in the panel and "not yet met" on the HUD, and you can neither
  ally with it, war on it, sign with it nor ask anything of it. Gods meet when:
  - villages they lead lie within 500 m, or one village follows both;
  - a road joins their villages;
  - their Creatures meet;
  - one's deeds touch the other's people;
  - they sit in the Divine Council, which brings every god together at its first session.

  An alert tells you whom you met and how, and the history keeps it.
- **The AI gods make plans** (ADR-036). Every 30 seconds each AI god sets itself up to three
  goals:
  - protect its home from the beast, an enemy's Creature or the plague;
  - convert a village within reach, or win back its doubting followers;
  - befriend or weaken a god it knows;
  - prepare its people for the plague or a drought, or slay the ancient beast.

  Its miracles, Creature, diplomacy and villages lean towards its goals. Well informed of a rival,
  you see its goals in the panel: "it means to weaken you and convert Brightwater".

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

### Treaties

Under "Treaties and requests" on a god's card, propose any treaty the god could accept now. The
row at the top of the panel chooses how long your treaties last (two minutes up to "until
broken") and whether you sign them in secret.

| Treaty | What it does |
| --- | --- |
| Non-aggression pact | Neither may declare war on the other until the pact is broken. Signing it in war makes peace. |
| Defensive pact | You defend the other god: when its enemy (or anyone) declares war on it, you join the war. |
| Mutual defence pact | Each joins the other's wars against any aggressor. |
| Joint war | Both make war on a god you name, and neither may make a separate peace. |
| Shared influence | Each casts in the other's influence as an ally would. |
| Religious access | Your temples' missionaries reach every village the other leads, and its people are open to you. |
| Creature truce | Your Creatures never fight. |
| Disaster assistance | Each answers disasters among the other's people. |
| Territorial agreement | Neither casts miracles on the villages the other leads. |
| Tribute | One pays the other Divine Power every minute; a payer that cannot pay breaks it. |
| Migration agreement | Each welcomes the other's migrants, whatever its refugee policy. |

- **Secret treaties** are known only to the two gods until they are invoked or leak. The god a
  secret pact was aimed at remembers the plot; if it was your ally, you betrayed it.
- **Breaking a treaty** (Break, on your treaty) is a betrayal. The partner trusts you much less,
  and so does every god that knew of it. Your word counts for less in every later treaty.
- **Favours.** When you save another god's followers in great need, it owes you a favour. Its
  card then offers to call it in: Divine Power, war on your enemy, its vote at the council, a
  territorial agreement, or help for your people. A god that refuses what it owes is remembered
  for it. You may owe favours too.
- **Absorbing an ally.** You may break an alliance and take a share of your ally's followers
  wherever you are followed more than it is. It is a betrayal, and the alternative to sharing a
  victory. An AI ally that means to betray you absorbs you instead when that takes it enough of
  your followers (ADR-040).

### The Divine Council

Press U for the council.

- **What summons it:**
  - a betrayal summons it to condemn the betrayer;
  - a god rising to dominate summons it to make war on that god together;
  - a disaster summons it to coordinate the response;
  - otherwise a regular session every five minutes takes up a motion a god has tabled.
- **Voting:** the vote lasts a minute. A motion passes with more votes for than against and at
  least two for. Under a public ballot the gods remember who voted against them; a secret ballot
  shows only the count. A god that promised you its vote must vote as you do.
- **Decrees:**

  | Decree | Effect, for eight minutes |
  | --- | --- |
  | Condemn a god | Every god trusts it less, its followers doubt it, and AI gods turn on it more readily. |
  | Protect a holy site | No harmful miracle may fall there. |
  | Prohibit smiting, cataclysms or terror | Lightning and judgement, fire, quakes and floods, or terror, everywhere. |
  | Coordinate the response to disasters | Help for a village struck lately costs half. |
  | Pantheon-wide war | Every god that voted for it goes to war with the god named. |
  | Neutral territory | No god may convert or strike in the village. |

- **Defiance:** decrees bind by reputation. Defying one is possible, but every god trusts you
  less.

### War and intrigue

- **War goals.** Under "Treaties and requests", declare war for conquest, tribute, a holy site the
  god keeps, or to humble its Creature ("Declare war" alone means conquest).
  - The war's line shows its score. Converting the enemy's villages counts most, then defeating
    its Creature and taking its sites. Striking its people counts least, so slaughter is not the
    road to victory.
  - With your goal met and a lead, "Demand surrender": a conquest cedes half the enemy's
    believers in one of its villages, a site war hands you the site, a tribute war brings ten
    minutes of tribute, and humbling keeps the enemy out of your villages.
  - Repel an attack well enough and you may demand tribute yourself.
- **What you know.** The HUD shows a rival's following exactly only if you are well informed;
  partly informed, roughly; in the dark, not at all. You learn of a god through:
  - an alliance or any treaty with it;
  - your own followers living among its people;
  - your Creature's visits to its villages;
  - the Knowledge domain.

  Well informed, the diplomacy panel shows its Divine Power and whether an ally is weighing
  betrayal.
- **Veiled miracles (T).** A veiled miracle costs twice as much and nobody knows it was you:
  kindness earns no faith or love, and a strike makes the people doubt their own god for not
  protecting them. The god whose people it reached may trace it, the more surely the more it
  knows of you.
- **Proxy acts.** In the Settlements panel of a village another god leads, where your influence
  reaches:
  - incite rebels against that god: resentment, fear and unrest, perhaps a revolt;
  - or spread propaganda: doubt.

  Your agents then rest four minutes, and the god may trace them to you.
- **Holy wars.** A militarist village may go to holy war, of its own will, on a neighbour that
  follows another god: raids kill and convert.
  - If its people follow you, the Settlements panel lets you urge them on (fiercer raids) or hold
    them back (an earlier end, but they love you less).
  - It ends after four minutes, when the zeal fades, when the gods make peace pacts, or when the
    attacked village turns.

## Weather

Weather comes and goes on its own. Clouds blow in from the sea on the prevailing wind, rain on the
island and thin away as they drift on. Storms are dark, windy and full of lightning. From afar you
see the clouds; up close you see the rain, and the light dims under a storm.

- **Spells.** The island is in normal weather, a dry spell (little rain, and the land dries out), a
  wet spell (frequent rain and storms), a heat wave or a cold wave. The line under your HUD names
  the spell and how long it lasts; alerts announce dry spells, heat and cold waves and their end.
- **Snow.** Rain on land cold enough (high ground, and lower land in winter or a cold wave) lies
  as snow. Nothing grows or burns under it, and when the land warms it melts into the soil, which
  swells the rivers in spring. Snowing clouds drop slow white flakes.
- **Fog.** Calm, cool, humid weather is fog: mist and drizzle.
- **Hurricanes.** A storm born over warm sea in summer or autumn may grow into a hurricane. It
  keeps its winds far inland and tears down houses where it passes; shields, the Protection
  domain, shelters and architecture spare some. Other storms never reach its strength. An alert
  warns you as soon as one forms: "A hurricane is approaching the western coast" (ADR-041).
- **Heat and cold waves.** A heat wave bakes and dries the land, lights fires, spoils stores and
  sickens the weak; a cold wave freezes and snows, spoils the harvest and sickens the homeless.
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

## Seasons

A new world turns through four seasons unless you choose an even climate on the start screen
(or `--seasons=off`). Each lasts two and a half minutes, so a year is ten minutes. (The history
counts its years faster, a minute each.)

| | Spring | Summer | Autumn | Winter |
| --- | --- | --- | --- | --- |
| Warmth | | warmer | | colder: snow on the hills |
| Rain | more | less | most | |
| Harvest | x0.9 | x1.25 | x1.3 | x0.5 |
| Fire | | far more | | far less |
| Stores | | spoil | spoil a little | |
| Health | | | | the cold sickens, the homeless most |
| Migration | | | | few set out |
| Extremes | | heat waves, hurricanes | hurricanes | cold waves |

Plan for winter: villages that come into it with full stores get through, and those that do not
go hungry. The line under your HUD names the season and the time left in it ("Winter of year 2
(1:20 left)"), and an alert marks each season. The seasons are in `Seasons` in
`game/Data/weather.json`; heat and cold waves, snow, fog and hurricanes are in `Extremes`.

## Disasters

- **Wildfires.** Dry, green land burns. A spark from a village's hearths, a thunderstorm on parched
  land, an earthquake or a god sets the fields alight, and the fire spreads, faster downwind and
  where the land is driest. Rain and the villagers put fires out. Fire scorches the land, which
  yields less until it regrows, and a fire in a village burns its houses, people and food. Fires
  come with droughts and dry spells.
- **Earthquakes.** Now and then the ground shakes for a few seconds. Houses collapse, people die,
  stores spill and fires may break out, worst near the epicentre; the camera shakes with the
  ground.
- **Cracked houses (spec §94, ADR-048).** Every disaster that strikes a village's houses (a quake, a
  fire, a flood, a storm, a falling star and the rest) cracks those it leaves standing. A village whose houses are cracked loses more of them in the next
  disaster, so two quakes close together do far more harm than one; its people mend the cracks
  over a few minutes. Houses stand up to floods better than to the other disasters, and a flood
  takes a quarter fewer of them.
- **Debris.** When houses fall near the camera, their planks, stones and thatch fly and tumble to
  the ground, and the stones a falling star or a thrown rock throws up do the same; a thrown rock
  or tree lands and rolls where it struck. The pieces settle and fade after nine seconds. The
  simulation decides what falls; the debris is only shown.
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

## Hazards

- **Tsunamis.** A strong earthquake near the coast raises a great wave that floods the low land
  around the shore. A sea wall spares a village most of it.
- **Landslides.** Steep slopes give way when the soil is soaked or an earthquake shakes them,
  burying the land below in rubble. Rubble blocks the way for migrants and Creatures until it is
  cleared, and the village it buries is marked "cut off".
- **The volcano.** The island's highest peak, if high enough, is a volcano. Now and then it wakes:
  it rumbles with tremors for 40 seconds, then erupts for a minute. Lava runs down its steepest
  slope and burns everything in its way, and ash falls around it, harming the land, sickening the
  people and spoiling their stores. A plume of smoke rises while it rumbles, and embers and a red
  glow while it erupts.
- **Tornadoes.** A thunderstorm over warm land may spin one down: a grey funnel that runs with the
  storm for half a minute or so and tears down the houses it passes.
- **Falling stars.** Rarely, a meteor strikes: a crater, fire, an earthquake and ruin around it.
- **Blight.** Crops sicken in warm, wet fields, and blight spreads from field to field. Blighted
  fields (yellowed on the land) yield less, and their villages are marked "blight". Dry weather,
  fire or Bless Crops ends it.
- **Locusts.** Swarms rise from hot, dry land and drift with the wind, eating the stores of the
  villages they pass over. Rain drowns them.
- **Marks on the land.** Earthquakes crack the ground, landslides scar the slopes and meteors
  leave craters; the marks fade over ten minutes.
- **Preparing.** Villages preparing for disaster raise sea walls by the sea, dams beside rivers
  and lakes, and evacuation routes, which save half the lives any disaster would take. Villages
  putting food first irrigate their fields beside water, which saves their harvest in dry weather.

The alerts and the history tell of each hazard, and the Danger overlay (O) shows rubble in grey and
blight in purple. The rules are in `Hazards` in `game/Data/disasters.json`.

## Great events

Now and then the whole world lives through something together (spec §52). Each is announced, named
in the top bar and the book, and remembered in the history.

- **The plague** breaks out in crowded, sick or polluted villages and spreads to their neighbours
  and along roads. The sick lose health, and those who recover are immune for about an hour.
  Hospitals and medicine slow it, and your healing miracles cure it. When three villages are sick
  at once it becomes a great plague, and the gods' council meets to coordinate the response: help
  costs less in stricken villages.
- **Eclipses:** now and then the sun goes dark for 45 seconds. Every believer is afraid, and a
  miracle worked in the dark awes its witnesses twice as much.
- **Reformation:** when your followers doubt you and their villages have learned enough, your faith
  reforms. Most of the doubt goes and faith grows, but some of the doubters leave you.
- **The ancient beast** sleeps in a lair on the island's heights. Once the world has grown, a great
  quake or the volcano may wake it, or it wakes by itself. It walks to the nearest village and
  ravages it, then the next. Fight it with your Creature (send it with V) and with Lightning and
  other smiting miracles; walls spare a village half its harm. No god easily brings it down alone,
  so the council's response makes help cheaper. When it falls, every god who harmed it shares the
  glory with its followers.
- **The magical anomaly:** now and then power surges at a place, a column of violet light, for three
  minutes. Miracles there cost half, and each god draws power there by its influence.
- **World war, great drought and great migration:** when most of the gods are at war at once,
  everyone grows afraid. When the whole island stays parched, the council is called. And when a
  fifth of the people are on the road, the world remembers it.

## The island's biomes

The island has every biome of §5 (ADR-038).

- **What the climate decides:**
  - grassland and forest;
  - highland on the heights;
  - tundra, the cold band below them;
  - desert where it is hot and dry;
  - swamp where it is wet and low.
- **What the place decides:**
  - islands off the main one;
  - coasts, the low land by the sea;
  - river valleys, the low land along rivers and lakes;
  - the dark volcanic land around the island's highest peak, which is the volcano.
- **What each biome does:** its fertility and growth set what the land yields, where villages
  are founded, how much wood and stone it holds, and how readily it burns.
  - River valleys are the richest land, and villages crowd into them.
  - Volcanic ash is fertile but bare and full of stone.
  - Deserts yield little and dry out.
  - The tundra grows conifers.
- **Seeing it:** each biome has its own ground (ADR-051): grass, forest floor, highland earth,
  desert sand, tundra moss, swamp mud or volcanic ash, made in code with its own pattern and
  relief, blending into its neighbours' over a few metres. Rock covers steep slopes, snow the
  cold heights, and sand the land nearest the sea. The fallback renderer shows the biomes' colours
  instead. The Book of the World's World page counts the land by biome, and the F3 map's first
  layer shows each region's biome.

The rules are in `game/Data/worldgen.json` (`Biomes` and `Landforms`), and the colours and the
ground's textures in `game/Data/biome_palette.json` (`Ground`).

## The land's life

- **Harm.** Fires, floods, earthquakes and lightning harm the life of the land they reach. Harmed
  land yields less and holds fewer animals.
- **Healing and scars.** Harm heals over time, but harm done again and again scars the land for
  good: a scar never heals by itself. An alert tells you when the land around one of your villages
  is scarred, and the history remembers it.
- **Nature.** Land whose villages mostly follow a god of Nature heals three times as fast and
  loses its scars, and Regrowth heals harm and scars wherever it falls.
- **Wildlife.** Wild animals return as far as the land allows (its cover, unburned, unharmed and
  unpolluted) and die in fires. Villages hunt the animals around them for food.
- **Pollution.** Villages of more than 250 people pollute their land, which yields less until the
  pollution clears, slowly.
- **Seeing it.** The Nature overlay (O) paints the land from red (lifeless) to green (rich), scars
  in dark violet and snow in white.

The rules are in `game/Data/ecology.json`.

## Society and priorities

- **A village's life.** Safety falls with danger and war and rises behind walls. Happiness
  follows food, health, water, safety and the people's love for their gods, and falls with fear
  and homelessness. Order follows happiness, unity of faith and peace, and falls with a schism.
  Unhappy or unsettled villages lose people.
- **Wealth and learning.** Villages put by goods from their surplus harvests and from trade with
  neighbours, and learn faster when they are rich and settled.
- **Technology.** Agriculture (richer harvests), architecture (lighter building, sturdier
  houses), medicine (healing), warfare (defence), navigation (faster migrants, more trade),
  engineering (works against floods and fires), science (learning) and religion (stronger
  worship). Each grows with learning, fastest where the village's priority lies, and spreads
  from its most advanced neighbour. None passes level 5.
- **Priorities.** Press N with the cursor over a village to see it. If at least 40% of its people
  follow you, choose what it puts first; a course holds at least a minute. AI gods set the course
  of the villages they lead.

  | Priority | What happens |
  | --- | --- |
  | Increase food | More is harvested; a granary keeps more and saves food from disasters; irrigation waters the fields beside water |
  | Improve health | Healers tend the sick; a hospital saves lives |
  | Grow | More births while there is food and room |
  | Focus on worship | Twice the worship, so more power for its gods |
  | Prepare for war | Watchmen; walls turn attacks away |
  | Prepare for disaster | Levees and dams beside water, sea walls by the sea, firebreaks, shelters, cisterns and evacuation routes |
  | Expand | Settlers found new villages on free land nearby |
  | Build a temple | A temple to you: half again as much power from its worship |
- **Buildings** rise once the village has put by the wealth for them. Temples (roofed in their
  god's colour) and walls show in the village.
- **A village looks like what it worships** (ADR-042). Once most of a village follows one god, it
  takes that god's style:
  - a loved god of nature, life or plenty brings flower beds and bushes among the houses;
  - a feared god of war, death or fire brings dark obelisks and burning braziers about the shrine;
  - a god of knowledge, justice or protection brings a ring of white columns.

  The style comes from the god's domains and traits, the village's doctrines and how its people
  feel, and it grows with the god's hold. F1 shows the selected village's style and why. The
  numbers and colours are in `settlement_view.json` (`Style`).

The rules are in `game/Data/society.json`, with what technology and buildings do in
`settlement_rules.json` (`Civics`), `disasters.json` (`Preparedness`), `divine_rules.json` and
`migration.json`.

## Culture

- **Doctrines.** Each village holds §33's doctrines more or less firmly. They follow what it
  lives through and the god most of it follows, and what they do:

  | Doctrine | Grows with | What it does |
  | --- | --- | --- |
  | Pacifism | Peace, safety, a merciful or compassionate god | Harm by its god stirs more doubt and resentment |
  | Militarism | War, a Prepare for war priority, a War or ambitious god | Its people accept force from their god |
  | Sacrifice | Fear of its god, a Death god | The fearful worship harder, and resent less |
  | Charity | Love of its god, help in need, a compassionate or generous god | Gratitude for help runs deeper; full stores feed hungry neighbours |
  | Nature worship | A blessed land, blessings of the land, a Nature god | Blessings move it more, and fires and quakes outrage it |
  | Knowledge seeking | Learning, a Knowledge god | It learns faster, and turns from the gods a little |
  | Expansion | An Expand priority, crowding, an ambitious god | It sends settlers sooner |
  | Isolation | Few neighbours, a god that turns refugees away, war | Other gods and strangers find it closed |
- **Turning from the gods.** Doubt among its followers, people who follow no god, an Old Faith,
  resentment and science raise a village's secularism; faith holds it back. Agnostic villages
  (30%) worship and convert less, atheist ones (55%) still less, and anti-divine ones (80%)
  turn people away from every god and spread their reason to their neighbours.
- **Dependency.** People you help when they need it depend on you: they worship harder, and
  doubt you in hard times when you do not come.
- **Resentment.** Fear breeds resentment and love dissolves it. Harming your own followers breeds
  it fast, and more among pacifists. An unsettled village that fears and resents its god revolts:
  many renounce it and its temple is torn down.
- **Missionaries.** A temple wins people who follow no god in the villages around it.
- **Reputation.** The world remembers what each god does: help, gifts, harm, cruelty to its own,
  vengeance, upheaval, deceit, faithfulness and wars. Over time this earns traits, which fade if
  the god changes its ways. A god known for mercy that strikes its own shocks them the more.
- **Where to see it.** The Settlements panel (N) lists the doctrines a village holds, how far it
  has turned from the gods, and its people's love, fear, need and resentment of each god. The HUD
  and the diplomacy panel (P) say how the world sees each god. The Culture overlay (O) colours
  each village's lands by its strongest doctrine, grey where it has turned from the gods.

The rules are in `game/Data/divine_rules.json` (`Culture`).

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
- **It gives up a pointless errand** (ADR-047): when the village it went to feed no longer needs
  food, the house it went to help build is done, the stores it went to raid are empty or the
  village it went to attack now follows an ally. You are told, and it decides again.
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
- **It grows.** Its deeds earn experience. Each level, up to 10, makes it bigger, stronger,
  faster, cleverer (it learns faster), tougher, and carries your influence further. Its card shows
  these attributes.
- **It walks paths.** At levels 3, 6 and 9 it takes up the path its deeds have called it to most,
  so how you raise it decides what it becomes:

  | Path | Called by | What it does |
  | --- | --- | --- |
  | Healer | feeding | heals the people of the villages it helps |
  | Guardian | building, mercy | your villages near it suffer less from disasters |
  | Missionary | play, friendship | wins the godless of the villages it visits to you |
  | Nature Keeper | foraging, wandering | heals the land where it goes |
  | Warrior | fights won | fights harder |
  | Destroyer | attacks | its attacks kill more |
  | Trickster | stealing food, fleeing | sows doubt in your rivals among the villages it visits |
  | Commander | going where you send it | your villages near it are harder to attack |

  A reward calls it further down the paths of the deed. No Creature walks more than three.
- **It can be lost for a while.** If it starves, or loses a fight, it is banished for a minute
  and comes home weakened.
- **It has a character** (ADR-037). Eight traits make it up: kindness, aggression, laziness,
  curiosity, courage, protectiveness, jealousy and playfulness. Its card names the strong ones in
  §26's words, for example "brave, protective, playful and loyal" (loyal or independent come from
  its loyalty).
  - A brave Creature fights where a coward runs.
  - A protective one stands its ground near your villages, and goes by itself to defend them from
    the ancient beast.
  - A jealous one makes rivals of stronger Creatures.
  - A playful one plays with the others.
  - Its character changes:
    - rewarding a deed strengthens the traits behind it (reward its running away, and it grows
      cowardly);
    - rewards ease its jealousy and punishments feed it;
    - victories make it braver, while defeats and wounds cow it and make it jealous;
    - play makes it more playful.

  Its species shades it from birth: a Stonehide is brave, a Lumen playful, a Swiftclaw jealous.

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

The rival gods' Creatures are a Stonehide (Vael), a Lumen (Seren) and a Swiftclaw (Kaio), and in
larger worlds a Swiftclaw (Aurel), a Mossback (Maren), a Stonehide (Thorn) and a Lumen (Ysolde).

## Sound and settings

Every sound is made by the game itself:

- the music, which grows tense in war and triumphant when you dominate;
- bells for kind miracles and a crack for lightning;
- thunder and roars heard from across the island, where they happen;
- the earthquake's rumble, fire, and the wind and rain where you look.

The settings page (start screen or Esc) sets fullscreen, vertical sync, shadows, the lighting
effects (ambient occlusion and a soft glow), the render scale, the citizens shown, the
vegetation (how far the grass and flowers reach and the forests are planted), the terrain quality,
the particles (rain, fire, smoke and the miracles' looks), the simulation budget, and the loudness
of everything, the music, the effects and the wind and rain (spec §105, ADR-056). Citizens,
vegetation and terrain quality take effect when the game next starts.

The simulation keeps to its budget, 6 ms a frame unless you choose another (2–16 ms). When a big
world at ×16 needs more, the ticks still due wait for the next frame, so the game runs a little
slower than asked instead of stuttering. The debug panel's performance lines show the budget, how
often it cut a frame short, and the ticks waiting or dropped.

## World sizes

The start screen's "World size" (or `--world-size`) chooses the island of a new world:

| Size | Island | Villages at the start | Gods | Rivers |
| --- | --- | --- | --- | --- |
| Small (the default) | 2 km across | 8 | 4 | up to 7, 4 of them to the sea |
| Large | 4 km across | 20 | 6 | up to 14, 8 of them to the sea |
| Huge | 8 km across | 40 | 8 | up to 24, 14 of them to the sea |
| Vast | 12 km across | 48 | 8 | up to 24, 14 of them to the sea |

- **Regions** are 64 m across (96 m on the vast island), so a larger island has more of them: 64
  or 128 a side.
- **Pantheons.** As many gods may win together as the size allows: two on a small island, three
  on a large one, four on a huge or vast one (see [Winning](#winning)).
- **Long games.** Six or eight gods hold each other in check, so larger worlds take longer to
  win: in ten hour-long games with a passive player, four large worlds and one huge one found a
  winner.
- **More gods.** Larger worlds bring new rivals, each with a Creature, in this order: Aurel the
  Zealot, Maren the Nature Keeper, Thorn the Warlord and Ysolde the Isolationist.
- **Seeing a large island.** The camera pulls back further and the fog thins, so the far shore
  still shows. Labels make way for more important ones: holy sites first, then villages by
  population, then Creatures and migrants. A label with no room shows its name alone, or nothing
  until you come closer. The rivals' line on the HUD wraps between gods.
- **Saves** keep their world's size. Hosting from the start screen makes a small world.

The sizes, with the larger worlds' extra village and river names, are in
`game/Data/world_sizes.json`; the extra gods are `ExtraGods` in `game/Data/gods.json`.

## Winning

How a world may be won is chosen when it is made (the start screen's "Victory", or `--victory`):

- **Divine Ascendancy** (the default): hold 60% of all belief for ten minutes. Allies can win
  together, as many as the world's size allows (two on a small island, three on a large one, four
  on a huge or vast one): each god counts with its strongest allies only, so a god allied with
  more must choose whom to share its victory with.
- **Conquest:** bring every god outside your alliances below 5% of all belief, and keep it there
  for ten minutes. Your allies win with you.
- **Culture:** lead three in four of the living villages (more of their people follow you than
  any other god) for ten minutes, while some rival keeps at least 5% of all belief: dominant
  without wiping your rivals out.
- **Every victory** puts all three in play. The first reached is the only one.
- **Sandbox:** nobody wins, and the world goes on as long as you like. Dominance still counts for
  the council and the AI gods.

The HUD's race line follows the victories in play: "Vael has all but crushed its rivals: it
conquers the world in 9:12", "You lead 6 of 8 villages: you win the world's culture in 9:12" or
"A sandbox: no god can win". When ascendancy is off it says what you must do, for example "You
lead 2 of 8 villages; lead 6 for 10:00 to win the world's culture". Alerts ("Vael has conquered
the world."), the history ("The Conquest of the World by Vael") and the end screen say how the
world was won. The end screen says who won, how your god and Creature ended up, and what
the world remembers; you can keep watching, start a new world or go back to the start screen.

The shares and the time are in `game/Data/divine_rules.json` (`VictoryShare`, `ConquestShare`,
`CulturalShare` and `VictorySeconds`).

## Debug tools

- **F1** shows the debug panel (hidden at the start): frame rate, tick, seed with the island's
  width and the world's victories ("Seed 42   4096 m island   Ascendancy, conquest and cultural
  victory"), terrain renderer,
  camera band, the performance metrics (frame time, the simulation's share of a frame and a tick's
  time, ticks per second, memory, the engine's objects, nodes and draw calls, the citizens drawn and
  the last save's times) and the world's load (settlements, people, gods, Creatures, weather cells,
  disasters, groups on the road, notables and followed villagers), one line per god (power and income, rank, followers, the next rank), each AI god's
  goal now and its top candidate actions with their scores and reasons, its plans (the goals it
  holds with their weights and ages, when it plans next, the gods it knows) and the goals it is
  weighing, its diplomatic moves with the factors behind them, the climate (the season and what it and a heat or cold wave
  change), the weather (the spell, the wind and the strongest cells),
  disasters (earthquakes and the fiercest fires), hazards (the volcano, tornadoes, swarms, blighted
  and buried regions, and marks on the land), migration (the groups on the road and every
  god's refugee policy with the AI's reason), the Creature
  view (level, health, loyalty, morality, attributes, paths and callings, activity, its state and
  behaviour tree, needs, personality, its best options with
  their scores, how it would answer the nearest Creature, its combat skill, scars and power, its
  view of every Creature it has met with their latest encounters, its learned preferences by
  situation and its latest memories), every followed villager with what they do and their faith,
  and for yours their needs, options with their reasons, danger and last memory, the land's
  reshapings, the region
  under the cursor with its statistics, soil, rain, wind, snow, wildlife, harm, scar, pollution and
  biodiversity and every god's influence there, one
  line per settlement, and, for the settlement nearest the cursor:
  - its belief: the share that follows no god, its hardship and comfort, and for each god the
    followers, Faith, Love, Fear, Doubt, worshippers and the power they raise;
  - population / housing, houses and health;
  - food stored / storage, the net change per second, production and consumption;
  - the jobs (farming, building, not working) and the farmland;
  - expected births and deaths per second, the house under construction and the time to the
    next settlement step;
  - the simulation tier, the camera distance that changes it and, in Tier 2, the citizens
    shown and the people each stands for, and the local navigation grid's cells, blocked cells and
    ways searched;
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
  vegetation come blessing, your god's influence, the soil's water, fire, scorch, fresh water,
  flood, snow, wildlife, damage, scar, pollution, blight and rubble, all on a fixed 0 to 1 scale.
- **O** cycles the map overlays: influence, religion, faith, food, weather, danger, migration,
  culture, nature, population, disaster risk, political control, navigation and authority:
  - **Navigation** shows the land walkers may cross, the open roads, the land buried in rubble they
    go round, and the ways every migrant group, prophet, followed villager and Creature is on.
  - **Authority** shows who commands the god each village follows: you in gold, another player in
    blue, the AI in grey (ADR-044).
- **K** brings the nearest other Creature beside yours, so they meet at once and you can watch them
  play, run or fight.
- **F4** grants your god 500 Divine Power.
- **`.`** pauses the simulation and runs exactly one tick, so the panel shows what a single
  tick changed. Space resumes.
- **`,`** pauses and runs the ticks up to and including the next settlement step, so the panel
  shows what one step changed.
- **F6** spoils every settlement's food stores and **F7** brings settlers (every settlement
  grows by half, at least 10 people). Use them to drive famines and recoveries.
- **F8** starts the next weather spell (normal, dry, wet, heat wave, cold wave) for its longest
  length, and **F10** calls a storm at the cursor, short of a hurricane. Use them to drive
  droughts and storm damage. **Y** begins the next season.
- **I** starts the next hazard of the cycle at the cursor: a tornado, a landslide, a meteor,
  blight in the fields, a locust swarm, a wave on the nearest coast, then the volcano's waking.
- **F11** starts an earthquake (magnitude 6.5) and **F12** a fire at the cursor.
- **L** makes the nearest river within 400 m burst its banks, or sends a wave over the low land at
  the cursor. The debug panel lists the rivers' levels and the lakes, and a selected settlement's
  water, access to water and river; the region map gains fresh water and flood layers.
- **M** sends 20 people out of the village under the cursor to the best place they can reach.
- The debug panel also lists every god's domains, each holy site with its keeper, its claimant
  and who leads its influence by how much, and the selected settlement's Old Faith and sanctuary.
  `--rank` and `--domains` try out domains at once, and `--focus=siteN` looks at a holy site.
- The command-line options below reproduce a world (`--seed`, `--load`, `--world-size`,
  `--victory`), force a renderer
  (`--terrain`), start from a grown settlement up close (`--advance-ticks`,
  `--camera-distance`) and script smoke runs (`--quit-after-frames`, `--screenshot`).
  Unattended runs end by logging one summary line per settlement, with its tier and the
  citizens shown, then the performance lines with a save timed, and the world's load.
- The selected settlement's dangers line in the debug panel shows its houses' integrity (1 is
  sound), and the performance lines count the debris pieces and those still moving.
  `--disaster=meteor` and the I key's falling star strike villages as a natural one does, and
  `--hand=throw` throws a rock or a tree at the focused settlement.
- **World streaming (spec §84, ADR-050).** The land near the camera carries its detail and the
  rest stays simulation data: forests are planted only within 2.1 km of the camera, a few chunks
  a frame, the fallback terrain is drawn in 2 m detail within 650 m and coarser beyond, and fires
  are drawn within 2.5 km (their villages' labels and the Danger overlay show the rest). A camera
  high above loads only what it could see. The debug panel's streaming line, also printed by
  unattended runs, says what is loaded; the radii are in `Streaming` in
  `game/Data/settlement_view.json`.
- **Stress testing (spec §104, ADR-044).** `--stress=on` makes the stress scenario: the vast island
  with 50 villages of 2,000 people (100,000 in all), 8 gods and their Creatures, 20 weather cells,
  and an earthquake, a fire and a flood at once, the camera over the busiest villages and thousands
  of citizens drawn. For example:

  ```bash
  godot --path game -- --stress=on --seed=42 --quit-after-frames=600 --screenshot=/tmp/stress.png
  ```

  On an Apple M3 Pro it runs at about 100 FPS with 7,000 citizens drawn, a tick takes 1.5 ms,
  and a 10 MB save is written in about 130 ms. A shared game on the stress world sends a player
  about 144 KB/s.

## Command-line options

Pass options after `--`, for example `godot --path game -- --seed=42 --terrain=fallback`.

| Option | Effect |
| --- | --- |
| `--seed=<number or text>` | Seed of the generated world (text is hashed). Default: `1234`. |
| `--load=<path>` | Load a save at startup, for example `user://saves/quicksave.json`. If it fails, a new world is generated. |
| `--terrain=auto\|terrain3d\|fallback` | Terrain renderer. `auto` (the default) and `terrain3d` use Terrain3D when it is available; `fallback` forces the built-in mesh renderer. |
| `--advance-ticks=<n>` | Run `n` simulation ticks (10 per simulated second) before the first frame, for example `--advance-ticks=9000` for a settlement of about 425 people. |
| `--camera-distance=<m>` | Starting camera distance in metres (default 380, clamped to 25–2600, or further over a larger island). |
| `--quit-after-frames=<n>` | Quit after `n` frames (smoke tests). The run prints its frame times, the performance lines with a save timed, and the world's load. |
| `--stress=on\|off` | Start the stress scenario of spec §104 (ADR-044): the vast island with 50 villages of 2,000 people, 8 gods and their Creatures, 20 weather cells and an earthquake, a fire and a flood, the crowd drawn in thousands. |
| `--wildlife=on\|off` | Take the camera to the herd of deer nearest the focused settlement at the start (ADR-054). |
| `--screenshot=<file.png>` | Together with `--quit-after-frames`, save a screenshot just before quitting (not in headless mode). |
| `--divine-power=<n>` | Your god starts with `n` Divine Power. |
| `--cast=<id>[,<id>...]` | Cast these miracles (ids from `miracles.json`) on the focused settlement at the start. |
| `--select=<id>` | Start with a miracle selected; scripted runs show its ring at the screen centre. |
| `--overlay=influence\|religion\|faith\|food\|weather\|danger\|migration\|culture\|nature\|population\|risk\|politics\|navigation\|authority` | Start with a map overlay shown. |
| `--book=world\|religion\|miracles\|creature\|villager` | Open the Book of the World on a page at the start. |
| `--overview=on\|off` | Start with the camera over the whole island. |
| `--focus=<n>\|creature\|site<n>` | Start looking at settlement `n` (1 is your home; `--cast` targets it), at your Creature's face or at holy site `n`. |
| `--judge=reward\|punish` | Reward or punish your Creature on the first frame (for captures of the hand). |
| `--roads=on\|off` | Every road the villages would plan is built at the start (for captures). |
| `--stores=full\|empty` | Every village's wood and stone start full or empty. |
| `--great-event=plague\|eclipse\|reformation\|beast\|anomaly` | Start a great event at the start: the plague in the focused settlement, an eclipse, your faith's reformation, the beast's waking or the anomaly at the focused settlement; the camera looks at the beast or the anomaly. |
| `--prophet=on\|off` | A prophet of your god rises at the focused settlement at the start (for captures). |
| `--villager=on\|off` | You follow one of the focused settlement's people at the start, the camera on them (ADR-043). |
| `--reshape=island\|gorge\|river` | Raise an island, split a mountain or turn a river near the focused settlement at the start, as the Divine Act would, and look at it (ADR-045; without `--camera-distance`, which looks at the settlement). |
| `--hand=villagers\|creature\|offering\|throw` | Your hand lifts villagers of the focused settlement, your Creature, or an offering from the focused settlement's stores, or throws a rock or a tree from beside the focused settlement at it, at the start (for captures). |
| `--debug-panel=on\|off` | Show or hide the F1 debug panel at the start. |
| `--diplomacy-panel=on\|off` | Show or hide the diplomacy panel (P) at the start. |
| `--weather=normal\|dry\|wet\|storm\|heatwave\|coldwave\|hurricane` | Start in a weather spell, or with a storm or a hurricane over the focused settlement. |
| `--disaster=quake\|fire\|flood\|tornado\|landslide\|meteor\|blight\|locusts\|tsunami\|eruption` | Start a disaster or a hazard at the focused settlement (the volcano wakes wherever it is, and the camera goes to it). |
| `--priority=<priority>` | Ask the focused settlement to put something first (Food, Health, Growth, Worship, Defence, Preparation, Expansion, Temple). |
| `--settlement-panel=on\|off` | Open the Settlements panel (N) on the focused settlement at the start. |
| `--doctrine=<doctrine>\|secular` | Give the focused settlement a doctrine in full (Pacifism, Militarism, Sacrifice, Charity, NatureWorship, KnowledgeSeeking, Expansionism, Isolationism), or turn it against the gods. |
| `--treaty=<treaty>` | Sign a treaty with your strongest rival at the start (NonAggression, DefensivePact, JointWar, SharedInfluence, ReligiousAccess, CreatureNonAggression, DisasterAssistance, MutualDefense, TerritorialAgreement, Tribute, MigrationAgreement). |
| `--council-panel=on\|off` | Open the council panel (U) at the start. |
| `--summon-council=on\|off` | Summon the Divine Council at the start. |
| `--gods-met=on\|off` | Every god has met every other at the start, so all can deal from the first moment. |
| `--war-goal=conquest\|holysite\|tribute\|humiliation` | Declare war on your strongest rival at the start, for that goal. |
| `--holy-war=on\|off` | Send the settlement the camera looks at to holy war on its nearest neighbour of another faith at the start. |
| `--veil=on\|off` | Cast veiled miracles from the start. |
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
| `--temperament=<words>` | Give your Creature these traits strongly at the start, separated by commas: brave, cowardly, kind, sadistic, curious, lazy, protective, jealous, playful, aggressive, loyal or independent. |
| `--world-size=small\|large\|huge\|vast` | The size of a new world (see [World sizes](#world-sizes)). An unknown size makes a small world. |
| `--victory=all\|sandbox\|<list>` | How a new world may be won: every way, no way, or a list of `ascendancy`, `conquest` and `cultural` separated by commas, for example `--victory=conquest,cultural`. Default: ascendancy. |
| `--seasons=on\|off` | Whether a new world turns through four seasons (default on; see [Seasons](#seasons)). |
| `--season=spring\|summer\|autumn\|winter` | Begin this season at the start, giving an even-climate world seasons (debug). |
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
  or uses the debug keys. Hosting from the start screen makes a small world won by Divine
  Ascendancy.
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

Saves are indented JSON of the simulation state, starting with `"SaveVersion": 26`. The scene tree
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
their holy sites when they are loaded. Saves from before Milestone 15, part 7 are won by Divine
Ascendancy alone. Saves from before part 8 keep an even climate, without seasons, with no snow or
harm and their land's animals at its cover. Saves from before part 9 start with no blight, rubble,
tornadoes, swarms or marks, and find their volcano when it is first needed. Saves from before part 10
start with Creatures that have walked no path, empty hands and no hallowed land. Saves from before part 12
start with no prophets or elders; their villages choose elders at once. Saves from before part 14
start with nobody sick and no great event, and set their first eclipse when loaded. Saves from before part 15
find their beast's lair and set their first anomaly when loaded. In saves from before part 16
every god has met every other, and the AI gods make their first plans when loaded. Creatures from before part 17
start with middling courage, protectiveness, jealousy and playfulness. Worlds from before part 18
keep their four biomes. Hands from before part 19 hold no offering. In saves from before part 22
no god follows a villager. In saves from before part 24 the land is as the seed made it. Saves from before part 13
grow each region's forest from its vegetation (without a new world's patches), give each village a
new world's stores of wood and stone, and have no roads yet. A save keeps its world's
size, how it may be won and its seasons.

Your settings are kept in `user://settings.json`; the start screen and the pause menu have a
settings page.

## Repository layout

```text
game/                 Godot project: scenes, client scripts (Pantheon.Client), data, materials, shaders
  Data/               worldgen.json, settlement_rules.json, divine_rules.json, miracles.json, gods.json,
                      creatures.json, god_ai.json, diplomacy.json, weather.json, disasters.json, migration.json,
                      history.json, domains.json, holy_sites.json, world_sizes.json, ecology.json,
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
- The game reads `res://Data/*.json` at startup, so every export must include those files (the
  presets in `game/export_presets.cfg` do).
- About one run in four, Godot warns at exit that a few ObjectDB instances were leaked. It happens
  since at least part 15 and never with `--verbose`; it is harmless and not yet traced.
- Four gods crowd the small island: a player who does nothing keeps only a few followers after ten
  minutes, and Seren the Guardian is the strongest AI god (kindness converts better than fear).
- The whole island is built at once rather than streamed in pieces: a huge world's heightfield
  takes 1.1 s to build. No world is larger than 12 km or holds more than eight gods.
- A history record names two winners, so a pantheon of three or four is told by its first two.
- Snow does not slow travel, grass and trees keep their colour through the year, and pollution
  does not depend on a village's technology. Biomes do not change how fast anyone walks.
- The representative citizens are presentation only: they show what the settlement's numbers
  say, but nothing can interact with an individual citizen until a later milestone adds full
  (Tier 3) agents ([ADR-008](docs/ADR/ADR-008-settlement-step-and-representative-citizens.md)).

## Third-party code

- [Terrain3D](https://github.com/TokisanGames/Terrain3D) 1.0.2: MIT licence,
  `game/addons/terrain_3d/LICENSE.txt`.
- [FastNoiseLite](https://github.com/Auburn/FastNoiseLite) 1.1.1: MIT licence, header of
  `src/Pantheon.Simulation/Noise/FastNoiseLite.cs`.
