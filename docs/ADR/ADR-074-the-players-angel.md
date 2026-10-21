# ADR-074: The player's angel, a conscience and a guide shaped by the god's deeds

- Status: Accepted
- Date: 2026-10-21

## Context

The owner: "In the original Black & White there were two 'angels', one good and one evil. They were
like guides or consultants. I want to make something similar but only one angel that, based on my
actions, becomes more good or evil and suggests what to do next. We should make him like a guide
too. It should be based on an LLM also."

### The original

Black & White had two consciences:

- **The good one:** an old, sage-like man with a long beard, rainbow-coloured at its end, sitting on
  a cloud.
- **The evil one:** a short, red, overweight demon with an underbite, flying on his own wings.

One actor voiced both. They commented on nearly everything and argued, each trying to win the player
over. The player's alignment changed the land, the hand, the music and the temple.

### Our constraints

- **Originality.** The specification forbids reusing that game's characters, look or animations
  (§2).
- **No score.** It has no single good-or-evil score (§3.2): the world remembers what a god does.
  `GodConduct` keeps nine weights that build with a god's acts and fade, and its traits emerge from
  them (ADR-024).

### Sketches and the owner's choices

Sketches compared:

- three forms: a seraph, a lantern spirit and a masked herald;
- three places: beside the hand, a corner portrait and a whisper at the screen's edge.

The owner chose the recommendation:

- a seraph;
- beside the hand;
- one suggestion in its own voice (both ways when it is torn);
- calling it, and asking it anything;
- the tutorial spoken by it.

## Decision

### Its look comes from rules, and shows the world's memory

`Pantheon.Game.Angel.AngelLooks.Of(god, content)` reads the god's conduct, weighed by `angel.json`'s
`Weights`:

- **Kindness:** help, gifts and faithfulness.
- **Harm:** harm, cruelty (worse), vengeance, upheaval, deceit and aggression.
- **Lean:** kindness against harm, times how *formed* the angel is. It is half formed at `Softness`
  weight of deeds.
- **Conflict:** high when both kindness and harm are large.

It is a reading of the world, never a rule's score: nothing in the simulation reads it, and nothing
of it is saved.

| Stage | When | Look |
| --- | --- | --- |
| Radiant | lean ≥ 0.6 | white and gold; the halo whole; motes of light |
| Kind | lean ≥ 0.2 | white |
| Unformed | little done, or as little of either | pale silver |
| Torn | conflict ≥ `TornConflict` | split: the viewer's left wing and side dark, the right light; the halo broken |
| Stern | lean ≤ −0.2 | ash-grey; the halo breaking, thorns rising from it, ember edges |
| Fallen | lean ≤ −0.6 | black; ember-rimmed wings drooping; a burning crown of thorns; ember eyes; cracks of fire |

**Marks.** The god's traits mark the angel:

| Trait | Mark |
| --- | --- |
| Vengeful | a burning blade |
| Deceptive | a sheer veil |
| Cruel | chains at its wrists |
| Ambitious | a crown of thorns |
| Chaotic | cracks of fire in its skin |
| Generous | gold threads in its robe |
| Merciful | light in its open hands |
| Honest | clear, shining eyes |

### What moves it

`AngelHooks.Read` turns the world's events into moments for the player's god:

- **Its own deeds, kind or harsh:**
  - a miracle that helped or harmed;
  - people rescued or drowned by its hand;
  - a thrown thing that killed;
  - an offering;
  - war, peace, an alliance, or a broken alliance or treaty.
- **What happens to it:**
  - its villages hungry, thirsty, struck, won, lost or rising against it;
  - a rival's Creature in its lands;
  - war, betrayal, an offer, a meeting;
  - its Creature's fights;
  - a trait the world now sees in it;
  - a rank.

`AngelDirector` adds:

- its first greeting in a world;
- its god's call or question;
- a change of its own stage;
- its god's Creature growing hungry or tired;
- a long quiet, when it offers what to do next.

### What it may urge

`AngelChoices.Of` offers only what the god can do now:

- miracles a kind heart would cast (the Guardian profile weighs them through the god AI, with
  `GodAi.Evaluate(world, god, profile)`);
- miracles a harsh heart would cast (the Tyrant profile);
- for its moments:
  - help for a hungry or struck village, or a look at it;
  - the Creature set on a visitor, or sent to work where hunger is;
  - an answer to a god.

