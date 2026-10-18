# ADR-002: C# as the primary language

- Status: Accepted
- Date: 2026-10-18

## Context

PANTHEON's core is a large, long-lived simulation (belief, population, ecology, AI gods,
learning creatures). It must be testable without the engine, refactor-friendly and fast enough
to simulate thousands of entities statistically.

## Decision

- **C# (.NET 8)** is the primary language for all gameplay, simulation, persistence and tooling
  code.
- Core logic lives in plain .NET class libraries under `src/` with no Godot dependency
  (see ADR-006). Godot-specific code lives in the Godot C# project under `game/Scripts`.
- GDScript is not used for game code. It may only appear inside vendored third-party addons.
- Shared compiler settings live in `Directory.Build.props` (nullable reference types enabled,
  warnings as errors, XML documentation required for production libraries).

## Consequences

- Positive: one language across engine glue and simulation; strong typing and mature tooling
  (xUnit, analyzers, debuggers, profilers).
- Positive: the simulation can be unit-tested and benchmarked headless with `dotnet test`.
- Negative: Godot's C# support has rough edges (no web export for .NET projects, editor reload
  quirks). Web is not a target platform, so this is acceptable.
- Negative: GDExtension classes such as Terrain3D have no generated C# wrappers and must be
  driven through `ClassDB`/`GodotObject.Call`. That code is confined to adapters.

## Alternatives considered

- GDScript-first: fast iteration, but weak typing and poor headless testability for a large simulation.
- C++ GDExtension for the core: maximum speed, much higher development cost. It can be added
  later for proven hot spots.
