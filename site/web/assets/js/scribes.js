// The scribes (ADR-065), ported from C# (PersonaScribe, PersonaText and WorldMaker.PantheonFor): they
// write a new world's rival gods from its seed and the lore in minds.json, as the game does when no
// model is chosen. The same seed, size and species give the same gods as the game (ADR-076).

import { SimulationRandom, derive, parseSeed, roundHalfEven } from './random.js';

const f32 = Math.fround;

// The personalities in the order of the game's GodPersonality enum, None left out.
export const PERSONALITIES = ['Guardian', 'Tyrant', 'Opportunist', 'Zealot', 'NatureKeeper', 'Trickster', 'Isolationist', 'Warlord'];

const SPOKEN = { NatureKeeper: 'Nature Keeper' };
const LEANING_SPREAD = f32(0.5);
const DEFINING_LEANING = f32(0.8);
const NAME_TRIES = 40;
const TOLD = f32(0.3);
const LEANINGS = [
  ['aggression', 'quicker to war', 'slower to war'],
  ['cooperation', 'readier to make peace', 'less ready to make peace'],
  ['treachery', 'readier to break its word', 'truer to its word'],
  ['grudge', 'slower to forgive', 'quicker to forgive'],
  ['protect', 'more careful of its own', 'less careful of its own'],
  ['convert', 'keener to win converts', 'less keen to win converts'],
  ['intimidate', 'fonder of fear', 'less fond of fear'],
];

/** A personality as the game names it: "Nature Keeper", "Tyrant"; empty for none. */
export function personalityName(personality) {
  return personality === 'None' ? '' : SPOKEN[personality] ?? personality;
}

/**
 * The gods of a world as the content gives them, at a size and with the player's Creature of a
 * species (WorldMaker.PantheonFor): the first size, or an unknown one, has gods.json's own gods.
 */
export function pantheonFor(godsContent, sizesContent, size, species) {
  const sizes = sizesContent?.Sizes ?? [];
  const preset = size == null ? null : sizes.find((s) => s.Name.toLowerCase() === String(size).toLowerCase());
  let gods = godsContent.Gods.map((g) => ({ ...g }));
  if (preset && preset !== sizes[0]) {
    gods = gods.concat(godsContent.ExtraGods.slice(0, Math.max(0, preset.Gods - gods.length)).map((g) => ({ ...g })));
  }
  const player = gods.findIndex((g) => g.Controller === 'Human');
  if (species && player >= 0) {
    const given = gods[player].CreatureSpecies;
    gods.forEach((god, i) => {
      if (i !== player && god.CreatureSpecies === species) {
        god.CreatureSpecies = given;
      }
    });
    gods[player].CreatureSpecies = species;
  }
  return gods;
}

function pick(items, random) {
  return items[random.nextInt(items.length)];
}

function shuffle(items, random) {
  for (let i = items.length - 1; i > 0; i--) {
    const j = random.nextInt(i + 1);
    [items[i], items[j]] = [items[j], items[i]];
  }
}

/** A set of names that ignores case, as the game's OrdinalIgnoreCase sets do. */
class NameSet {
  constructor(names) {
    this.names = [];
    this.keys = new Set();
    names.forEach((n) => this.add(n));
  }

  add(name) {
    const key = name.toUpperCase();
    if (this.keys.has(key)) {
      return false;
    }
    this.keys.add(key);
    this.names.push(name);
    return true;
  }
}

function startsLike(a, b) {
  return a.length >= 3 && b.length >= 3 && a.slice(0, 3).toUpperCase() === b.slice(0, 3).toUpperCase();
}

function unique(taken, make, alike) {
  for (let i = 0; i < NAME_TRIES; i++) {
    const name = make();
    if (!taken.names.some((t) => alike?.(t, name) === true) && taken.add(name)) {
      return name;
    }
  }
  const first = make();
  for (let n = 2; ; n++) {
    const numbered = `${first} ${n}`;
    if (taken.add(numbered)) {
      return numbered;
    }
  }
}

function makeName(lore, random) {
  const name = pick(lore.NameStarts, random) + pick(lore.NameMiddles, random) + pick(lore.NameEnds, random);
  return name.length === 0 ? 'Nameless' : name[0].toUpperCase() + name.slice(1).toLowerCase();
}

function tenth(value) {
  return f32(roundHalfEven(f32(value * 10)) / 10);
}

function temper(random) {
  const leanings = [];
  for (let i = 0; i < 7; i++) {
    leanings.push(tenth(f32(f32(f32(random.nextFloat() * 2) - 1) * LEANING_SPREAD)));
  }
  const defining = random.nextInt(7);
  leanings[defining] = random.nextInt(2) === 0 ? -DEFINING_LEANING : DEFINING_LEANING;
  const clamp = (v) => (Number.isFinite(v) ? Math.min(1, Math.max(-1, v)) : 0);
  return Object.fromEntries(LEANINGS.map(([key], i) => [key, clamp(leanings[i])]));
}

