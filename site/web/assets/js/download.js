// The download box: the latest release's packages from GitHub, the one for this computer first.
// With no release, or no network, it points to the releases page and to building from source.

const REPO = 'roeyazroel/pantheon';

const PACKAGES = [
  { system: 'Windows', match: /windows/i, label: 'Windows (64-bit)' },
  { system: 'macOS', match: /mac/i, label: 'macOS (Apple Silicon and Intel)' },
  { system: 'Linux', match: /linux/i, label: 'Linux (64-bit)' },
];

function thisSystem() {
  const platform = navigator.userAgentData?.platform || navigator.platform || navigator.userAgent;
  return /win/i.test(platform) ? 'Windows' : /mac|iphone|ipad/i.test(platform) ? 'macOS' : /linux|x11/i.test(platform) ? 'Linux' : null;
}

/** Fills the download box. */
export async function initDownload(root) {
  const assets = root.querySelector('#assets');
  const status = root.querySelector('#release-status');
  const system = thisSystem();
  try {
    const response = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: 'application/vnd.github+json' } });
    if (!response.ok) {
      throw new Error(String(response.status));
    }
    const release = await response.json();
    const found = PACKAGES.map((p) => ({ ...p, asset: release.assets.find((a) => p.match.test(a.name)) })).filter((p) => p.asset);
    if (found.length === 0) {
      throw new Error('no packages');
    }
    found.sort((a, b) => (b.system === system) - (a.system === system));
    assets.innerHTML = found.map((p, i) => `<a class="button${i === 0 && p.system === system ? '' : ' ghost'}" href="${p.asset.browser_download_url}">
      ${p.label}<span class="muted" style="font-family:var(--body);font-weight:400">${(p.asset.size / 1048576).toFixed(0)} MB</span></a>`).join('');
    status.textContent = `${release.name || release.tag_name}, released ${new Date(release.published_at).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })}.`;
  } catch {
    assets.innerHTML = `<a class="button" href="https://github.com/${REPO}/releases">See the releases</a>
      <a class="button ghost" href="https://github.com/${REPO}/blob/main/docs/DEVELOPMENT.md#getting-started">Build it from source</a>`;
    status.textContent = 'Builds for Windows, macOS and Linux are published on the releases page.';
  }
}
