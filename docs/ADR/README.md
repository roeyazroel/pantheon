# Architecture Decision Records

Each ADR records one significant, hard-to-reverse decision: its context, the decision, its
consequences and the alternatives considered.

ADRs are immutable once accepted; only their status line changes. Decisions never change
silently:

- To replace a decision, add a new ADR that supersedes it, and set the old ADR's status to
  "Superseded by ADR-NNN".
- To change part of a decision, add a new ADR that amends it, and set the old ADR's status to
  "Accepted; amended by ADR-NNN". The new ADR states what changes; the rest of the old ADR
  still applies.
- Update `docs/ARCHITECTURE.md`, the milestone plan in `.docs/` and any comments that cite the
  decision in the same change.

| ADR | Title | Status |
| --- | --- | --- |
| [ADR-001](ADR-001-godot-dotnet-pinned-version.md) | Godot 4.x .NET, pinned to 4.7.2-stable | Accepted |
| [ADR-002](ADR-002-csharp-primary-language.md) | C# as the primary language | Accepted |
| [ADR-003](ADR-003-host-authoritative-enet-networking.md) | Host-authoritative networking over ENet, no lockstep | Accepted (deferred) |
| [ADR-004](ADR-004-statistical-population-simulation-with-tiering.md) | Statistical population simulation with tiering | Accepted; amended by ADR-008 |
| [ADR-005](ADR-005-terrain3d-for-terrain.md) | Terrain3D for terrain rendering | Accepted; amended by ADR-007 |
| [ADR-006](ADR-006-simulation-state-separate-from-scenes.md) | Simulation state is separate from scenes | Accepted |
| [ADR-007](ADR-007-terrain3d-shader-patch-and-fallback-reason.md) | Patch Terrain3D's generated shader and report why the fallback is used | Accepted (amends ADR-005) |
| [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) | Settlement step cadence and representative citizens | Accepted (amends ADR-004) |
| [ADR-009](ADR-009-belief-influence-and-miracles.md) | Belief, influence and miracles | Accepted |
| [ADR-010](ADR-010-creature-simulation-and-learning.md) | The Creature: core simulation, learned utility AI and the hand | Accepted |
| [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md) | Rival gods: utility AI, conversion between gods and victory | Accepted; amended by ADR-015 |
| [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) | Diplomacy: directional relations, shared status and remembered acts | Accepted |
| [ADR-013](ADR-013-weather-cells-and-soil-water.md) | Weather: moving cells, spells and the water in the soil | Accepted |
| [ADR-014](ADR-014-disasters-as-processes.md) | Disasters as processes: wildfires in regions, earthquakes that shake, ruins to rebuild | Accepted |
| [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) | Four gods: coalitions, allies' memory and pantheon victory | Accepted (amends ADR-011) |

ADR-004 and ADR-008 were written before the owner's complete specification
([PANTHEON_SPEC.md](../PANTHEON_SPEC.md)) was added, and cite later milestones by the earlier
working roadmap: their Milestone 12 is that roadmap's Tiers at Scale, and ADR-008's Milestone 4
is Influence & the Hand. Their bodies are not edited, so
[GAME_SPEC.md](../GAME_SPEC.md#earlier-working-roadmap-superseded) keeps that roadmap and maps it
to the specification's milestones. Everything else, including ADR-003's status line, uses the
specification's numbers.
