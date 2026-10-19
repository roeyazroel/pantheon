# ADR-046: Local navigation

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (the Tier 2
  representative citizens) and [ADR-010](ADR-010-creature-simulation-and-learning.md) (navigation
  as a region graph in the core)

## Context

§85 asks for navigation at two scales:

- **Strategic:** a region graph for migration, armies, trade and long journeys.
- **Local:** a navigation mesh (it names Recast) for visible agents, so a walker goes from a region
  path to a specific building.

The region graph exists (`RegionPathfinder`, ADR-010). Inside a village, the representative
citizens walked straight between their door, their work and the shrine. Their lines crossed huts,
the shrine and the temple, so figures were seen walking through walls. Some farmers' and
villagers' work spots even lay inside a neighbour's hut.

ADR-010 chose to navigate in the core, not with Godot's navigation server, so the game is
deterministic and tested headless. Godot's server bakes with Recast, but it lives in the client,
bakes from scene geometry, and would leave the core's routines untestable.

## Decision

Each village shown in Tier 2 gets a local navigation grid in `Pantheon.Game`, baked from its layout:
a navigation mesh of square cells in place of Recast's polygons.

- **The grid (`LocalNavGrid`):**
  - cells of `NavCellSize` metres (1 m) over the village and its fields;
  - blocked cells: the sea and fresh water, each hut, ruin and the building site (2.1 m times its
    scale), the shrine (2.8 m) and, once built, the temple (3.4 m), each with `NavClearance`
    (0.4 m) of room;
  - the numbers are in `settlement_view.json` (`Citizens`).
- **A way:**
  - a straight line when nothing blocks it;
  - else A* over the cells (eight directions, no corner cutting, octile distance), from the open
    cells nearest the two ends;
  - the result is pulled taut to the points that can see each other, ending at the goal;
  - ways are cached by their two ends, so each is searched once.
- **Walking it (`CitizenRoutine`, `ICitizenWays`):**
  - a figure walking a leg follows the leg's way;
  - it goes a little faster on a detour, so each leg still takes its straight line's time: the
    routine stays a closed-form function of time and its cycle is unchanged;
  - without ways the routine is the old straight one.
- **The crowd (`SettlementCrowd`):**
  - it bakes the grid while its figures are shown, and again when the layout or the temple changes;
  - it supplies the ways, searched the first time a figure walks a leg, so they spread over the
    round instead of coming all at once.
- **No work inside walls:** a work spot inside a hut moves just outside its wall
  (`RepresentativeRoster`).
- **F1:** the selected village's grid shows its cells, those blocked, the ways searched and those
  that go round something.

## Consequences

- **§85:** both of its scales exist, for the visible crowd.
- **Seen:** figures walk round huts, the shrine and the temple instead of through them, and nobody
  works inside a house. In a test, a whole village's round has no walking figure inside a hut,
  where straight lines put some there.
- **Cost, measured on the Mac:**
  - a village up close is unchanged (113 FPS);
  - the stress world (7,000 figures) runs at 87 FPS instead of 100 in its first seconds, while about
    21,000 ways are first searched; they are then cached.
- **Not modelled:**
  - a followed villager, the prophets and the Creature still walk their simulated straight lines
    between their places in a village;
  - the walls' posts are not obstacles, as their gaps let people through;
  - figures do not steer round each other.
