# ADR-069: Feet on uneven ground, the world still under dialogs, and the gods' territories on the land

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-061](ADR-061-animal-creatures.md) (the Creatures' bodies and motion),
  [ADR-068](ADR-068-every-command-in-view.md) (the commands in view), and spec §10 (divine influence)

## Context

The owner, playing, reported three things:

- "The creature legs can sometimes go inside the ground."
- "When opening the dialog, scrolling in the world should not work."
- "It is not clear what is my god territory."

The causes:

- **Feet.** `AnimalMotion` makes poses for flat ground. Only the walk took the ground under each
  foot into account, clamped to half a body unit. Standing, working, farming, uprooting, fighting,
  attacking and playing did not. The body as a whole was also tipped by 40% of the slope, which
  moved the feet too. On a hillside the uphill foot sank.
- **Scrolling.** Godot's interface stops mouse buttons and motion at a control that stops the
  mouse, but not gestures. A Mac trackpad's two-finger scroll and pinch arrive as
  `InputEventPanGesture` and `InputEventMagnifyGesture`, so they passed through any panel to the
  camera. The wheel outside a panel moved the world while a dialog was open.
- **Territory.** Spec §10's influence zones were shown only by the Influence overlay, off by
  default. Nothing on the land said whose it was.

## Decision

### Feet planted in every pose

`AnimalFooting` is a last pass over any pose made for flat ground:

- **Where the foot goes.** Each ankle's place is read from its leg by forward kinematics, then moved
  by the ground under it. Its lift is kept: a swinging foot stays as high above its ground as above
  flat ground.
- **Reaching it.** The hip and knee are bent to reach the ankle by two-bone inverse kinematics.
- **The foot's angle.** The foot keeps its turn, laid along the ground's slope ahead of it, up to
  0.6 radians.
- **The hips** move to give the legs room:
  - they drop when a foot cannot reach low ground, by up to 0.45 leg lengths;
  - they rise when a foot cannot fold for high ground, by up to 0.5;
  - a rise wins over a drop, so no foot sinks;
  - the client raises the hips at once and lowers them smoothly.
- **The body.** The legs stay upright, and only the torso leans into the slope. A sitting Creature
  rises over the higher ground under its legs.

The client samples the ground under each foot and just behind and ahead of it.

### No scrolling under dialogs

`CommandController` takes in hand the wheel, pan gestures and pinches before the camera sees them:

- while a dialog is open: any panel the god's bar opens, the key map, a talk with a god, or the menu;
- while the pointer is over the interface.

A panel's own scroll still works, since the interface sees the event first.

### The gods' territories on the land

`Territories` samples every god's **lead** over a grid of at most 160 cells a side, 16 m at the
finest. A god's lead is its influence less the greatest of 0.1 and every other god's influence.
(0.1 is the least influence most miracles need.)

- **A god's territory** is where its lead is above nothing. So land is at most one god's, and two
  borders meet where the gods' influence is equal.
- **Distance from the border.** A point's distance inside the border is its lead over the lead's
  slope there.

`TerritoryImage` paints the territories. The client lays the picture over the island as a decal, as
the map overlays are, so it follows the ground of either renderer and not the houses.

- **Your land:** a bright line inside the border in your colour, 4.5 m at the default height, with a
  dark edge outside it so it reads on sand and grass alike. A glow fades inward over 34 m, and a 12%
  wash of your colour covers all your land.
- **A rival's land:** a line half as wide, and its glow, at 60% of the opacity.
- **Land only.** Influence spreads over the sea, but only land is painted.
- **Width with height.** The widths double with each doubling of the camera's height (from 0.7 to 4
  times), so the borders read from close by and from the whole island's view.

`TerritoryPresenter` paints the picture on a worker thread. It repaints at most every 2.5 seconds,
and only when the influence has moved (its sources rounded to a few metres) or the camera has
changed zoom band. It has a layer of its own, since the holy sites' layer is cleared when it is
rebuilt.

## Consequences

- **Feet.** They stand on the ground they are over in every pose and on any slope, and a test holds
  that for every species and pose over stepped and sloping ground. On flat ground the poses are
  unchanged.
- **Dialogs.** They hold the player's attention. Moving the view while one is open needs the keys,
  or closing it first.
- **Territory at a glance.** Where you can act and where rivals lead show on the land. The
  Influence overlay keeps the finer picture.
- **Cost.** About 1.4 million pixels are painted on a worker thread when influence moves, with no
  cost to the frame.
- **No save or protocol change.**

## Alternatives considered

- **Ribbons of mesh along traced borders.** These were built first, tracing the borders by marching
  squares. On steep ground, ribbons cut into the hills between their points, and their width cannot
  follow the zoom without rebuilding the mesh. A decal follows the ground exactly.
- **Each god's whole influence zone, overlapping.** This is true to §10, but four overlapping
  outlines do not answer "whose land is this". The Influence overlay shows the overlap.
- **Godot's physics for the legs.** A ragdoll or physical IK would give the bodies weight, but would
  need joints tuned per species. The kinematic pass is deterministic and testable, and the
  simulation does not need it.
