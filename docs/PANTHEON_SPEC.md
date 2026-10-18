# PANTHEON

## Complete Game Design & Technical Architecture Specification

**Working title:** Pantheon  
**Genre:** God Game / Strategy / Simulation / Diplomacy  
**Primary inspiration:** Classic god games such as Black & White, combined with emergent simulation, diplomacy, grand strategy, creature raising, ecology, and sandbox storytelling.  
**Primary platforms:** Windows, macOS, Linux  
**Perspective:** 3D top-down / free-moving god camera  
**Players:** 1–8 gods  
**Offline:** Fully supported  
**Multiplayer:** Host-authoritative peer-hosted multiplayer  
**Backend requirement:** None  
**Business model assumption:** Premium PC game  
**Core principle:** One shared living world containing multiple competing gods.

---

# 1. PRODUCT VISION

Pantheon is a living god simulation.

Multiple gods exist simultaneously in the same world.

Each god competes for the belief of mortals.

Players gain power by influencing civilizations through:

- protection
- prosperity
- miracles
- fear
- punishment
- diplomacy
- manipulation
- war
- environmental intervention
- alliances
- betrayal

Every god owns one unique Creature.

The Creature grows from a relatively small companion into a legendary being with:

- personality
- memory
- morality
- relationships
- abilities
- scars
- learned behavior
- combat experience

The game is not primarily about conquering territory.

It is about becoming the dominant divine power in a world where:

- mortals have agency
- nature changes
- civilizations grow
- gods remember betrayal
- Creatures learn
- alliances shift
- disasters create opportunities
- actions create long-term consequences

The target player experience is:

> “I cannot believe that happened.”

rather than:

> “I completed mission seven.”

The primary storytelling mechanism is systemic emergence.

---

# 2. ORIGINALITY REQUIREMENT

Pantheon is a spiritual successor to the god-game genre.

It must not reproduce protected creative expression from Black & White.

The project must use original:

- Creature designs
- names
- lore
- civilizations
- UI
- architecture
- visual identity
- animations
- sounds
- spell designs
- world design
- story
- characters

The conceptual fantasy of interacting with a world as a god is retained.

The implementation and presentation must be original.

---

# 3. CORE DESIGN PILLARS

## 3.1 Belief is power

Mortals are the primary source of divine power.

More followers do not automatically mean more power.

The quality and intensity of belief matters.

---

## 3.2 Morality emerges from behavior

There is no simple:

```text
Good: +10
Evil: -10
```

The world observes what the god actually does.

---

## 3.3 Mortals have agency

People may:

- worship
- doubt
- migrate
- rebel
- convert
- ignore gods
- create new religious movements
- reject religion entirely

---

## 3.4 Nature is systemic

Weather, ecology, fire, water, disasters and terrain must interact.

A miracle should often produce indirect consequences.

---

## 3.5 Politics matters

Gods are not simply enemies.

They may:

- cooperate
- bargain
- threaten
- manipulate
- betray
- rescue
- create coalitions
- fight proxy wars

---

## 3.6 The Creature is emotionally important

The Creature is not simply a combat unit.

It should feel like a living companion.

---

## 3.7 Systems create stories

Whenever possible, gameplay should emerge from interacting systems rather than scripted mission chains.

---

# 4. CORE GAME LOOP

The main loop is:

```text
Observe world
     ↓
Influence mortals
     ↓
Gain belief
     ↓
Generate Divine Power
     ↓
Expand influence
     ↓
Develop civilization and Creature
     ↓
Interact with rival gods
     ↓
Respond to world events
     ↓
Create new political/environmental consequences
     ↓
Repeat
```

The player should constantly face tradeoffs.

Example:

A hurricane approaches the territory of a rival god.

Possible actions:

- ignore it
- weaken it
- stop it
- secretly strengthen it
- save civilians
- demand payment for help
- demand conversion for help
- rescue refugees afterward
- coordinate intervention with another god
- blame another god
- exploit the destruction militarily

The game should rarely provide one objectively correct solution.

---

# 5. THE WORLD

There is one continuous world.

The world is divided internally into simulation regions.

A region contains:

- biome
- terrain
- elevation
- temperature
- rainfall
- fertility
- vegetation
- wildlife
- water
- resources
- settlements
- religious influence
- ecological health
- disaster risk

Possible biomes:

- grassland
- forest
- desert
- mountain
- tundra
- swamp
- volcanic region
- coast
- island
- river valley

The player should perceive the world as continuous.

Regions exist primarily for simulation and optimization.

---

# 6. WORLD SCALE

Initial target:

```text
World:
10–20 km across

Gods:
4 initially
up to 8 later

Settlements:
8 MVP
20–50 full game

Conceptual population:
10,000–100,000+

Visible nearby citizens:
hundreds to low thousands

Fully simulated individual agents:
only nearby/important entities
```

The game must not create a heavyweight game object for every conceptual citizen.

---

# 7. BELIEF SYSTEM

Each population tracks belief toward individual gods.

Important values:

## Faith

How strongly the population believes the god exists and possesses divine power.

## Love

How positively the population views the god.

## Fear

How afraid the population is of the god.

## Dependency

How strongly the population believes it needs the god.

## Doubt

How uncertain the population has become.

## Resentment

How angry the population is about the god's behavior.

Example:

```text
God A

Faith:      91
Love:       82
Fear:       12
Dependency: 67
Doubt:       4
Resentment:  3
```

The population worships willingly.

Another:

```text
God B

Faith:      96
Love:        8
Fear:       94
Dependency: 71
Doubt:       3
Resentment: 78
```

The population worships through terror.

Both generate Divine Power.

But their societies behave differently.

---

# 8. LOVE VERSUS FEAR

Neither strategy should dominate.

## Fear advantages

- rapid obedience
- fast conversion
- strong short-term worship
- useful during conquest
- populations respond strongly to displays of power

## Fear disadvantages

- migration
- resentment
- rebellion
- instability
- followers abandon weak gods quickly

## Love advantages

- loyalty
- population growth
- migration attraction
- resistance to conversion
- stability during difficult periods

## Love disadvantages

- slower conversion
- followers expect protection
- betrayal causes major reputation damage
- expensive support expectations

Hybrid strategies are possible.

---

# 9. DIVINE POWER

Divine Power is the primary god resource.

Approximate generation:

```text
DivinePowerGeneration =
Followers
× Faith
× WorshipIntensity
× ReligiousInfrastructure
× situational modifiers
```

