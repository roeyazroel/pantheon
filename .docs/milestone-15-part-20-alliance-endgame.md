# Milestone 15, part 20 — The alliance endgame, lightning's fires and hurricane warnings

## Overview

Part 20 closes small gaps the progress summary still listed:

- AI gods take the last choice of §54's alliance endgame, absorbing an ally's believers
  ([ADR-040](../docs/ADR/ADR-040-ai-gods-absorb-their-allies.md)).
- A god's lightning sets dry woods alight, as §15's example has it.
- Hurricanes are announced as they form, as §59's example has it
  ([ADR-041](../docs/ADR/ADR-041-lightning-fires-and-hurricane-warnings.md)).
- The summary's stale rows are brought up to date.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Domain` | • `DisasterRules.LightningMiracleIgnition`<br>• `Events/HurricaneApproaching` |
| `Pantheon.Simulation` | • `GodDiplomat` weighs `Absorb` beside `BreakAlliance`: the betrayal's appeal plus treachery × 3 × the share of all believers it would take, from 0.5%<br>• `MiracleEffects.Strike` lights the struck region by its flammability<br>• `WeatherSystem` announces a hurricane as it forms |
| `Pantheon.Game` | • `WeatherText.Coast`<br>• The hurricane alert |
| Content | `disasters.json`: `LightningMiracleIgnition` 1.0 |
| Docs | `PROGRESS_SUMMARY.md`: the rows for §15, §30, §33–36, §51–52, §54, §59, §75, §79, §89, §90, §91, §107, interaction and advanced politics brought up to date |

## Test plan

- **The AI's choice (`AbsorbAiTests`):**
  - a treacherous ally that leads where its ally is followed would rather absorb than merely
    break;
  - with none of the ally's believers where it leads, it does not weigh absorbing;
  - a Trickster sick of its ally absorbs it, taking its believers and ending the alliance.
- **Lightning (`LightningFireTests`):** a bolt sets dry growth alight but not wet land.
- **Hurricanes (`HurricaneWarningTests`):**
  - a hurricane is announced as it forms;
  - the warning names the coast it comes towards.

### Verification results

- **Build and tests:** `dotnet build` has no warnings, and 1397 tests pass (1391 before).
- **Rates:** over 40 seeds (30–69), AI gods absorbed an ally twice (5 and 8 people) and broke with
  one 7 times.
- **Balance:** over 160 seeds (30–189), 141 games are won within the hour (part 19: 138; 139 with
  absorbing alone, before lightning lit fires). Over seeds 30–69, 37 are won at a median of 26.3
  minutes.
- **Found on the way:**
  - **Absorbing nobody:** absorbing is a betrayal plus a gain, so at first every betrayal became an
    absorption, nine in 40 games, some taking no one at all. Absorbing now needs 0.5% of all
    believers.
  - **Stale rows in the summary:** most of what §33–36, §75, §79 and §90 asked for was already
    built:
    - §90's wildlife, pollution and treaties;
    - §90's world events with a duration (great events);
    - the save's ecology, world events, technology and sacred locations;
    - the systems of ecology, technology, history and world events.

    Their rows now say so.

## Tasks

### Completed

- [x] AI gods absorb their allies near the endgame.
- [x] Lightning's fires and hurricane warnings.
- [x] The progress summary's stale rows.

### Future

- A god's choice between absorbing and sharing a victory told in the history.
