# ADR-021: Polish and expansion: domains, holy sites, schisms, Creature species, settings and synthesized audio

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-009](ADR-009-belief-influence-and-miracles.md) (belief, influence and miracles),
  [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md) (the gods' utility AI),
  [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) and
  [ADR-019](ADR-019-replication-by-section-and-rate.md) (shared worlds), and
  [ADR-020](ADR-020-vertical-slice-history-flow-and-eligible-pantheons.md) (history and the game's flow)

## Context

Milestone 15 (spec §130) is Polish and Expansion. Its list is:

- domains, doctrines and holy sites;
- advanced politics and schisms;
- larger worlds, more miracles and more Creatures;
- Steam networking (optional).

The specification describes these in places:

- **Domains (§12):** a god specializes in two or three domains. They should open distinct
  strategies, "rather than +5%" (its example: Storm can redirect storms).
- **Schisms (§34):** "The Old Faith" splits off when a god's behaviour contradicts a
  civilization's history.
- **Sacred locations (§51):** a World Tree, a sacred mountain, ruins, a waterfall, an ancient
  forest. They give unique benefits and become political flashpoints.
- **Audio (§60):** dynamic music, miracle sounds suited to where they happen, and distant thunder
  or a Creature's roar heard from afar.
- **Settings (§105):** graphics scaling.
- **The AI (§47):** "preferred domains" were still missing from its personalities.
- **The game is offline (§65, §70):** it downloads nothing and ships no recordings.

## Decision

### Domains

- `domains.json` offers eleven of §12's domains: Nature, Life, Storm, Fire, War, Death,
  Prosperity, Protection, Knowledge, Trickery and Chaos. Ocean and Justice wait, the one for
  water bodies and the other for laws.
- A god takes up one domain as a Minor Spirit, a second as a Lesser God and a third as a God. A
  domain is held for good. `GodState.Domains` is saved.
- Taking one up is a command checked by a `DomainAuthority`: it travels as an intent in a shared
  world (`DomainIntent`, protocol version 2) and is history ("Year 3 — Ilyra takes up the Storm").
- Each domain does three things:
  - **Cheaper miracles:** its miracles cost 0.8 of their price.
  - **A signature miracle:** `RequiresDomain` in `miracles.json`, castable only by a god that
    holds the domain. There are eleven: Regrowth, Gift of Life, Redirect Storm, Firestorm,
    Spear of Heaven, Dread, Abundance, Sanctuary, Enlighten, Sow Discord and Upheaval. Eight
    new effect kinds carry them: Regrow, Populate, Redirect, Wound, Terrify, Sanctuary, Teach
    and Discord.
  - **A boon:** one rule of the world changes for the god, or for the settlements where it holds
    the majority. `DomainSystem` applies Nature's, Life's, Storm's, Fire's and Prosperity's after
    each belief step; War's, Death's, Protection's, Knowledge's, Trickery's and Chaos's live in
    the act they change (combat, miracles, disasters, learning).
- Some signature miracles act on something in particular: a cloud, a rival Creature, the god's
  own Creature. They are refused (`NothingThere`) when it is not there. Sow Discord is unseen:
  nobody witnesses it and no god notes it.
- AI gods take up their personality's preferred domains in order (`god_ai.json` `Domains`), and
  value the new effects in `MiracleValuer`, which moved out of `GodAi`.

### Holy sites

- One site of each kind in `holy_sites.json`: the Sacred Peak, the Ancient Grove, the Holy Spring
  and the Standing Stones.
- `HolySitePlacer` finds them in the land when a world is made, from its regions and settlements
  only. It finds them again for a world from an older save, and it relaxes its spacing where the
  land is cramped.
- `HolySiteSystem` steps once a second. The god whose influence at a site is strongest, at least
  `ClaimInfluence` and `ClaimLead` ahead of every other, takes the site after `ClaimSeconds` without
  a break. Its Creature's presence counts.
- A keeper gains:
  - Divine Power each step;
  - the site's own influence (`HolySiteInfluenceStrength` and `Reach`);
  - its boon: cheaper miracles everywhere, blessed land, healing for its villages near the site,
    or experience for its Creature near the site.
- The god that loses a site remembers it (`TookHolySite`), and the player is warned when a site is
  being taken. AI gods send their Creatures to claim sites and to hold their own.
- Sites are saved (`WorldState.HolySites`) and replicated as a section.

### Schisms

- `Schisms`, part of the belief step, splits off a share of the followers of a settlement's
  main god when they doubt it past `Schism.Doubt`. They keep its Old Faith
  (`SettlementState.OldFaith`):
  - they worship no god;
  - no god can convert them;
  - they return while the god is loved and trusted there again, and otherwise fade into unbelief.
- A god that harms people who love it plants extra doubt (`BetrayalDoubt`): §34's behaviour
  contradicting history. So a loving god turned cruel provokes schisms, and a god that was always
  feared does not.
- Sanctuary keeps a god's followers from falling away or splitting for a minute.
- Schisms and reunions are history and alerts.

### Creature species

- `creatures.json` lists species, each with factors on toughness, strength, speed and learning,
  and a temperament at birth: the Mossback, the Stonehide, the Swiftclaw and the Lumen.
  `CreatureState.Species` (saved since version 4) names it.
- `gods.json` gives each god's Creature its species. The player may choose another for a new
  world, on the start screen or with `--species`.
- `divine_view.json` gives each species its look: colours, girth, height, horns and glow.

### Settings

- `GameSettings` is kept in `user://settings.json`: fullscreen, vertical sync, shadows, render
  scale (FSR upscaling below 1), citizens shown, and the loudness of everything, the music, the
  effects and the wind and rain.
- Unlike content, the file is the player's, so a broken file gives the defaults and values are
  brought into range.
- The menus have a settings page that applies changes at once.

### Audio

- Every sound is synthesized from code by `SoundBank`: bells, shimmer, the crack of lightning,
  distant thunder, the earthquake's rumble, fire, a Creature's roar, alerts, fanfares, and the
  wind and rain loops. `MusicComposer` makes music loops for three moods: Calm, Tension and
  Triumph.
- `AudioDirector` decides what the world sounds like:
  - which events make which sound and where, played positionally so a storm or a fight across
    the island is heard faintly;
  - the music's mood: Triumph while the player dominates, Tension in war, near a rival's victory
    or while the ground shakes;
  - the wind and rain where the camera looks.
- The client makes the streams on a worker thread at start (about half a second) and plays them on
  buses the settings control.

### Steam networking (§69)

- Not built. It needs the Steamworks SDK and a Steam app id. The specification calls it optional
  and the game is offline-first.
- `INetworkTransport` (ADR-018) is the seam where a Steam transport would sit beside ENet; ENet
  stays the only transport.

### Larger worlds

- Not offered yet. `worldgen.json` already sizes the island, but Terrain3D's regions, the
  presenters' budgets and the AI's evaluation have only been measured at 2 km.

### Saves

- Version 13 adds gods' domains, holy sites, and settlements' Old Faith and sanctuary.
- Older saves migrate with no domains, no Old Faiths, no sanctuaries and an empty list of sites.
  The session then finds the sites as a new world would.

## Consequences

- Positive: gods play differently by what they are gods of:
  - a Storm god moves clouds onto parched followers or over rivals, and is worshipped in storms;
  - a Death god earns power from the lives it takes;
  - a Trickster turns its rivals against each other unseen;
  - a Life god grows its villages.
- Positive: holy sites are places to fight over without miracles. Creatures walk to them, keepers
  change, and the history tells of it. About six claims are made in an hour.
- Positive: schisms arise from the rules, not from scripts:
  - from famine and doubt, and from a loved god's cruelty;
  - 18 an hour on average, nearly half of which return to their god.
- Positive: the game has sound and settings without shipping a single asset.
- Negative: domains made the strongest personality stronger at first; the measures behind this
  are in the milestone plan.
  - Before tuning, Seren won all ten probe games in 22 minutes, with Sanctuary and Gift of Life
    as the main causes.
  - Tuning restored Milestone 14's pace: 8 of 10 games had a winner, in 32 minutes on average.
  - The balance with an active player depends on the domains chosen.
- Negative: a tick costs 0.106 ms against 0.076 ms in Milestone 14. AI gods weigh twice as many
  miracles, so an AI evaluation tick takes 2.8 ms.
- Negative: synthesized sound is plain. Recorded or composed audio can replace `SoundBank` later
  without changing what the director decides.

## Alternatives considered

- **Domains as percentages:** §12 asks for distinct strategies, so each domain has a signature
  miracle and a boon that changes a rule.
- **Holy sites as settlements' buildings:** §51's places are in nature and apart from villages,
  and their keepers change with influence. A building would follow its village.
- **Schisms as new gods:** a heresy with its own god needs new AI and diplomacy. The Old Faith is
  a people apart, within the belief model, that can return.
- **Recorded audio files:** they are assets to make, license and ship. Synthesis keeps the game
  data-free and deterministic.
- **Tolerant content parsing for settings:** content stays strict. Settings are the player's file
  and must never stop the game.
