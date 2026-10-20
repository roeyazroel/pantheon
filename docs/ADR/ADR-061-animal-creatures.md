# ADR-061: Animal Creatures, distinct for every god, and their motion close up

- Status: Accepted (amends ADR-021: the four species become eight animals)
- Date: 2026-10-20
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature),
  [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (species),
  [ADR-055](ADR-055-expressive-creatures-and-miracle-looks.md) (faces) and
  [ADR-058](ADR-058-camera-controls-for-mouse-and-trackpad.md) (following the Creature)

## Context

The owner wants the Creatures to be "some sort of animals like in the original Black and White
game", different for every god. Close up, "the physics looked odd, and it was not visualized well".

The Creatures were four invented species (Mossback, Stonehide, Swiftclaw, Lumen) sharing one body
of about thirty primitive meshes, recoloured and stretched. Two gods could share a species. A study
of the code found these faults close up:

- **Pulsing motion:** the body eased towards each 10 Hz tick's place, so its speed pulsed between
  about 4 and 11 m/s.
- **Feet:** they floated half a metre above the ground and slid, since a fixed swing never matched
  the stride.
- **Resting:** the Creature lay on its back half buried.
- **Held:** it hovered 12 m above the cursor rather than hanging from the hand.

## Decision

### Eight animals

- **The species:** Cow, Ape, Tiger, Lion, Wolf, Bear, Horse and Tortoise, each with its factors and
  temperament in `creatures.json`.
- **Upright:** they walk upright with arms and hands, as Black & White's Creatures did, to carry,
  build and fight.
- **One per god:** Ilyra a Cow, Vael the Tyrant a Lion, Seren the Guardian a Bear, Kaio the
  Trickster an Ape, and in larger worlds Aurel a Horse, Maren a Tortoise, Thorn a Tiger and Ysolde a
  Wolf.
- **Choosing:** when the player chooses a rival's animal, that rival takes the player's
  (`WorldMaker`).
- **Old saves:** version 34 renames the four old species by their natures: a Mossback becomes a
  Cow, a Stonehide a Tortoise, a Swiftclaw a Tiger and a Lumen an Ape. Their factors are kept.

### Bodies as signed distances (`Pantheon.Game.Presentation.Animals`, pure)

- **Parts:** each part of a body (torso, head, thigh, shin, foot, upper arm, forearm, hand, tail
  segment and tip) is a shape of signed-distance parts (ellipsoids, round cones, round boxes).
  The parts blend into one organic surface.
- **Shared parts:** every animal shares the torso, limbs, and paws, hooves, hands or stumps, shaped
  by its proportions.
- **Its own parts:** each has its own head (horns, mane, muzzle, ears), tail and markings.
- **Faces:** each head carries a face plan for its eyes and mouth, so ADR-055's expressions show on
  every species.
- **Meshing:** `SurfaceNets` meshes each shape into smooth, closed, outward-facing triangles, with
  normals from its gradient.
- **Painting:** `AnimalPaint` paints each vertex by the nearest part's paint (fur, belly, muzzle,
  nose, inner ear, horn, hoof, mane, shell), with the species' pattern: stripes, patches or plates,
  and a soft mottle.
- **Colours:** in `divine_view.json`, where each look may name its body.
- **The Beast:** a great horned bear on the same body.

### Motion (`AnimalMotion`, pure)

- **Walking:** a foot touches down ahead of its hip, stays where it landed while the body passes
  over it, then lifts and arcs forward. Two-bone inverse kinematics bends the hip and knee to reach
  it.
  - The stride grows with the distance moved, so feet never slide at any size or speed.
  - A run has a longer stride, a shorter stance and a forward lean.
  - Each foot reaches the ground under it.
- **Other poses:** standing (breathing), sitting to rest, working with both hands, boxing, raking
  in an attack, hopping in play, and dangling. `BodyPose.Lerp` blends them.

### The view (`CreatureView`, `AnimalBody`, `AnimalMeshes`)

- **Meshes:** each species' meshes are made once a run on worker threads. A Creature appears when
  they are ready.
- **Position:** the Creature is drawn between the simulation's last two places by
  `SimulationScheduler.Alpha`, so it moves at an even pace. A jump of over 25 m (the hand set it
  down, a fight drew it in) is drawn at once.
- **Slope:** the body leans a little with it.
- **Held:** it dangles by its scruff from the hand's grip. The hand rises (`DivineHand.HoldLift`) so
  the Creature's feet clear the ground.
- **The god's mark:** a collar of its god's colour shows whose Creature it is.
- **Nature:** it tints the hide warm or dark and lights a cruel Creature's eyes. Horns no longer
  grow with cruelty.
- **The camera:** it rises until its line of sight to its focus clears the land between them, so a
  hill never fills the view of a followed Creature.

## Consequences

- **§61, expressive Creatures:** each god's Creature is an animal of its own, recognisable from
  the air and expressive close up.
- **§55:** a held Creature hangs from the hand.
- **Cost:** a body is about 26,000–33,000 vertices. Meshing a species takes 0.5–1.7 s on a worker
  thread on the M3 Pro; the island's eight species are made in parallel at the start.
- **Saves:** version 34; the fixture of version 33 and its migration are tested.
- **Not modelled:**
  - skinned meshes (the parts are rigid on their joints);
  - fur that moves;
  - hands that close around what they carry;
  - each animal's own way of moving: an ape walks as the others do, and a horse does not gallop
    on four legs.
