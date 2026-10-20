# ADR-071: An orchestra made in code, and a piece for every mood

- Status: Accepted (amends ADR-021)
- Date: 2026-10-20
- Amends: [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (its music in
  three moods)

## Context

The owner, playing: "We need more dramatic music. The music should be more orchestra."

ADR-021's music was three 24-second loops (calm, tension and triumph). Each was four chords of sine
pads with bell notes and an echo, at 22 kHz. Spec §60 asks the music to follow morality, region,
warfare, Creature state, disaster, Divine Power and political tension. Spec §2 asks for original
sounds, and the game ships no assets it did not make (ADR-021): the sounds are synthesized.

## Decision

### An orchestra synthesized in code

`Pantheon.Game.Audio.Orchestra` plays the music when the game starts, with no samples or recordings.

**The instruments:**

| Instrument | How it is made |
| --- | --- |
| Strings, tremolo strings, low strings, spiccato | Sections of five, four or three band-limited sawtooths (PolyBLEP), detuned within 13 cents and spread across the stage. A vibrato comes in after the attack, and two state-variable low-passes open with velocity. The tremolo shivers at 11.5 Hz, and the spiccato is struck short. |
| Horns, brass | Detuned sawtooths that scoop up to pitch, under a filter that opens as they are blown. The horns are round, and the brass opens 12 times its pitch. |
| Choir | Detuned sawtooths sung through the three formants of "ah", or of "oo" below G3, with breath. |
| Flute, oboe | The flute: a near-sine with breath and a chiff. The oboe: a narrow pulse through a nasal band. |
| Harp, celesta | The harp: six partials of a plucked string, the higher dying sooner. The celesta: bell partials. |
| Timpani, taiko | Tuned skins, their pitch settling after the blow and brighter when struck harder, and rolls. |
| Cymbal | Noise and inharmonic metal, crashed, or swelled into a downbeat. |

**The hall.** Every instrument sends a share to a hall after Jezar's Freeverb: eight damped combs and
four all-passes a side, the right side's delays a little longer for width, after a 24 ms pre-delay.

**Mastering:**

- a high-pass takes out the rumble;
- a low shelf about 150 Hz thins the low end;
- the presence about 3 kHz and the air above 6 kHz are lifted;
- the piece is set to a loudness by its level;
- a soft knee rounds its peaks below 1.

**Loops.** Every piece loops seamlessly: notes and the hall's tail run on into the start, the hall and
the filters running over the loop twice. The same score always gives the same samples.

The sound is 32 kHz stereo. Each piece takes 1.6–5 seconds to play, and the seven take about 5
seconds together on the worker threads at start. Each plays as soon as it is ready, the Theme first.
`Pantheon.Game` is now built optimised in every configuration, because a debug build, as the game
runs from its project, took four times as long.

### A score for every mood, as content

`game/Data/music.json` holds seven original pieces, all centred on D so one mood crosses into
another without a jolt. Each loops, its last bar leading into its first.

| Mood | Piece | When (spec §60) |
| --- | --- | --- |
| Theme | The Pantheon (76 bpm, 24 bars): a horn melody over harp and strings, then the full orchestra with choir and drums | the title screen |
| Calm | The Quiet Fields (72 bpm): harp, strings, flute and horns, pastoral | peace |
| Dark | Shadows Over the Altar (60 bpm): low strings, a low choir, a heartbeat of timpani, an oboe | peace under a cruel god (morality) |
| Tension | Gathering Storm (104 bpm): a spiccato ostinato under swelling horns and tremolo, drums gathering | a rival nearing victory, or a foe's Creature in the player's lands (political tension) |
| War | The Gods Make War (140 bpm): driving strings, timpani and taiko, a brass theme, choir | war, or the player's Creature fighting (warfare, Creature state) |
| Disaster | Wrath of the Earth (66 bpm): low tremolo clusters, brass stabs, rolls and great drums | an earthquake, an eruption, a tornado or the beast awake |
| Triumph | Ascendant (100 bpm): a fanfare for full orchestra | the player's god dominating or victorious (Divine Power) |

**The notation:**

- **Harmony:** chord symbols, one a bar or so many beats each.
- **Parts:** each part plays some bars in one of seven ways:
  - Pad: chords voiced near an octave, each voice moving as little as it can;
  - Bass: the chord's root, or its slash bass;
  - Ostinato and Arpeggio: patterns of chord tones;
  - Melody: notes in scientific pitch with their beats;
  - Rhythm: drum patterns, the drums tuned to the chord;
  - Swell: into the next downbeat.
- **Dynamics:** a part can rise or fall over its bars.
- **Validation.** The file is parsed strictly: an unknown chord, note, tone, step or bar is an error,
  every mood needs exactly one score, and its harmony must fill its bars.

### Choosing the mood

`AudioDirector.Explain` takes the first that holds of:

1. Triumph;
2. Disaster;
3. War;
4. Tension;
5. Dark;
6. Calm.

The title screen plays the Theme. A more urgent mood comes in at once over 1.5 seconds. A calmer one
waits until the last has held 20 seconds, then crossfades over 3. The log names each mood and why
("Music: War (at war with Wynlius)").

Region is not yet heard; the pieces do not change with the land.

### Tooling

- `--music=<mood>` holds a mood for the run.
- `--music-export=<folder>` writes every piece as a WAV file once made, then quits, to listen outside
  the game.

## Consequences

- **Real orchestral writing, synthesized.** Phrases, cadences, crescendos, ostinatos, fanfares and a
  hall, in place of three pads. It still sounds synthesized beside a recorded orchestra.
- **About 50 MB of music in memory,** at 32 kHz.
- **About 5 seconds of all cores at start,** on worker threads; the game is silent until the Theme
  is ready.
- **The music is content:** a composer can change it in `music.json` without code.
- **Faster debug runs:** the game library is optimised in debug builds too, so the simulation is
  quicker there as well.

## Alternatives considered

- **Recorded orchestral samples** (a sample library, or a CC0 one such as VSCO 2). They sound truer,
  but the game makes all its own sound (spec §2's original sounds, ADR-021), and they would add
  hundreds of megabytes.
- **Rendering offline and shipping the WAV files.** That needs a build step to render them, and the
  score could no longer be changed as content. Playing the music at start costs 5 seconds of
  background work.
- **Playing the orchestra live** (`AudioStreamGenerator`). It would let the music react note by note,
  but would take the CPU from the simulation every frame.
