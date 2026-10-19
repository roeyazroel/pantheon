# ADR-055: Expressive Creatures and miracle looks

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-010](ADR-010-creature-simulation-and-learning.md) (the Creature and its god's
  rewards and punishments), [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md)
  (the species' looks), [ADR-037](ADR-037-creature-temperament.md) (temperament) and
  [ADR-054](ADR-054-wildlife-and-bridges.md) (the art pass)

## Context

§61's goals include "expressive Creatures" and "visually strong miracles". The Creature's body
showed its species, level, scars and pose, but not how it felt: a Creature in a fight, in flight,
at play or just slapped by its god wore the same face, and a cruel Creature looked like a kind one.

Miracles had four looks: falling motes, rising motes, a spreading ring and a lightning bolt.
Rain, fire, healing, a shield and an earthquake were told apart only by colour.

## Decision

### The face (`CreatureExpression`, pure)

`FaceOf` derives a `CreatureFace` from the Creature's state. It is never saved. The face has five
values: joy from -1 to 1, and anger, fear, sleep and wag from 0 to 1.

- **Nature:** a kind Creature smiles more, and a cruel one scowls.
- **Needs:** hunger past half and boredom past 0.6 make it glum. Fatigue past 0.6 makes it drowsy.
- **Health:** below 0.3 it is afraid.
- **Doing:** what it is performing or travelling to do shows strongest:
  - in a fight or an attack it scowls;
  - in flight it widens its eyes;
  - at play or befriending it grins;
  - at rest its eyes shut;
  - feeding, building and eating please it.
- **Its god's word:** a reward brings joy and a punishment fear, for 300 ticks after the deed
  they marked. A word on a deed still under way marks no memory, so the client passes what it has
  just seen given, and the face shows it for four seconds.

`MoodOf` names the strongest feeling: content, joyful, angry, afraid, sad or sleepy. F1's Creature
lines show the face and the Creature's nature.

### The body (`CreatureBody`)

- **Face parts:** brows, upper lids, cheeks, pupils and a mouth on the front of the snout, where
  the god's camera sees it.
  - **Anger:** the brows draw down and in, and the mouth bares.
  - **Fear:** the brows rise, the pupils widen and the mouth gapes.
  - **Joy:** the brows rise, the cheeks push up into the eyes and the mouth's corners turn up into
    a grin.
  - **Sleep:** the lids close.
  - The face parts cast no shadows. Each would draw again in every shadow pass, about 130 draw
    calls for nothing seen.
- **Tail:** it wags in joy, lashes in anger and tucks in fear.
- **Nature:** as the Creature's morality moves, its body shows it.
  - A kind Creature's hide warms and glows softly.
  - A cruel one's darkens and reddens, its eyes burn red and its horns grow up to 1.8 times as
    long.
- **The camera:** looking at the Creature (C, or `--focus=creature`) turns the view the short way
  round to a three-quarter view of its face (`GodCameraModel.FaceFront`).

### The miracles (`MiracleEffectStyle`, `MiracleLayers`)

Eleven signature looks join the four, and `divine_view.json` gives every miracle one:

| Look | What it shows | Miracles |
| --- | --- | --- |
| Rain | a dark cloud gathering, and rain pouring from it | Rain |
| Harvest | a golden pillar, and grain falling about it | Food, Abundance |
| Healing | a spiral of light rising under a soft dome | Heal, Mass Healing, Regrowth, Gift of Life |
| Flames | a burst of flames, embers and smoke | Fire, Firestorm, Awaken |
| Quake | the ground heaves: a dust burst and a shock ring | Earthquake, Upheaval |
| Shield | a dome of light rising over the radius | Shield, Sanctuary |
| Fountain | a fountain of water springing up | Water |
| Holy | a pillar of light from the sky, motes rising into it | Enlighten, Hallow, Anoint |
| Shadow | a dark swirl and shadows rising | Dread, Sow Discord |
| Storm | a storm cloud wheeling over the target | Redirect Storm |
| Strike | a god's bolt, with a shock ring and sparks | Lightning, Spear of Heaven, Judgement |

The weather's lightning keeps the plain Bolt.

**How the layers are drawn:**

- **Domes and pillars** use `miracle_glow.gdshader`. They glow at the rim, where the eye looks
  along the surface, and pillars fade towards the top. So a great dome over a village reads as a
  shining edge, not a veil over everything. A dome dims as it grows (a 150 m dome at 0.35 of a
  small one's strength).
- **Clouds** are one set of soft particles that swell in and fade out. Their puffs fade near the
  camera, so a cloud never hides the village it rains on.
- **Particles:** rain, grain, flames, embers, smoke, dust, sparks, spirals and fountains are
  particles on the GPU.

## Consequences

- **§61:** its goals are met.
  - Readable from altitude: labels, overlays and the strategic view.
  - Expressive Creatures.
  - Visually strong miracles.
  - Distinct biomes and recognisable civilisations: ADR-051 to ADR-054.
  - Scalable performance: ADR-050.
  - Religion shapes how villages look: ADR-042 and ADR-052.
- **Performance:** a grown game at ×16, where the AI gods cast about sixty miracles in a minute,
  measured on the same Mac in the same checkout:
  - the previous build: 11.2 ms a frame;
  - with the new looks on CPU particles: 12.3 ms, a millisecond of main-thread particle work;
  - on GPU particles: 11.5 ms.
- **Not modelled:** faces on the other species' own shapes (every species shares the face's
  parts), lip-sync or sound for moods, and miracle looks that change with the god's domains.
