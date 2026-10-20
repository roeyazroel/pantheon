# ADR-072: Gods and Creatures as agents, woken by what happens

- Status: Accepted
- Date: 2026-10-20
- Builds on:
  - [ADR-065](ADR-065-the-gods-minds-and-a-born-pantheon.md) (the gods' minds, and the contract every model keeps);
  - [ADR-066](ADR-066-talking-to-the-gods.md) (talk);
  - [ADR-036](ADR-036-known-gods-and-the-gods-long-term-plans.md) (the gods' plans);
  - [ADR-070](ADR-070-gods-answer-creatures.md) (the gods' watch over their lands).

## Context

ADR-065 gave the rival gods minds that write who they are, once a world, and ADR-066 let the player
talk with them. Everything else the gods and Creatures did was still the utility AI.

The owner asked to make each god an agent, woken by hooks in the world, and the same for the
Creatures. Asked how far an agent may go, the owner chose:

- **A god:** free within the rules, as a player is.
- **A Creature:** it decides at the moments that matter, and speaks.
- **Both** in one part.

What ADR-065 holds stays:

- the game is whole without a network;
- a model's answer is an input taken between ticks;
- nothing waits for a model;
- the key goes only to its provider.

## Decision

### Hooks

`HookReader` reads the world's events each frame. Each event that matters to a god or a Creature
becomes a hook for it, in words from its side.

**A god is woken by:**

| Hook | Example |
| --- | --- |
| Visitor | another god's Creature came into its lands, and it answered (ADR-070) |
| Answered | its own Creature was noticed in another's lands |
| Diplomacy | war, peace, an alliance, a proposal or a betrayal, as the actor or the other |
| Treaty | a treaty signed or ended |
| Village | a village came to follow it, or left it |
| Struck | a harmful miracle (naming the caster unless veiled) or a disaster on a village it leads |
| CreatureFight | its Creature's fight ended |
| Words | another god spoke to it |
| Talked | the player's talk with it ended |
| Met | it met a god |
| Reflect | every `ReflectSeconds` (240) of the world's time, to look about it |

**A Creature is woken by:**

| Hook | Example |
| --- | --- |
| Meeting | it met another Creature, other than passing by |
| Fight | its fight ended: won, lost, drew or fled |
| Judged | its god rewarded or punished it |
| Grew | a level or a calling |
| Need | it grew hungry or tired |
| Home | a village of its god near it was struck |
| Noticed | a god noticed it in its lands, or it was sent to answer a visitor |

Every AI god has a mind, and so does every Creature, the player's own included. The player's god
has none.

### A turn

`AgentDirector` keeps each mind's hooks.

**When a mind is asked:**

- after its hooks have gathered for `GatherSeconds` (2);
- not sooner than `GodTurnSeconds` (40) or `CreatureTurnSeconds` (30) of real time after its last
  turn;
- at most `MaxAsking` (2) questions at once;
- not while the world is paused.

Words from another AI god wait until something else wakes the god, so two gods cannot talk the hour
away.

**A god is told:**

- what woke it;
- itself and its Creature;
- each god it knows: status, feeling and memories in its own words (`MindWords`), share of the
  people, ties, and where its Creature is;
- the villages that concern it most: its own, those within its reach, the nearest;
- the holy sites;
- the proposals waiting for its answer;
- its aims;
- the miracles it can afford;
- what its instincts would do: the utility AI's best moves, diplomatic moves and goals;
- its journal;
- what came of its last acts.

**A god answers** (structured JSON) with:

- a reason;
- at most `MaxActions` (3) acts;
- at most `MaxMessages` (2) messages to gods it knows;
- its journal, rewritten, at most `JournalLength` (900) characters.

**A Creature is told:**

- what happened;
- how it is and where;
- the Creatures near it;
- what its god's rewards and punishments taught it to like and dislike;
- what it did last;
- its choices, which are its brain's options, the strongest instinct first.

**A Creature answers** with a line to say and a choice, or none.

### Free within the rules

A god's acts (`GodTools`) are everything a player may do with a god, through the same authorities:

- `cast` a miracle on a village;
- `send_creature`, `creature_fight` and `creature_work`;
- `offer_alliance`, `request_peace`, `declare_war`, `break_alliance`, `accept` and `decline`;
- `propose_treaty` of eleven kinds, on plain terms;
- `aim`.

The authority judges every act. What it refuses is not done, and the god is told why on its next
turn. Names are matched whatever their case, and with or without "the".

**Aims** (`GodAims`) are the planner's own goal kinds:

- the planner judges each one, and refuses a moot aim, such as converting a village the god leads;
- each aim the god keeps weighs at least `AimWeight` (0.6);
- the planner leaves them be for `AimHoldSeconds` (300) before it plans again.

The god's strategy and diplomacy favour them as they favour the planner's goals. A god's will
steers its instincts without taking their place.

**A Creature's choice** goes through its will (`CreatureWill`, a new authority):

- **The choices:** its brain's options, each once, with no errands, at most eight.
- **How it is done:** as the brain would do it, with any randomness drawn from the seed and the
  tick.
- **Never** against its god's errand, and never out of a fight or a hand.

### Instincts between turns

The utility AI goes on as the gods' and Creatures' instincts:

- everyday miracles, errands and diplomacy;
- answering proposals;
- teaching its Creature;
- the watch over its lands.

A turn adds to them: acts, words and aims.

### Words, memory and talk

- **Speech as events.** A god's words are a `GodSpoke` event, and a Creature's a `CreatureSpoke`.
  They travel to joined players with the other events.
- **The player sees them.**
  - A god's words to the player are an alert ("Belrays: “…” (P, Speak to answer)").
  - They are also a line of its conversation, and its model is told it said them when the player
    next speaks.
  - A Creature's words show above it for `SpeechSeconds` (6).
- **The journal.** A god's journal is `GodState.Journal`, saved (version 36). It is given to the god
  in every turn and in talk, so the god that acts and the god that talks are one mind.
- **Talk feeds the agent.** When a talk ends, the god is woken with what was said.

### Without a model, the scribes speak

- **When:** the agents are off, there is no model, an hour's `TurnsPerHour` (300) questions are
  spent, or the agents rest `RestSeconds` (120) after three failures in a row.
- **What they say:** lines from `minds.json`'s `Voices` for the moment, chosen from the seed, the
  speaker and the tick:
  - a god speaks only to a player's god, at most every `ScribeGodSeconds` (45) of the world's time;
  - a Creature speaks aloud, at most every `ScribeCreatureSeconds` (25).
- **Everything else is instinct.**

Every player hears the gods and Creatures speak, with or without a key.

### Settings, model and cost

The settings' Minds tab gains:

- **Think for themselves** (on): the agents ask the chosen provider.
- **Quickest model for their turns** (on): Claude Haiku 5.5, GPT-6 Luna or Gemini 3.5 Flash-Lite,
  or the same local model, through `MindFactory.OpenAgents`.
- **Questions an hour, at most** (300, from 10 to 2000).
- **Gods and Creatures speak** (on).

**Cost.** A god's turn is about 1,500 tokens asked and a few hundred answered, and a Creature's
about 600. The instructions are cached where the provider caches.

**The title screen.** No mind thinks or speaks while the title screen is open, since the island
behind it is only a backdrop.

### Determinism, saves and shared worlds

An agent's acts are commands between ticks, like a remote player's (ADR-018).

- **Reproducing a world.** A world played with agents is reproduced by its save, not its seed. A
  world played with the scribes alone stays as reproducible as before: their lines are chosen from
  the seed and change nothing in the simulation.
- **Saves.** Version 36 adds the journals.
- **Shared worlds.**
  - The host thinks for every god and Creature.
  - The protocol is version 22.
  - Joined players see the words meant for them.

### Tools

- **Logs:** the log's "Mind:" lines show each turn: what woke the mind, why, what came of it and
  what it said.
- **F1:** shows who thinks, the questions this hour, the tokens, and each mind's last turn and a
  god's journal.
- **`--agents=on|off`:** turns the agents on or off for a run.
- **`--wake=<god>`:** wakes an AI god, or every one with 0, at the start.

## Consequences

- **The gods and Creatures react to what happens, in their own words and acts.**
  - A god warns, threatens, thanks, makes and breaks pacts, and turns its plans.
  - A Creature speaks and chooses.
- **The utility AI stays.** It is the tested baseline, and the game is whole without a model.
- **Balance.** An agent god is held to the rules, not to a balance. With a model, a god may play
  better or worse than its instincts would, so win rates with agents are not the probes' numbers.
- **Cost is the player's,** bounded by the hourly limit. Turns are quick and small, on the quick
  models by default.
- **Delay.** A turn takes a second or more, more at ×16. Instincts cover the gap, and the agents
  shine at ×1 to ×4.

## Alternatives considered

- **The agent chooses among the utility AI's best few moves.** Safer for balance, but the owner
  chose a god free within the rules.
- **Tool calls with several steps** (look, then act). Richer, but slower and dearer, and local models
  follow one JSON schema more reliably than tool loops. One turn with up to three acts and the world
  in the question serves.
- **Asking every tick, or every few seconds.** It would cost far more, and the simulation would wait
  on the network. Hooks and reflections ask only when something matters.
- **Keeping conversations between AI gods.** It would cost turns for little the player sees. Their
  words wait for something that matters, and the journal is their memory.
