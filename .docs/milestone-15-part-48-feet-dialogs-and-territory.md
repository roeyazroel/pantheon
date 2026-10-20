# Milestone 15, part 48 — Feet on the ground, the world still under dialogs, and territory on the land

## Overview

The owner, playing, reported three things:

- "The creature legs can sometimes go inside the ground; we need to fix the physics."
- "When opening the dialog, scrolling in the world should not work."
- "It is not clear what is my god territory."

The decisions are in [ADR-069](../docs/ADR/ADR-069-feet-dialogs-and-territory.md).

### What was found

- **Feet.** The legs met uneven ground only while walking, and only within half a body unit. Every
  other pose (standing, working, farming, uprooting, fighting, attacking, playing) assumed flat
  ground. The whole body was also tipped with the slope, which moved the feet further. On a
  hillside the uphill foot sank into the ground.
- **Scrolling.** A panel stops clicks and the wheel over it. A Mac trackpad's two-finger scroll and
  pinch, however, arrive as gestures, and Godot's interface lets gestures through. They reached the
  camera under the panel, as did the wheel outside it.
- **Territory.** Influence (spec §10) showed only on the Influence overlay (O), off by default. The
  land had no border.

### What changes

1. **Feet planted in every pose.** `AnimalFooting` is a last inverse-kinematics pass over whatever
   pose the Creature is in:
   - each ankle moves by the ground under it, keeping its lift (a swinging foot stays as high above
     its ground as it would above flat ground);
   - the hip and knee bend to reach it;
   - the foot is laid along the slope;
   - the hips drop where a leg cannot reach low ground, and rise where it cannot fold for high
     ground, rising at once so no foot sinks;
   - the legs stay upright, and only the torso leans into the slope;
   - a sitting Creature rises over the higher ground under its legs.
2. **No world scrolling under a dialog.** While a dialog is open (a panel, the key map, a talk with a
   god or the menu), or while the pointer is over the interface, the wheel and the trackpad's
   scrolling and pinching do not move the world.
3. **Territories on the land.** `Territories` and `TerritoryImage` paint every god's territory, laid
   over the island as a decal:
   - **Whose land.** A god's territory is the land where its influence is the strongest of all and
     at least 0.1, the least most miracles need.
   - **Your land:** a bright line in your colour, edged in dark so it reads on sand and grass, a
     glow fading inward, and a wash of your colour over all of it.
   - **A rival's land:** a thinner, fainter line and glow in its colour.
   - **What is shown.** Only land is painted. The borders widen as the camera rises, and the picture
     is painted again off the main thread when influence moves.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Game` | • `Presentation/Animals/AnimalFooting` (`FootGround`, `Ankle`, `HipShift`, `Plant`)<br>• `Presentation/Territories` (each god's lead over a grid, its distance inside the border, the shares)<br>• `Presentation/TerritoryImage` and `TerritoryLook` (the painting, widths by island and zoom band) |
| client | • `CreatureView` plants the feet for every pose, leans the torso only, and samples the ground under each foot and its slope<br>• `CommandController` keeps scrolling off the world under dialogs and the interface<br>• `Presentation/Divine/TerritoryPresenter` (a decal on its own layer, painted on a worker thread, owned by `FaithController`) |

## Test plan

- **`AnimalFootingTests`**, for every species:
  - on flat ground every pose is left as it is;
  - on stepped and sloping ground (24 cases under each of 31 poses), no foot sinks below its
    ground, and a foot within reach stands on it in more than nine cases in ten;
  - low ground drops the hips, high ground raises them up to the limit, and the foot lies along its
    slope, up to the steepest.
- **`TerritoryTests`:**
  - land is at most one god's;
  - each god's land is where it leads;
  - the border is bright on its god's side;
  - nothing is painted outside a territory except the player's dark edge;
  - only the player's land is washed;
  - only land is painted;
  - the borders widen with height;
  - the same world gives the same picture.

## Tasks

### Completed

- [x] Feet planted in every pose, scrolling kept off the world, territories on the land, tests,
  captures and docs.

### Verification results

- **Build and tests:** `dotnet build` has no warnings, and 1781 tests pass (1759 before).
- **Feet:** captures of the Cow climbing a steep hillside with both feet on it, and resting by the
  river.
- **Scrolling:** a temporary probe fed ten wheel turns, trackpad scrolls and pinches into the game.
  - With the gods' panel or a talk open, the camera stayed at 380 m over the same point.
  - With no dialog, it zoomed to 12 m and moved.
  - A first probe sent the wheel's press without its release, and the camera's polled zoom then
    read the key as held. Real wheels send both.
- **Territories:** captured at 380 m and 1500 m on Terrain3D, and on the fallback renderer:
  - your land around Larkspur and the pocket about your holy site, in gold with its dark edge;
  - the rivals' borders fainter;
  - nothing over the sea.

### Future

- Your share of the island on the god's plaque.
