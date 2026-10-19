# ADR-034: Great events: the plague, eclipses, reformations, world wars, great droughts and great migrations

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-025](ADR-025-treaties-secrets-favours-and-the-divine-council.md) (the council's
  coordinated response to disasters), [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md)
  (hospitals, medicine and learning) and [ADR-033](ADR-033-resources-gathering-forests-and-roads.md)
  (roads)

## Context

§52 asks for world events and gives examples: plague, global drought, an ancient monster awakening,
a supervolcano, a meteor, mass migration, religious reformation, world war, an eclipse and a
magical anomaly. Some events encourage rival gods to cooperate.

- Meteors and the volcano exist (ADR-029), and so do dry spells and migration (ADR-013, ADR-016).
- Nothing yet named a time the whole world lived through, and no plague, eclipse, reformation or
  world war existed.
- The project's rules prefer emergent interactions to scripted content, so a great event should
  come from the world's state, or from the seed like the weather, not from a script.

## Decision

### Great events

`WorldState.GreatEvents` keeps the great events under way:

- each with a kind, a name from the rules' lists, a start, an end for one of set length, the god
  it concerns and the most it reached;
- the tick of the next eclipse;
- when each god's faith last reformed;
- since when the land has been dry.

`GreatEventSystem` steps once a second, drawing from a stream derived from the seed and the tick.
Each great event is announced when it begins and ends (`GreatEventBegan`, `GreatEventEnded`) and
recorded in the history. The rules are `DisasterRules.GreatEvents` in `disasters.json`. The type is
named "great event" because `WorldEvent` is already the simulation's event type.

### The plague

- **The state:** each village has `Plague` (the share sick) and `Immunity` (the share immune).
- **Outbreaks:** a village breaks out at 3 an hour at its worst. That means crowded beyond its
  housing, its health poor, its land polluted and its people not immune, and it is rarer as each
  of these eases.
- **Its course:** the sick infect the healthy who are not immune, and recover immune after about
  50 seconds on average. Immunity lasts about an hour. The sick lose health, so deaths, hardship and
  doubt follow through the rules already in place.
- **Spreading:** a neighbour within 500 m can catch it, and so can a village along an open road,
  whatever the road's length.
- **Shields and cures:**
  - a hospital stops 40% of the spread, and each level of medicine 8%;
  - a healing miracle cures twice its healing, which the AI gods already value by the health it
    restores;
  - a plague ends on its own after about nine minutes.
- **The great event:** three villages sick at once make a plague a great event (for example "the
  Grey Plague").
- **The council:** a plague, like a great drought, summons the Divine Council to coordinate the
  response, and a sick village counts as struck. Rival gods who help it pay less.

### Eclipses

- **Timing:** the sun goes dark 20 to 35 minutes into a world, then every 30 to 45 minutes, for 45
  seconds. The times are drawn from the seed and the tick, so they need no saved stream.
- **Dread:** every believer feels it: fear +0.12 and doubt +0.06.
- **Awe:** a miracle worked in the dark moves its witnesses twice as much.
- **The client:** the sun dims by 85%, fading out and back over five seconds.

### Reformations

When a god's followers' mean doubt reaches 0.45 in at least three villages whose mean learning is
0.25 or more, its faith reforms:

- the followers keep 40% of their doubt and gain 0.15 faith;
- 30% of the doubting share leave the god;
- the same faith reforms at most once every 30 minutes.

### The watch

- **World war:** a world war begins when four fifths of the gods, and at least three, are at war at
  once. It lasts while all but one of them still fight, and every believer grows afraid
  (0.0005 fear a second).
- **Great drought:** the land's mean soil water stays at 0.25 or below for two minutes. It summons
  the council's coordinated response.
- **Great migration:** a fifth of the island's people are on the road at once, until fewer than
  half as many are.

### What the player sees and does

- **Alerts:** every great event's beginning and end, and the plague reaching the player's villages.
- **The top bar** names the great events under way.
- **The book's World page** has a "Great events" section.
- **Labels and overlays:** a sick village's label says "plague", and the danger overlay paints it
  sickly green.
- **F1:** the events, the next eclipse, the sick villages and each faith's doubt and learning, and
  for the selected village its sickness, immunity and shield.
- **Debug drivers:** `;` starts the next of the plague at the cursor, an eclipse and the player's
  reformation; `--great-event=plague|eclipse|reformation` starts one at the start.
- **Saves and sharing:**
  - saves move to version 25;
  - the protocol moves to version 13, with a GreatEvents section; a village's plague travels with
    the Settlements section.

## Consequences

- **§52:** plague, global drought, mass migration, religious reformation, world war and eclipse are
  built. Supervolcano and meteor were already there, and the plague and drought bring the gods to
  cooperate. Still missing: the ancient monster and the magical anomaly.
- **Balance:** over 40 seeds (30–69), 37 games are won within the hour, at a median of 25.0
  minutes and a mean of 27.4. Part 13 gave 38, at 25.7 and 27.3.
- **Rates:** over ten one-hour games, a game sees 1.1 eclipses, 0.7 world wars, 0.5 reformations
  and 0.8 plague outbreaks. One game in five sees a great plague.
