# ADR-015: Four gods: coalitions, allies' memory and pantheon victory

- Status: Accepted (amends ADR-011); its pantheon victory amended by ADR-020
- Date: 2026-10-19
- Amends: [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md) (a single winner) and builds on
  [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) (diplomacy)

## Context

Milestone 9 (spec §124) asks for one player, three AI gods, diplomacy between all gods and
several strategic personalities, with the playable outcome "Basic politics emerges". The
specification also asks for:

- personalities such as Tyrant, Guardian, Trickster, Isolationist, Warlord, Opportunist, Zealot
  and Nature Keeper (§47);
- alliances, betrayal and memory between every pair of gods (§37–41);
- a pantheon victory, where allied gods dominate together, and the pull of personal ascendancy
  inside an alliance (§53–54);
- a diplomacy panel that shows each god's relations (§57).

Before this milestone the world had two gods: the player and one Tyrant. ADR-012's relations,
stances and AI diplomat already work for any pair. With only two gods, though, politics reduced
to "war or peace with the one rival". There was no third party to fear, side with or betray.
`WorldState.WinnerGodId` held a single winner.

## Decision

- **Four gods.** `gods.json` founds the player (Ilyra) and three AI gods: Vael the Tyrant, Seren
  the Guardian and Kaio the Trickster. Each has its own Creature and its own home, spread out as
  ADR-011 describes. Nothing in the simulation assumes a number of gods; the HUD, the panel, the
  overlays and the alerts list every god.
- **Three more personalities**, each with a profile in `god_ai.json` (the eight of §47 are now all
  playable):
  - Trickster: converts others' people in hardship and doubt, cooperates readily and betrays
    whenever it pays (`Treachery` 1).
  - Isolationist: tends and blesses its own people, rarely courts others and avoids war.
  - Warlord: intimidates, sends its Creature, and goes to war readily.
- **Blocs.** A god's bloc is the god and every god allied with it, directly or through other allies
  (`DiplomacyModel.BlocOf`).
- **Pantheon victory** (`DivineRules.PantheonVictory`, on):
  - A god's standing (`VictorySystem.StandingOf`) is its own share of all followers or, if it is
    allied and the bloc holds more, the bloc's share with the allies named.
  - Dominance and victory follow standing. Every member of a dominant bloc dominates, and on
    victory the god and its allies all win. `WorldState.WinnerGodIds` lists them in id order, and
    `VictoryAchieved` and `DominanceChanged` carry the allies.
  - A god that holds the share alone wins alone.
  - Breaking the alliance ends the bloc's dominance.
  - With the flag off, every god stands alone, as before.
- **Allies remember.** When a god harms, converts, attacks, betrays or declares war on another god,
  each ally of the victim (other than the actor) loses trust in the actor and gains rivalry by
  half the victim's amounts. It also remembers a new hostile act, `WrongedAlly`.
- **The AI diplomat weighs the third gods.** These terms are added to ADR-012's utilities:
  - *Coalition*: a third god, allied with neither, threatens both gods once its share passes 30%,
    and fully at the victory share or when it dominates. The threat adds
    `Cooperation × 0.8 × threat` to the appeal of an alliance.
  - *Balance of power*: the same threat adds `Cooperation × threat` to peace and subtracts
    `0.8 × threat` from war, so two lesser gods stop fighting while a third runs away with the
    world.
  - *Against the leader*: war on a god that holds more followers than this one gains
    `Aggression × its lead` (its share past 30%, scaled to 1 at the victory share).
  - *Fresh peace and old help*: the goodwill of remembered friendly acts (peace, alliance, help)
    is subtracted from war and from betrayal. Wars no longer restart a minute after peace, and a
    new alliance is not broken at once.
  - *Endgame*: when the bloc is within 15 points of the victory share, betrayal gains
    `Treachery × 3 × (own share − ally's share)` and loses `(1 − Treachery) × 0.5`. A treacherous
    god that outweighs its ally is tempted to go alone; a loyal one stays to win together.
  - An ally that dominates *with* this god is no reason to betray it. Only an ally dominating alone
    adds ADR-012's dominance term.
- **Seeing it.**
  - The diplomacy panel shows each god's alliances and wars with the other gods.
  - Alerts announce alliances between other gods, a bloc's joint dominance (once per bloc) and a
    joint victory.
  - The HUD's contest line names the bloc ("Vael and Seren hold 76% of all belief together: they
    win together in 8:26").
  - The AI's reasons name the threat ("against Seren 0.67", "they lead 0.40").
- **Saves** move to version 9. `WinnerGodId` becomes `WinnerGodIds`: the one winner, or empty.

## Consequences

- Positive: politics emerges from the shared state. Headless runs of 40 simulated minutes (seeds
  1234, 42 and 7, with a passive player, a player who casts Food, and the three personalities
  moved to other gods) show:
  - wars declared on the leader;
  - peace between lesser gods;
  - alliances in six of nine runs;
  - three joint victories (Vael and Seren, Seren and Kaio twice);
  - one god winning alone;
  - five contests still open after 40 minutes.
- Positive: an alliance is now a road to victory, and both the AI and the player can take it.
- Positive: deterministic, and saved whole; the AI explains every diplomatic weight in the debug
  view.
- Negative: the Guardian is the strongest personality. Moved to another god's seat it still leads.
  Kindness converts better than intimidation under the belief rules of ADR-009, and the
  Guardian's `Convert` weight was lowered from 0.6 to 0.35 to keep it from winning every game.
- Negative: an ally with no followers left still wins with its bloc. That is allowed: the alliance
  is what wins.
- Negative: four gods crowd a small island, and a passive player is reduced to a few followers
  within ten minutes.

## Alternatives considered

- Victory only for single gods, with alliances as temporary help. That leaves out the spec's
  pantheon victory (§53).
- Shares pooled only between direct allies, not allies of allies. A chain of alliances is one
  political bloc, and counting it whole is simpler to explain.
- A scripted "dogpile the leader" rule. The coalition and balance terms are utilities weighed
  against trust, grudge and personality, so a Tyrant with a grudge may still fight a fellow
  underdog.
