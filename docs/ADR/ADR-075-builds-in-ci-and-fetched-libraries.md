# ADR-075: Release builds in CI, and Terrain3D's libraries fetched rather than committed

- Status: Accepted (amends ADR-005 and ADR-031)
- Date: 2026-10-25

## Context

The owner: "We are building the installers locally right now. I want it to be in GitHub CI
instead, cross OS, and remove the artifacts from git source code."

### Before

- **The builds** were exported on the owner's Mac with the presets of ADR-031:
  - Windows x64 and Linux x64 were cross-exported there, and never run;
  - the macOS app was signed ad hoc with Apple's `codesign`, which only runs on a Mac.
- **The export templates** (1.2 GB) were installed by hand in the editor.
- **`build/` was already ignored.** No build output was ever committed.
- **The binaries in git** were Terrain3D's six native libraries (about 20 MB), committed by ADR-005.
  Every upgrade would add more. `tools/vendor-terrain3d.sh` could already reproduce them from the
  pinned release archive.

### Our constraints

- **No infrastructure without need** (§134). This task needs CI.
- **No cloud dependency at runtime.** The builds still run fully offline. CI only makes them.
- **Cost.** The repository is private, so a macOS runner's minute counts ten times and a Windows
  minute twice. The owner pushes several times a day.

## Decision

### One workflow, `.github/workflows/build.yml`

| Job | Runs on | When | What |
| --- | --- | --- | --- |
| Build and test | Ubuntu | every push to `main`, every pull request, by hand, and on tags | fetches Terrain3D's libraries, `dotnet build Pantheon.sln` (warnings are errors), `dotnet test` |
| Export | Windows, macOS and Linux, each on its own system | by hand (Actions > Build > Run workflow) and on `v*` tags, after the tests pass | installs Godot, fetches the libraries, imports the project, exports its own preset, packages it and uploads it |
| Publish the release | Ubuntu | on `v*` tags, after every export | creates the GitHub release with the three packages |

- **Each platform exports on its own system.** That keeps the macOS app signed by Apple's
  `codesign` (ADR-031), and it compiles and publishes the .NET assemblies on the system that runs
  them.
- **The packages:**
  - `Pantheon-windows-x86_64.zip`;
  - `Pantheon-macos-universal.zip`, Godot's own zip of the signed app;
  - `Pantheon-linux-x86_64.tar.gz`, a tarball, so the executable bit survives.

  Each is uploaded as it is (`archive: false`), so it downloads as itself from the run's page.
- **Exports are not made on every push.** A run on three systems costs about a hundred billed
  minutes in a private repository. The tests are cheap and run on every push.

### `tools/install-godot.sh`

- It installs Godot 4.7.2 .NET (ADR-001) for the system it runs on: the editor goes in `--dest`,
  and that platform's export templates go in Godot's per-user `export_templates` folder.
- It checks every download against its SHA-512 from Godot's `SHA512-SUMS.txt`, pinned in the
  script.
- It takes only the platform's own templates from the 1.2 GB archive. CI caches them with the
  editor, keyed by the script's hash, so a new pin downloads afresh.
- On Windows it returns the console executable, which prints the editor's output and passes on its
  exit code.

### Terrain3D's libraries are fetched, not committed (amends ADR-005)

- `game/addons/terrain_3d/bin/` is ignored. The rest of the addon is still committed: the
  licence, the readme, the trimmed manifest, the icon and its import file.
- `tools/vendor-terrain3d.sh` now fetches only the libraries, from the same archive and checksum.
  With `--all` it re-creates the whole addon, as before, for an upgrade.
- After cloning, run `tools/vendor-terrain3d.sh` and then `godot --headless --path game --import`.
  CI runs it before building.
- `ArchitectureRulesTests` still requires every desktop library. A clone that has not fetched them
  fails that test, and its message says to run the script.

## Consequences

- Positive: the Windows and Linux builds are compiled, exported and packaged on Windows and Linux.
- Positive: anyone with access can start a build, and a tag makes a release, without the owner's Mac.
- Positive: the repository no longer grows with every Terrain3D upgrade.
- Positive: the tests run on Linux on every push. They passed there (1921) before this change.
- Negative: a fresh clone needs one more step, and the network, before the game runs on Terrain3D.
  Without it, the game still runs on the fallback renderer and says so (ADR-005).
- Negative: the libraries stay in the history before this change, so the clone does not shrink.
  Rewriting `main`'s history was not worth breaking every existing clone.
- Negative: the macOS app is still signed ad hoc, now on the runner. Downloaded, it is quarantined,
  and Gatekeeper blocks it until the player clears the quarantine, with
  `xattr -dr com.apple.quarantine Pantheon.app` or Privacy & Security > Open Anyway. Signing with a
  developer identity and notarizing still need the owner's Apple account, given to CI as secrets.
- Negative: the packages are archives, not installers. An Inno Setup installer, a disk image or an
  AppImage can be added to the Package step when wanted.
- When ADR-001's pin moves, `tools/install-godot.sh`'s version and checksums move with it, as do
  the presets' templates.

## Alternatives considered

- **Exporting all three from one Linux runner:** the cheapest option, but `codesign` needs macOS.
  Godot's built-in signing adds the hardened runtime, which macOS 26 kills (ADR-031).
- **Exporting all three from one macOS runner:** one job, but every minute costs ten times, and
  Windows and Linux would still be cross-built.
- **A community action to install Godot:** it would work, but a pinned, checksummed script, like
  the Terrain3D one, also runs locally and can be checked with shellcheck.
- **Git LFS for the libraries (ADR-005's suggestion):** it keeps binaries in the repository's
  storage and quota, and every clone still needs LFS. The pinned archive is already a reproducible
  source.
- **Exporting on every push:** builds would always be at hand, but in a private repository they
  would use up the free minutes within days.
