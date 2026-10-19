# ADR-054: Wildlife and bridges

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-028](ADR-028-seasons-extremes-and-ecology.md) (the land's wildlife),
  [ADR-049](ADR-049-people-that-look-and-move-like-people.md) (the crowds' clock) and
  [ADR-053](ADR-053-trees-ground-cover-and-wind.md) (the art pass)

## Context

§61 names wildlife among what a benevolent nature god produces. The simulation already keeps each
region's wildlife (ADR-028): it grows back, villages hunt it, fire kills it and cleared forests
hold less. Nothing showed it but a debug layer and an overlay.

Roads also ran through rivers as ribbons on the river bed.

## Decision

### Herds and flocks (`Wildlife`, pure)

- **Where:** within 280 m of the camera:
  - a region of the deer's biomes may hold a herd, with a chance of 0.55 times its wildlife;
  - a region of the birds' biomes may hold a flock, with a chance of 0.35 times its wildlife.
  - herds have 2 to 8 deer and flocks 4 to 10 birds, more where wildlife abounds;
  - at most 140 deer and 140 birds are drawn.
- **Herds:** a herd grazes 45 s about one spot, then walks 18 s to the next. Spots are on dry
  ground away from the villages' houses, fields and squares, drawn from `PresentationHash`. Each
  deer stands about the herd's centre, a third of them stags.
- **Flocks:** a flock wheels round its point 22–42 m above the ground at 8 m/s, its birds a little
  apart and out of step.
- **Time:** all of it is closed-form in the crowds' time (ADR-049). So animals keep a natural pace
  at any game speed and stop when it is paused, and the same herd is in the same place at the same
  time.
- **The simulation:** hunted, burnt and cleared land shows empty. Nothing is saved or simulated.

### Bodies (`AnimalMesh`, `animal.gdshader`)

- **The deer:** a body, neck and head, ears, four legs with hooves, a tail and antlers.
- **The bird:** a body, head and beak, two wings and a tail.
- **Animation:** the shader turns parts by motion:
  - walking: the legs swing in diagonal pairs and the head bobs;
  - grazing: the head lowers;
  - flying: the wings beat;
  - antlers show only on stags.

### Bridges (`BridgeLayout`)

- **Where:** each stretch of a road's built part over water (fresh water, or ground below the
  sea's edge).
- **What:** a span from the dry point before it to the dry point after it, its deck 0.6 m above
  the higher bank. The road presenter lays a plank deck with rails along it, painted by the
  building shader.

### Tools

- `--wildlife=on` takes the camera to the deer nearest the focused settlement.
- F1's streaming line counts the deer and birds drawn.

## Consequences

- **§61:** the land's life is visible: herds in the meadows, birds over the woods, and empty land
  where it has been hunted out. Roads cross rivers on bridges.
- **Performance:**
  - a grown game at ×16: 132 FPS;
  - the stress world: 91 FPS, once the wildlife and ground cover look only at the villages' bare
    ground near them. At first they tested all of the stress world's 20,000 bare patches each
    time, and the frame rate fell to 68 FPS.
- **Not modelled:** animals that flee the people or the Creature, fish, and villagers who keep to
  bridges (rounds across a river still wade where no way round exists).
