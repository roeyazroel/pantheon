# ADR-058: Camera controls for the mouse and the trackpad

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-031](ADR-031-book-of-the-world-overlays-overview-and-exports.md) (the strategic overview),
  [ADR-055](ADR-055-expressive-creatures-and-miracle-looks.md) (looking at the Creature's face) and
  [ADR-057](ADR-057-tactile-hand-camera-above-roofs-and-bridges.md) (the camera above the roofs)

## Context

After playing on a MacBook, the owner reported that zooming in and moving around did not work well.
Investigating found several causes:

- **Trackpad:** the camera understood only mouse-wheel notches. On macOS, Godot reports a trackpad's
  (or Magic Mouse's) two-finger scroll as a pan gesture and a pinch as a magnify gesture, so neither
  zoomed or moved the view. Zooming was left to the `=` and `-` keys.
- **Buttons:** grabbing the land needed the right button and turning the view the middle button,
  which a trackpad does not have. The left button did nothing unless a miracle was aimed.
- **Window's edge:** the edge of the window always panned, so reaching for the Dock or the menu bar
  slid the view away.
- **Following:** looking at the Creature glided to it once, and it walked out of view at 7 m/s or
  more.
- **Close up:** the view could come no closer than 25 m, could not be tilted, and aimed at the
  Creature's feet, so its head was cut off.
- **Duplicated gestures:** Godot 4.7 on macOS pushes each trackpad gesture through the viewport
  twice.

## Decision

### The mouse

- **Left button:** dragging the land grabs it, as the god's hand does in the genre. The press becomes
  a grab only once the mouse moves 5 pixels, so a click stays a click. Letting go mid-drag flings the
  view, which glides on and slows (`Fling`, damping 4 a second, at most 3 × the distance a second).
  While a miracle is aimed or the Creature is being sent, the left button keeps its meaning and does
  not grab.
- **Double click:** on the land, flies there and closes in.
- **Right or middle button:** dragging orbits. Sideways turns the view; up and down tilts it.
- **Wheel:** zooms towards the cursor, as before.

### The trackpad

- **Two fingers:** pan the view by a share of what it shows (`PanViews`). A setting makes them zoom
  instead. With Command or Control held they zoom anyway.
- **Option or Shift with two fingers:** turns and tilts the view.
- **Pinch:** zooms about the fingers (`ZoomByFactor`).
- **The repeat:** a gesture identical to the last one in the same frame is ignored. That undoes the
  engine's doubling, and a real repeat in one frame is rare.

### Tilt, distance and aim

- **Tilt:** the player may tilt the view up to 30° either way from the pitch its zoom gives
  (`TiltBy`, also Page Up and Page Down). The pitch stays between 10° and 85°.
- **Distance:** the closest the camera comes is 12 m instead of 25 m.
- **Aim:** `AimAbove` raises the point the camera looks at above the ground. A followed Creature is
  framed by its body, not its feet.

### Following

- **What follows:** `GodCameraController.Follow` keeps the focus each frame on where a thing is
  drawn. Looking at the Creature (C, the Look button, `--focus=creature`) follows it at 60 m, aimed
  at half its height.
- **Zooming:** while following, the view zooms about what it follows.
- **Ending:** panning by keys, the window's edge, a grab or two fingers ends following, with a
  status line that says so. Focusing a settlement ends it too.

### Settings

- **Pan at the window's edge:** off by default.
- **Trackpad scrolling zooms:** off by default, so two fingers pan.
- **Camera speed:** 0.5–2, scaling the keys, the window's edge and the gestures.

## Consequences

- **Platforms:** the camera handles a Mac trackpad, a Magic Mouse (two-finger scroll pans, or zooms
  by the setting), a wheel mouse and the keyboard.
- **Grabbing:** grabbing the land moves from the right button to the left. The right button now
  orbits.
- **Tests:** the model's new behaviour is pure and tested: tilt and its limits, the trackpad's pan,
  the pinch, the fling and the aim. The input handling was checked in the running game by injected
  pan, pinch, click, drag and orbit events.
- **Not modelled:** a trackpad's rotate gesture (Godot does not report it), inertia for keyboard
  pans, and remapping the controls.
