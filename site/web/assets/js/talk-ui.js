// "Words tip the balance": the game's rule for how far talk moves a god (ADR-066, TalkAct and
// GodTalk), with minds.json's numbers. A god does a deal when its own judgement, leaned by the
// conversation's goodwill, is above 0; a hostile act is held back by goodwill and drawn on by ill will.

import { content } from './content.js';

const SPAN = 0.4;

function place(value) {
  return `${((Math.max(-SPAN, Math.min(SPAN, value)) + SPAN) / (2 * SPAN)) * 100}%`;
}

/** Wires the talk demonstration. */
export async function initTalk(root) {
  const { Talk: rules } = await content('minds.json');
  const reach = rules.PersuasionReach;
  const acts = [
    { id: 'alliance', hostile: false, utility: -0.08 },
    { id: 'war', hostile: true, utility: -0.1 },
  ];
  let goodwill = 0;
  const show = root.querySelector('#goodwill');

  root.querySelectorAll('.scale').forEach((scale) => scale.style.setProperty('--unwilling', place(-reach)));
  root.querySelector('#reach').textContent = reach.toFixed(2);
  root.querySelector('#half-life').textContent = `${Math.round(rules.GoodwillHalfLifeSeconds / 60)} minutes`;

  const render = () => {
    show.textContent = `${goodwill >= 0 ? '+' : '−'}${Math.abs(goodwill).toFixed(2)}`;
    for (const act of acts) {
      const leaning = act.utility + (act.hostile ? -goodwill : goodwill) * reach;
      const state = leaning > 0 ? 'willing' : leaning > -reach ? 'reluctant' : 'unwilling';
      const row = root.querySelector(`[data-act="${act.id}"]`);
      row.querySelector('.mark').style.left = place(leaning);
      row.querySelector('.base').style.left = place(act.utility);
      const label = row.querySelector('.state');
      label.className = `state ${state}`;
      label.textContent = state === 'willing' ? (act.hostile ? 'It strikes' : 'It agrees') : state === 'reluctant' ? 'Could be swayed' : 'Will not';
    }
  };

  const say = (regard) => {
    goodwill = Math.max(-1, Math.min(1, goodwill + (Math.max(-3, Math.min(3, regard)) / 3) * rules.RegardStep));
    render();
  };

  root.querySelectorAll('[data-regard]').forEach((button) => button.addEventListener('click', () => say(Number(button.dataset.regard))));
  root.querySelector('#wait').addEventListener('click', () => {
    goodwill *= 0.5;
    render();
  });
  root.querySelector('#reset').addEventListener('click', () => {
    goodwill = 0;
    render();
  });
  root.querySelectorAll('input[data-utility]').forEach((input) => {
    const act = acts.find((a) => a.id === input.dataset.utility);
    input.value = act.utility;
    input.addEventListener('input', () => {
      act.utility = Number(input.value);
      render();
    });
  });
  render();
}
