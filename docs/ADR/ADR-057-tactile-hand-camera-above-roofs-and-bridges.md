# ADR-057: A tactile hand, the camera above the roofs, and walkers on bridges

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md) (the Divine Hand),
  [ADR-048](ADR-048-structural-integrity-and-debris.md) (debris on Jolt),
  [ADR-046](ADR-046-local-navigation.md) (the walkers' grid) and
  [ADR-054](ADR-054-wildlife-and-bridges.md) (bridges)

## Context

§55 asks that "physics interactions should feel tactile". A thrown rock or tree already lands and
rolls among the debris it throws up (ADR-048). But what the hand held was stuck rigidly under its
palm, and the debris lying about could not be touched.

Two faults remained from the art pass:

- **The camera:** it kept a clearance above the ground but not above houses. On a steep village's
  edge it could look out from inside a house.
- **The walkers:** their grid treated all water as blocked. Villagers whose round crossed a river
  waded straight across, even where a road's bridge spanned it.

## Decision

### What the hand holds swings (`HeldSwing`, pure)

- **The pendulum:** a held rock, tree, basket or group of villagers hangs below a grip under the
  palm, as a damped pendulum (5.5 rad/s, damping 2.2 a second, at most 1 rad).
- **What drives it:** the hand's acceleration, measured in hand sizes so it swings alike at any
  zoom. A quick move leaves the thing lagging behind; it swings past as the hand stops, then
  settles. An even glide leaves it hanging straight down.
- **Cursor jumps:** the hand's acceleration is capped, so a jump of the cursor is not a throw.
- **A held Creature:** it hangs offset by the same swing, up to 6 m.

### The hand's press knocks the debris (`DebrisPresenter.Push`)

When a press reaches the ground (casting, or a throw landing), the debris within 12 m is knocked
away from it and a little up, the nearer the harder (up to 7 m/s).

### The camera keeps above the roofs (`Rooftops`, pure)

- **Clearance:** the camera's pose keeps its clearance above the highest roof within 3 m of the
  walls beneath it, as well as above the ground.
- **The focus:** the point it looks at still follows the ground, so it never rides up onto a roof.

### Walkers cross on bridges (`LocalNavGrid`)

- **The decks:** a village's walkers' grid opens the water cells under the bridges of its roads,
  1.3 m either side of each deck (`BridgeLayout.Spans`).
- **When:** the grid is baked again as those roads are built further.
- **The way:** villagers whose round crosses a bridged river walk over the bridge. Where no bridge
  spans it there is still no way, and they wade straight across, as before.

## Consequences

- **§55:** its interactions feel tactile: what the hand holds swings with its moves, a thrown rock
  rolls, and the hand's press scatters the rubble.
- **The camera:** the Kestrel Rise view that looked out from inside a house now looks over the
  village from above its roofs.
- **The walkers:** villagers keep to bridges where their roads have them.
- **Not modelled:** a held thing that collides with the land, a hand that pushes things without
  pressing, and walkers who go round a river by a far bridge outside their village's grid.