function rival(lore, personality, species, random, names, creatures) {
  const name = unique(names, () => makeName(lore, random), startsLike);
  const creature = unique(creatures, () => pick(lore.CreatureNames, random));
  const lines = lore.Personalities.find((p) => p.Personality === personality) ?? lore.Personalities[0];
  const fill = (text) => text.replaceAll('{name}', name).replaceAll('{creature}', creature).replaceAll('{animal}', species.toLowerCase());
  const history = [fill(pick(lore.Origins, random)), fill(pick(lines.Deeds, random)), fill(pick(lore.Turns, random))].join(' ');
  const persona = {
    epithet: pick(lines.Epithets, random),
    history,
    voice: pick(lines.Voices, random),
    desire: pick(lines.Desires, random),
    dread: pick(lines.Dreads, random),
    temper: temper(random),
  };
  return { name, creature, personality, persona };
}

/** The leanings strong enough to tell, strongest first, at most two (PersonaText.Leanings). */
export function leanings(temperValues) {
  return LEANINGS.map(([key, more, less]) => ({ value: temperValues[key], more, less }))
    .filter((l) => Math.abs(l.value) >= TOLD)
    .sort((a, b) => Math.abs(b.value) - Math.abs(a.value))
    .slice(0, 2)
    .map((l) => (l.value > 0 ? l.more : l.less));
}

/** A temper in numbers, as the debug panel shows it (PersonaText.Numbers). */
export function temperNumbers(temperValues) {
  const told = LEANINGS.map(([key]) => [key, temperValues[key]]).filter(([, v]) => v !== 0);
  return told.length === 0 ? 'even' : told.map(([key, v]) => `${key} ${v < 0 ? '-' : '+'}${Math.abs(v).toFixed(1)}`).join(', ');
}

function lower(line) {
  let text = line.trim();
  while (text.endsWith('.')) {
    text = text.slice(0, -1);
  }
  const upper = (c) => c !== c.toLowerCase() && c === c.toUpperCase();
  return text.length > 1 && upper(text[0]) && !upper(text[1]) ? text[0].toLowerCase() + text.slice(1) : text;
}

/** A god's title: its name and epithet, or its name and personality (PersonaText.Title). */
export function title(god) {
  if (god.persona) {
    return `${god.name} ${god.persona.epithet}`;
  }
  const personality = personalityName(god.personality);
  return personality.length > 0 ? `${god.name} the ${personality}` : god.name;
}

/** What kind of god it is, as the gods' page says: its personality, or "A god" for the player's. */
export function kindOf(god) {
  const personality = personalityName(god.personality);
  return personality.length > 0 ? personality : 'A god';
}

/** What a god is like, in a sentence or three (PersonaText.Nature). */
export function nature(god) {
  if (!god.persona) {
    return '';
  }
  const personality = personalityName(god.personality);
  const told = leanings(god.persona.temper);
  const article = personality.length > 0 && 'AEIOU'.includes(personality[0].toUpperCase()) ? 'An' : 'A';
  const kind = personality.length === 0 ? ''
    : told.length === 0 ? `${article} ${personality}. `
      : `${article} ${personality}, ${told.join(' and ')} than most. `;
  const persona = god.persona;
  return `${kind}Speaks ${lower(persona.voice)}. Wants ${lower(persona.desire)}; dreads ${lower(persona.dread)}.`;
}

/**
 * The gods the scribes write for a world (PersonaScribe.Write): one per slot, the player's god as
 * the content names it, and each rival with a personality, a name, a Creature, a history and a
 * temper. Returns null for an empty seed.
 */
export function writePantheon({ lore, gods: godsContent, sizes }, seedText, size = null, species = null) {
  const seed = parseSeed(seedText);
  if (seed === null) {
    return null;
  }
  const setup = pantheonFor(godsContent, sizes, size, species);
  const random = new SimulationRandom(derive(seed, 'pantheon-personas'));
  const personalities = [...PERSONALITIES];
  shuffle(personalities, random);
  const humans = setup.filter((g) => g.Controller === 'Human');
  const names = new NameSet(humans.map((g) => g.Name));
  const creatures = new NameSet(humans.map((g) => g.CreatureName));
  let rivals = 0;
  const born = setup.map((slot) => {
    const god = slot.Controller === 'Human'
      ? { name: slot.Name, creature: slot.CreatureName, personality: 'None', persona: null }
      : rival(lore, personalities[rivals++ % personalities.length], slot.CreatureSpecies, random, names, creatures);
    return { ...god, species: slot.CreatureSpecies, player: slot.Controller === 'Human' };
  });
  return { seed, gods: born };
}
