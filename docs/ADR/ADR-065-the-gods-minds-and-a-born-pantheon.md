# ADR-065: The gods' minds — optional language models and a pantheon born for each world

- Status: Accepted; amended by ADR-067
- Date: 2026-10-20
- Builds on: [ADR-011](ADR-011-rival-gods-utility-ai-and-victory.md) (the rival gods' utility AI),
  [ADR-036](ADR-036-known-gods-and-the-gods-long-term-plans.md) (the gods' plans),
  [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) (a host's world and players' intents)
  and [ADR-060](ADR-060-main-menu-and-local-sqlite-profile.md) (the menus and the profile)

## Context

The owner asked to give the rival gods real minds through Anthropic, OpenAI or Gemini models. Each
god would have a unique personality and history, generated for every new game, and the player
could talk to them. Answering the follow-up questions, the owner decided:

- Local models, and a fully offline game, are supported alongside the online providers.
- Talking to the gods is how the game flows: it changes treaties and wars, not only flavour.
- The player brings their own key, entered in the settings.
- Anthropic comes first.
- AI-suggested replies spare the player from typing, but free text stays possible.

The specification's §65 ("Local-first architecture") requires single-player to run with "no
external API". §47 to §49 and §87 describe the god AI as proprietary utility AI. The
specification is the owner's, and this is the owner's direction. This ADR records how the game
honours both: it stays whole without a network, and a model, when the player chooses one, can only
act through the rules the utility AI already keeps.

The work comes in parts:

- **This part (Milestone 15, part 44):** the foundation, and a pantheon born for each world.
- **Next parts:** talking to the gods, with suggested replies; the gods' strategic counsel from the
  model; and the Creatures' voices.

## Decision

### Three providers, one contract

`Pantheon.Game.Minds.ILanguageModel` answers a `ModelQuestion` with a `ModelAnswer`:

- **The question:** standing instructions, the conversation, a JSON schema the answer must follow,
  an effort and a length.
- **The answer:** JSON that follows the schema, or why there is none — `Refused`, `CutShort` or
  `Failed`, in words the player can act on.
- An adapter never throws for a model's or a network's failure.

The player chooses the provider in Settings, on a new Minds tab (`GameSettings.Minds`):

- **The scribes (the default, offline).** `PersonaScribe` writes the gods from the world's seed and
  the lore in `game/Data/minds.json`. No model, no network; the same seed gives the same gods.
- **Claude.** `Pantheon.Oracle.AnthropicLanguageModel` uses Anthropic's official .NET SDK (the
  `Anthropic` NuGet package).
  - The model is chosen from `minds.json`: Claude Opus 5.5 by default, Sonnet 5.5, Haiku 5.5 or
    Fable 5.1.
  - Every question asks for structured output (`output_config.format` with the question's schema)
    at the question's effort. The instructions are cached with `cache_control`.
  - Thinking is left at the model's default, and no sampling parameters are sent.
  - The server-side fallback beta (`server-side-fallback-2026-07-01`, `fallbacks: "default"`) lets a
    question a model declines for policy reasons pass to Anthropic's default fallback model.
  - The SDK's errors become answers by kind: a key refused, a model unknown, a rate limit, Anthropic
    in trouble, the network down, or a timeout.
- **A model on this computer.** `Pantheon.Oracle.LocalLanguageModel` talks to Ollama's own chat
  API (`/api/chat` with `format` set to the schema, `stream: false`). It also lists the models the
  server has (`/api/tags`), for the settings. Nothing leaves the computer.

### The key

The player's Anthropic key is kept in the profile (ADR-060) under its own key, `anthropic_key`,
apart from the settings, so it is never written with them.

- When it is empty, the `ANTHROPIC_API_KEY` environment variable is used.
- It is sent only to Anthropic, by the SDK.
- It is never logged, saved in a world or sent to other players.
- The field in Settings is masked, and the tab says where the key is kept and that questions are
  billed to the player's Anthropic account.

There is no operating-system keychain behind it: one that works on macOS, Windows and Linux would
need code for each.

### `Pantheon.Oracle`

The adapters live in a new project, `src/Pantheon.Oracle`:

- It references `Pantheon.Game`, for the contract and the settings, and the `Anthropic` package.
- The Godot client and the tests reference it; the core projects do not.
- `ArchitectureRulesTests` checks its references and scans it for the clock and unseeded
  randomness, like the core.

### A pantheon born for each world

When a world is made from the menu (a new world, or hosting one):

1. `Births` (client) opens the model the settings choose. `--minds` overrides the choice for a run.
2. **No model chosen, or Claude without a key:** the scribes write the gods at once.
3. **A model chosen:** it is asked off the main thread. Meanwhile the title screen shows "The gods
   are waking" with "Let the scribes write them", and the island lives on behind it.
4. **The answer is read strictly** (`PantheonPrompt.Read`). Each rival needs:
   - its slot, a name and its Creature's name: letters, at most 18 characters, not taken;
   - an epithet, a history of at most 700 characters, a voice, a desire and a dread;
   - one of the eight personalities;
   - a temper of whole numbers from -3 to 3.

   A god missing any of these, or written badly, is the scribes' instead, and the player is told
   how many. A failed, refused, cut-short, cancelled or timed-out question (180 s,
   `minds.json`'s `TimeoutSeconds`) leaves all of them to the scribes.
5. The world is made from the gods born (`NewWorldOptions.Born`):
   - each rival's slot takes its name, Creature's name and personality, so the founder and the god
     AI need no change;
   - each god's `Persona` is set from its slot, as the founder makes gods in slot order from id 1.
6. A page shows the gods of the world before the player enters it: each with its epithet,
   personality and Creature, its history and its nature.

Worlds made without asking (the boot world, F2 and the tutorial) get the scribes' gods. A world
made in code without `Born` keeps the content's gods, so the simulation's tests are unchanged.

**What the scribes write.** Each rival draws, from the seed:

- a personality of its own: the eight, shuffled;
- a name of three syllables from the lore, and a Creature's name, neither taken;
- an epithet, a voice, a desire and a dread from its personality's lore;
- a history of three sentences: an origin, a deed and a turn, with `{name}`, `{creature}` and
  `{animal}` filled in;
- a temper in which every leaning strays at most 0.5, and one leaning, the god's defining one, is
  ±0.8.

### Personas and tempers

`GodState.Persona` (save version 35; older saves load with none) holds the epithet, history,
voice, desire, dread, temper and author.

`GodTemper` holds seven leanings from -1 to 1:

- aggression, cooperation, treachery and grudge;
- protect, convert and intimidate.

**How a temper changes play.** `GodAi.ProfileOf(GodState)` is the god's own profile: its
personality's profile, with each leaning times `god_ai.json`'s `TemperReach` (0.15) added to the
matching weight, kept from 0 to 1.

- Every place that read a god's profile by its personality now reads the god's own: the AI's turn,
  its advisors, the treaty advisor's judgement of what another god would accept, and the previews.
- A god with no persona, or an even temper, has its personality's profile exactly. Old saves and
  test worlds therefore play as before.

**Where the persona shows:**

- the god's title in the Book and in diplomacy: "Vael the Ash-Crowned";
- its history and nature: "A Tyrant, quicker to war than most. Speaks cold and imperious. Wants…";
- the F1 god AI lines: who wrote the mind, and the temper in numbers.

### Determinism, saves and shared worlds

A model's answer is an input to the world, like a remote player's intent (ADR-018). It enters only
in between ticks, on the main thread, and is kept in the world.

- **Reproducing a world:**
  - a world whose gods the scribes wrote is reproduced by its seed;
  - a world whose gods a model wrote is reproduced by its save.
- **Shared worlds:** the host bears the gods, and players receive them with the world, since
  `GodsPart` replicates the gods whole.
- The simulation never waits for a model.

### Tools to see and drive it

- **Settings' Minds tab:**
  - the provider;
  - Claude's key and model;
  - the local model's address and name, with Find for the models the server has;
  - Test, which asks the chosen model for a greeting (`MindCheck`) and shows the answer or why
    there is none.
- **Launch options:**
  - `--minds=scribes|claude|local` chooses the minds for a run without changing the settings;
  - `--pantheon=on` opens the page of the world's gods at the start.
- **F1:** each AI god's mind and temper.

## Consequences

- **The game still needs no network.** The scribes are the default, and every failure falls back
  to them. With a model chosen, a new world waits for it, and the player may skip the wait.
- **The rivals are new in every world.**
  - Personalities are drawn for each world, and every god leans its own way, so a Tyrant may be
    slow to war and a Guardian quick to hold a grudge.
  - Worlds made from the menu no longer have the default Vael, Seren and Kaio, and their balance
    varies with the personalities drawn.
- **A new package.** The `Anthropic` package brings `System.Text.Json` 10, `Microsoft.Extensions.AI.
  Abstractions` and `System.Net.ServerSentEvents` into the client.
  - They are managed code, with no native libraries, so every platform's export carries them.
  - The core's own serialisation is unchanged, and its tests run against the same versions.
- **Cost.** Questions to Claude cost the player money, on their own account. The settings say so;
  the game asks Claude once for each world made, and nothing more in this part.

## Alternatives considered

- **Raw HTTP instead of the SDK.** Less to depend on. But the SDK keeps the request shapes, the
  retries, the typed errors and the betas current, and Anthropic recommends it.
- **Letting a model play the gods directly**, choosing their miracles and moves. That would break
  determinism, saves and fairness, and the game would be unplayable offline. The model writes who
  the gods are; the utility AI still decides what they do. Later parts let the model's words reach
  the game only through the authorities that validate a player's commands.
- **An OpenAI-compatible endpoint for every provider.** One adapter, but each provider's own API is
  where its structured output, caching and errors are best supported. Anthropic is native, and
  Ollama's own API covers local models. OpenAI and Gemini can join behind the same contract.
- **Keeping the key in the settings' JSON.** Simpler, but the settings may be shared or logged.
  The key is kept apart.
