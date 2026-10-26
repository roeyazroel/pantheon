// The codex (ADR-076): the game's miracles, domains, personalities, Creatures, holy sites, world sizes
// and biomes, read from its own content files, so the site always says what the game does.

import { initChrome } from './chrome.js';
import { content, escapeHtml, words } from './content.js';

const RANKS = { MinorSpirit: 'Minor Spirit', LesserGod: 'Lesser God', God: 'God', GreaterGod: 'Greater God', HighGod: 'High God', Ascendant: 'Ascendant' };
const SCALES = { Minor: 'Minor', Regional: 'Regional', Great: 'Great', DivineAct: 'Divine Act' };

function entry(title, tags, body, detail = '', swatch = '') {
  const text = [title, ...tags, body, detail].join(' ').toLowerCase();
  return `<article class="entry" data-text="${escapeHtml(text)}">
    <h3>${swatch}${escapeHtml(title)}</h3>
    <div class="tags">${tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join('')}</div>
    <p>${body}</p>${detail ? `<p class="detail">${detail}</p>` : ''}</article>`;
}

function keyOf(miracle, index) {
  if (index < 10) {
    return `key ${(index + 1) % 10}`;
  }
  return miracle.Id === 'anoint' ? 'Shift+1' : '';
}

const TABS = {
  async Miracles() {
    const { Miracles } = await content('miracles.json');
    return {
      intro: 'Every miracle a god can work: what it costs at full influence, how wide it reaches, how long before it can be cast again, and the rank it needs. Signature miracles need their domain; Divine Acts need a Greater God.',
      entries: Miracles.map((m, i) => entry(
        m.Name,
        [SCALES[m.Scale] ?? m.Scale, RANKS[m.MinimumRank] ?? m.MinimumRank, words(m.Domain), ...(m.RequiresDomain ? ['Signature'] : []), keyOf(m, i)].filter(Boolean),
        escapeHtml(m.Description),
        `${m.PowerCost} Divine Power · ${m.Radius} m · ${Math.round(m.CooldownTicks / 10)} s to recover`,
      )),
    };
  },
  async Domains() {
    const [{ Domains, Slots, DomainCostFactor }, { Miracles }] = await Promise.all([content('domains.json'), content('miracles.json')]);
    const slots = Slots.map((s) => `${s.Domains} as a ${RANKS[s.Rank] ?? s.Rank}`).join(', ');
    return {
      intro: `What a god is the god of. It takes up domains as it rises (${slots}), keeps them for good, and their miracles cost it ${Math.round((1 - DomainCostFactor) * 100)}% less. Each changes one rule of the world for its god and opens a signature miracle.`,
      entries: Domains.map((d) => {
        const signature = Miracles.find((m) => m.RequiresDomain && m.Domain === d.Domain);
        return entry(d.Domain, ['Domain'], escapeHtml(d.Description), `<strong>Boon:</strong> ${escapeHtml(d.Boon)}${signature ? `<br><strong>Signature:</strong> ${escapeHtml(signature.Name)}. ${escapeHtml(signature.Description)}` : ''}`);
      }),
    };
  },
  async Personalities() {
    const { Lore } = await content('minds.json');
    return {
      intro: 'The eight natures a rival god can be born with. The scribes draw a god\'s epithet, voice, desire and dread from its personality\'s lore; a model writes its own.',
      entries: Lore.Personalities.map((p) => entry(
        words(p.Personality),
        p.Epithets.slice(0, 4).map((e) => e.replace(/^the /i, '')),
        `Speaks ${escapeHtml(p.Voices[0].replace(/\.$/, ''))}.`,
        `<strong>Wants</strong> ${escapeHtml(p.Desires[0].replace(/\.$/, ''))}. <strong>Dreads</strong> ${escapeHtml(p.Dreads[0].replace(/\.$/, ''))}.`,
      )),
    };
  },
  async Creatures() {
    const { Species } = await content('creatures.json');
    const traits = ['Kindness', 'Aggression', 'Courage', 'Protectiveness', 'Jealousy', 'Playfulness'];
    return {
      intro: 'The animals a Creature can be. Each walks upright, carries, builds and fights, and its species shades its body and its character from birth.',
      entries: Species.map((s) => entry(
        s.Name,
        ['Species', ...traits.filter((t) => Math.abs(s[t]) >= 0.1).map((t) => `${s[t] > 0 ? 'more' : 'less'} ${t.toLowerCase()}`)],
        escapeHtml(s.Description),
        `Toughness ×${s.Toughness} · Strength ×${s.Strength} · Speed ×${s.Speed} · Learning ×${s.Learning}`,
      )),
    };
  },
  async 'Holy sites'() {
    const sites = await content('holy_sites.json');
    return {
      intro: `Four sacred places in every world. The god whose influence holds one, clearly ahead of the others, takes it after ${sites.ClaimSeconds} seconds, and gains power and a gift there.`,
      entries: sites.Sites.map((s) => entry(s.Name, ['Holy site', words(s.Boon)], escapeHtml(s.Description))),
    };
  },
  async 'World sizes'() {
    const { Sizes } = await content('world_sizes.json');
    return {
      intro: 'The islands a new world can be, chosen on the start screen.',
      entries: Sizes.map((s) => entry(
        s.Name,
        [`${((s.HeightmapResolution * s.VertexSpacing) / 1000).toFixed(0)} km`, `${s.Gods} gods`],
        `${s.Settlements} villages at the start, up to ${s.Rivers} rivers (${s.MainRivers} to the sea).`,
        `As many as ${s.PantheonMaxGods} allied gods can win together.`,
      )),
    };
  },
  async Biomes() {
    const [world, palette] = await Promise.all([content('worldgen.json'), content('biome_palette.json')]);
    const percent = (v) => `${Math.round(v * 100)}%`;
    const landforms = ['Coast', 'RiverValley', 'Island', 'Volcanic'];
    const rules = world.Biomes.filter((b) => b.Biome !== 'Sea');
    const swatch = (name) => `<i class="swatch" style="background:${palette[name] ?? '#888'};width:14px;height:14px;margin-right:8px"></i>`;
    return {
      intro: 'The land the generator makes: biomes from the climate (elevation, temperature, moisture) and landforms from the place. Fertility sets what a village can grow; vegetation how much wood and wildlife the land holds.',
      entries: [
        ...rules.map((b) => entry(words(b.Biome), ['Climate'], `Fertility ${percent(b.BaseFertility)} · vegetation ${percent(b.BaseVegetation)}.`, '', swatch(b.Biome))),
        ...landforms.map((name) => entry(words(name), ['Landform'], {
          Coast: 'The low land by the sea.',
          RiverValley: 'The low land along rivers and lakes: the richest land, where villages crowd.',
          Island: 'Islands off the main one.',
          Volcanic: 'The dark land around the highest peak, which is the volcano: fertile ash, bare and full of stone.',
        }[name], '', swatch(name))),
      ],
    };
  },
};

