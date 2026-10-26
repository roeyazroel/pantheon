# The website

PANTHEON's website, published with GitHub Pages and built from the game's own code and data
([ADR-076](../docs/ADR/ADR-076-a-website-built-from-the-game.md)).

- `web/`: the pages, the stylesheet and the scripts, including the JavaScript ports of the scribes
  and the angel.
- `export/`: `Pantheon.SiteExport`, which writes the content as strict JSON, islands from the world
  generator, and fixtures of the game's own answers.
- `tests/`: the ports' checks against those fixtures (`node:test`).

`tools/build-site.sh --serve` builds it into `build/site` and serves it at http://localhost:8000/.
[docs/DEVELOPMENT.md](../docs/DEVELOPMENT.md#the-website) says more.