Love and fear modify generation differently.

Divine Power is consumed by miracles and major divine actions.

---

# 10. DIVINE INFLUENCE

Gods cannot freely perform powerful actions everywhere.

Influence spreads through:

- followers
- shrines
- temples
- holy sites
- prophets
- Creature presence
- allied religious access

Influence zones may overlap.

Example:

```text
God A influence: 72%
God B influence: 41%
God C influence: 13%
```

All may act there.

But actions cost more for weaker gods.

---

# 11. GOD ATTRIBUTES

Every god contains:

```text
GodState

Id
Name
DivineRank
DivinePower
Followers
Domains
Reputation
KnownGods
Relationships
CreatureId
ControlledHolySites
HistoricalActions
```

Gods also accumulate behavioral traits.

Examples:

- compassionate
- cruel
- honest
- deceptive
- orderly
- chaotic
- merciful
- vengeful
- generous
- ambitious

These should usually emerge from behavior rather than being explicitly selected.

---

# 12. GOD DOMAINS

Gods specialize.

Possible domains:

- Nature
- War
- Life
- Death
- Knowledge
- Prosperity
- Storm
- Fire
- Ocean
- Trickery
- Justice
- Chaos
- Protection

Players eventually specialize primarily in approximately 2–3 domains.

Domains should unlock distinct strategies.

Avoid generic:

```text
+5% damage
```

Prefer:

```text
Nature Tier IV:
Manipulate wildlife migration.

Storm Tier IV:
Redirect existing storms.

Trickery Tier IV:
Hide responsibility for certain miracles.
```

---

# 13. DIVINE RANK

Possible progression:

```text
Minor Spirit
Lesser God
God
Greater God
High God
Ascendant
```

Rank unlocks increasingly powerful actions.

Progress derives from:

- followers
- historical achievements
- holy locations
- religion
- Creature development
- world-changing events

---

# 14. MIRACLES

Miracles have several scales.

## Minor

Examples:

- heal
- produce food
- create water
- extinguish fire
- small rainfall
- frighten individual
- lightning strike
- bless field

## Regional

Examples:

- mass healing
- major rainfall
- forest growth
- crop abundance
- disease removal
- earthquake tremor
- firestorm
- mass blessing

## Great

Examples:

- hurricane
- regional shield
- major earthquake
- resurrection event
- regional drought
- stop tsunami
- mass conversion event

## Divine Acts

Extremely expensive.

Examples:

- redirect river
- permanently bless land
- create island
- split mountain
- awaken dormant volcano
- stop continent-scale disaster

These should become historical events.

---

# 15. SYSTEMIC MIRACLE CONSEQUENCES

Miracles should manipulate world systems.

Example:

```text
Rain Miracle
     ↓
soil moisture increases
     ↓
crop productivity increases
     ↓
river rises
     ↓
downstream flood risk increases
```

Another:

```text
Lightning
     ↓
tree hit
     ↓
forest fire
     ↓
wildlife migrates
     ↓
nearby settlement loses hunting
     ↓
food decreases
     ↓
migration begins
```

The player should learn to think in systems.

---

# 16. WEATHER

Weather is simulated.

Weather cells contain approximately:

```text
WeatherCell

position
velocity
pressure
temperature
humidity
precipitation
wind
electricalActivity
```

Weather cells move across regions.

Possible states:

- clear
- rain
- snow
- thunderstorm
- windstorm
- heat wave
- cold wave
- fog
- hurricane

Weather simulation does not run at rendering frequency.

Approximate:

```text
1–2 simulation updates per second
```

---

# 17. SEASONS

Optional depending on map configuration.

Potential seasons:

- spring
- summer
- autumn
- winter

Seasons affect:

- temperature
- rainfall
- crop growth
- fire risk
- snow
- migration
- disease
- food storage

---

# 18. ECOLOGY

Regions track:

```text
Vegetation
Wildlife
SoilQuality
Moisture
WaterAvailability
Pollution
Temperature
Biodiversity
FireDamage
```

God actions alter ecology.

Repeated destructive miracles may permanently damage a region.

Nature-focused gods may restore damaged ecosystems.

---

# 19. NATURAL DISASTERS

Possible disasters:

- earthquake
- tsunami
- hurricane
- tornado
- flood
- drought
- wildfire
- volcanic eruption
- landslide
- meteor
- extreme winter
- crop disease
- locust swarm

Disasters should be represented as processes.

Not simply:

```text
deal 500 damage
```

---

# 20. EARTHQUAKE EXAMPLE

```text
Earthquake

Magnitude: 7.3
Epicenter: Region 14

        ↓

Ground shaking
        ↓
Building structural checks
        ↓
Collapsed structures
        ↓
Road blockage
        ↓
Fire
        ↓
Population panic
        ↓
Migration
        ↓
Religious interpretation
        ↓
God intervention
```

---

# 21. DISASTER PREPARATION

Settlements may build:

- dams
- reservoirs
- storm shelters
- sea walls
- firebreaks
- irrigation
- granaries
- hospitals
- evacuation routes

Civilization policy affects disaster resilience.

---

# 22. THE CREATURE

Every god receives exactly one Creature.

The Creature represents:

- companion
- champion
- physical avatar
- military unit
- missionary
- political symbol

Creatures begin small and relatively inexperienced.

They eventually become legendary beings.

---

# 23. CREATURE PROGRESSION

Creature attributes include:

```text
Level
XP
Health
Strength
Speed
Intelligence
Resilience
DivineAffinity
CombatSkill
Loyalty
Morality
Personality
Relationships
```

Target maximum:

```text
50
```

MVP maximum:

```text
10
```

---

# 24. CREATURE SPECIALIZATIONS

Possible paths:

- Guardian
- Warrior
- Destroyer
- Healer
- Nature Keeper
- Missionary
- Trickster
- Commander

Players should not obtain every specialization in one game.

---

# 25. CREATURE LEARNING

Creature actions are influenced by learned preferences.

Example:

```text
Situation:
starving villager

Actions:

Feed villager
Ignore
Steal food
Eat food itself
Attack villager
```

Initially:

```text
Feed:       0.40
Ignore:     0.30
Eat:        0.55
Steal:      0.20
Attack:     0.05
```

The player repeatedly rewards feeding.

Later:

```text
Feed:       0.85
Ignore:     0.12
Eat:        0.25
Steal:      0.05
Attack:     0.01
```

