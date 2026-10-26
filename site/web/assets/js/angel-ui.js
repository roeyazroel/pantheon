// "An angel that becomes what you do": set a god's deeds and see its angel as the game reads it
// (ADR-074, ADR-076): its stage, how formed and torn it is, the traits the world sees and their
// marks, and a line the scribes give it in that voice.

import { DEEDS, look, traits } from './angel.js';
import { content, escapeHtml, words } from './content.js';

const KIND = ['Help', 'Gifts', 'Faithfulness'];
const MAX = 20;

const MARK_WORDS = {
  Blade: 'a burning blade (vengeful)',
  Veil: 'a veil (deceptive)',
  Chains: 'chains at its wrists (cruel)',
  Crown: 'a crown of thorns (ambitious)',
  Cracks: 'cracks of fire in its skin (chaotic)',
  Gold: 'threads of gold in its robe (generous)',
  OpenHands: 'light in its open hands (merciful)',
  ClearEyes: 'clear, shining eyes (honest)',
};

// Gods of every stage, for the presets and the strip of stages.
export const PRESETS = {
  Radiant: { Help: 14, Gifts: 8, Faithfulness: 9 },
  Kind: { Help: 6, Faithfulness: 3, Harm: 2 },
  Unformed: { Help: 1, Harm: 1 },
  Torn: { Help: 14, Gifts: 6, Harm: 12, Cruelty: 4, Aggression: 6 },
  Stern: { Help: 2, Harm: 6, Aggression: 4 },
  Fallen: { Harm: 14, Cruelty: 8, Upheaval: 6, Aggression: 10, Vengeance: 6 },
};

const NAMES = {
  Help: 'Help in need',
  Gifts: 'Gifts',
  Faithfulness: 'Keeping faith',
  Harm: 'Harm',
  Cruelty: 'Cruelty to its own',
  Vengeance: 'Vengeance',
  Upheaval: 'Upheaval',
  Deceit: 'Deceit',
  Aggression: 'Aggression',
};

function fill(line, names) {
  return line.replace(/\{(\w+)\}/g, (all, key) => names[key] ?? all);
}

/** Wires the angel's section. */
export async function initAngel(root) {
  const [angel, rules] = await Promise.all([content('angel.json'), content('divine_rules.json')]);
  const threshold = rules.Culture.TraitThreshold;
  const sliders = root.querySelector('#deed-sliders');
  const image = root.querySelector('#angel-img');
  const stageName = root.querySelector('#angel-stage');
  const note = root.querySelector('#angel-note');
  const voice = root.querySelector('#angel-voice');
  const line = root.querySelector('#angel-line');
  const readings = root.querySelector('#angel-readings');
  const marks = root.querySelector('#angel-marks');
  const strip = root.querySelector('#stage-strip');
  const conduct = Object.fromEntries(DEEDS.map((d) => [d, 0]));
  const names = { name: 'Vesperine', god: 'Ilyra', village: 'Duskwater', creature: 'Bramble', other: 'Wynlius', visitor: 'Ashjaw', detail: 'it' };
  let spoken = 0;
  let lastStage = '';

  const groups = [['kind', 'Kindness', KIND], ['harm', 'Harm', DEEDS.filter((d) => !KIND.includes(d))]];
  sliders.innerHTML = groups.map(([cls, heading, deeds]) => `<h3 class="${cls}">${heading}</h3>${deeds.map((d) => `
    <label class="deed"><span>${NAMES[d]}</span><input type="range" min="0" max="${MAX}" step="0.5" value="0" data-deed="${d}" aria-label="${NAMES[d]}"><output>0</output></label>`).join('')}`).join('');

  strip.innerHTML = Object.keys(PRESETS).map((stage) => `<button type="button" data-stage="${stage}" aria-pressed="false" title="${stage}">
    <img src="images/angel-${stage.toLowerCase()}.jpg" alt="${stage} angel" loading="lazy"></button>`).join('');

  const set = (values) => {
    for (const input of sliders.querySelectorAll('input')) {
      const value = values[input.dataset.deed] ?? 0;
      input.value = value;
      conduct[input.dataset.deed] = value;
    }
    render();
  };

  const render = () => {
    for (const input of sliders.querySelectorAll('input')) {
      input.nextElementSibling.textContent = Number(input.value).toFixed(1).replace(/\.0$/, '');
    }
    const shown = traits(conduct, threshold);
    const seen = look(conduct, angel, shown);
    const stage = angel.Stages[seen.stage];
    image.src = `images/angel-${seen.stage.toLowerCase()}.jpg`;
    image.alt = `A ${seen.stage.toLowerCase()} angel`;
    stageName.textContent = stage.Name;
    stageName.className = `angel-stage-name ${seen.tone === 'Dark' ? 'dark' : seen.tone === 'Torn' ? 'torn' : ''}`;
    note.textContent = stage.Note;
    voice.textContent = `It speaks ${stage.Voice.split('.')[0]}.`;
    if (seen.stage !== lastStage) {
      spoken += 1;
      lastStage = seen.stage;
    }
    const lines = angel.Lines.Called[seen.tone];
    line.textContent = `“${fill(lines[spoken % lines.length], names)}”`;
    readings.innerHTML = [
      ['Lean', seen.lean, 'from harm (−1) to kindness (1)'],
      ['Formed', seen.formed, 'how much it has seen you do'],
      ['Torn', seen.conflict, 'great good and great harm at once'],
    ].map(([label, value, title]) => `<div class="reading" title="${title}"><strong>${value >= 0 ? '' : '−'}${Math.abs(value).toFixed(2)}</strong>${label}</div>`).join('');
    marks.innerHTML = shown.length === 0
      ? '<span class="muted">The world sees no trait in you yet.</span>'
      : shown.map((t) => `<span class="mark-pill">${escapeHtml(words(t))}</span>`).join('')
        + (seen.marks.length ? `<p class="notice">Its marks: ${seen.marks.map((m) => MARK_WORDS[m]).join('; ')}.</p>` : '');
    for (const button of strip.querySelectorAll('button')) {
      button.setAttribute('aria-pressed', String(button.dataset.stage === seen.stage));
    }
  };

  sliders.addEventListener('input', (event) => {
    const deed = event.target.dataset?.deed;
    if (deed) {
      conduct[deed] = Number(event.target.value);
      render();
    }
  });
  root.querySelectorAll('[data-preset]').forEach((button) => button.addEventListener('click', () => set(PRESETS[button.dataset.preset])));
  strip.addEventListener('click', (event) => {
    const button = event.target.closest('button[data-stage]');
    if (button) {
      set(PRESETS[button.dataset.stage]);
    }
  });
  set(PRESETS.Radiant);
}
