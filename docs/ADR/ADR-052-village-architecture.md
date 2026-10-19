# ADR-052: Village architecture

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md) (temples and walls),
  [ADR-042](ADR-042-religions-look-on-villages.md) (religion's look) and
  [ADR-051](ADR-051-ground-textures-and-light.md) (the art pass)

## Context

§61 asks for recognizable civilizations, and for religion to affect how they look: gardens and
organic structures for a benevolent nature god, intimidating monuments, fortifications and darker
ceremonies for a fearful war god. ADR-042 laid out gardens, obelisks and columns among the houses.

The houses themselves were all the same: a cylinder, a cone and a door. Temples were one box under
a prism in the god's colour, and walls a ring of thin posts.

## Decision

### Houses (`HouseMesh`, `MeshBuilder`, pure)

Five styles, built in code, each fitting the 2 m radius the layout and paths keep for a house and
sunk 0.6 m into the ground:

| Style | What it is |
| --- | --- |
| Round hut | Wattle and daub on a stone footing, a thatched cone with a finial, a door under a lintel |
| Timber longhouse | Planked walls framed by corner posts and beams, a thatched gable, windows |
| Stone cottage | Coursed stone under a slate gable, with a chimney |
| Adobe house | Plastered walls, a flat roof with a lip, protruding beam ends |
| Stilt house | A plank house on four posts, under thatch, with a ladder to the door |

- **Upper storeys:** every style but the round hut and the stilt house can take one.
- **Slots:** each vertex is tagged with its slot: wall, roof, timber, door, window or stone.
- **Shared builder:** `MeshBuilder`, taken out of the person mesh (ADR-049), builds tubes,
  ellipsoids, boxes, slabs and gabled roofs.

### A village's look (`VillageArchitecture`, pure)

- **The land picks the style:**

  | Land | Style |
  | --- | --- |
  | Desert | Adobe |
  | Swamp | Stilts |
  | Highland, volcanic land | Stone |
  | Forest, tundra | Timber |
  | Elsewhere | Round huts |

- **Growth:** villages of huts build longhouses at 18 houses, and any village builds an upper
  storey at 40.
- **Colours:** a village picks one of its style's wall and roof colours. Each house varies by 8%.
- **The god:** where the god most of a village follows shows at least 0.35 of its style (ADR-042),
  the houses take its colours:
  - turf roofs for nature and growth;
  - dark timber and roofs for war and death;
  - whitewash and blue roofs for learning.

### Temples and walls (`CivicMesh`, `CivicLayout`)

- **Temples:** a temple takes the style of the god it is raised to, when that god's style is
  strongly felt in the village, under the god's colour:
  - a domed round temple with a ring of columns (nature);
  - a dark stepped pyramid with a brazier (war);
  - a columned hall (learning);
  - a plain hall on a plinth otherwise.
- **Walls:** a palisade of pointed stakes, 0.36 m apart and each cut a little differently, with
  timber watchtowers on each side of the four gates. A village that builds in stone or adobe, or
  follows a god of learning, raises crenellated stone walls with round stone towers instead.

### Painting (`building.gdshader`)

- **The instance:** a building's instance carries its walls' colour and, in its custom data, its
  roof's colour and the patterns of both.
- **Patterns:** the fragment stage paints a pattern worked out in the building's own space:
  - daub;
  - upright planks with dark seams;
  - coursed stone in mortar;
  - thatch in layers combed into strands;
  - slate tiles;
  - timber grain.
- **Building:** a house under construction is the village's house with everything above the work
  done hidden (`built`), so it rises from the ground.

## Consequences

- **§61:** villages are recognizable by their land, their size and their god, with gardens and
  turf roofs, dark timber and dark pyramids, whitewash and columned halls, and walls with towers.
- **No change to:** the simulation, saves or sharing. The look is derived from the world and
  never saved (ADR-008).
- **Cost:** at 45–140 m, 141–166 FPS (152–196 before, from more geometry and patterned
  surfaces).
- **Not modelled:**
  - styles by culture rather than land;
  - one-off buildings for the granary, cistern and the others;
  - painted roads;
  - wading figures where their round crosses a river with no way round.