The Creature eventually acts without instruction.

---

# 26. CREATURE PERSONALITY

Traits may include:

- brave
- cowardly
- kind
- sadistic
- curious
- lazy
- protective
- jealous
- playful
- aggressive
- loyal
- independent

Personality evolves through:

- experience
- reinforcement
- trauma
- relationships
- divine treatment

---

# 27. CREATURE RELATIONSHIPS

Creatures remember each other.

Track:

```text
Friendship
Fear
Rivalry
Respect
Trust
```

Two allied Creatures may become friends.

If their gods later fight, this creates interesting behavior.

---

# 28. CREATURE DEFEAT

Creatures normally do not permanently die.

Defeated Creatures become:

```text
Banished
```

Consequences:

- temporary absence
- scars
- reputation impact
- loyalty effect
- XP or progression consequence

Permanent death should only be considered for rare late-game mechanics.

---

# 29. SETTLEMENTS

The player is not a conventional city-builder mayor.

Settlements act autonomously.

They determine:

- housing
- roads
- gathering
- expansion
- job allocation
- local construction

The god influences priorities.

Possible instructions:

- increase food
- prepare for war
- improve health
- grow population
- expand territory
- prepare for disaster
- focus on religion
- build temple

---

# 30. SETTLEMENT STATE

Track approximately:

```text
SettlementState

Id
Name
Region
Population
Food
Water
Housing
Health
Safety
Happiness
Education
Wealth
PoliticalStability

BeliefByGod
FearByGod
LoveByGod
DoubtByGod
ResentmentByGod
```

---

# 31. POPULATION MIGRATION

Populations may leave.

Reasons include:

- famine
- persecution
- war
- disaster
- poverty
- fear
- low religious confidence
- opportunity elsewhere

Refugees create political consequences.

A neighboring god may:

- accept them
- reject them
- demand conversion
- negotiate their return

---

# 32. RELIGIOUS CONVERSION

Conversion is gradual.

Influences include:

- miracles
- prosperity
- missionaries
- Creature actions
- disaster relief
- intimidation
- propaganda
- temples
- divine appearances
- military victory
- enemy failure

Example:

```text
Settlement Elar

God A: 61
God B: 29
God C: 10
```

Multiple religions may coexist.

---

# 33. CULTURE

Religious culture may evolve into doctrines.

Examples:

- pacifism
- militarism
- sacrifice
- charity
- nature worship
- knowledge seeking
- expansionism
- isolationism

The player influences culture but cannot instantly rewrite it.

---

# 34. RELIGIOUS SCHISMS

Major inconsistency between historical divine behavior and current behavior may produce schisms.

Example:

A historically peaceful god begins mass executions.

Followers create:

```text
The Old Faith
```

They still believe the god exists but reject the new interpretation.

---

# 35. ATHEISM AND ANTI-DIVINE CULTURES

Populations may become:

- agnostic
- atheistic
- anti-divine

Possible causes:

- repeated divine failure
- contradictions
- excessive destruction
- cultural evolution
- technological development

Secular societies generate little Divine Power.

But may receive:

- science bonuses
- independence
- resistance to conversion
- miracle resistance

A powerful secular civilization may threaten every god.

---

# 36. TECHNOLOGICAL DEVELOPMENT

Civilizations gradually improve in:

- agriculture
- architecture
- medicine
- warfare
- navigation
- engineering
- science
- religion

The base game remains fantasy-oriented.

Do not automatically progress into contemporary technology.

---

# 37. DIVINE POLITICS

Relationships between gods are multi-dimensional.

Track:

```text
Trust
Fear
Respect
Rivalry
Gratitude
Suspicion
StrategicInterest
```

A god may:

```text
respect you
hate you
fear you
remain allied with you
```

simultaneously.

---

# 38. DIPLOMACY

Possible treaties:

- Alliance
- Non-Aggression Pact
- Defensive Pact
- Joint War
- Shared Influence
- Religious Access
- Creature Non-Aggression
- Disaster Assistance
- Mutual Defense
- Territorial Agreement
- Tribute
- Migration Agreement

Treaties may have durations and conditions.

---

# 39. SECRET AGREEMENTS

Some diplomacy may remain hidden.

Example:

```text
God B publicly allies with God A.

God B secretly promises God C:

"If A attacks you,
I will join you against A."
```

---

# 40. BETRAYAL

Any treaty may be broken.

Consequences affect:

- trust
- global reputation
- future treaties
- mortal opinion
- allies
- diplomatic credibility

Betrayal should remain strategically viable.

It must simply have consequences.

---

# 41. FAVORS

Gods remember major help.

Example:

```text
God B stopped a hurricane
from destroying your capital.
```

Create:

```text
Major Divine Favor owed to God B
```

God B may later request:

- war support
- territory
- miracle assistance
- Divine Power
- political backing

---

# 42. DIVINE COUNCIL

Periodic major events may summon a Divine Council.

Possible votes:

- condemn a god
- protect sacred land
- prohibit a practice
- coordinate disaster response
- declare pantheon-wide war
- establish neutral territory

Votes may be:

- public
- secret
- traded
- negotiated

---

# 43. INFORMATION AND DECEPTION

Players should not know everything.

Unknown information may include:

- secret treaties
- exact enemy belief
- hidden temples
- planned betrayal
- enemy resources
- miracle preparation

Intelligence may come from:

- prophets
- spies
- Creature scouting
- allies
- miracles
- defectors

---

# 44. WAR BETWEEN GODS

Victory in war may involve:

- conversion
- temple destruction
- Creature defeat
- rebellion support
- migration
- economic collapse
- holy-site occupation
- military invasion
- divine confrontation

Direct destruction should not always be optimal.

---

# 45. PROXY WARS

Gods may act indirectly.

Possible tools:

- support rebels
- encourage rival tribe
- secretly create disasters
- send disguised missionaries
- anonymously provide miracles
- spread propaganda

---

# 46. MORTAL HOLY WARS

Civilizations can develop their own motivations.

A militaristic religion may independently decide another faith is hostile.

The god may:

- encourage
- discourage
- ignore

Mortals are not puppets.

---

# 47. AI GODS

AI gods behave like strategic actors.

Each AI has:

```text
Personality
Moral tendencies
Risk tolerance
Ambition
Aggression
Preferred domains
Diplomatic memory
Short-term goals
Long-term goals
```

