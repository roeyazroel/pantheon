# ADR-067: OpenAI and Gemini as the gods' minds

- Status: Accepted (amends ADR-065)
- Date: 2026-10-20
- Amends: [ADR-065](ADR-065-the-gods-minds-and-a-born-pantheon.md) (its three providers become five)

## Context

ADR-065 gave the rival gods' minds to the scribes, to Claude, or to a local model, with Anthropic
first, as the owner asked. The owner then asked for OpenAI and Gemini too. The contract
(`ILanguageModel`), the prompts, the strict reading of answers, the fallback to the scribes, and
the judgement of every act by the god's own AI (ADR-066) do not depend on the provider. What
remains is how each provider is asked, and how the player chooses it and gives a key.

## Decision

### Two more providers

`MindProvider` gains `OpenAI` and `Gemini`. `minds.json` lists the models offered, the default
first:

- **OpenAI** (`OpenAIModels`): GPT-6.1 Sol (`gpt-6.1-sol`, the default), GPT-6 Astra and GPT-6 Luna.
- **Gemini** (`GeminiModels`): Gemini 3.8 Flash (`gemini-3.8-flash`, the default, as Google
  recommends for new projects), Gemini 4 Argon and Gemini 3.5 Flash-Lite.

The models offered are content, so they can follow the providers' releases without code.

### How each is asked

Both adapters, in `Pantheon.Oracle`, post JSON with `HttpClient` (`JsonHttp`), as the local
model's does.

**OpenAI** (`OpenAILanguageModel`) uses the Responses API (`POST /v1/responses`), with the key as a
bearer token. The request:

- `instructions` holds the standing instructions;
- `input` holds the turns as `user` and `assistant` messages;
- `text.format` is the question's schema as `json_schema` with `strict: true` (the game's schemas
  already have every property required and no others);
- `reasoning.effort` is low, medium or high;
- `max_output_tokens` caps the answer;
- `store: false`, so nothing is kept on OpenAI's side.

Reading the answer:

- **the text:** the `output_text` of the `message` items, past any reasoning items;
- **refused:** a `refusal`, or `content_filter`;
- **cut short:** `incomplete_details.reason` of `max_output_tokens`.

**Gemini** (`GeminiLanguageModel`) uses `models/{model}:generateContent` in the Gemini API
(`v1beta`), with the key in `x-goog-api-key`. The request:

- `systemInstruction` holds the instructions;
- `contents` holds the turns as `user` and `model`;
- `generationConfig` sets `responseFormat.text` (`APPLICATION_JSON` with the schema), the
  `thinkingConfig.thinkingLevel` (low, medium or high), and `maxOutputTokens`.

Reading the answer:

- **the text:** the first candidate's parts, without its thought parts;
- **refused:** a prompt blocked by `promptFeedback.blockReason`, or a candidate stopped for safety,
  recitation, a blocklist, prohibited content or personal data;
- **cut short:** `MAX_TOKENS`.

Errors from both become words the player can act on:

- a key not accepted (Gemini answers 400 with `API_KEY_INVALID`);
- a model unknown;
- a rate limit or a spent quota;
- the provider in trouble;
- no connection, or a timeout.

### Keys

**Where each key comes from.** The profile keeps each provider's key under its own name, apart from
the settings: `anthropic_key`, `openai_key` and `gemini_key` (`Startup.Keys`, a `MindKeys`). When
one is empty, the environment's is used:

| Provider | Variable |
| --- | --- |
| Claude | `ANTHROPIC_API_KEY` |
| OpenAI | `OPENAI_API_KEY` |
| Gemini | `GEMINI_API_KEY`, then `GOOGLE_API_KEY` |

**`MindFactory.Open`** takes the keys and opens the chosen provider's model with its own key. One
provider's key is never another's. Without its key it opens nothing and says which key is
missing.

**The Minds tab** offers the five providers. Each online one has the same rows: its masked key
(with the variable named when the environment gives one), its model, and where to get a key.

### Choosing the models' versions

`GameSettings` gains `OpenAIModel` and `GeminiModel`, defaulting to the content's first. An unknown
id falls back to the default, as Claude's does. `--minds=openai|gemini` chooses them for a run.

## Consequences

- **More choice.** The player can bring whichever key they have. The pantheon's birth and talk work
  the same through every provider, with the same fallbacks and the same judgement of acts.
- **No new dependencies:** the game ships with no OpenAI or Google package. The two web APIs are
  pinned by tests against a stub server: each request's shape, every answer and stop, and the
  errors. A change on the providers' side shows as an answer the player is told about, and the
  scribes write instead.
- **Claude only:** the server-side refusal fallback. The others decline as refusals, and the
  scribes write instead.
- **Model ids date.** The lists in `minds.json` will need updating as providers retire models; a
  retired id is reported as unknown.

## Alternatives considered

- **The providers' official .NET SDKs** (OpenAI's `OpenAI` package, Google's `Google.GenAI`). They
  keep the request shapes current, but each brings its own dependency tree into every export, for
  two endpoints the game uses in one way. Anthropic's SDK stays, as ADR-065 decided.
- **One OpenAI-compatible adapter for all.** Google offers an OpenAI-compatible endpoint, but its
  structured output and thinking settings are best supported in its own API, and the Responses
  API is OpenAI's own way forward.
- **Gemini's Interactions API.** It is newer, and was in the middle of breaking changes in 2026;
  `generateContent` is stable.
