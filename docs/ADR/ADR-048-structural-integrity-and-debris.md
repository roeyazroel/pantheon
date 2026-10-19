# ADR-048: Structural integrity, flood resistance and debris

- Status: Accepted
- Date: 2026-10-20
- Builds on: [ADR-014](ADR-014-disasters-as-processes.md) (disasters and ruins),
  [ADR-029](ADR-029-hazards-preparation-and-marks-on-the-land.md) (hazards), [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md)
  (the hand) and [ADR-046](ADR-046-local-navigation.md) (local navigation)

## Context

§94 asks physics to be used selectively:

- buildings have structural abstractions: structural integrity and earthquake, fire and flood
  resistance;
- every building brick is not simulated, and the result is decided by the simulation;
- when buildings are destroyed, visual physics creates convincing debris.

§55 asks the hand's physical interactions to feel tactile.

Houses had earthquake and fire resistance (ADR-014), and a fallen house became rubble. A flood
took houses at its full rate. Nothing remembered a blow that left houses standing, and nothing
physical moved: Jolt was chosen as the physics engine (§63) but no body existed.

## Decision

### Structural integrity (the simulation)

- **Integrity:** each settlement keeps `Integrity` (0..1, 1 sound), the state of its houses as a
  whole. Villages are statistical (ADR-008), so the abstraction is the village's, not a house's.
- **Cracking:** every blow that destroys a share of a village's houses (`DisasterDamage.Destroy`,
  used by quakes, fires, floods, storms and every hazard) lowers integrity by `CrackPerShare` × the
  share, whether or not a house falls.
- **Weakness:** cracked houses fall more easily: the share is raised by `Weakness` × (1 −
  integrity). A village struck twice in quick succession loses more than twice as much.
- **Mending:** a lived-in village mends `RepairPerSecond` of integrity a second, so a village is
  sound again a few minutes after a blow.
- **Flood resistance:** houses stand up to floods by `HouseFloodResistance`, as they do to quakes
  and fires by `HouseQuakeResistance` and `HouseFireResistance`.
- **The numbers** (`Structure` and `HouseFloodResistance` in `disasters.json`):

  | Rule | Value |
  | --- | --- |
  | `CrackPerShare` | 2.0 |
  | `Weakness` | 1.0 |
  | `RepairPerSecond` | 0.002 |
  | `HouseFloodResistance` | 0.25 |

- **The hand** wrecks houses outright (ADR-030); a throw does not crack the rest.

### Debris (presentation only)

- **Where:** the client throws debris where houses fell and where a thrown thing or a falling star
  struck. A fallen house is found by comparing a village's layout before and after: the houses that
  stood and no longer do, as many as its ruins grew by (`DebrisLayout.Fallen`).
- **What:** a fallen house throws planks, stones and thatch; a strike throws stones, and a thrown
  rock or tree lands and rolls on along the line of the throw (`DebrisLayout.Burst` and `Thrown`).
  Each piece's size, start, velocity and spin come from `PresentationHash` of where it fell, so the
  same fall throws the same debris.
- **How:** each piece is a Jolt `RigidBody3D` box. Each burst lands on a `HeightMapShape3D` patch
  of the terrain under it, so the terrain renderer is not involved and both renderers behave
  alike.
- **Limits:**
  - pieces fade and are freed after `Lifetime` seconds, the oldest first beyond `MaxBodies`;
  - nothing is thrown beyond `Reach` of the camera;
  - pieces are never saved or shared, and change nothing in the simulation (ADR-006).
- **The numbers** (`Debris` in `settlement_view.json`):

  | Setting | Value |
  | --- | --- |
  | Lifetime | 9 s |
  | Fade | 2 s |
  | Most pieces | 240 |
  | Per house | 6 planks, 4 stones, 3 shards of thatch |
  | Per strike | 7 stones |
  | Reach | 700 m |

### Saves, sharing and tools

- **Saves:** move to version 33; older saves give every village sound houses.
- **Sharing:** the protocol moves to version 21, as settlements carry their integrity.
- **Tools:**
  - F1's dangers line shows the houses' integrity;
  - the performance lines count debris pieces and those still moving (Jolt does not fill Godot's
    physics monitors);
  - `--disaster=meteor` and the I key's falling star now strike villages as a natural one does
    (`HazardModel.FallingStar`), where before they only made the crater and the quake;
  - `--hand=throw` throws a rock or a tree at the focused village.

### Found on the way: local navigation's searches

Measuring this part showed a grown game at ×16 at 71 FPS, against 85–92 before. With the new
rules switched off in data the frame rate was back at 92, so the debris cost nothing; the world
differed. More villages were rebuilding, so their figures (ADR-046) searched more new ways.

- **The cost:** on seed 42, 89 of the 364 ways searched in one village had no way. Each such
  search explored the whole reachable grid of 43,681 cells: 1.5 million cells in all.
- **The fix:** the grid numbers its connected areas when baked, so a way between two areas is
  known to be missing at once, with the same answer. A* keeps its costs in arrays reused across
  the grid's searches, with a closed set.
- **The ways:** reachability is unchanged. Where two ways are equally short on the grid, the
  search may now break the tie differently; such ways differ in length by under 4% after pulling
  taut.
- **The result:** 534 ways in 101 ms, from 247–493 ms. The grown game runs at 113 FPS at ×16, and
  the stress world at 93 (86 before).

## Consequences

- **§94:** implemented. Structural integrity and earthquake, fire and flood resistance decide the
  damage in the simulation, and visual physics makes debris of it.
- **§55:** a throw now lands with weight. Lifting and carrying are still shown without physics.
- **§63:** Jolt is in use.
- **Balance:** 141 of 160 seeds won within the hour (148 in part 26, 141 in part 20), within the
  spread between parts.
- **Not modelled:**
  - integrity per house or per building kind;
  - debris that blocks the way or that villagers clear;
  - debris from the Creature's blows.
