// The game's content as the site reads it: game/Data's files, which the build copies to data/ as
// strict JSON (ADR-076), each fetched once.

const cache = new Map();

/** A content file by name, for example "minds.json". */
export function content(name) {
  if (!cache.has(name)) {
    cache.set(name, fetch(`data/${name}`).then((response) => {
      if (!response.ok) {
        throw new Error(`data/${name}: ${response.status}`);
      }
      return response.json();
    }));
  }
  return cache.get(name);
}

/** Text made safe to put in HTML. */
export function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch]);
}

/** Splits a game name such as "NatureKeeper" or "SacredPeak" into words. */
export function words(name) {
  return String(name).replace(/([a-z])([A-Z])/g, '$1 $2');
}
