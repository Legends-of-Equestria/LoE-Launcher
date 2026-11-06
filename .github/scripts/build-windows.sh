#!/bin/bash
source "$(dirname "$0")/common.sh"

resolve_version
WORK="build/windows"
OUTPUT="Publish/Windows"

rm -rf "$WORK" "$OUTPUT"
echo "Building LoE Launcher $VERSION for Windows"

group "Publish win-x86"
# shellcheck disable=SC2046
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net8.0 \
    -r win-x86 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishReadyToRun=false \
    -p:PublishTrimmed=false \
    -p:DebugType=None \
    $(version_args "$VERSION") \
    -o "$WORK" \
    --verbosity minimal
[ -f "$WORK/LoE-Launcher.exe" ] || fail "Publish didn't produce LoE-Launcher.exe"
endgroup

group "Package with Velopack"
vpk pack \
    --packId "$PACK_ID" \
    --packVersion "$VERSION" \
    --packTitle "$PACK_TITLE" \
    --packAuthors "$PACK_AUTHORS" \
    --packDir "$WORK" \
    --mainExe "LoE-Launcher.exe" \
    --icon "Assets/Icon.ico" \
    --channel win \
    --outputDir "$OUTPUT"
endgroup

ls -lh "$OUTPUT"
