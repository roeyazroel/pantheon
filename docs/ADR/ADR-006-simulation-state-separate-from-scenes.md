# ADR-006: Simulation state is separate from scenes

- Status: Accepted
- Date: 2026-10-18

## Context

Godot encourages storing game state on nodes in the SceneTree. For a long-running god-game
simulation with saves, headless tests and host-authoritative networking (ADR-003), state held
in nodes would be hard to serialize, replicate and test, and easy to corrupt from presentation
code.

## Decision

- The simulation's **`WorldState` is the single source of truth.** It is plain C# data defined
  in `Pantheon.Domain` (no Godot types) and mutated only by simulation systems
  (`Pantheon.Simulation`) and application commands (`Pantheon.Game`).
- **Godot nodes are presentation only.** They are created and updated by presentation adapters
  (`WorldPresenter`, `ITerrainPresenter`, `SettlementPresenter`, ...) that read `WorldState` and
  derived data. Nodes never own authoritative state.
- **Never serialize the SceneTree.** Saves serialize `WorldState` to versioned JSON
  (`SaveVersion`). Loading replaces `WorldState` and rebuilds all visuals from it.
- Derived data (the terrain heightfield) is regenerated deterministically from state (seed plus
  generation settings stored in `WorldState`) instead of being saved.
- Core assemblies (`Domain`, `Simulation`, `Persistence`, `Game`) do not reference Godot. They
  build and test headless with `dotnet test`.
- The simulation advances in fixed ticks (`SimulationScheduler`, 10 Hz × time scale), decoupled
  from the render frame rate.
- Tooling adjustment: Godot requires its C# project next to `project.godot` and node scripts
  under `res://`. The Godot glue therefore lives in `game/Scripts` (assembly `Pantheon`,
  namespace `Pantheon.Client`), while `src/Pantheon.Game` is the engine-agnostic application
  layer (session, scheduler, camera math, readouts).

## Consequences

- Positive: deterministic, headless-testable simulation. Trivial save/load and a clean path to
  network replication. The renderer can be swapped (Terrain3D or fallback) without touching state.
- Positive: presentation bugs cannot corrupt the simulation.
- Negative: there is an explicit mapping layer (state → nodes), and rebuilding visuals costs
  time on load. This is acceptable at M1 scale; incremental sync can come later if profiling
  shows a need.
- Negative: the Godot editor cannot be used to place gameplay entities directly. Content comes
  from data files and world generation instead.

## Alternatives considered

- Node-centric state with Godot scene serialization: idiomatic, but couples saves to scene
  layout and blocks headless testing.
- An ECS library inside Godot: more infrastructure than M1 needs. It can be revisited if
  agent counts demand it.
