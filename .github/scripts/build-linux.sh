#!/bin/bash
source "$(dirname "$0")/common.sh"

resolve_version
WORK="build/linux"
OUTPUT="Publish/Linux"

rm -rf "$WORK" "$OUTPUT"
echo "Building LoE Launcher $VERSION for Linux"

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
    $(version_args "$VERSION") \
    -o "$WORK" \
    --verbosity minimal
[ -f "$WORK/LoE-Launcher" ] || fail "Publish didn't produce LoE-Launcher"
endgroup

group "Package with Velopack"
vpk pack \
    --packId "$PACK_ID" \
    --packVersion "$VERSION" \
    --packTitle "$PACK_TITLE" \
    --packAuthors "$PACK_AUTHORS" \
    --packDir "$WORK" \
    --mainExe "LoE-Launcher" \
    --icon "Assets/Icon.png" \
    --channel linux \
    --outputDir "$OUTPUT"
endgroup

ls -lh "$OUTPUT"
