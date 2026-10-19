# ADR-023: Settlements' society: safety, happiness, order, wealth, learning, priorities, buildings, technology and expansion

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the settlement step),
  [ADR-016](ADR-016-migration-groups-and-refugee-policies.md) (migration) and
  [ADR-022](ADR-022-rivers-lakes-floods-water-ocean-and-justice.md) (water and floods)

## Context

The specification asks for more of a settlement than food, houses and health:

- **§29:** settlements act on their own, but the god influences their priorities: increase
  food, prepare for war, improve health, grow the population, expand the territory, prepare for
  disaster, focus on religion, build a temple.
- **§30:** settlements track safety, happiness, education, wealth and political stability.
- **§21:** settlements may build dams, reservoirs, storm shelters, sea walls, firebreaks,
  irrigation, granaries, hospitals and evacuation routes.
- **§9 and §10:** religious infrastructure multiplies Divine Power, and temples spread influence.
- **§36:** civilizations improve in agriculture, architecture, medicine, warfare, navigation,
  engineering, science and religion, without reaching modern technology.
- **§6:** a full game has 20–50 settlements, against the MVP's 8.

## Decision

### A society for every settlement

- **New state:** `SettlementState.Society` (`SocietyState`) holds:
  - safety, happiness, stability (political stability) and education, each 0..1;
  - wealth;
  - `TechnologyLevels` in §36's eight fields, 0 up to `MaxTechnology`, so the world stays one of
    fantasy;
  - the buildings raised;
  - the priority, the god that set it and when;
  - the building under way and its progress;
  - the last founding.
- **The society step:** a new `SocietySystem` runs at 1 Hz after the belief step.
  - **Safety, happiness and stability drift towards their targets** (`DriftPerSecond`). Safety
    falls with danger and war over the village; walls and watchmen restore some. Happiness weighs
    food security, health, water, safety and love for the gods, less fear and homelessness.
    Stability weighs happiness, unity of faith (the largest group's share, the godless included)
    and peace, less an Old Faith's share.
  - **Wealth** comes from the harvest beyond what is eaten while the stores are full, and from
    trade with each neighbour in range (more with navigation). A share is used up each second.
  - **Education** grows with stability and with wealth to spare per person, faster with
    science, and is slowly forgotten.
  - **Technology:** each field grows with education. It grows `FocusFactor` times faster in the
    field the priority or the building under way favours, and closes part of the gap to the most
    advanced neighbour in range. `TechnologyAdvanced` is raised at each whole level.
  - **Buildings:** a priority calls for its buildings in order. Once the wealth is paid, the
    workforce raises the building, and `BuildingCompleted` is raised.
    - Temple raises a temple to the god that set the priority; Food a granary; Health a
      hospital; Defence walls.
    - Preparation raises levees (only beside water), firebreaks, shelters and cisterns.
  - **Expansion:** under an Expansion priority, a settlement big enough sends settlers with food
    to the best free land in range, no nearer than `Spacing` to any settlement. The new village
    is founded at once with its founders' beliefs and most of their knowledge, and
    `SettlementFounded` is raised. This is how worlds grow towards §6's numbers.
- **What the effects change.** Each number lives with the rules of the system it changes, so no
  system needs another's rules.

  | Rules | Effects |
  | --- | --- |
  | `SettlementRules.Civics` | Agriculture and a Food priority raise the harvest. Medicine, a hospital and a Health priority heal and save lives. Architecture lightens house work. A granary and a cistern store more. A Growth priority brings births, and a Worship priority doubles the worship. Walls and warfare turn attacks away. |
  | `DisasterRules.Preparedness` | Architecture saves houses. Firebreaks and levees, with engineering, spare a village fire and flood damage. Shelters prevent quake and flood deaths. A granary keeps food. |
  | `DivineRules` | A temple multiplies its god's religious infrastructure (`TempleInfrastructure`), and religion raises everyone's (`ReligionPerLevel`): §9's formula. |
  | `MigrationRules` | Unhappiness and unrest, squared so that only real misery counts, add to the pressure to leave, as new causes. Navigation speeds migrants. |

### Who sets the priority

- **The authority:** `SocietyAuthority` checks a `PriorityCommand`. The god must be followed by at
  least `PriorityShare` of the people. A priority holds `PriorityCooldownSeconds`, and only the
  god's own player may speak for it. `PriorityChanged` is raised.
- **Joined players:** they send a `PriorityIntent`. The protocol is version 4.
- **AI gods:** a `PriorityAdvisor` sets the priority of each village an AI god leads.
  - It answers hunger, illness, danger (or thirst) and war first.
  - Then it wants a temple of its own.
  - Ambitious gods (convert at least 0.8 or aggression at least 0.9) expand when crowded.
  - Otherwise the god asks for growth or worship, by its nature.
- **Players:** the player sets priorities from the new Settlements panel (N), which shows a
  village's faiths, stores, society, technology, buildings and work.

### Saves and presentation

- **Saves:** version 15. Migrating from 14 gives every settlement a newly founded society: safe,
  fairly content and settled, poor, unlearned, with no buildings and no priority.
- **Alerts:** they tell of:
  - a rival setting the course of a village where the player has a stake;
  - temples and buildings;
  - foundings;
  - the player's villages mastering a field.
- **History:** "The Founding of …" and "… raises a temple to …".
- **Debug:** the F1 panel lists a settlement's society and technology. `--priority` and
  `--settlement-panel` drive society from the command line.
- **The village view:** a temple (roofed in its god's colour) beside the shrine, inside the ring
  the houses keep clear, and walls as posts beyond the houses with four gates, laid out by
  `CivicLayout`.

## Consequences

- **The world grows:** in an hour AI-led worlds found up to eight villages (16 in all) and 10,000
  people. The settlement presenter already adds views for new settlements.
- **Technology:** it ends an hour around levels 1–2 of 5 (the first tuning reached 4–5).
- **Divine Power:** temples raise power income, so Divine Power still has no sink worth the name.
  Divine Acts (a later part) are the intended one.
- **Balance:** measured over 40 seeds against part 2's build (see the plan).
- **What later parts build on this:**
  - doctrines, secularism and resentment (part 4) read the society;
  - politics (part 5) will use stability and safety;
  - disaster preparation is in place for the nature part.
- **Not modelled:**
  - roads and evacuation routes;
  - dams and sea walls, beyond levees;
  - buildings other than the temple and walls shown in the village.
