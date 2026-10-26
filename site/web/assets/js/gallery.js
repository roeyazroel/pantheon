// The gallery: screenshots the game took of itself, opened large in a dialog.

import { escapeHtml } from './content.js';

export const SHOTS = [
  ['pantheon.jpg', 'The gods of a new world, born with it.'],
  ['village.jpg', 'Duskwater after fifteen minutes: a rival heals it while Bramble helps build.'],
  ['talk.jpg', 'Talking with Wynlius the Iron Hand.'],
  ['proposal.jpg', 'Ashsaeth proposes a pact in the middle of a conversation.'],
  ['fight.jpg', 'Bramble and Pip box; the loser is banished with a scar.'],
  ['reward.jpg', 'Rewarded, Bramble says so.'],
  ['eruption.jpg', 'The volcano wakes.'],
  ['storm.jpg', 'A storm floods Duskwater.'],
  ['beast.jpg', 'Charos the Ancient comes down from its lair.'],
  ['fire.jpg', 'A wildfire, and people on the road.'],
  ['religion.jpg', 'The Religion overlay: whose faith holds which land.'],
  ['diplomacy.jpg', 'Every god remembers what you did.'],
  ['book.jpg', 'The Book of the World.'],
  ['minds.jpg', 'Choosing a mind for the gods.'],
  ['title.jpg', 'The title screen, over the living island.'],
  ['creature.jpg', 'Bramble among the huts of its home.'],
];

/** Builds the gallery and its dialog. */
export function initGallery(root) {
  const grid = root.querySelector('#gallery');
  const dialog = root.querySelector('#lightbox');
  const big = dialog.querySelector('img');
  const caption = dialog.querySelector('p');
  grid.innerHTML = SHOTS.map(([file, text], i) => `<figure>
    <button type="button" data-shot="${i}" aria-label="Open: ${escapeHtml(text)}"><img src="images/${file}" alt="${escapeHtml(text)}" loading="lazy" width="1600" height="900"></button>
    <figcaption>${escapeHtml(text)}</figcaption></figure>`).join('');
  grid.addEventListener('click', (event) => {
    const button = event.target.closest('button[data-shot]');
    if (!button) {
      return;
    }
    const [file, text] = SHOTS[Number(button.dataset.shot)];
    big.src = `images/${file}`;
    big.alt = text;
    caption.textContent = text;
    dialog.showModal();
  });
  dialog.querySelector('button').addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', (event) => {
    if (event.target === dialog) {
      dialog.close();
    }
  });
}
