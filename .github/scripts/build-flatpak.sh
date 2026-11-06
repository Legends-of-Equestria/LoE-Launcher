#!/bin/bash
source "$(dirname "$0")/common.sh"

resolve_version
WORK="build/flatpak"
OUTPUT="Publish/Flatpak"

rm -rf "$WORK" "$OUTPUT"
mkdir -p "$OUTPUT"
echo "Preparing LoE Launcher $VERSION for Flatpak"

group "Install ICU"
if command -v dnf > /dev/null 2>&1; then
    dnf install -y libicu-devel
elif command -v zypper > /dev/null 2>&1; then
    zypper install -y libicu-devel
elif command -v pacman > /dev/null 2>&1; then
    pacman -S --noconfirm icu
else
    warn "Unknown package manager, continuing without installing ICU"
fi
endgroup

group "Publish linux-x64"
# shellcheck disable=SC2046
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net8.0 \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:DebugType=None \
    -p:DefineConstants=FLATPAK \
    $(version_args "$VERSION") \
    -o "$WORK" \
    --verbosity minimal
[ -f "$WORK/LoE-Launcher" ] || fail "Publish didn't produce LoE-Launcher"
endgroup

cp -r "$WORK"/* "$OUTPUT/"
chmod +x "$OUTPUT/LoE-Launcher"

cat > loe-launcher-wrapper.sh << 'EOF'
#!/bin/bash
mkdir -p /var/data/loe-launcher
cp -r /app/bin/* /var/data/loe-launcher/
chmod +x /var/data/loe-launcher/LoE-Launcher
cd /var/data/loe-launcher
exec ./LoE-Launcher "$@"
EOF
chmod +x loe-launcher-wrapper.sh

ls -lh "$OUTPUT"