Possible personalities:

- Guardian
- Tyrant
- Opportunist
- Zealot
- Trickster
- Isolationist
- Warlord
- Nature Keeper

---

# 48. GOD AI MODEL

God AI uses Utility AI.

Do not primarily use rigid behavior trees.

Possible actions are continuously scored.

Example:

```text
DECLARE WAR ON GOD B

Military advantage          +0.50
Desired territory           +0.30
Historical betrayal         +0.40
Powerful enemy allies       -0.60
Creature injured            -0.20
Current famine              -0.45
Aggressive personality      +0.25
```

Compare with:

```text
Offer alliance      0.68
Convert Elar        0.82
Help God B          0.31
Declare war         0.20
```

The AI chooses:

```text
Convert Elar
```

---

# 49. AI BETRAYAL

AI betrayal must be explainable.

Factors include:

- trust
- opportunity
- military balance
- historical relationship
- threat
- personality
- secret treaties
- expected retaliation

Avoid random betrayal.

Provide player-facing clues.

Example:

> Zephar distrusts you because you have repeatedly expanded into territory he considers part of his sphere of influence.

---

# 50. WORLD HISTORY

The world records important events.

Examples:

```text
Year 31 — The Flood of Elar
Year 44 — Pact of Three
Year 57 — Betrayal of Arkon
Year 63 — Battle of Giants
Year 74 — Burning of Valeria
```

Civilizations may refer to these events.

---

# 51. SACRED LOCATIONS

Examples:

- World Tree
- ancient volcano
- sacred mountain
- meteor crater
- ancient ruins
- giant waterfall
- primordial forest

Holy locations provide unique benefits.

They naturally become political flashpoints.

---

# 52. WORLD EVENTS

Examples:

- plague
- global drought
- ancient monster awakening
- supervolcano
- meteor
- mass migration
- religious reformation
- world war
- eclipse
- magical anomaly

Some events encourage rival gods to cooperate.

---

# 53. VICTORY CONDITIONS

## Divine Ascendancy

Default.

A god or eligible alliance controls approximately:

```text
70% of global active belief
```

for a configurable duration.

## Conquest

All opponents fall below a minimum influence threshold.

## Cultural Victory

Religion becomes globally dominant without eliminating rivals.

## Pantheon Victory

An alliance jointly achieves dominance.

## Sandbox

No victory condition.

---

# 54. ALLIANCE ENDGAME

Allied victory should create tension.

Example:

```text
God A: 35%
God B: 30%
God C: 18%
```

A and B are allied.

Together they are near victory.

A can:

- remain loyal
- share victory
- betray B
- absorb B's believers
- seek personal Ascendancy

This is intentional.

---

# 55. PLAYER INTERACTION

The player primarily interacts using a Divine Hand.

Possible actions:

- pick up objects
- carry villagers
- rescue people
- throw rocks
- move trees
- interact with Creature
- place offerings
- manipulate objects
- cast miracles

Physics interactions should feel tactile.

---

# 56. CAMERA

Controls:

- WASD
- edge movement
- drag
- zoom
- rotate
- focus settlement
- focus Creature
- strategic overview

Zoom levels:

## Close

Individuals and Creature interaction.

## Regional

Settlements and local simulation.

## Strategic

Religious influence, weather and diplomacy.

Transitions are seamless.

---

# 57. USER INTERFACE

HUD:

- Divine Power
- followers
- worship generation
- selected miracle
- Creature status
- important alerts

Panels:

- World
- Settlements
- Creature
- Diplomacy
- Religion
- History
- Domains
- Miracles

Avoid excessive permanent RTS UI.

---

# 58. MAP OVERLAYS

Include:

- god influence
- faith
- population
- food
- ecology
- weather
- danger
- disaster risk
- migration
- political control

---

# 59. ALERT SYSTEM

Examples:

> Elar is starving.

> Vael proposes an alliance.

> Your Creature attacked neutral civilians.

> A hurricane is approaching the western coast.

> Nara is converting your followers.

Alerts require prioritization.

Avoid notification spam.

---

# 60. AUDIO

Dynamic music responds to:

- morality
- region
- warfare
- Creature state
- disaster
- Divine Power
- political tension

Miracles should have geographically appropriate sound.

A distant thunderstorm or Creature roar should sometimes be audible across the landscape.

---

# 61. VISUAL DIRECTION

Stylized 3D fantasy.

Not photorealistic.

Goals:

- readable from high altitude
- expressive Creatures
- visually strong miracles
- distinct biomes
- recognizable civilizations
- scalable performance

Religion should visually affect civilizations.

A benevolent nature god might produce:

- gardens
- organic structures
- wildlife

A fearful war god might produce:

- intimidating monuments
- fortifications
- darker ceremonies

---

# 62. SUPPORTED PLATFORMS

Primary platforms:

```text
Windows x64
macOS Apple Silicon
macOS Intel if practical
Linux x64
```

Cross-platform multiplayer should allow:

```text
Windows host
+
macOS players
+
Linux players
```

The architecture must remain operating-system independent.

---

# 63. CORE TECHNOLOGY STACK

## Engine

**Godot 4.x .NET**

Use a pinned stable Godot version for each development milestone.

Do not automatically update engine versions during milestone work.

---

## Primary language

**C#**

Use for:

- simulation
- gameplay
- AI
- networking
- systems
- persistence
- testing

Use GDScript only when a very small editor/tool script benefits from it.

---

## Terrain

**Terrain3D**

Initial terrain solution.

Provides:

- large terrain
- heightmaps
- terrain LOD
- texture layers
- foliage integration
- editing tools

---

## Physics

**Jolt Physics**

Use for:

- Creature physics
- thrown objects
- destruction
- debris
- falling trees
- environmental interactions

---

## Navigation

**Godot Navigation / Recast**

Use tiled navigation.

Do not individually navigate every conceptual citizen.

---

## Procedural generation

**FastNoiseLite**

Use for:

- terrain variation
- biome masks
- moisture
- temperature
- vegetation distribution
- resources

World generation must be based on a reproducible seed.

---

## Creature AI

**LimboAI**

Use:

- behavior trees
- hierarchical state machines
- blackboards

Creature learning values are implemented separately.

---

## God AI

**Custom Utility AI**

Core strategic AI is proprietary game logic.

---

## Rendering crowds

**Godot MultiMesh**

Use GPU instancing for:

- crowds
- vegetation
- repetitive props
- distant units

