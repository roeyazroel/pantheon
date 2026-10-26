// What the site's tests read: the exporter's output (tools/build-site.sh writes it), from
// SITE_DATA or build/site-data at the repository's root.

import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const siteData = process.env.SITE_DATA ? resolve(process.env.SITE_DATA) : join(root, 'build', 'site-data');

/** A JSON file of the exporter's output, by its path under the output directory. */
export function readExport(...parts) {
  return JSON.parse(readFileSync(join(siteData, ...parts), 'utf8'));
}
