#!/usr/bin/env bash
#
# Reproduces the vendored Terrain3D runtime subset in game/addons/terrain_3d from the pinned
# upstream release archive (see docs/ADR/ADR-005-terrain3d-for-terrain.md).
#
# Only what the game needs at runtime on desktop is kept: the licence, the readme, the extension
# manifest (trimmed to the shipped binaries), the node icon and the Windows/Linux x86_64 and
# macOS debug/release libraries. The editor plugin, brushes, extras and mobile/web binaries are
# left out because PANTHEON generates its terrain at runtime.

set -euo pipefail
IFS=$'\n\t'

readonly TERRAIN3D_VERSION="1.0.2-stable"
readonly ARCHIVE_NAME="Terrain3D_v${TERRAIN3D_VERSION}.zip"
readonly ARCHIVE_URL="https://github.com/TokisanGames/Terrain3D/releases/download/v${TERRAIN3D_VERSION}/${ARCHIVE_NAME}"
readonly ARCHIVE_SHA256="a071850250ec5e596aa54da61c01d75768774eb379ee997584d426a45f4884a2"
readonly ADDON_PATH_IN_ARCHIVE="addons/terrain_3d"
readonly DESTINATION_RELATIVE_PATH="game/addons/terrain_3d"
readonly MANIFEST_NAME="terrain.gdextension"
readonly EXIT_USAGE=2

# Files copied verbatim, relative to the addon directory.
readonly -a RUNTIME_FILES=(
  "LICENSE.txt"
  "README.md"
  "terrain.gdextension.uid"
  "icons/terrain3d.svg"
  "icons/terrain3d.svg.import"
  "bin/libterrain.linux.debug.x86_64.so"
  "bin/libterrain.linux.release.x86_64.so"
  "bin/libterrain.windows.debug.x86_64.dll"
  "bin/libterrain.windows.release.x86_64.dll"
  "bin/libterrain.macos.debug.framework/libterrain.macos.debug"
  "bin/libterrain.macos.release.framework/libterrain.macos.release"
)

# Prints usage information.
print_usage() {
  cat <<EOF
Usage: tools/vendor-terrain3d.sh [--archive <path>] [--help]

Re-creates ${DESTINATION_RELATIVE_PATH} with the Terrain3D ${TERRAIN3D_VERSION} runtime subset.

Options:
  --archive <path>  Use a local copy of ${ARCHIVE_NAME} instead of downloading it.
  --help            Show this help and exit.

The archive is always checked against its pinned SHA-256 before anything is copied.
EOF
}

# Prints an error message and exits with the given status.
fail() {
  local message="$1"
  local status="${2:-1}"
  printf 'vendor-terrain3d: %s\n' "${message}" >&2
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

# Downloads the pinned release archive to the given path.
download_archive() {
  local target="$1"
  require_command curl
  printf 'Downloading %s\n' "${ARCHIVE_URL}"
  curl --fail --location --silent --show-error --retry 3 --output "${target}" "${ARCHIVE_URL}"
}

# Fails when the archive does not match the pinned checksum.
verify_archive() {
  local archive="$1"
  local actual
  actual="$(compute_sha256 "${archive}")"
  if [[ "${actual}" != "${ARCHIVE_SHA256}" ]]; then
    fail "checksum mismatch for ${archive}: expected ${ARCHIVE_SHA256}, got ${actual}"
  fi
  printf 'Verified SHA-256 %s\n' "${actual}"
}

# Fails when any runtime file is missing from the extracted addon directory.
check_runtime_files() {
  local addon_dir="$1"
  local file
  for file in "${RUNTIME_FILES[@]}"; do
    [[ -f "${addon_dir}/${file}" ]] || fail "archive is missing ${ADDON_PATH_IN_ARCHIVE}/${file}"
  done
}

# Copies the runtime files into the destination, keeping their relative paths.
copy_runtime_files() {
  local addon_dir="$1"
  local destination="$2"
  local file
  for file in "${RUNTIME_FILES[@]}"; do
    mkdir -p "$(dirname "${destination}/${file}")"
    cp "${addon_dir}/${file}" "${destination}/${file}"
  done
}

# Writes the extension manifest, listing only the libraries that are vendored.
write_manifest() {
  local destination="$1"
  cat >"${destination}/${MANIFEST_NAME}" <<'EOF'
[configuration]

entry_symbol = "terrain_3d_init"
compatibility_minimum = 4.4

[icons]

Terrain3D = "res://addons/terrain_3d/icons/terrain3d.svg"

[libraries]

windows.debug.x86_64 = "res://addons/terrain_3d/bin/libterrain.windows.debug.x86_64.dll"
windows.release.x86_64 = "res://addons/terrain_3d/bin/libterrain.windows.release.x86_64.dll"

linux.debug.x86_64 = "res://addons/terrain_3d/bin/libterrain.linux.debug.x86_64.so"
linux.release.x86_64 = "res://addons/terrain_3d/bin/libterrain.linux.release.x86_64.so"

macos.debug = "res://addons/terrain_3d/bin/libterrain.macos.debug.framework"
macos.release = "res://addons/terrain_3d/bin/libterrain.macos.release.framework"
EOF
}

# Parses arguments, then downloads (or reuses), verifies, extracts and copies the subset.
main() {
  local archive=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --help | -h)
        print_usage
        return 0
        ;;
      --archive)
        [[ $# -ge 2 && -n "$2" ]] || { print_usage >&2; fail "--archive needs a path" "${EXIT_USAGE}"; }
        archive="$2"
        shift 2
        ;;
      *)
        print_usage >&2
        fail "unknown argument '$1'" "${EXIT_USAGE}"
        ;;
    esac
  done

  require_command unzip
  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local destination
  destination="$(cd "${script_dir}/.." && pwd)/${DESTINATION_RELATIVE_PATH}"

  local work_dir
  work_dir="$(mktemp -d)"
  # Expand now: work_dir is local and no longer set when the EXIT trap runs.
  # shellcheck disable=SC2064
  trap "rm -rf '${work_dir}'" EXIT

  if [[ -z "${archive}" ]]; then
    archive="${work_dir}/${ARCHIVE_NAME}"
    download_archive "${archive}"
  fi
  [[ -f "${archive}" ]] || fail "archive not found: ${archive}"
  verify_archive "${archive}"

  unzip -q "${archive}" "${ADDON_PATH_IN_ARCHIVE}/*" -d "${work_dir}/extracted"
  local addon_dir="${work_dir}/extracted/${ADDON_PATH_IN_ARCHIVE}"
  check_runtime_files "${addon_dir}"

  rm -rf "${destination}"
  mkdir -p "${destination}"
  copy_runtime_files "${addon_dir}" "${destination}"
  write_manifest "${destination}"
  printf 'Vendored Terrain3D %s into %s\n' "${TERRAIN3D_VERSION}" "${destination}"
}

main "$@"