---

## Optional future simulation optimization

**Flecs**

Do not use initially.

Introduce only if profiling demonstrates the C# simulation cannot meet performance requirements.

Potential implementation:

```text
C++ Flecs simulation
       ↓
GDExtension
       ↓
Godot
```

---

## 3D content

**Blender**

Primary content creation tool.

---

# 64. TERRAIN DESTRUCTION STRATEGY

Initial game:

Use Terrain3D.

Earthquakes create:

- visual cracks
- physics shaking
- destroyed buildings
- landslides
- blocked roads
- environmental damage

Do not initially physically remesh the entire terrain.

Potential future technology:

**Godot Voxel Tools**

Only introduce if true terrain modification becomes core.

Examples:

- physically splitting mountain
- digging canyon
- creating crater
- dynamically redirecting rivers

This is deliberately postponed because dynamic terrain substantially increases:

- networking complexity
- persistence complexity
- navigation complexity
- performance cost

---

# 65. LOCAL-FIRST ARCHITECTURE

The game must function fully without internet.

Single-player requires:

```text
No account
No backend
No external API
No database server
No dedicated server
```

Everything runs inside the local game process.

---

# 66. SINGLE-PLAYER ARCHITECTURE

Conceptually single-player acts like:

```text
Local Host
+
one local player
+
AI gods
```

Architecture:

```text
Pantheon Client
│
├── Presentation
├── Input
├── Audio
│
└── Local Authority
    │
    ├── World
    ├── Gods
    ├── Settlements
    ├── Creatures
    ├── Population
    ├── Weather
    ├── Ecology
    └── Diplomacy
```

No server process is required.

---

# 67. MULTIPLAYER ARCHITECTURE

Multiplayer is host-authoritative.

One player's computer acts as:

```text
HOST
+
PLAYER
```

Example:

```text
                     PLAYER 1
                 Host + local player
               ┌──────────────────────┐
               │ Canonical Simulation │
               │ Gods                 │
               │ Population           │
               │ Weather              │
               │ Creatures            │
               │ Physics              │
               └──────────┬───────────┘
                          │
                ┌─────────┼─────────┐
                │         │         │
              P2          P3        P4
```

Clients do not own canonical world state.

---

# 68. NETWORKING TECHNOLOGY

Initial networking:

**Godot ENet**

Use:

```text
UDP
host authoritative
RPC
state replication
```

Initial connection methods:

- LAN
- direct IP
- UPnP when available

No matchmaking service is required for development.

---

# 69. OPTIONAL STEAM NETWORKING

For commercial release through Steam, optionally integrate:

- Steam lobbies
- friend invites
- networking relay
- NAT traversal
- identity

The actual world continues running on a player's computer.

Steam is not the game backend.

Architecture:

```text
Steam
  ↓
Lobby / connectivity

Host PC
  ↓
Canonical Pantheon world
```

Pantheon must remain playable without Steam integration during development.

---

# 70. NO REQUIRED SERVER INFRASTRUCTURE

Initial architecture intentionally excludes:

- Nakama
- Agones
- Kubernetes
- dedicated servers
- cloud database
- account services
- hosted simulation

These may only be introduced later if product needs change.

---

# 71. HOST DISCONNECT

MVP behavior:

```text
Host leaves
     ↓
Multiplayer session ends
     ↓
Host world is saved
```

Host migration is a future feature.

---

# 72. FUTURE HOST MIGRATION

Potential later behavior:

```text
Host disconnects
     ↓
elect new host
     ↓
transfer canonical state
     ↓
continue simulation
```

Do not build this for MVP.

---

# 73. NETWORK AUTHORITY

Clients send intents.

Example:

```text
CastMiracleRequest

GodId
MiracleType
TargetPosition
TargetEntity
```

Host validates:

```text
Does god have Divine Power?
Is location valid?
Is target inside influence?
Is miracle on cooldown?
Is player authorized?
```

Host executes action.

Clients receive result.

Clients must not directly set:

```text
DivinePower
CreatureHealth
SettlementPopulation
Belief
Damage
```

---

# 74. WHY NOT DETERMINISTIC LOCKSTEP

Do not initially use full deterministic P2P lockstep.

The game includes:

- Jolt physics
- different CPU architectures
- floating point simulation
- dynamic AI
- weather
- large simulation
- macOS ARM
- Windows/Linux x86

Perfect cross-platform determinism would create substantial engineering complexity.

Host authority avoids this.

---

# 75. SAVE ARCHITECTURE

World saves live locally on the host.

Example:

```text
world_001.save
```

Persist:

```text
WorldSeed
SimulationTick
GameTime

Regions
Gods
Settlements
PopulationState
Creatures
Beliefs
Diplomacy
Weather
Ecology
WorldEvents
History
Technology
SacredLocations
```

---

# 76. SAVE PRINCIPLE

Serialization belongs to the simulation layer.

Do not serialize rendered nodes as the canonical save state.

Wrong:

```text
save SceneTree
```

Correct:

```text
serialize WorldState
```

Then rebuild visuals from the saved state.

---

# 77. CORE ARCHITECTURAL RULE

The simulation is the source of truth.

```text
SIMULATION
    ↓
Presentation Adapter
    ↓
GODOT VISUAL WORLD
```

Never make:

```text
Node3D
```

the canonical model for important simulation state.

---

# 78. LAYERED ARCHITECTURE

Use approximately:

```text
┌────────────────────────────┐
│ Presentation               │
│ Scenes / UI / VFX / Audio  │
├────────────────────────────┤
│ Presentation Adapters      │
├────────────────────────────┤
│ Gameplay Application       │
├────────────────────────────┤
│ Simulation                 │
│ Gods                       │
│ Belief                     │
│ Population                 │
│ Diplomacy                  │
│ Ecology                    │
│ Weather                    │
├────────────────────────────┤
│ Infrastructure             │
│ Networking                 │
│ Persistence                │
│ Platform                   │
└────────────────────────────┘
```

Simulation code should have minimal Godot dependencies wherever practical.

---

# 79. SIMULATION SYSTEMS

Main systems:

```text
WorldSimulation
RegionSimulation
PopulationSimulation
SettlementSimulation
BeliefSimulation
GodSimulation
MiracleSimulation
CreatureSimulation
DiplomacySimulation
EcologySimulation
WeatherSimulation
DisasterSimulation
MigrationSimulation
TechnologySimulation
HistorySimulation
WorldEventSimulation
```

