# ADR-012: Diplomacy: directional relations, shared status and remembered acts

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md)

## Context

Milestone 6 (spec §121) delivers relationships, alliance, war and betrayal memory, with the
playable outcome "Rival can cooperate or attack". The specification asks that:

- relations be multi-dimensional, so that a god may respect, hate and fear another and stay
  allied with it all at once (§37, §90: trust, fear, respect, rivalry, gratitude, suspicion,
  status, treaties, memory);
- the MVP have the statuses Neutral, Friendly, Alliance, Hostile and War, the actions offer
  alliance, break alliance, declare war and request peace, and an AI that remembers betrayal
  (§113);
- any treaty can be broken, at a cost to trust, reputation and future treaties (§40);
- gods remember major help (§41);
- AI betrayal be explainable, with player-facing clues (§49);
- the god AI's debug view show relationship reasoning (§106).

Acts that matter to diplomacy happen in many places: miracles (`MiracleCaster`), Creature deeds
(`CreatureDeeds`) and conversions (`BeliefSystem`), and they come from players and AI alike.

## Decision

- **Relations are directional.** `GodRelation` (observer, subject) holds trust, fear, respect,
  rivalry, gratitude and suspicion (0..1), and the acts the observer remembers of the subject
  (`DiplomaticMemory`: act, settlement, fading weight).
- **Status is shared.** `GodStance` (one per unordered pair) holds Alliance, Friendly, Neutral,
  Hostile or War, when it began, and when the pair last proposed something. Proposals
  (alliance, peace) wait in `DiplomacyState.Proposals` until answered or lapsed. Treaties beyond
  the MVP statuses (§38) wait for later milestones.
- **Acts are recorded where they happen.** One `DiplomacyLedger` (an `IActRecorder`) is shared by
  the miracle authority, the Creature deeds and the belief step. Each act updates the observer's
  view at once and is remembered:
  - a miracle on another god's followers is help if they were in real need (food or health need
    of at least 0.5, §41), courting otherwise, or harm if it struck them;
  - a Creature's attack on them is remembered as such;
  - winning a settlement's majority from another god is a conversion.

  The ledger is passed in, so the systems do not know about each other.
- **The diplomacy step** (`DiplomacySystem`, 1 Hz):
  - relations drift back to rest, and memories fade (betrayal about eight times slower);
  - fear follows the other god's share of the pair's followers, and respect follows its rank;
  - proposals lapse;
  - pairs neither allied nor at war turn Friendly when both sides feel warm and Hostile when
    either feels cold. Warmth combines trust, gratitude, rivalry, suspicion and grudge.
- **Diplomatic commands** (`DiplomaticCommand`: offer alliance, break alliance, declare war,
  request peace, accept, decline) go through `DiplomacyAuthority`, like miracles and Creature
  orders (§73):
  - each is checked against the gods, the issuer, the pair's status, any waiting proposal, a
    status cooldown and a proposal cooldown;
  - breaking an alliance, or declaring war on an ally, is a betrayal: the betrayed god's trust
    collapses and every other god trusts the betrayer less (§40).
- **What status does:**
  - allies' miracles plant no doubt in each other's followers, and an ally's influence does not
    raise costs;
  - AI gods do not court or strike their allies' followers, and their Creatures rarely attack an
    ally's villages;
  - at war, a people resists the enemy god's pull where its own god holds the majority, AI gods
    weigh enemy followers half again as much, and Creatures are drawn to attack enemy villages.
- **The AI diplomat** (`GodDiplomat`, part of each AI evaluation):
  - it answers every proposal waiting for it;
  - it weighs offering an alliance, declaring war, breaking an alliance or asking for peace from
    its view of the other god, the balance of their followings, war-weariness (which wears on
    a god that is not winning) and its personality's `Cooperation`, `Aggression`, `Treachery`
    and `Grudge` (`god_ai.json`);
  - it acts above a threshold, and every candidate keeps its factors as text.
- **Explanations** (§49): the diplomacy panel turns the rival's memories into sentences ("remembers
  that you betrayed it; converted Elar"), lists the player's own grievances, and the AI view
  shows the diplomatic candidates with their factors.
- **Saves** move to version 6. The migration from version 5 adds empty diplomacy: no relations,
  no stances (every pair neutral) and no proposals.

## Consequences

- Positive: personalities play differently. With seed 1234 and a player who keeps offering
  alliances:
  - the Tyrant declares war, makes peace when worn down, and declares war again;
  - the Opportunist refuses alliances while it is winning;
  - the Guardian accepts an alliance and keeps it.
- Positive: every act a god holds against another can be explained to the player.
- Positive: the diplomacy commands share the miracle path through an authority, so multiplayer
  will add transport only.
- Negative: relations are created lazily, on the first act, so "no view yet" is a state the
  interface must show.
- Negative: alliances do not win together yet (§53 Pantheon victory, §54 alliance endgame).
  With two gods, an alliance mostly stalls the contest. It matters more with four gods
  (Milestone 9).
- Negative: no secret treaties, favours owed, tribute or councils (§39, §41–42).

## Alternatives considered

- One symmetric relation per pair: it cannot express "A respects B, B despises A" (§37).
- Diplomacy reacting to the event stream: player commands raise events outside ticks and AI
  events arrive after the diplomacy step, so acts would be noticed late or missed.
- Status as a pure function of the relations: alliances and wars must be decisions made through
  the authority, not side effects of numbers.
