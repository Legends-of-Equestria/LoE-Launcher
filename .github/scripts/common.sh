#!/bin/bash
set -euo pipefail

PROJECT_FILE="LoE-Launcher/LoE-Launcher.csproj"
PACK_ID="LoE-Launcher"
PACK_TITLE="LoE Launcher"
PACK_AUTHORS="Legends of Equestria"

group() { echo "::group::$1"; }
endgroup() { echo "::endgroup::"; }
warn() { echo "::warning::$1"; }
fail() { echo "::error::$1"; exit 1; }

resolve_version() {
    local raw="${RELEASE_TAG:-}"
    VERSION="${raw#v}"
    if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        [ "${RELEASE_BUILD:-false}" = "true" ] && fail "Release builds need a version like v1.2.3, got '$raw'"
        VERSION="0.0.1"
    fi
}

is_macho() {
    [[ "$(file -b "$1")" == Mach-O* ]]
}

version_args() {
    echo "-p:AssemblyVersion=$1 -p:FileVersion=$1 -p:InformationalVersion=$1"
}
