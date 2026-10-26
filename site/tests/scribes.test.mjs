// The site's scribes write what the game's scribes write: every pantheon in the exporter's fixture,
// which the C# PersonaScribe and PersonaText wrote, word for word (ADR-076).

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { angelName } from '../web/assets/js/angel.js';
import { kindOf, nature, temperNumbers, title, writePantheon } from '../web/assets/js/scribes.js';
import { readExport } from './support.mjs';

const content = {
  lore: readExport('data', 'minds.json').Lore,
  gods: readExport('data', 'gods.json'),
  sizes: readExport('data', 'world_sizes.json'),
};
const angelNames = readExport('data', 'angel.json').Names;
const cases = readExport('fixtures', 'scribes.json');

test('the fixture holds pantheons of every size', () => {
  assert.ok(cases.length >= 50);
  assert.deepEqual(new Set(cases.map((c) => c.size)), new Set(content.sizes.Sizes.map((s) => s.Name)));
});

for (const expected of cases) {
  test(`seed "${expected.seed}", ${expected.size}, ${expected.species ?? 'the default species'}`, () => {
    const written = writePantheon(content, expected.seed, expected.size, expected.species);
    assert.equal(written.seed.toString(), expected.seedValue);
    assert.equal(angelName(angelNames, written.seed, 1), expected.angel);
    assert.equal(written.gods.length, expected.gods.length);
    written.gods.forEach((god, i) => {
      const want = expected.gods[i];
      assert.deepEqual(
        {
          name: god.name,
          creature: god.creature,
          species: god.species,
          personality: god.personality,
          title: title(god),
          kind: kindOf(god),
          history: god.persona?.history ?? '',
          nature: nature(god),
          temper: god.persona ? temperNumbers(god.persona.temper) : '',
        },
        want,
      );
    });
  });
}
