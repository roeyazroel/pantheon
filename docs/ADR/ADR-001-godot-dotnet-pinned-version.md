# ADR-001: Godot 4.x .NET, pinned to 4.7.2-stable

- Status: Accepted
- Date: 2026-10-18

## Context

PANTHEON needs a free, offline-capable 3D engine that ships to Windows, macOS and Linux, supports
C# as a first-class language, and has a healthy terrain ecosystem. Engine upgrades can change
rendering, the C# bindings and GDExtension compatibility, so the version must be explicit.

## Decision

- Use the **Godot Engine .NET build, pinned to `4.7.2-stable`** (latest stable at decision time,
  released 2026-08-18).
- The Godot C# project uses `Sdk="Godot.NET.Sdk/4.7.2"` and targets `net8.0`, matching the
  `GodotSharp 4.7.2` package. The .NET 8 SDK (or newer) is required.
- The renderer defaults to Forward+. The Compatibility renderer remains usable for low-end
  hardware and for software-rendered CI/VMs (`--rendering-driver opengl3`).
- Upgrades are deliberate: a new ADR or an amendment to this one, a PR that bumps
  `Godot.NET.Sdk`, re-verifies Terrain3D compatibility (ADR-005) and passes the test suite.

## Consequences

- Positive: reproducible builds; every contributor and CI job uses the same engine and bindings.
- Positive: Godot's MIT licence and small footprint suit an offline, backend-free game.
- Negative: engine fixes from newer releases are not picked up automatically.
- Negative: the Godot editor rewrites `Godot.NET.Sdk` to its own version when a different editor
  opens the project, so contributors must use exactly 4.7.2 to avoid noisy diffs.

## Alternatives considered

- Unity / Unreal: licensing, editor weight and closed-source runtime conflict with an open,
  offline indie project.
- Tracking Godot `latest`: unpredictable breakage of GDExtensions such as Terrain3D.
