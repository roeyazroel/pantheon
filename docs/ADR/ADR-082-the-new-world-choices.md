# ADR-082: The New World page's choices: your god, the gods, the rivals and the rules

- Status: Accepted
- Date: 2026-11-22
- Amends: [ADR-027](ADR-027-world-sizes-victories-and-large-worlds.md) (a size no longer fixes the number of gods
  or the pantheon victory's size), [ADR-060](ADR-060-main-menu-and-local-sqlite-profile.md) (the New
  World page), [ADR-065](ADR-065-the-gods-minds-and-a-born-pantheon.md) (the rivals' personalities
  are drawn within a temper) and [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md)
  (a hosted world is made with every choice of a new one)

## Context

The owner asked that the player control their god's name and other settings, such as how many gods
the world holds, and chose two passes of settings from a proposal:

- **First:** the god's name, the Creature's name, the number of gods as a choice of its own (not tied
  to the size), the time a victory is held (spec §53: "for a configurable duration"), and the same
  choices when hosting.
- **Second:** the player's colour, the rivals' temper, how strong the rivals play, how often
  calamities strike, few or many villages, and remembering the last choices.

The proposal left out choosing starting domains, rank or traits: §11 and §12 say those come from how
a god plays.

Before this, the New World page offered a name, a seed, the Creature's species, the size, the
victory, the climate and whether the gods start as strangers. The size fixed the gods (4, 6 or 8)
and the pantheon victory's size (2, 3 or 4). The player's god was always gods.json's Ilyra, and a
hosted world was always made with the defaults.

## Decision

### What a world keeps

| Choice | Kept as | Read by |
| --- | --- | --- |
| God's and Creature's names | `GodState.Name`, the Creature's name | everything that names them |
| Number of gods | the gods | everything |
| Villages | the settlements founded; `WorldSetup.Villages` | world generation |
| Colour | `GodState.Color`, an index into `divine_view.json`'s colours | `DivineViewSettings.GodColor(world, id)` |
| Rivals' temper | their personalities; `WorldSetup.Temper` | the scribes and the model, at birth |
| Rivals' strength | their starting Divine Power; `WorldSetup.Rivals` | `GodAi`: patience and jitter |
| Calamities | `WorldSetup.Calamities` | every calamity's onset chance |
| Time to hold | `VictorySettings.HoldSeconds` | `VictorySystem`, the HUD, alerts, the guide, the Book |

The choices a world goes on reading are kept in it, so a save plays on as it was made. Changing the
content later does not change a world already made. Save version 37 adds them; protocol version 23
carries them to players.

### The limits (`WorldChoices`, `world_sizes.json`)

- **Gods:** from 2 to the 8 that gods.json names (its `Gods`, then its `ExtraGods`, in order). At most
  one god for every `VillagesPerGod` (2) villages, so some villages begin free: Small takes up to 4,
  Large and larger up to 8.
- **Pantheon victory:** `PantheonShare` (half) of the gods, rounded down, may win together. That gives
  2 of 4, 3 of 6 and 4 of 8, as before, and no shared victory in a world of 2 or 3 gods.
  `PantheonMaxGods` leaves the sizes.
- **Villages:** few are `FewVillages` (3/4) of the size's number, many are `ManyVillages` (5/4). Small
  gives 6, 8 or 10; Vast gives 36, 48 or 60. `MoreSettlementNames` are added only when the names run
  short, so every other world keeps the names it had.
- **Hold:** one of `HoldMinutes` (10, 20 or 40).
- **Names:** 1 to 18 characters, as the gods' minds give theirs; control characters are dropped.
  Empty keeps gods.json's. A rival that had the name asked takes the player's old one, so no two
  gods or Creatures share a name. The scribes and the model already avoid the player's names.
- **Colour:** the player wears the chosen colour, and the god that wore it by its id takes the
  player's. A colour leaves only the gods that hold none to their ids' colours.

### The tempers

`world_sizes.json` lists the personalities of a peaceful temper (Guardian, Nature Keeper, Trickster,
Isolationist) and of a warlike one (Tyrant, Opportunist, Zealot, Warlord). A mixed temper draws from
all of them.

- **The scribes** shuffle and deal from the temper's list, with the same stream as before. A mixed
  temper writes exactly the gods it always wrote.
- **The model** is told the list, and a god it writes of another personality is the scribes'.
- **The content's own gods** (when there are no minds) are given the list's personalities in turn.

### Strength and calamities (`god_ai.json`, `disasters.json`)

- **Strength:** `Strengths` holds multiples of `StartingDivinePower`, `Patience` and `DecisionJitter`
  for weak, even and strong rivals (0.5, 2, 5; 1, 1, 1; 1.5, 0.5, 0). Only AI-controlled gods are
  changed, never a player's or the angel's weighing of one.
- **Calamities:** `Calamities` scales onset chances: 0.4 gentle, 1 normal, 2 harsh. These are the
  natural earthquakes, the lightning and hearth fires, eruptions, landslides, meteors, tornadoes,
  locust swarms, blight's emergence, the plague's outbreaks and the beast's waking. How a calamity
  spreads and what it does are not scaled. Weather, floods and the great events that are not
  calamities (eclipses, the anomaly, reformations) are left alone.
- **Normal and even multiply by exactly 1,** so a world made without choices draws the same numbers
  as before.

### The page

The New World page has four tabs: the world, your god, the rivals and the victory. A line under them
sums up the world. The gods row offers what the size and the villages allow, and follows the size
until the player picks a number. The colour row shows swatches. Play Together's "Set up a shared
world…" opens the same page with the host's name and port; a hosted world takes its name.

`NewWorldChoices` is what the page shows, by name and number. It becomes the world's
`NewWorldOptions`, falling back to the content's own for anything unknown or out of reach. The
profile keeps it under `new-world-choices`; it never keeps the seed or the world's name. The end
screen's "New world" uses the last choices.

### Debug tools

- **F1** shows a setup line: gods, villages, calamities, rivals, the hold and any chosen colours.
- **The Book's world page** says how the world was made.
- **Launch options:** `--god-name`, `--creature-name`, `--gods`, `--villages`, `--color`,
  `--rival-temper`, `--rival-strength`, `--calamities` and `--hold` make the same choices.
  `--title-page` opens `new-god`, `new-rivals`, `new-victory` or `host`.

### Words

The hold is told as the world has it, never "ten minutes" fixed: in the victory alerts, the dominance
alerts, the HUD's race line, the guide and the Book. The guide counts the world's rivals rather than
"Three other gods". The tutorial's own world keeps the default.

### The website

The scribes' port takes the number of gods, the player's names and the temper. 21 of the fixture's
84 pantheons use them. The "Meet your pantheon" form offers them.

## Consequences

- A world is made as the player wants it, and a shared world too. Old saves load with the defaults,
  play as they did, and their gods keep their ids' colours.
- `DivineViewSettings.GodColor` needs the world. The by-id colour is now `IdColor`, so code that
  forgot the world does not compile. The territory painter had: its border was gold for an Azure
  god until the rename.
- The content's fingerprint changes, so this build's worlds and the previous build's do not see each
  other on the network. The protocol version would refuse them anyway.
- **Not done:**
  - a rival's name or personality chosen one by one;
  - difficulty beyond the rivals' strength;
  - the number of villages as a number;
  - per-player choices in a shared world;
  - the colour of a god a joining player takes.
