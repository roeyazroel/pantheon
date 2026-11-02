# ADR-080: People go home at night, and the small hours of a village

- Status: Accepted
- Date: 2026-11-03
- Amends: [ADR-079](ADR-079-day-and-night.md) (people hidden at night)
- Builds on: [ADR-046](ADR-046-local-navigation-for-citizens.md) (the villages' ways)

## Context

ADR-079 sent most people indoors at night by hiding them wherever they stood, a share by a hash, and
left a fifth of them outside. The owner asked that people return home and not stay outside.

## Decision

### The hour reaches the crowd (`CitizenEvening`, `SettlementCrowd`)

Each person keeps their own hours, from a hash, so a village empties and fills over a while:

- **Leaving off:** between 17:00 and 18:30 they stop what they are doing and walk home.
- **The way:** along the village's own ways, round the houses, to the spot before their door, then
  in through the door, where they stop being drawn.
- **Nightfall:** by 22:00 everybody is in. Nobody is left outside.
- **Rising:** between 5:30 and 7:30 they come out of the same door, and their routine leads them on.

The crowd stays derived and is never saved. The routine's clock runs on as before, so a person comes
out to wherever their routine has got to, and walks there.

- **Getting there in time:** at ×4 or ×16 the evening passes in seconds, while the crowds keep a human
  pace (ADR-049). People hurry to match, up to 8 times, so they are not lost in the middle of a lane.
  The shipped hours were tuned until a test village's 120 people all reach their own doors at ×1.
- **Villages first seen at night** are already asleep. Nobody walks home in view of a camera that has
  just arrived.
- **One-off ways** are searched without being kept (`LocalNavGrid.Route`): a person sets out from a
  different spot each evening, and a cache of them would only grow.
- **Who stays out:** followed villagers, prophets, migrants and Creatures keep their own business.
  They are not part of the crowds, and their rules know nothing of the hour.

### A village's night

Keys in `day_night.json` carry what an hour holds:

- **Lamps:** windows go out as the village falls asleep, a few stay lit through the small hours, and
  some are lit again before dawn. Lamps no longer simply follow the dark.
- **Hearths:** smoke rises from about half the roofs at breakfast and supper, more in winter, and
  drifts on the wind (`HearthView`). From a god's height a straight column hides itself, so the wind
  has to lay it out; it took more puffs and more drift than a first guess to be seen at all.
- **Mist:** low haze thickens around dawn, and clears by mid-morning.
- **Birds:** they go to roost through the evening.
- **Fireflies** fly around where the camera looks, on warm, dry nights (`FireflyView`).

### Sound

`SoundKind.Crickets` and `SoundKind.Birdsong` are made in code like the game's other sounds, and loop
on the ambience bus, under the music. `AudioDirector.SmallCreatures` gives crickets to warm nights,
and birdsong to the light, thickest at dawn. Rain hushes both, and the cold silences the crickets.

## Consequences

- **Performance**, uncapped at 2720x1530 on an M3 Pro, with the clock running, against the build before
  part 55:

  | View | Before | Noon | Evening | Night |
  | --- | --- | --- | --- | --- |
  | Close (40 m) | 15.6 ms | 15.6 ms | 15.5 ms | 15.3 ms |
  | Near (110 m) | 17.2 ms | 16.8 ms | 17.4 ms | 16.5 ms |
  | Wide (420 m) | 21.9 ms | 19.4 ms | 18.9 ms | 17.0 ms |
  | High (1400 m) | 17.8 ms | 16.6 ms | 16.3 ms | 16.0 ms |
  | Stress world, going home | 21.7 ms | | 18.4 ms | |

  Each hearth's smoke is a few dozen GPU particles, and none is drawn beyond 900 m.
- `SettlementCrowd.Advance` takes the hour, and the F1 panel counts who is indoors.
- Picking a villager to follow (`/`) still reads the routine, so at night it can pick someone indoors.
  They are then shown outside, as a followed villager keeps its own business.
- The new sounds were checked by the tests (in range, loud enough, looping without a jump), not by ear.

## Alternatives considered

- **Keeping the hash that hides people where they stand:** it was the problem.
- **A night watch with lanterns:** the owner asked for nobody outside.
- **Windows lit only where a house's people are home:** a house's residents are spread over a roster
  that changes with the crowd's size, for a difference few would see.
