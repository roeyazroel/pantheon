// The home page: the bar, the reveals, the islands and every interactive section (ADR-076). Each
// part starts on its own, so one that fails leaves the rest of the page working.

import { initChrome } from './chrome.js';
import { content, escapeHtml } from './content.js';
import { initAngel } from './angel-ui.js';
import { initDownload } from './download.js';
import { initGallery } from './gallery.js';
import { initPantheon } from './pantheon-ui.js';
import { initTalk } from './talk-ui.js';

// Without WebGL, or when the island cannot be loaded, the hero shows a screenshot instead.
function islandsUnavailable() {
  document.querySelector('.hero').classList.add('no-webgl');
  document.querySelector('#explorer').innerHTML = '<p class="notice" style="padding:24px">This browser cannot draw the island in 3D. The gallery below shows it from the game.</p>';
}

async function initIslands() {
  const explorer = document.querySelector('#explorer');
  const { canDraw, IslandView, loadIsland } = await import('./island.js');
  if (!canDraw()) {
    islandsUnavailable();
    return;
  }
  const [palette, view, index] = await Promise.all([
    content('biome_palette.json'),
    content('divine_view.json'),
    fetch('islands/index.json').then((r) => r.json()),
  ]);
  const small = matchMedia('(max-width: 720px)').matches;
  const heroView = new IslandView(document.querySelector('.hero-canvas'), { palette, godColors: view.GodColors, detail: small ? 2 : 1, labels: false, sky: true });
  const first = await loadIsland(index[0].file);
  heroView.show(first);

  const islandView = new IslandView(explorer, { interactive: true, palette, godColors: view.GodColors, detail: small ? 2 : 1, sky: true });
  islandView.show(first);
  const tabs = document.querySelector('#seed-tabs');
  tabs.innerHTML = index.map((island, i) => `<button type="button" data-file="${island.file}" aria-pressed="${i === 0}">Seed “${escapeHtml(island.seed)}”</button>`).join('');
  tabs.addEventListener('click', async (event) => {
    const button = event.target.closest('button[data-file]');
    if (!button) {
      return;
    }
    tabs.querySelectorAll('button').forEach((b) => b.setAttribute('aria-pressed', String(b === button)));
    islandView.show(await loadIsland(button.dataset.file));
  });
  document.querySelector('#labels-toggle').addEventListener('change', (event) => islandView.setLabels(event.target.checked));
  const legend = document.querySelector('#legend');
  const biomes = ['Grassland', 'Forest', 'Highland', 'RiverValley', 'Coast', 'Swamp', 'Desert', 'Tundra', 'Volcanic'];
  legend.innerHTML = biomes.map((b) => `<span><i class="swatch" style="background:${palette[b]}"></i>${b.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>`).join('')
    + `<span><i class="swatch" style="background:${palette.ShallowWater}"></i>Water</span>`;
}

async function initSpecies() {
  const creatures = await content('creatures.json');
  const stats = [['Toughness', 'Toughness'], ['Strength', 'Strength'], ['Speed', 'Speed'], ['Learning', 'Learning']];
  document.querySelector('#species-grid').innerHTML = creatures.Species.map((s) => `<article class="animal">
    <h4>${escapeHtml(s.Name)}</h4><p>${escapeHtml(s.Description)}</p>
    <div class="stats">${stats.map(([key, label]) => `<span>${label}</span><span class="stat"><span style="width:${Math.min(100, (s[key] / 1.6) * 100)}%"></span></span>`).join('')}</div>
  </article>`).join('');
}

function start(name, task) {
  task().catch((error) => console.error(`${name}:`, error));
}

initChrome();
initIslands().catch((error) => {
  console.error('islands:', error);
  islandsUnavailable();
});
start('pantheon', () => initPantheon(document.querySelector('#pantheon')));
start('talk', () => initTalk(document.querySelector('#talk')));
start('angel', () => initAngel(document.querySelector('#angel')));
start('species', initSpecies);
start('download', () => initDownload(document.querySelector('#download')));
initGallery(document.querySelector('#gallery-section'));
document.querySelector('#forge').addEventListener('submit', (event) => event.preventDefault());
if (location.hash.startsWith('#pantheon?')) {
  document.querySelector('#pantheon').scrollIntoView();
}