async function show(name, panel, intro) {
  const tab = await TABS[name]();
  intro.textContent = tab.intro;
  panel.innerHTML = tab.entries.join('');
}

initChrome();
const tabs = document.querySelector('#codex-tabs');
const panel = document.querySelector('#entries');
const intro = document.querySelector('#codex-intro');
const search = document.querySelector('#codex-search');
const names = Object.keys(TABS);
tabs.innerHTML = names.map((name, i) => `<button type="button" role="tab" aria-selected="${i === 0}" data-tab="${name}">${name}</button>`).join('');
const select = async (name) => {
  tabs.querySelectorAll('button').forEach((b) => b.setAttribute('aria-selected', String(b.dataset.tab === name)));
  search.value = '';
  try {
    await show(name, panel, intro);
  } catch (error) {
    panel.innerHTML = `<p class="empty">Could not read the game's content (${escapeHtml(error.message)}).</p>`;
  }
  history.replaceState(null, '', `#${encodeURIComponent(name)}`);
};
tabs.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-tab]');
  if (button) {
    select(button.dataset.tab);
  }
});
search.addEventListener('input', () => {
  const query = search.value.trim().toLowerCase();
  let shown = 0;
  panel.querySelectorAll('.entry').forEach((el) => {
    const match = query.length === 0 || el.dataset.text.includes(query);
    el.hidden = !match;
    shown += match ? 1 : 0;
  });
  panel.querySelector('.empty')?.remove();
  if (shown === 0) {
    panel.insertAdjacentHTML('beforeend', '<p class="empty">Nothing here by that name.</p>');
  }
});
const start = decodeURIComponent(location.hash.slice(1));
select(names.includes(start) ? start : names[0]);
