#!/bin/bash
source "$(dirname "$0")/common.sh"

resolve_version
WORK="build/mac"
OUTPUT="Publish/Mac"
ENTITLEMENTS=".github/macos/LoE-Launcher.entitlements"
NOTARY_PROFILE="loe-notary"

rm -rf "$WORK" "$OUTPUT"
echo "Building LoE Launcher $VERSION for macOS (universal)"

for rid in osx-arm64 osx-x64; do
    group "Publish $rid"
    # shellcheck disable=SC2046
    dotnet publish "$PROJECT_FILE" \
        -c Release \
        -f net8.0 \
        -r "$rid" \
        -p:PublishAot=true \
        -p:StripSymbols=true \
        -p:DebugType=None \
        $(version_args "$VERSION") \
        -o "$WORK/$rid" \
        --verbosity minimal
    find "$WORK/$rid" -iname "*.dsym" -prune -exec rm -rf {} +
    endgroup
done

group "Merge into universal binaries"
UNIVERSAL="$WORK/universal"
cp -R "$WORK/osx-arm64" "$UNIVERSAL"
while IFS= read -r -d '' file; do
    rel="${file#"$WORK/osx-arm64/"}"
    other="$WORK/osx-x64/$rel"
    [ -f "$other" ] || fail "$rel exists only in the Apple Silicon build"
    if cmp -s "$file" "$other"; then
        continue
    fi
    is_macho "$file" || fail "$rel differs between architectures but isn't a Mach-O binary"
    lipo -create "$file" "$other" -output "$UNIVERSAL/$rel"
    echo "lipo $rel"
done < <(find "$WORK/osx-arm64" -type f -print0)

while IFS= read -r -d '' file; do
    is_macho "$file" || continue
    archs=$(lipo -archs "$file")
    [[ "$archs" == *x86_64* && "$archs" == *arm64* ]] || fail "${file#"$UNIVERSAL/"} is not universal ($archs)"
done < <(find "$UNIVERSAL" -type f -print0)
lipo -archs "$UNIVERSAL/LoE-Launcher"
endgroup

SIGN_ARGS=()
INSTALL_IDENTITY=""
NOTARIZED=false
if [ -n "${MACOS_CERTIFICATE_P12:-}" ] && [ -n "${MACOS_CERTIFICATE_PASSWORD:-}" ]; then
    group "Set up code signing"
    KEYCHAIN="$RUNNER_TEMP/signing.keychain-db"
    KEYCHAIN_PASSWORD=$(openssl rand -base64 32)
    security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"
    security set-keychain-settings -lut 21600 "$KEYCHAIN"
    security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN"

    echo "$MACOS_CERTIFICATE_P12" | base64 --decode > "$RUNNER_TEMP/certificate.p12"
    security import "$RUNNER_TEMP/certificate.p12" -P "$MACOS_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN"
    rm -f "$RUNNER_TEMP/certificate.p12"
    security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN" > /dev/null
    # shellcheck disable=SC2046
    security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')

    identities=$(security find-identity -v -p basic "$KEYCHAIN")
    APP_IDENTITY=$(grep -o '"Developer ID Application: [^"]*"' <<< "$identities" | head -1 | tr -d '"' || true)
    INSTALL_IDENTITY=$(grep -o '"Developer ID Installer: [^"]*"' <<< "$identities" | head -1 | tr -d '"' || true)
    [ -n "$APP_IDENTITY" ] || fail "MACOS_CERTIFICATE_P12 has no Developer ID Application certificate"
    echo "Signing app as: $APP_IDENTITY"
    SIGN_ARGS+=(--keychain "$KEYCHAIN" --signAppIdentity "$APP_IDENTITY" --signEntitlements "$ENTITLEMENTS")

    if [ -n "$INSTALL_IDENTITY" ]; then
        echo "Signing installer as: $INSTALL_IDENTITY"
        SIGN_ARGS+=(--signInstallIdentity "$INSTALL_IDENTITY")
    else
        warn "No Developer ID Installer certificate, the .pkg installer will be unsigned"
    fi

    if [ -n "${NOTARIZATION_APPLE_ID:-}" ] && [ -n "${NOTARIZATION_PASSWORD:-}" ] && [ -n "${NOTARIZATION_TEAM_ID:-}" ]; then
        xcrun notarytool store-credentials "$NOTARY_PROFILE" \
            --apple-id "$NOTARIZATION_APPLE_ID" \
            --team-id "$NOTARIZATION_TEAM_ID" \
            --password "$NOTARIZATION_PASSWORD" \
            --keychain "$KEYCHAIN" > /dev/null
        SIGN_ARGS+=(--notaryProfile "$NOTARY_PROFILE")
        NOTARIZED=true
        echo "Notarization enabled"
    else
        warn "Notarization secrets missing, the app will be signed but not notarized"
    fi
    endgroup
else
    warn "Signing secrets missing, the app is only ad-hoc signed and macOS will warn about it on first launch"
    echo "macOS app is only ad-hoc signed and not notarized" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
    SIGN_ARGS+=(--signAppIdentity "-" --signEntitlements "$ENTITLEMENTS")
fi

group "Package with Velopack"
vpk pack \
    --packId "$PACK_ID" \
    --packVersion "$VERSION" \
    --packTitle "$PACK_TITLE" \
    --packAuthors "$PACK_AUTHORS" \
    --packDir "$UNIVERSAL" \
    --mainExe "LoE-Launcher" \
    --bundleId "com.legendsofequestria.loelauncher" \
    --icon "Assets/Icon.icns" \
    --channel osx \
    --outputDir "$OUTPUT" \
    ${SIGN_ARGS[@]+"${SIGN_ARGS[@]}"}
endgroup

group "Verify the packaged app"
ls -lh "$OUTPUT"
VERIFY="$WORK/verify"
mkdir -p "$VERIFY"
ditto -x -k "$OUTPUT/$PACK_ID-osx-Portable.zip" "$VERIFY"
APP=$(find "$VERIFY" -maxdepth 2 -name "*.app" -type d | head -1)
[ -n "$APP" ] || fail "The portable zip has no .app bundle"
MAIN="$APP/Contents/MacOS/LoE-Launcher"
[ -f "$MAIN" ] || fail "No LoE-Launcher in ${APP#"$VERIFY/"}/Contents/MacOS: $(ls "$APP/Contents/MacOS")"
archs=$(lipo -archs "$MAIN")
[[ "$archs" == *x86_64* && "$archs" == *arm64* ]] || fail "The packaged app isn't universal ($archs)"
echo "${APP#"$VERIFY/"} is universal ($archs)"

codesign --verify --deep --strict --verbose=2 "$APP"

if [ ${#SIGN_ARGS[@]} -gt 0 ]; then
    if [ "$NOTARIZED" = "true" ]; then
        spctl --assess --type execute --verbose=4 "$APP"
        xcrun stapler validate "$APP" || warn "The app is notarized but its ticket isn't stapled, the first launch needs internet"
    fi
    if [ -n "$INSTALL_IDENTITY" ]; then
        pkgutil --check-signature "$OUTPUT/$PACK_ID-osx-Setup.pkg"
    fi
fi
endgroup