---

# 80. SIMULATION TIERING

A critical architectural feature.

## Tier 1 — Statistical

Most of the world.

Examples:

```text
Settlement population:
7,842

No 7,842 NPC objects exist.
```

Simulate:

- economy
- food
- demographics
- belief
- migration
- health

---

## Tier 2 — Regional

Camera approaches.

Create lightweight spatial agents.

Examples:

- walking
- working
- worshipping
- escaping disasters

Hundreds of visible people may represent thousands.

---

## Tier 3 — Full Simulation

Important entities receive full behavior.

Examples:

- Creature
- selected villager
- prophet
- combatant
- object being held
- important leader

Includes:

- physics
- AI
- animation
- pathfinding
- individual memory

---

# 81. POPULATION REPRESENTATION

Example:

```text
Settlement:
Population = 8,240
```

Nearby player sees perhaps:

```text
500 visual citizens
```

Those characters statistically represent the larger population.

This is intentional.

---

# 82. MIGRATION REPRESENTATION

Instead of simulating 900 individual refugees across the entire world:

```text
MigrationGroup

Population = 900
Origin = Elar
Destination = Valen
Route = [...]
Health = 0.62
Food = 0.42
```

When the player approaches:

```text
spawn representative visible agents
```

---

# 83. RENDERING LARGE CROWDS

Use MultiMesh / GPU instancing where appropriate.

Citizens may share:

- mesh
- animation sets
- materials

Variation may come from:

- clothing
- scale
- animation phase
- texture index
- accessories

Do not create thousands of expensive independent render nodes unless required.

---

# 84. WORLD STREAMING

The world should be spatially divided.

Example:

```text
World
 ├── Region 0
 ├── Region 1
 ├── Region 2
 └── ...
```

Nearby regions load:

- high-detail terrain
- actors
- vegetation
- effects

Distant regions remain simulation data.

---

# 85. NAVIGATION

Navigation runs at multiple scales.

## Strategic navigation

Region graph.

Used for:

- migration
- armies
- trade
- long-distance Creature travel

## Local navigation

NavMesh / Recast.

Used for visible agents.

Example:

```text
Settlement A
     ↓
Region path
     ↓
Region B
     ↓
Local navmesh
     ↓
Specific building
```

---

# 86. CREATURE AI ARCHITECTURE

Creature AI combines:

```text
Behavior Tree
+
Hierarchical State Machine
+
Utility values
+
Learned preference weights
+
Memory
```

LimboAI may implement BT/HSM.

Pantheon implements:

- learning
- reinforcement
- personality
- relationship memory

---

# 87. GOD AI ARCHITECTURE

God AI operates at strategic intervals.

Not every frame.

Example:

```text
Strategic evaluation:
every 2–5 seconds

Major planning:
every 15–60 seconds

Diplomatic reevaluation:
event driven + periodic
```

AI maintains goals.

Example:

```text
Protect capital
Convert northern settlement
Increase alliance with Nara
Prepare for drought
Weaken Vael
```

---

# 88. THREADING MODEL

Avoid premature complexity.

Initial simulation:

Main thread where practical.

Move expensive independent workloads to worker threads after profiling.

Good candidates:

- world generation
- long-distance pathfinding
- strategic AI scoring
- weather
- population aggregation
- save serialization

Godot rendering/scene changes remain appropriately synchronized with engine requirements.

---

# 89. UPDATE FREQUENCIES

Approximate target:

```text
Rendering:
60 FPS target

Physics:
engine physics tick

Creature AI:
5–10 Hz

Visible citizen behavior:
2–5 Hz

Settlement simulation:
1 Hz

Belief simulation:
0.5–1 Hz

God strategic AI:
0.2–0.5 Hz

Weather:
1–2 Hz

Ecology:
0.1–0.5 Hz

World history:
event driven
```

Not every system needs frame-level updates.

---

# 90. CORE DATA MODELS

## GodState

```text
Id
Name
Rank
DivinePower
Domains
Followers
Personality
CreatureId
Reputation
Relationships
```

## SettlementState

```text
Id
Name
RegionId
Population
Food
Water
Wealth
Health
Safety
BeliefByGod
FearByGod
LoveByGod
DoubtByGod
```

## CreatureState

```text
Id
GodId
Species
Level
XP
Health
Strength
Intelligence
Loyalty
Morality
Traits
Relationships
LearnedPreferences
```

## RegionState

```text
Id
Biome
Elevation
Temperature
Moisture
Fertility
Vegetation
Wildlife
Pollution
Weather
```

## DiplomacyRelation

```text
GodA
GodB
Trust
Fear
Respect
Rivalry
Gratitude
Suspicion
Status
Treaties
Memory
```

## WorldEvent

```text
Id
Type
Location
Severity
Start
Duration
Participants
Effects
```

---

# 91. EVENT ARCHITECTURE

Systems communicate using domain events where practical.

Example:

```text
EarthquakeStarted
SettlementDamaged
CitizensKilled
RefugeesCreated
GodProvidedAid
BeliefChanged
AllianceOpinionChanged
```

This reduces direct coupling.

---

# 92. HISTORY SYSTEM

Important domain events may become historical records.

Do not store every simulation event forever.

History system evaluates significance.

Example:

```text
CitizenAteBread
```

not historical.

```text
God Vael destroyed the city of Elar
```

historical.

---

# 93. PROCEDURAL GENERATION

World generation starts from:

```text
WorldSeed
```

Generate:

- elevation
- rainfall
- temperature
- fertility
- biome
- forests
- resources
- river candidates
- settlement candidates
- sacred sites

Given the same configuration and seed, initial world generation should reproduce the same world.

---

# 94. PHYSICS AND DISASTERS

Use physics selectively.

Do not simulate every building brick.

Buildings have structural abstractions.

Example:

```text
Building

StructuralIntegrity
EarthquakeResistance
FireResistance
FloodResistance
```

When destroyed, visual physics creates convincing debris.

The high-level result is simulation-driven.

---

# 95. NETWORK REPLICATION

Do not continuously send the entire world.

Replicate:

- relevant world events
- nearby entity state
- important strategic state
- player actions
- changed values

Use interest management.

A player does not need high-frequency position updates for citizens on the opposite side of the world.

---

# 96. NETWORK FREQUENCY

Not all data needs equal frequency.

Example:

