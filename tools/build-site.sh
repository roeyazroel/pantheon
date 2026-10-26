#!/usr/bin/env bash
#
# Builds PANTHEON's website (ADR-076) into build/site, for GitHub Pages or to look at locally:
#
#   1. site/export, built against the game's own projects, writes the content as strict JSON, the
#      islands the world generator makes for a few seeds, and fixtures of what the game's scribes
#      and angel answer;
#   2. the site's tests check its JavaScript ports of the scribes and the angel against those
#      fixtures (they need Node.js 20 or newer);
#   3. the pages in site/web, the screenshots in docs/images, the game's fonts and the pinned,
#      checksummed three.js are put together.
#
# Nothing it writes is committed: build/ is ignored. The Pages workflow runs it on every push to
# main that changes the site, the game's content or its code.

set -euo pipefail
IFS=$'\n\t'

readonly THREE_VERSION="0.170.0"
readonly THREE_URL="https://registry.npmjs.org/three/-/three-${THREE_VERSION}.tgz"
readonly THREE_SHA256="4a608a355dcaba72e0e5383cdc814303f5b6060b43c238cdf6932dceb699238d"
readonly EXIT_USAGE=2

# Prints usage information.
print_usage() {
  cat <<EOF
Usage: tools/build-site.sh [--out <directory>] [--serve [<port>]] [--skip-tests] [--help]

Builds the website into build/site (or --out).

Options:
  --out <directory>  Where to put the site (default: build/site).
  --serve [<port>]   Serve the built site at http://localhost:<port>/ (default 8000) until stopped.
  --skip-tests       Do not run the site's tests (they need Node.js 20 or newer).
  --help             Show this help and exit.

Needs the .NET SDK, curl and tar. three.js ${THREE_VERSION} is downloaded once into build/cache and
checked against its pinned SHA-256.
EOF
}

# Prints an error message and exits with the given status.
fail() {
  local message="$1"
  local status="${2:-1}"
  printf 'build-site: %s\n' "${message}" >&2
  exit "${status}"
}

# Fails unless the named command is available.
require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || fail "required command '${command_name}' not found"
}

# Prints the SHA-256 of a file using whichever tool the platform provides.
compute_sha256() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "${file}" | cut -d ' ' -f 1
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "${file}" | cut -d ' ' -f 1
  else
    fail "neither sha256sum nor shasum is available"
  fi
}

# Downloads three.js into the cache unless it is there, and checks it.
fetch_three() {
  local cache="$1"
  local archive="${cache}/three-${THREE_VERSION}.tgz"
  mkdir -p "${cache}"
  if [[ ! -f "${archive}" ]]; then
    require_command curl
    printf 'Downloading %s\n' "${THREE_URL}" >&2
    curl --fail --location --silent --show-error --retry 3 --output "${archive}.part" "${THREE_URL}"
    mv "${archive}.part" "${archive}"
  fi
  local actual
  actual="$(compute_sha256 "${archive}")"
  if [[ "${actual}" != "${THREE_SHA256}" ]]; then
    rm -f "${archive}"
    fail "checksum mismatch for three.js ${THREE_VERSION}: expected ${THREE_SHA256}, got ${actual}"
  fi
  printf '%s\n' "${archive}"
}

# Copies three.js's module, the orbit controls and its licence into the site.
vendor_three() {
  local archive="$1"
  local destination="$2"
  local work
  work="$(mktemp -d)"
  tar -xzf "${archive}" -C "${work}" package/build/three.module.min.js package/examples/jsm/controls/OrbitControls.js package/LICENSE
  mkdir -p "${destination}/addons/controls"
  cp "${work}/package/build/three.module.min.js" "${destination}/three.module.min.js"
  cp "${work}/package/examples/jsm/controls/OrbitControls.js" "${destination}/addons/controls/OrbitControls.js"
  cp "${work}/package/LICENSE" "${destination}/LICENSE"
  rm -rf "${work}"
}

# Runs the site's tests against the exporter's output.
run_tests() {
  local data="$1"
  if ! command -v node >/dev/null 2>&1; then
    fail "Node.js is needed for the site's tests (or pass --skip-tests)"
  fi
  SITE_DATA="${data}" node --test site/tests/*.test.mjs
}

# Puts the site together.
assemble() {
  local root="$1"
  local data="$2"
  local out="$3"
  local three="$4"
  rm -rf "${out}"
  mkdir -p "${out}"
  cp -R "${root}/site/web/." "${out}/"
  mkdir -p "${out}/data" "${out}/islands" "${out}/images" "${out}/fonts" "${out}/vendor/three"
  cp "${data}/data/"*.json "${out}/data/"
  cp "${data}/islands/"* "${out}/islands/"
  cp "${root}/docs/images/"*.jpg "${out}/images/"
  cp "${root}/game/Fonts/"*.ttf "${root}/game/Fonts/"*-OFL.txt "${out}/fonts/"
  vendor_three "${three}" "${out}/vendor/three"
  # GitHub Pages serves the files as they are, without Jekyll.
  touch "${out}/.nojekyll"
}

# Parses arguments, then exports, tests, assembles and optionally serves.
main() {
  local out=""
  local serve=false
  local port="8000"
  local tests=true
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --help | -h)
        print_usage
        return 0
        ;;
      --out)
        [[ $# -ge 2 && -n "$2" ]] || { print_usage >&2; fail "--out needs a directory" "${EXIT_USAGE}"; }
        out="$2"
        shift 2
        ;;
      --serve)
        serve=true
        if [[ $# -ge 2 && "$2" =~ ^[0-9]+$ ]]; then
          port="$2"
          shift
        fi
        shift
        ;;
      --skip-tests)
        tests=false
        shift
        ;;
      *)
        print_usage >&2
        fail "unknown argument '$1'" "${EXIT_USAGE}"
        ;;
    esac
  done

  require_command dotnet
  require_command tar
  local root
  root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
  cd "${root}"
  out="${out:-${root}/build/site}"
  local data="${root}/build/site-data"

  printf 'Exporting the game'"'"'s data\n'
  rm -rf "${data}"
  dotnet run --project site/export/Pantheon.SiteExport.csproj --configuration Release -- --data game/Data --out "${data}"

  if [[ "${tests}" == true ]]; then
    printf 'Testing the site against the game\n'
    run_tests "${data}"
  fi

  local three
  three="$(fetch_three "${root}/build/cache")"
  assemble "${root}" "${data}" "${out}" "${three}"
  printf 'Built the site in %s\n' "${out}"

  if [[ "${serve}" == true ]]; then
    require_command python3
    printf 'Serving http://localhost:%s/ (Ctrl+C to stop)\n' "${port}"
    python3 -m http.server "${port}" --directory "${out}"
  fi
}

main "$@"