Each deed has an id, a short text ("Rain on Mossgate"), a kindness, and its reasons for the model.

### How it speaks

**With a model.** The angel asks the model the player chose, through the agents' quick model when
they prefer it, off the simulation's thread. The answer is taken between ticks.

- **The instructions say:**
  - who the angel is;
  - its god;
  - what its god's deeds have made it;
  - how it speaks at that stage (`Stages.*.Voice`);
  - the god's traits.
- **The turn tells:**
  - what moved it;
  - how things stand for its god (`GodView.Describe`);
  - what it said lately (`Remembered` lines);
  - the deeds offered.
- **It answers** with what it says (one to three sentences) and the ids of at most two deeds it urges.

**Without a model.** The scribes speak instead in these cases:

- there is no model;
- the player keeps the angel from the model;
- the hour's `TurnsPerHour` questions are spent;
- after three failures, for `RestSeconds`.

The scribes speak a line from `Lines` for the weightiest moment, in its voice: Light, Silver, Torn or
Dark. They urge the deeds its nature would:

- a light angel's kind deeds;
- a dark angel's harsh ones;
- a torn angel's one of each.

**Timing.** It answers a call at once (after `CallSeconds`), even paused. Otherwise it waits:

- for its moments to gather;
- for `WordsSeconds` after its last words, by how often the player lets it speak, or a third of that
  for urgent moments.

A quiet of `QuietSeconds` brings its counsel. It never acts: the god does what it chooses.

### Where it appears

The angel is a figure grown in code (`AngelMesh`):

- a robed body with open hands;
- two wings of three layers of feathers that beat about the shoulders;
- a halo with thorns hidden in it;
- the marks.

`angel.gdshader` colours each part by its lean, side and conflict, and shows only the god's marks. The
figure is drawn in a small world of its own (`AngelFigure`, a `SubViewport` with its own lights and
motes), so the island's hills never cut through it. Its look eases from one stage to the next.

`AngelCard` shows it beside the god's hand, over the ground the pointer is on, with a card:

- its name;
- its stage and what that says of the god;
- its words, told out letter by letter;
- the deeds it urges: each is **Show me**, which flies the view there and readies it (the miracle
  chosen, the Creature's work or fight armed, the gods' panel opened);
- **Thank you**;
- **Ask**.

It follows the hand loosely, holds still while the pointer is on it, and leaves when its words have
been read.

**Calling it.** The angel key (') and the first button of the god's bar call it.

**The tutorial.** During the tutorial it stands beside the tutorial's card, and the card is signed
with its name.

### Settings and tools

- **Settings** (Minds tab):
  - `Angel`: keep one;
  - `AngelThinks`: let it think with the model;
  - `AngelChatter`: rarely, sometimes or often.
- **Launch options:**
  - `--angel=on|off|<stage>`: a stage holds the look and calls it;
  - `--angel-ask=<words>`.
- **Debug:**
  - F1 lines: its name, look, lean, how formed and torn, the kindness and harm behind it, its marks,
    who speaks for it, its questions and tokens, the moments waiting, and its last words and deeds
    urged;
  - log lines beginning "Angel:".

No save or protocol change: the look comes from the world, and the angel's memory is the session's.

## Consequences

- **It reflects the god.** The player sees what their god is becoming, without a score: from the
  world's memory of its deeds, its traits marked on the angel.
- **A guide for every player.** It helps a new player all game with deeds they can do now, not only
  in the tutorial.
- **Cost.** With a model, each answer is a question to the player's provider, at most `TurnsPerHour`
  (60) an hour, by the quick model when chosen.
  - The figure's own picture takes about 0.1 ms of the CPU a frame while it shows, and nothing while
    it is away.
  - On the owner's Mac, frame times with and without it differed by less than their noise. Another
    game was running at the time.
- **Joined players** have their own angel, on their own computer, by their own model.

## Alternatives considered

- **Two advisors, as in the original.** Two characters arguing is the original's expression. One
  angel that changes with the god is new, and shows the god itself rather than two sides.
- **A score that the angel follows and the rules read.** The specification forbids a simple
  good-and-evil score (§3.2). The angel reads the conduct the world already remembers.
- **A corner portrait or a whisper.** Sketched and kept in reserve. Beside the hand keeps the angel
  near what the player is doing.
- **Drawing the angel in the island's world.** Hills and houses would cut through it as the camera
  moves, and its lighting would follow the weather. A picture of its own avoids both.
