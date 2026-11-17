#!/bin/bash
source "$(dirname "$0")/common.sh"

resolve_version
APP_ID="com.legendsofequestria.Launcher"
WORK="build/flatpak"
OUTPUT="Publish/Flatpak"

rm -rf "$WORK" "$OUTPUT"
mkdir -p "$WORK" "$OUTPUT"
echo "Building LoE Launcher $VERSION for Flatpak"

group "Publish linux-x64"
# shellcheck disable=SC2046
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net10.0 \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:DebugType=None \
    -p:DefineConstants=FLATPAK \
    $(version_args "$VERSION") \
    -o "$WORK/publish" \
    --verbosity minimal
[ -f "$WORK/publish/LoE-Launcher" ] || fail "Publish didn't produce LoE-Launcher"
chmod +x "$WORK/publish/LoE-Launcher"
endgroup

group "Build the Flatpak"
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak-builder \
    --user \
    --install-deps-from=flathub \
    --disable-rofiles-fuse \
    --force-clean \
    --state-dir="$WORK/state" \
    --repo="$WORK/repo" \
    "$WORK/app" \
    "$APP_ID.json"
endgroup

group "Bundle"
flatpak build-bundle "$WORK/repo" "$OUTPUT/LoE-Launcher-linux.flatpak" "$APP_ID"
endgroup

ls -lh "$OUTPUT"
