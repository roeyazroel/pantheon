# Milestone 15, part 31 — Village architecture

## Overview

The second part of the §61 art pass. Villages are recognizable by their land, their size and
their god. The decisions are in
[ADR-052](../docs/ADR/ADR-052-village-architecture.md).

- **Houses:** five styles made in code (round huts, timber longhouses, stone cottages, adobe,
  stilt houses), one or two storeys.
- **A village's look:** the style from its land, longhouses and upper storeys as it grows, and its
  god's colours where its style is strongly felt.
- **Temples and walls:** temples in their god's style, palisades with watchtowers or stone walls
  with towers.
- **A building shader:** it paints daub, planks, stone, thatch, slate and timber, and raises a
  house as it is built.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Game` | • `Presentation/MeshBuilder` and `TaggedMesh`, taken out of `PersonMesh`, with slabs and gables<br>• `HouseMesh` (`HouseStyle`, `BuildingSlot`, `SurfacePattern`)<br>• `CivicMesh` (temples, stakes, stone walls, towers)<br>• `VillageArchitecture` and `VillageLook`<br>• `ArchitectureSettings` (`Architecture` in `settlement_view.json`)<br>• `CivicLayout.Walls` spacing, `Towers` and `AlongRing` |
| client | • `Shaders/building.gdshader`<br>• `Buildings/BuildingAssets`<br>• `SiteView` (houses in the look), `ConstructionSiteView` (the house rising) and `CivicView` (temple, walls, towers)<br>• `SettlementView` works out the look each sync<br>• the old hut meshes and the citizens' and huts' materials removed from `SettlementAssets` |

```mermaid
flowchart LR
    land[The village's region's biome] --> look[VillageArchitecture.Of]
    houses[Houses now] --> look
    style[ReligiousStyles.Of, ADR-042] --> look
    look --> site[SiteView: one MultiMesh of the style's house]
    look --> build[ConstructionSiteView: the house rising]
    look --> civic[CivicView: palisade or stone]
    style --> temple[CivicView: the temple in its god's style]
    site --> shader[building.gdshader: slots and patterns]
    build --> shader
    civic --> shader
    temple --> shader
```

## Test plan

- **`ArchitectureTests`:**
  - every house stands in the ground, fits its plot, faces its door out, and an upper storey raises
    it (five styles);
  - the land picks the style, and villages grow into longhouses and upper storeys;
  - a strongly felt god colours the houses, and each house's shade varies, the same each time;
  - temples take their god's style, and walls have stakes, stone and towers by the gates along
    the ring;
  - the architecture needs colours for every style.

### Verification results

- **Build and tests:** `dotnet build` has no warnings, and 1506 tests pass (1497 before).
- **On the Mac:**
  - seed 1234's Duskwater at 45 and 110 m: round huts of daub and layered thatch on stone
    footings, at 166 and 141 FPS;
  - seed 42's vast island: two-storey timber houses in Fernhollow; turf-roofed dark timber and a
    domed temple in Harrowmere, whose god is strongly felt; longhouses by Willowmere's river;
  - seed 1234 after 12,000 ticks: Kestrel Rise, walled, with Seren's domed temple, turf roofs, a
    palisade and watchtowers.
- **Found on the way:**
  - the gable ends stood at the roof's width, beyond the walls: they now stand at the walls';
  - stilt houses no longer take an upper storey;
  - at 70 m on Kestrel Rise's slope the camera sits inside a house at the village's edge; part 30's
    build shows the same with a hut, so it is the camera keeping above the ground but not above
    buildings, left for the final pass.

## Tasks

### Completed

- [x] The houses, the village's look, temples and walls, and the building shader.
- [x] The building site rising in the village's style.

### Future

- Styles by culture, one-off buildings for the granary and the others, painted roads.
- Bridges, or rounds that keep to their side of a river.
- A camera that keeps above buildings.
