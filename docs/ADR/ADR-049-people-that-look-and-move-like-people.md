# ADR-049: People that look and move like people

- Status: Accepted (amends ADR-008)
- Date: 2026-10-20
- Amends: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the representatives'
  routines in simulation time)
- Builds on: [ADR-032](ADR-032-prophets-elders-and-temple-influence.md) (prophets and elders),
  [ADR-043](ADR-043-the-selected-villager.md) (the followed villager) and
  [ADR-016](ADR-016-migration-groups-and-refugee-policies.md) (migration groups)

## Context

The owner played the game and found the villagers poor: "they look like colored dots, and they move
unrealistically fast, like ants, not like humans". Both were true:

- **The look:** a crowd figure was a cone with a ball for a head, enlarged 1.4 times and coloured
  by its work: red, amber or blue. Migrants were capsules, and prophets, elders and the followed
  villager were cones.
- **The pace:** ADR-008 made each routine a closed-form function of simulation time, so at ×4 a
  person walked 5.6 m/s and at ×16 over 20 m/s. Each stride was a hop.
- **The bustle:** rounds were short (20 s of rest, 45 of work, 15 of prayer), so most of a village
  was always on the move.

§61 asks for readable, recognizable civilizations in a stylized look, not a photorealistic one,
with scalable performance. The stress world draws 7,000 figures (ADR-044).

## Decision

### The crowds' clock

- **The clock (`CrowdClock`):** the routines run on a clock of their own, held by the session. It
  advances by real time × min(game speed, `PaceCap`) and stops while the game is paused. With
  `PaceCap` 1, people walk at 1.3 m/s whatever the game's speed.
- **What this amends:** ADR-008's routines in simulation time. The routines are still
  closed-form, derived, never saved and without effects.
- **Picking:** the `/` key poses the figures by the same clock.
- **Rounds:** longer: 50 s sitting by the door, 110 s of work, 30 s at the shrine. Rerouted
  people hurry at 2.6 m/s rather than 4.

### People

- **The figure (`PersonMesh`):** built in code, 1.74 m tall and drawn at 1.15 times life size. It
  has legs and shoes, a tunic with a belt, arms and hands, a neck and head, and hair. It can also
  show a straw hat, a scarf, a long dress, a hoe, a hammer and a staff. A detailed and a simple
  figure are built; beyond `DetailDistance` (170 m) a village uses the simple one.
- **The look (`PersonLooks`):** from `PresentationHash` of the seed, the village and the person's
  place:
  - a skin tone between six;
  - one of six hair colours;
  - clothes by work: earth colours for farmers, greys and browns for builders, dyed linen for
    others;
  - a height between 0.9 and 1.07;
  - a straw hat (60% of farmers) or a scarf (35% of builders, 30% of others);
  - a long dress (40% of farmers and others).

  Prophets and elders wear a robe of their god's colour and carry a staff.
- **The motion (`PersonMotions`):** what a figure does picks a motion:

  | Doing | Motion |
  | --- | --- |
  | Resting at home | Sitting |
  | Walking | Walking |
  | Farming | Hoeing |
  | Building | Hammering |
  | Chores | Chores |
  | At the shrine | Praying |

  A pose carries its motion's cycle: strides of two steps, work strokes, breaths or prayers.
- **Animation on the GPU (`person.gdshader`):**
  - each vertex is tagged with its part and colour slot;
  - each instance carries its clothes and skin tone in its colour, and its cycle, motion, lean
    and look in its custom data;
  - the vertex stage swings legs and arms about the hips and shoulders, bends the body at the
    hips, sits it down, nods the head, and hides what the person does not wear or hold;
  - so the CPU sets one transform and one custom value per person a frame, as before, and needs
    no skeleton.
- **The others:**
  - migrants walk two abreast as people (`MigrationView`);
  - the followed villager, prophets and elders are single people (`PersonFigure`) whose walk keeps
    step with the ground they cover. Their positions are simulated, so at high speed they run
    rather than glide.

### Tools

F1 shows a Tier 2 village's people clock: "people: their own clock at 412 s, running x1 with the
game at x16, kept to a human pace".

## Consequences

- **§80 and §61:** the villages read as people at work, from 20 m to about 150 m.
- **Pace:** people keep a human pace at every game speed.
- **Performance:** the stress world runs at 108 FPS with 7,000 people (93 FPS before, with cones),
  drawing 6.5 million triangles. A grown game at 22–140 m runs at 118–121 FPS.
- **No change to:** the simulation, saves or sharing.
- **Not modelled:**
  - faces and fingers;
  - knees and elbows (each limb is one piece);
  - carried goods;
  - looks that follow a village's culture or biome, which is part of the art pass.
