// "Meet your pantheon": the gods the game's scribes write for any seed, size and species, in the
// browser (ADR-076), shown as the game's own page of the gods shows them.

import { angelName } from './angel.js';
import { content, escapeHtml } from './content.js';
import { kindOf, nature, title, writePantheon } from './scribes.js';

const LEANINGS = [
  ['aggression', 'War'],
  ['cooperation', 'Peace'],
  ['treachery', 'Treachery'],
  ['grudge', 'Grudge'],
  ['protect', 'Protect'],
  ['convert', 'Convert'],
  ['intimidate', 'Fear'],
];

const RANDOM_WORDS = ['ember', 'tide', 'ash', 'oath', 'thorn', 'gold', 'storm', 'grove', 'crown', 'salt', 'moon', 'iron', 'reed', 'flame', 'stone', 'wolf'];

function temperBars(temper) {
  return LEANINGS.map(([key, label]) => {
    const value = temper[key];
    const width = `${Math.abs(value) * 50}%`;
    const bar = value === 0 ? '' : `<span class="${value > 0 ? 'up' : 'down'}" style="width:${width}"></span>`;
    return `<span>${label}</span><span class="leaning" title="${key} ${value > 0 ? '+' : ''}${value.toFixed(1)}">${bar}</span>`;
  }).join('');
}

function card(god, index, colors) {
  const color = colors[index % colors.length];
  return `<article class="god" style="--god-color:${color}; animation-delay:${index * 70}ms">
    <h4>${escapeHtml(title(god))}</h4>
    <div class="kind">${escapeHtml(kindOf(god))} · its Creature, ${escapeHtml(god.creature)} the ${escapeHtml(god.species.toLowerCase())}</div>
    <p class="history">${escapeHtml(god.persona.history)}</p>
    <p class="nature">${escapeHtml(nature(god))}</p>
    <div class="temper" aria-label="Temper">${temperBars(god.persona.temper)}</div>
  </article>`;
}

/** Wires the generator in its section. */
export async function initPantheon(root) {
  const [minds, gods, sizes, creatures, angel, view] = await Promise.all([
    content('minds.json'), content('gods.json'), content('world_sizes.json'), content('creatures.json'),
    content('angel.json'), content('divine_view.json'),
  ]);
  const seedInput = root.querySelector('#seed');
  const sizeSelect = root.querySelector('#size');
  const speciesSelect = root.querySelector('#species');
  const meta = root.querySelector('#forge-meta');
  const list = root.querySelector('#gods');
  const player = root.querySelector('#player-god');

  sizes.Sizes.forEach((size) => {
    const km = (size.HeightmapResolution * size.VertexSpacing) / 1000;
    sizeSelect.add(new Option(`${size.Name}: ${km.toFixed(0)} km, ${size.Gods} gods`, size.Name));
  });
  speciesSelect.add(new Option(`${gods.Gods[0].CreatureSpecies} (the default)`, ''));
  creatures.Species.forEach((s) => speciesSelect.add(new Option(s.Name, s.Name)));

  const fromHash = new URLSearchParams(location.hash.replace(/^#pantheon\??/, ''));
  if (fromHash.get('seed')) {
    seedInput.value = fromHash.get('seed');
  }
  if (fromHash.get('size')) {
    sizeSelect.value = fromHash.get('size');
  }
  if (fromHash.get('species')) {
    speciesSelect.value = fromHash.get('species');
  }

  let changed = false;
  const render = () => {
    const written = writePantheon({ lore: minds.Lore, gods, sizes }, seedInput.value, sizeSelect.value, speciesSelect.value || null);
    if (!written) {
      list.innerHTML = '<p class="empty">Type a seed: a number, or any words.</p>';
      meta.textContent = '';
      player.textContent = '';
      return;
    }
    const rivals = written.gods.filter((g) => !g.player);
    const you = written.gods.find((g) => g.player);
    const colors = view.GodColors;
    list.innerHTML = written.gods.map((g, i) => (g.player ? '' : card(g, i, colors))).join('');
    meta.innerHTML = `<span>Seed value <strong>${written.seed}</strong></span><span><strong>${rivals.length}</strong> rival gods</span>`;
    player.innerHTML = `<span><strong>You:</strong> ${escapeHtml(you.name)}, with ${escapeHtml(you.creature)} the ${escapeHtml(you.species.toLowerCase())}</span>
      <span><strong>Your angel:</strong> ${escapeHtml(angelName(angel.Names, written.seed, 1))}</span>`;
    const share = new URLSearchParams({ seed: seedInput.value.trim() });
    if (sizeSelect.value !== sizes.Sizes[0].Name) {
      share.set('size', sizeSelect.value);
    }
    if (speciesSelect.value) {
      share.set('species', speciesSelect.value);
    }
    if (changed) {
      history.replaceState(null, '', `#pantheon?${share}`);
    }
  };
  const change = () => {
    changed = true;
    render();
  };

  seedInput.addEventListener('input', change);
  sizeSelect.addEventListener('change', change);
  speciesSelect.addEventListener('change', change);
  root.querySelector('#reroll').addEventListener('click', () => {
    const pick = () => RANDOM_WORDS[Math.floor(Math.random() * RANDOM_WORDS.length)];
    seedInput.value = Math.random() < 0.5 ? String(Math.floor(Math.random() * 1e9)) : `${pick()} ${pick()}`;
    change();
  });
  root.querySelector('#copy-link')?.addEventListener('click', async (event) => {
    change();
    try {
      await navigator.clipboard.writeText(location.href);
      event.target.textContent = 'Link copied';
    } catch {
      event.target.textContent = 'Copy the address bar';
    }
    setTimeout(() => { event.target.textContent = 'Copy a link'; }, 2000);
  });
  render();
}