```text
Creature movement:
high frequency

Nearby physics:
high frequency

God resource:
medium frequency

Settlement food:
low frequency

Diplomatic state:
event based

World history:
event based
```

---

# 97. CLIENT PREDICTION

Use only where it improves responsiveness.

Examples:

- camera movement requires no authority
- UI interaction local
- Divine Hand visual feedback may be predicted
- miracle result waits for authority

This is not a twitch shooter.

Perfect latency hiding is less important than correctness.

---

# 98. CHEATING MODEL

Host-authoritative multiplayer inherently trusts the host.

For private/cooperative/competitive friend sessions this is acceptable initially.

Do not build anti-cheat infrastructure for MVP.

If competitive public multiplayer later becomes important, reconsider authority architecture.

---

# 99. PERSISTENCE FORMAT

Start simple.

Recommended development path:

```text
MVP:
human-readable/debuggable serialization

Later:
compressed/binary format if save size becomes problematic
```

Saves must include a version number.

Example:

```text
SaveVersion = 7
```

Add migrations for incompatible save changes.

---

# 100. REPOSITORY STRUCTURE

Recommended:

```text
pantheon/
│
├── Pantheon.sln
│
├── game/
│   ├── project.godot
│   │
│   ├── Scenes/
│   ├── Assets/
│   ├── Materials/
│   ├── Shaders/
│   ├── Audio/
│   └── Scripts/
│
├── src/
│   ├── Pantheon.Domain/
│   │   ├── Gods/
│   │   ├── Belief/
│   │   ├── Creatures/
│   │   ├── Settlements/
│   │   ├── Population/
│   │   ├── Diplomacy/
│   │   ├── Ecology/
│   │   ├── Weather/
│   │   └── Events/
│   │
│   ├── Pantheon.Simulation/
│   ├── Pantheon.Game/
│   ├── Pantheon.Networking/
│   ├── Pantheon.Persistence/
│   └── Pantheon.Tests/
│
├── tools/
│
├── docs/
│   ├── GAME_SPEC.md
│   ├── ARCHITECTURE.md
│   └── ADR/
│
└── build/
```

Exact structure may be adjusted if Godot tooling requires it.

Preserve conceptual separation.

---

# 101. ARCHITECTURE DECISION RECORDS

Important architecture decisions should be documented.

Examples:

```text
ADR-001 Godot engine
ADR-002 C# primary language
ADR-003 Host-authoritative networking
ADR-004 Statistical population simulation
ADR-005 Terrain3D
ADR-006 Simulation state separate from scenes
```

AI coding agents must not silently reverse these decisions.

---

# 102. TESTING

Simulation systems must support automated tests.

Examples:

```text
Fear decreases after prolonged safety.

Starving population loses health.

Rain increases soil moisture.

Breaking alliance reduces trust.

High resentment increases rebellion probability.

God cannot cast miracle without enough power.
```

---

# 103. DETERMINISTIC TESTING

Even though multiplayer is not lockstep deterministic, many simulation tests should allow seeded random generators.

Example:

```text
SimulationRandom(seed: 12345)
```

This makes failures reproducible.

Avoid uncontrolled global randomness in core systems.

---

# 104. PERFORMANCE TESTING

Create automated or repeatable stress scenarios.

Examples:

```text
100,000 conceptual citizens

5,000 visible citizens

50 settlements

8 gods

8 Creatures

20 active weather cells

3 simultaneous disasters
```

Measure:

- CPU
- memory
- frame rate
- simulation time
- networking bandwidth
- save time

---

# 105. TARGET PERFORMANCE

Initial desktop target:

```text
1080p
60 FPS target

Mid-range recent gaming PC
Modern Apple Silicon Mac
Reasonable Linux gaming system
```

Simulation should attempt to remain below an allocated CPU budget.

Graphics options should allow scaling:

- shadows
- vegetation
- particles
- visible population
- terrain quality

---

# 106. DEBUG TOOLS

Build debug tools early.

Required overlays:

- influence
- belief
- population
- region boundaries
- AI intentions
- navmesh
- weather cells
- migration routes
- simulation tier
- network authority
- performance metrics

God AI debug view should show:

```text
Current goal
Top candidate actions
Utility scores
Relationship reasoning
```

Creature debug view should show:

```text
Current behavior
Personality
Needs
Learned weights
Memories
```

---

# 107. CONTENT DATA

Miracles, Creature abilities and domains should be data-driven.

Avoid hard-coding all balance values.

Example conceptual structure:

```text
MiracleDefinition

Id
Name
Domain
PowerCost
Cooldown
Range
InfluenceRequirement
Effects
```

This allows designers and AI agents to add content without modifying core engine logic.

---

# 108. MVP WORLD

First vertical slice:

```text
1 map
3 biomes
8 settlements
4 gods
4 Creatures
```

Players:

```text
1 human
3 AI
```

---

# 109. MVP BELIEF

Implement:

```text
Faith
Love
Fear
Doubt
```

Skip Dependency and Resentment initially if necessary.

---

# 110. MVP MIRACLES

Start with:

```text
Heal
Food
Water
Rain
Lightning
Fire
Bless Crops
Earthquake
Shield
```

---

# 111. MVP CREATURE

Implement:

- movement
- health
- needs
- combat
- XP
- levels 1–10
- basic personality
- help villagers
- attack villagers
- fight Creatures
- player reward
- player punishment
- simple learned preference values

---

# 112. MVP SETTLEMENTS

Settlements autonomously:

- gather food
- build housing
- grow population
- worship
- react to danger
- respond to miracles

The player does not manually place ordinary houses.

---

# 113. MVP DIPLOMACY

Statuses:

```text
Neutral
Friendly
Alliance
Hostile
War
```

Actions:

```text
Offer alliance
Break alliance
Declare war
Request peace
```

AI remembers betrayal.

---

# 114. MVP NATURE

Implement:

- rain
- storm
- drought
- wildfire
- earthquake

Avoid advanced climate modeling initially.

---

# 115. MVP VICTORY

First god to maintain:

```text
60% global belief
for 10 continuous minutes
```

wins.

Keep values configurable.

---

# 116. DEVELOPMENT MILESTONE 1

## Foundation

Deliver:

- Godot project
- C# solution
- camera
- terrain
- one settlement
- saveable WorldState
- debug UI

Playable outcome:

Player can explore the world.

---

# 117. DEVELOPMENT MILESTONE 2

## Living Settlement

Deliver:

- aggregate population
- food
- housing
- health
- visible representative citizens

Playable outcome:

Settlement visibly lives and changes.

---

# 118. DEVELOPMENT MILESTONE 3

## First God

Deliver:

- Divine Power
- influence
- belief
- basic miracles

Playable outcome:

Player can gain and spend belief.

---

# 119. DEVELOPMENT MILESTONE 4

## Creature

Deliver:

- Creature
- movement
- interaction
- needs
- simple AI
- reward/punishment

Playable outcome:

Creature behaves independently.

---

# 120. DEVELOPMENT MILESTONE 5

## Rival God

Deliver:

- second god
- Utility AI
- competing belief
- conversion

Playable outcome:

Religious competition exists.

---

# 121. DEVELOPMENT MILESTONE 6

## Diplomacy

Deliver:

- relationships
- alliance
- war
- betrayal memory

Playable outcome:

Rival can cooperate or attack.

---

# 122. DEVELOPMENT MILESTONE 7

## Weather

Deliver:

- weather cells
- moving storms
- rain
- drought

Playable outcome:

World produces environmental pressure.

---

# 123. DEVELOPMENT MILESTONE 8

## Disaster Simulation

Deliver:

- earthquake
- wildfire
- destruction
- settlement response

Playable outcome:

Disasters produce emergent consequences.

---

# 124. DEVELOPMENT MILESTONE 9

## Four Gods

Deliver:

- 1 player
- 3 AI
- diplomacy between all gods
- multiple strategic personalities

Playable outcome:

Basic politics emerges.

---

# 125. DEVELOPMENT MILESTONE 10

## Population Movement

Deliver:

- migration
- refugees
- conversion through migration

Playable outcome:

Populations can abandon gods.

---

# 126. DEVELOPMENT MILESTONE 11

## Creature Relationships

Deliver:

- rival Creatures
- friendship
- rivalry
- combat
- memories

---

# 127. DEVELOPMENT MILESTONE 12

## Multiplayer Foundation

Deliver:

- ENet host
- join
- player assignment
- action authority
- synchronized god state

First target:

```text
2 players
```

---

# 128. DEVELOPMENT MILESTONE 13

## Multiplayer Simulation

Synchronize:

- miracles
- Creatures
- settlements
- disasters
- diplomacy

Target:

```text
4 players
```

---

# 129. DEVELOPMENT MILESTONE 14

## Complete Vertical Slice

Includes:

- 4 gods
- 8 settlements
- Creatures
- belief
- politics
- weather
- disaster
- local saves
- multiplayer
- victory

This is the first meaningful validation build.

---

# 130. DEVELOPMENT MILESTONE 15

## Polish and Expansion

Only after the vertical slice is fun:

- domains
- doctrines
- holy sites
- advanced politics
- schisms
- larger worlds
- more miracles
- more Creatures
- Steam networking

---

# 131. NON-GOALS FOR INITIAL DEVELOPMENT

Do not initially build:

- MMO infrastructure
- dedicated server fleet
- massive army RTS
- hundreds of miracles
- modern technology
- full terrain destruction
- sophisticated genetics
- dozens of Creature species
- scripted campaign
- VR
- host migration
- competitive anti-cheat
- global matchmaking backend

---

# 132. VALIDATION PRIORITIES

The first version exists to answer:

## Is converting populations fun?

## Is raising the Creature fun?

## Are rival gods interesting?

## Does diplomacy generate stories?

## Do disasters create meaningful decisions?

If these are not fun, adding more content will not solve the problem.

---

# 133. EXAMPLE SYSTEMIC STORY

God A causes a drought in God B's territory.

God B cannot generate enough water.

God C provides rain.

God B's followers begin converting to C.

God B accuses C of exploiting the crisis.

God A secretly offers B military support.

B attacks C.

A joins B publicly.

A then secretly converts B's border settlements.

Lightning from a natural storm starts a forest fire.

The fire reaches A's farmland.

A must stop supporting the war to save its own population.

A's Creature independently starts rescuing civilians because the player trained it to value lives.

Refugees flee into C's territory.

C accepts them.

C's faith increases.

A eventually loses military influence but earns a strong reputation for protecting civilians.

No scripted quest produced this sequence.

The simulation did.

---

# 134. CODING AGENT RULES

Any AI coding agent working on Pantheon must follow these principles.

1. Build incrementally.

2. Every milestone must leave the game playable.

3. Simulation and presentation remain separate.

4. WorldState is authoritative.

5. Do not make SceneTree nodes canonical domain objects.

6. Keep core simulation testable without rendering.

7. Prefer data-driven content.

8. Do not optimize before profiling.

9. Do not add infrastructure without demonstrated need.

10. Do not introduce a cloud dependency for a feature that can reasonably remain local.

11. Maintain macOS, Windows and Linux compatibility.

12. Any native dependency must support all target desktop platforms.

13. Use seeded randomness where simulation reproducibility is valuable.

14. Add automated tests for important simulation rules.

15. Document architectural changes.

16. Never silently replace an architecture decision simply because another approach is easier locally.

17. Prioritize gameplay systems over graphical polish.

18. Prioritize emergent interactions over scripted content.

---

# 135. TECHNICAL NORTH STAR

The final architecture should conceptually look like:

```text
                         PANTHEON
                             │
                ┌────────────┴────────────┐
                │                         │
          PRESENTATION                SIMULATION
                │                         │
     Godot rendering/UI             WorldState
     particles/audio                     │
     animation                            │
                              ┌───────────┼───────────┐
                              │           │           │
                           Belief      Ecology     Diplomacy
                              │           │           │
                         Population    Weather       Gods
                              │                       │
                         Settlements                Creatures
                              │
                              └───────────┬────────────┘
                                          │
                                      Authority
                                          │
                            ┌─────────────┴─────────────┐
                            │                           │
                      Single Player                Multiplayer
                            │                           │
                       Local host                Player-hosted
                                                     ENet
```

No required backend.

No required cloud.

No required dedicated server.

---

# 136. PRODUCT NORTH STAR

Pantheon should feel like a world where:

**Mortals remember what you did.**

**Your Creature learns from what you did.**

**Nature responds to what you did.**

**Civilizations evolve because of what you did.**

**Other gods plan around what you did.**

**Your allies may betray you because of what you did.**

And sometimes:

**the world creates something nobody planned.**

That emergent behavior is the central goal of Pantheon.
