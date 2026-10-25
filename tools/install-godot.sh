#!/usr/bin/env bash
#
# Installs the pinned Godot .NET editor for this machine's platform, with that platform's export
# templates, so the game can be exported without the editor's window. The release builds in CI use
# it (.github/workflows/build.yml, see docs/ADR/ADR-075-builds-in-ci-and-fetched-libraries.md).
#
# The editor is unpacked into --dest. The export templates of this platform only (not the other
# desktops', mobile or web) are taken from the official templates archive into --dest, then copied
# to Godot's per-user export_templates folder, where an export looks for them. Every download is
# checked against its SHA-512 from Godot's SHA512-SUMS.txt. A second run reuses what --dest holds.
#
# Progress goes to stderr; the path of the Godot executable is printed on stdout.

set -euo pipefail
IFS=$'\n\t'

readonly GODOT_VERSION="4.7.2"
readonly GODOT_RELEASE="${GODOT_VERSION}-stable"
readonly TEMPLATES_FOLDER="${GODOT_VERSION}.stable.mono"
readonly DOWNLOAD_URL="https://github.com/godotengine/godot/releases/download/${GODOT_RELEASE}"
readonly TEMPLATES_ARCHIVE="Godot_v${GODOT_RELEASE}_mono_export_templates.tpz"
readonly TEMPLATES_SHA512="bb5c41d72370ed743660361f6228006f808ab04ca33abdc545d740b044f3fe057f32ae8cb7873a1bc86ddcd82ae683b9f6dfdfe4179852f2c0f1acde2ff6bd5a"
readonly EXIT_USAGE=2

# Set by select_platform.
EDITOR_ARCHIVE=""
EDITOR_SHA512=""
EDITOR_EXECUTABLE=""
TEMPLATES_ROOT=""
TEMPLATE_FILES=()

# Prints usage information.
print_usage() {
  cat <<EOF
Usage: tools/install-godot.sh --dest <directory> [--help]

Installs Godot ${GODOT_RELEASE} (.NET) for this platform into <directory>, and its export
templates for this platform into Godot's export_templates/${TEMPLATES_FOLDER} folder.
Prints the path of the Godot executable.

Options:
  --dest <directory>  Where the editor and the templates are kept (reused when run again).
  --help              Show this help and exit.

Platforms: Linux x86_64, macOS and Windows x86_64 (from Git Bash).
EOF
}

# Prints an error message and exits with the given status.
fail() {
  local message="$1"
  local status="${2:-1}"
  printf 'install-godot: %s\n' "${message}" >&2
  exit "${status}"
}

# Prints a progress message to stderr, keeping stdout for the executable's path.
say() {
  printf '%s\n' "$1" >&2
}

# Fails unless the named command is available.
require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 || fail "required command '${command_name}' not found"
}

# Converts a Windows path (C:\...) to the shell's form; other paths are printed unchanged.
unix_path() {
  local path="$1"
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -u "${path}"
  else
    printf '%s\n' "${path}"
  fi
}

# Chooses the editor archive, its executable, the templates and their folder for this machine.
select_platform() {
  local system
  system="$(uname -s)"
  case "${system}" in
    Linux)
      [[ "$(uname -m)" == "x86_64" ]] || fail "only Linux x86_64 is supported, not $(uname -m)"
      EDITOR_ARCHIVE="Godot_v${GODOT_RELEASE}_mono_linux_x86_64.zip"
      EDITOR_SHA512="1855960b27ee3ef5e66e5e228cced69d55637b24334a7411162687dcd077d8f9f645348cdb8eae984bec8135d49ed855a1e3a16476786b8bce60774fd8402d13"
      EDITOR_EXECUTABLE="Godot_v${GODOT_RELEASE}_mono_linux_x86_64/Godot_v${GODOT_RELEASE}_mono_linux.x86_64"
      TEMPLATES_ROOT="${XDG_DATA_HOME:-${HOME}/.local/share}/godot/export_templates"
      TEMPLATE_FILES=("linux_debug.x86_64" "linux_release.x86_64")
      ;;
    Darwin)
      EDITOR_ARCHIVE="Godot_v${GODOT_RELEASE}_mono_macos.universal.zip"
      EDITOR_SHA512="0862c53d7158c7a67f745e2e46f90b68cf5343cbe8b95d6d4333c469e42ca104af9c121d1746a50e5d221a99d09d82ef7016495f8e0d09255842884ed0502795"
      EDITOR_EXECUTABLE="Godot_mono.app/Contents/MacOS/Godot"
      TEMPLATES_ROOT="${HOME}/Library/Application Support/Godot/export_templates"
      TEMPLATE_FILES=("macos.zip")
      ;;
    MINGW* | MSYS* | CYGWIN*)
      [[ -n "${APPDATA:-}" ]] || fail "APPDATA is not set"
      EDITOR_ARCHIVE="Godot_v${GODOT_RELEASE}_mono_win64.zip"
      EDITOR_SHA512="79229fd112b0c9cbeab82363a4ef7be18ea70f1caf86bf912789335b136fbe7e01db0053a33461438c5da1c680c17bbb10040bd09bedc51221cd4423d0367757"
      # The console executable prints the editor's output and returns its exit code.
      EDITOR_EXECUTABLE="Godot_v${GODOT_RELEASE}_mono_win64/Godot_v${GODOT_RELEASE}_mono_win64_console.exe"
      TEMPLATES_ROOT="$(unix_path "${APPDATA}")/Godot/export_templates"
      TEMPLATE_FILES=(
        "windows_debug_x86_64.exe"
        "windows_debug_x86_64_console.exe"
        "windows_release_x86_64.exe"
        "windows_release_x86_64_console.exe"
      )
      ;;
    *)
      fail "unsupported platform ${system}"
      ;;
  esac
  TEMPLATE_FILES+=("version.txt" "icudt_godot.dat")
}

