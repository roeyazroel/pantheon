// The player's angel (ADR-074), ported from C# (AngelLooks, AngelContent.NameFor and
// ConductModel.Traits): how it looks is read from the god's conduct, in the game's float arithmetic,
// so the site's angel takes the stage the game's would for the same deeds (ADR-076).

import { derive } from './random.js';

const f32 = Math.fround;
const RADIANT_LEAN = f32(0.6);
const KIND_LEAN = f32(0.2);

/** The conduct's deeds, kind ones first. */
export const DEEDS = ['Help', 'Gifts', 'Faithfulness', 'Harm', 'Cruelty', 'Vengeance', 'Upheaval', 'Deceit', 'Aggression'];

// The marks of the traits, in the order of the game's AngelMarks flags.
const MARKS = [
  ['Blade', 'Vengeful'],
  ['Veil', 'Deceptive'],
  ['Chains', 'Cruel'],
  ['Crown', 'Ambitious'],
  ['Cracks', 'Chaotic'],
  ['Gold', 'Generous'],
  ['OpenHands', 'Merciful'],
  ['ClearEyes', 'Honest'],
];

const TONES = { Radiant: 'Light', Kind: 'Light', Unformed: 'Silver', Torn: 'Torn', Stern: 'Dark', Fallen: 'Dark' };

/** The angel's name in a world (AngelContent.NameFor): drawn from the seed and its god. */
export function angelName(names, seed, godId = 1) {
  return names[Number(derive(seed, 'angel', godId) % BigInt(names.length))];
}

/** The kindness and the harm in a conduct, each deed by its weight (AngelLooks.Weigh). */
export function weigh(conduct, weights) {
  const term = (deed) => f32(f32(conduct[deed] ?? 0) * f32(weights[deed]));
  const kind = f32(f32(term('Help') + term('Gifts')) + term('Faithfulness'));
  let harm = term('Harm');
  for (const deed of ['Cruelty', 'Vengeance', 'Upheaval', 'Deceit', 'Aggression']) {
    harm = f32(harm + term(deed));
  }
  return { kind: Math.max(0, kind), harm: Math.max(0, harm) };
}

/** The traits a conduct shows, in the order of the game's GodTrait (ConductModel.Traits). */
export function traits(conduct, threshold) {
  const t = f32(threshold);
  const c = Object.fromEntries(DEEDS.map((d) => [d, f32(conduct[d] ?? 0)]));
  const shown = [];
  const add = (trait, shows) => shows && shown.push(trait);
  add('Compassionate', c.Help >= t && c.Help >= f32(2 * c.Harm));
  add('Cruel', c.Cruelty >= f32(f32(0.5) * t) || (c.Harm >= t && c.Harm >= f32(2 * c.Help)));
  add('Vengeful', c.Vengeance >= t);
  add('Merciful', c.Faithfulness >= t && c.Aggression < c.Faithfulness);
  add('Generous', c.Gifts >= t && f32(c.Help + c.Gifts) >= f32(3 * t));
  add('Deceptive', c.Deceit >= f32(f32(0.6) * t));
  add('Honest', c.Faithfulness >= t && c.Deceit < f32(f32(0.1) * t));
  add('Chaotic', c.Upheaval >= t);
  add('Ambitious', c.Aggression >= t);
  return shown;
}

/** The marks a god's traits leave on its angel, in flag order. */
export function marksOf(godTraits) {
  return MARKS.filter(([, trait]) => godTraits.includes(trait)).map(([mark]) => mark);
}

/**
 * How the angel looks for a conduct (AngelLooks.Of): its lean from harm to kindness, how formed it
 * is, how torn, its stage, its voice and its marks. The content is angel.json's.
 */
export function look(conduct, content, godTraits = []) {
  const { kind, harm } = weigh(conduct, content.Weights);
  const softness = f32(content.Softness);
  const total = f32(kind + harm);
  const formed = f32(total / f32(total + softness));
  const lean = total > 0 ? f32(f32(f32(kind - harm) / total) * formed) : 0;
  const conflict = Math.min(1, Math.max(0, f32(f32(2 * Math.min(kind, harm)) / f32(total + softness))));
  const stage = conflict >= f32(content.TornConflict) ? 'Torn'
    : lean >= RADIANT_LEAN ? 'Radiant'
      : lean >= KIND_LEAN ? 'Kind'
        : lean > -KIND_LEAN ? 'Unformed'
          : lean > -RADIANT_LEAN ? 'Stern'
            : 'Fallen';
  return { kind, harm, lean, formed, conflict, stage, tone: TONES[stage], marks: marksOf(godTraits) };
}
