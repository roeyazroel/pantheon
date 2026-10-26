// The site's angel looks as the game's does: every conduct in the exporter's fixture, which the C#
// AngelLooks and ConductModel read, gives the same stage, voice, traits and marks, and the same
// lean, form and conflict to the float (ADR-076).

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { look, traits } from '../web/assets/js/angel.js';
import { readExport } from './support.mjs';

const angel = readExport('data', 'angel.json');
const threshold = readExport('data', 'divine_rules.json').Culture.TraitThreshold;
const cases = readExport('fixtures', 'angel.json');

test('the fixture reaches every stage', () => {
  assert.deepEqual(new Set(cases.map((c) => c.stage)), new Set(['Radiant', 'Kind', 'Unformed', 'Torn', 'Stern', 'Fallen']));
});

cases.forEach((expected, i) => {
  test(`conduct ${i + 1}: ${expected.stage}`, () => {
    const shown = traits(expected.conduct, threshold);
    const seen = look(expected.conduct, angel, shown);
    assert.deepEqual(shown, expected.traits);
    assert.equal(seen.stage, expected.stage);
    assert.equal(seen.tone, expected.tone);
    assert.equal(seen.marks.length === 0 ? 'None' : seen.marks.join(', '), expected.marks);
    for (const key of ['lean', 'formed', 'conflict']) {
      assert.equal(seen[key], Math.fround(expected[key]), key);
    }
  });
});
