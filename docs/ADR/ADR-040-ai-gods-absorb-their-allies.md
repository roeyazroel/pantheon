# ADR-040: AI gods absorb their allies

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) (allies winning
  together) and [ADR-025](ADR-025-treaties-secrets-favours-and-the-divine-council.md) (absorbing an
  ally)

## Context

§54 lists what a god near a joint victory with its ally can do:

- remain loyal and share the victory;
- betray its ally;
- absorb the ally's believers;
- seek personal ascendancy.

ADR-015 gave AI gods the choice between loyalty and breaking away, weighed by their treachery and
their lead over their ally. ADR-025 added absorbing an ally: the god breaks the alliance, a
betrayal like any other, and a share of the ally's followers (`AbsorbShare`, 40%) turn to it
wherever it is followed more. Only players could absorb; AI gods broke away instead.

## Decision

- **Weighing it:** when `GodDiplomat` weighs breaking with an ally, it also weighs absorbing it.
  - The appeal is the betrayal's own, plus the god's treachery × 3 × the share of all believers
    absorbing would take.
  - The take is counted as `PoliticsActions.Absorb` would count it.
- **When it is worth it:** absorbing has every consequence of a betrayal and more, so a god that
  would betray its ally absorbs it instead whenever that gains something. The gain must reach 0.5%
  of all believers; below that the god merely breaks away, so no history records an absorption of
  nobody.
- **Seeing it:** F1 lists the move with its reason ("would take 1.2% of all believers"). The
  diplomacy panel's intelligence line already warns a well-informed player that an ally is weighing
  betrayal or absorption.

## Consequences

- **§54:** every choice it names is open to AI gods.
- **Rates:** over 40 seeds (30–69), AI gods absorbed an ally twice (taking 5 and 8 people) and
  broke with one 7 times.
- **Balance:** unchanged within the noise. Over 160 seeds (30–189), 139 games are won within the
  hour; part 19 won 138.
