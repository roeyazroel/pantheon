# ADR-076: A website on GitHub Pages, built from the game's own code and data

- Status: Accepted
- Date: 2026-10-26

## Context

The owner: "I want to create the most ambitious web site for the game based on GitHub pages." The
README had just been rewritten around the gods' minds (ADR-065, ADR-066, ADR-072, ADR-074), which
the owner calls the big thing in the game.

### Our constraints

- **No infrastructure without need** (§134). A website needs a build and a host. GitHub Pages
  serves static files, free for a public repository, with nothing to run or pay for.
- **No cloud dependency at runtime.** The game is untouched: the site describes it.
- **Honesty.** A site that shows the game should show the game, not an imitation. Screenshots are
  the game's own (README, ADR-031's `--screenshot`), and anything interactive should give the
  answers the game gives.
- **The repository's tools.** Development scripts are bash, the build is .NET, and nothing built
  is committed (ADR-075).

## Decision

### What the site is

A static site in `site/web`: two pages (the home page and a codex) and a 404 page, in plain HTML,
CSS and JavaScript modules, with no framework and no bundler. It uses the game's fonts (Cinzel and
Alegreya Sans, `game/Fonts`) and colours.

| Part | What it shows | Where it comes from |
| --- | --- | --- |
| The hero and "A living island" | an island in 3D: heights, biomes, fresh water, villages with their gods, holy sites and Creatures | the world generator, through the exporter |
| Meet your pantheon | the rival gods the scribes write for any seed, size and species, with the angel's name | a JavaScript port of `PersonaScribe`, `PersonaText` and `WorldMaker.PantheonFor` |
| Your angel | the angel's stage, form, conflict, the traits and their marks, for any deeds | a port of `AngelLooks`, `ConductModel.Traits` and `AngelContent.NameFor` |
| Talk to them | how far goodwill leans a deal or a hostile act | `TalkAct.Willingness` and `GodTalk` with `minds.json`'s `Talk` numbers |
| Codex | miracles, domains, personalities, species, holy sites, world sizes and biomes | the content files themselves |
| Gallery and screenshots | the game | `docs/images`, taken by the game |
| Download | the latest release's packages, this computer's first | the GitHub releases API, at view time, with a link to the releases page when it cannot be reached |

### The game's code, ported and checked

The scribes and the angel are pure functions of the seed and the content. They are ported to
JavaScript (`site/web/assets/js/random.js`, `scribes.js`, `angel.js`):

- the seeded randomness: FNV-1a, SplitMix64 and xoshiro256** in `BigInt`, and Lemire's bounded
  integers;
- the game's float arithmetic, with `Math.fround` at every step, and .NET's round-half-to-even;
- `OrdinalIgnoreCase` name sets, stable sorting, and the same order of draws.

**Parity is tested, not assumed.** `site/export` (`Pantheon.SiteExport`, a console project in the
solution) runs the C# code and writes fixtures: the gods the scribes write for 63 seeds (numbers,
words, the largest seed and one too large, every size, several species) and the angel's look and
traits for 400 conducts reaching every stage and trait. `site/tests` (`node:test`, no packages)
checks the ports against them word for word and float for float. A change to the C# that the ports
do not follow fails the site's build.

### Data from the game, never committed

The exporter also writes:

- **`data/`:** every file of `game/Data`, as strict JSON (the comments removed), which the pages
  read. The site therefore always says what the game's content says.
- **`islands/`:** three islands (seeds 1234, 7 and "first light", the small size, the scribes'
  gods): a 257 × 257 grid of heights, fresh water surfaces and biomes in a little-endian binary
  file, and the villages, gods, Creatures, holy sites and rivers in JSON.

### three.js, fetched and checked

The island is drawn with three.js 0.170.0 (MIT). Like Terrain3D's libraries (ADR-075), it is not
committed: `tools/build-site.sh` downloads its npm package once, checks it against a pinned SHA-256,
and copies the module, the orbit controls and the licence. The pages import it through an import
map.

### Building and publishing

`tools/build-site.sh` exports, tests, copies the pages, the screenshots and the fonts, vendors
three.js into `build/site`, and with `--serve` serves it locally.

`.github/workflows/pages.yml`, the second workflow:

| When | What |
| --- | --- |
| a pull request that touches the site, the code, the content, the fonts or the screenshots | builds and tests the site |
| a push to `main` that touches them, or by hand | also publishes it with GitHub Pages (`upload-pages-artifact`, `deploy-pages`) |

The repository's Settings > Pages must choose GitHub Actions as the source, once.

## Consequences

- **The site shows the game as it is.** The islands, the gods, the angel, the codex and the
  screenshots all come from the game; a content change reaches the site on its next build.
- **A second implementation to keep.** The scribes and the angel exist in C# and JavaScript. The
  fixtures make any divergence fail a build, so it is caught, not shipped. The ports are about 430
  lines.
- **The exporter is in the solution,** so `dotnet build Pantheon.sln` compiles it against the
  current code, and the Build workflow catches an API change before the Site workflow runs.
- **A bigger CI bill, slightly.** The Site workflow runs only for changes that can change the site,
  on Ubuntu, in a few minutes.
- **The site is public.** It shows what the README and the screenshots already show.
- **Browsers without WebGL** see a screenshot behind the title and a note in place of the island.

## Alternatives considered

- **Compiling the game's C# to WebAssembly** and running the real scribes, or the whole
  simulation, in the browser. It would need no port, but it adds .NET's WebAssembly workload to the
  build and several megabytes of runtime to every visit, for two small pure functions. The ports,
  checked against the C#, give the same answers.
- **Precomputing the gods for a list of seeds.** No port, but visitors could not type their own
  seed, which is the point.
- **A static-site generator (Jekyll, Hugo, Astro).** More tooling for two pages. Plain files serve.
- **Committing the island data and three.js.** Simpler to build, but data would drift from the
  generator, and ADR-075 keeps third-party binaries and build output out of git.
- **A three.js CDN.** One fewer download in the build, but the site would depend on a third party
  at view time. The vendored copy is checked once and served with the site.
