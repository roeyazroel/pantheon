# ADR-039: Offerings

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-030](ADR-030-creature-paths-divine-hand-and-divine-acts.md) (the Divine Hand)
  and [ADR-033](ADR-033-resources-gathering-forests-and-roads.md) (villages' wood and stone)

## Context

§55 lists what the player does with the Divine Hand:

- pick up objects, carry villagers, rescue people, throw rocks, move trees;
- interact with the Creature, manipulate objects, cast miracles;
- place offerings.

ADR-030 built all but placing offerings. The specification says nothing more of them, so their
meaning is ours to choose. It should fit a god game whose hand is tactile and whose systems
interact: villages keep food, wood and stone (ADR-033), and their people's needs drive their
gratitude.

## Decision

An offering is what a village's people offer their god from their stores. The god's hand carries
it to where it is needed.

### Taking an offering (`HandAction.TakeOffering`, Ctrl+R)

- **Where:** at the village the god leads nearest the hand, within the hand's reach and where its
  influence reaches.
- **What:** a quarter of the village's food, wood and stone, at most 80 food, 20 wood and 12 stone
  (`HeldKind.Offering`, with `HeldObject.Food`, `Wood` and `Stone`).
- **Refused:** a village another god leads, or one with nothing in store, has no offering to give
  (`NoOffering`).
- **The people's feelings:** they are glad their god accepts it (`Offered`: faith +0.03, love
  +0.02, doubt −0.01).

### Giving it (R, or Shift+R)

The offering goes to whichever is nearer the point within reach.

- **A village:**
  - its stores fill;
  - another village than the one it came from is grateful by its need (`Gift`: up to love +0.12
    and faith +0.04 at its fullest need of food, half as much when it lacks nothing);
  - the god's conduct notes help to the needy (above 0.3 need), else a gift, so a generous god
    earns the trait (ADR-024);
  - an offering given back home only returns the stores.
- **The god's Creature:** eats the food, easing its hunger by 0.01 per unit and gaining 0.02
  loyalty. The wood and stone go back to the village they came from.
- **Anywhere else:** refused (`NoPlaceForOffering`), and the hand keeps it.
- **Throwing:** a thrown offering lands as gently as one set down. Gifts do not hurt.
- **Announcing:** each gift is announced (`OfferingGiven`), and the hand's status line says what
  went where ("You give Elar 50 food and 2 stone", "Bramble eats the offering's 40 food").

### What the player sees and does

- **The hand:** holds a wicker basket heaped with grain and fruit, a log across it.
- **Controls:** Ctrl+R takes an offering; R gives it.
- **Debug driver:** `--hand=offering` takes one from the focused village at the start.
- **Saves and sharing:**
  - saves move to version 30, whatever a version 29 hand held carrying no stores;
  - the protocol moves to version 18, for the new action and the held stores.

## Consequences

- **§55:** every action it lists exists.
- **Play:** offerings let the player feed a starving village from a rich one, stock a village's
  building, or feed the Creature by hand. They are the god's own act, so a generous god is seen as
  generous.
- **Balance:** AI gods do not use the hand (ADR-030), so balance is unchanged.
- **Not modelled:**
  - physics-driven objects (§55's tactile physics, §94's debris);
  - offerings to rival gods or at holy sites.