# Prints the SHA-512 of a file using whichever tool the platform provides.
compute_sha512() {
  local file="$1"
  if command -v sha512sum >/dev/null 2>&1; then
    sha512sum "${file}" | cut -d ' ' -f 1
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 512 "${file}" | cut -d ' ' -f 1
  else
    fail "neither sha512sum nor shasum is available"
  fi
}

# Downloads a release file to the given path and fails unless it matches its checksum.
download_verified() {
  local name="$1"
  local expected="$2"
  local target="$3"
  say "Downloading ${DOWNLOAD_URL}/${name}"
  curl --fail --location --silent --show-error --retry 3 --output "${target}" "${DOWNLOAD_URL}/${name}"
  local actual
  actual="$(compute_sha512 "${target}")"
  [[ "${actual}" == "${expected}" ]] || fail "checksum mismatch for ${name}: expected ${expected}, got ${actual}"
  say "Verified SHA-512 of ${name}"
}

# Unpacks the editor into the destination unless it is already there.
install_editor() {
  local destination="$1"
  local work_dir="$2"
  if [[ -f "${destination}/editor/${EDITOR_EXECUTABLE}" ]]; then
    say "Godot ${GODOT_RELEASE} is already in ${destination}/editor"
    return
  fi

  local archive="${work_dir}/${EDITOR_ARCHIVE}"
  download_verified "${EDITOR_ARCHIVE}" "${EDITOR_SHA512}" "${archive}"
  unzip -q "${archive}" -d "${work_dir}/editor"
  rm -f "${archive}"
  [[ -f "${work_dir}/editor/${EDITOR_EXECUTABLE}" ]] || fail "${EDITOR_ARCHIVE} has no ${EDITOR_EXECUTABLE}"
  rm -rf "${destination}/editor"
  mv "${work_dir}/editor" "${destination}/editor"
}

# Whether every template this platform needs is already in the destination.
have_templates() {
  local destination="$1"
  local file
  for file in "${TEMPLATE_FILES[@]}"; do
    [[ -f "${destination}/templates/${file}" ]] || return 1
  done
}

# Takes this platform's templates out of the templates archive unless they are already there.
install_templates() {
  local destination="$1"
  local work_dir="$2"
  if have_templates "${destination}"; then
    say "The ${GODOT_RELEASE} export templates are already in ${destination}/templates"
    return
  fi

  local archive="${work_dir}/${TEMPLATES_ARCHIVE}"
  local -a members=()
  local file
  for file in "${TEMPLATE_FILES[@]}"; do
    members+=("templates/${file}")
  done
  download_verified "${TEMPLATES_ARCHIVE}" "${TEMPLATES_SHA512}" "${archive}"
  unzip -q -j "${archive}" "${members[@]}" -d "${work_dir}/templates"
  rm -f "${archive}"
  rm -rf "${destination}/templates"
  mv "${work_dir}/templates" "${destination}/templates"
}

# Copies the templates to the folder Godot reads them from.
publish_templates() {
  local destination="$1"
  local folder="${TEMPLATES_ROOT}/${TEMPLATES_FOLDER}"
  mkdir -p "${folder}"
  cp "${destination}/templates/"* "${folder}/"
  say "Export templates in ${folder}"
}

# Parses arguments, then installs the editor and the templates and prints the executable.
main() {
  local destination=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --help | -h)
        print_usage
        return 0
        ;;
      --dest)
        [[ $# -ge 2 && -n "$2" ]] || { print_usage >&2; fail "--dest needs a directory" "${EXIT_USAGE}"; }
        destination="$2"
        shift 2
        ;;
      *)
        print_usage >&2
        fail "unknown argument '$1'" "${EXIT_USAGE}"
        ;;
    esac
  done
  [[ -n "${destination}" ]] || { print_usage >&2; fail "--dest is required" "${EXIT_USAGE}"; }

  require_command curl
  require_command unzip
  select_platform
  mkdir -p "$(unix_path "${destination}")"
  destination="$(cd "$(unix_path "${destination}")" && pwd)"

  local work_dir
  work_dir="$(mktemp -d)"
  # Expand now: work_dir is local and no longer set when the EXIT trap runs.
  # shellcheck disable=SC2064
  trap "rm -rf '${work_dir}'" EXIT

  install_editor "${destination}" "${work_dir}"
  install_templates "${destination}" "${work_dir}"
  publish_templates "${destination}"
  printf '%s\n' "${destination}/editor/${EDITOR_EXECUTABLE}"
}

main "$@"
