#!/bin/bash

# .github/scripts/build-linux.sh
# Builds LoE Launcher for Linux as AppImage in GitHub Actions

set -e

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/linux-x64/publish"
APP_NAME="LoE Launcher"
DESKTOP_FILE_NAME="loe-launcher"
APPDIR_NAME="LoE-Launcher.AppDir"

echo "🚀 Starting build for Linux AppImage..."

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "Publish/Linux"

# Build + publish
echo "🔨 Publishing project..."
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net8.0 \
    -r linux-x64 \
    --self-contained true \
    -p:UseAppHost=true \
    -p:SelfContained=true \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:TrimUnusedDependencies=false \
    -p:CopyOutputSymbolsToPublishDirectory=false \
    -p:UseSharedCompilation=true \
    -p:BuildInParallel=true \
    --verbosity minimal

# Make sure it worked
if [ ! -f "$OUTPUT_DIR/LoE-Launcher" ]; then
    echo "❌ Build failed — couldn't find output executable"
    exit 1
fi

# Set up AppDir structure
echo "📦 Creating AppDir structure..."
mkdir -p "Publish/Linux"
APPDIR="Publish/Linux/$APPDIR_NAME"
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/lib"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

# Copy all published files
echo "📁 Copying files into AppDir..."
cp -r "$OUTPUT_DIR"/* "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/LoE-Launcher"

echo "📁 Copied $(find "$APPDIR/usr/bin/" -type f | wc -l) files into AppDir"

# Include icon if it exists
ICON_FILE=""
if [ -f "Assets/Icon.png" ]; then
    echo "🖼️  Adding PNG icon..."
    cp "Assets/Icon.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/$DESKTOP_FILE_NAME.png"
    cp "Assets/Icon.png" "$APPDIR/$DESKTOP_FILE_NAME.png"
    ICON_FILE="$DESKTOP_FILE_NAME"
elif [ -f "Assets/Icon.svg" ]; then
    echo "🖼️  Adding SVG icon..."
    cp "Assets/Icon.svg" "$APPDIR/usr/share/icons/hicolor/256x256/apps/$DESKTOP_FILE_NAME.svg"
    cp "Assets/Icon.svg" "$APPDIR/$DESKTOP_FILE_NAME.svg"
    ICON_FILE="$DESKTOP_FILE_NAME"
else
    echo "ℹ️  No icon included"
fi

# Create desktop file
echo "📄 Writing desktop file..."
cat > "$APPDIR/usr/share/applications/$DESKTOP_FILE_NAME.desktop" << 'EOF'
[Desktop Entry]
Type=Application
Name=LoE Launcher
Comment=Legends of Equestria Game Launcher
Exec=LoE-Launcher
Categories=Game;
Terminal=false
EOF

# Add icon to desktop file if available
if [ -n "$ICON_FILE" ]; then
    echo "Icon=$ICON_FILE" >> "$APPDIR/usr/share/applications/$DESKTOP_FILE_NAME.desktop"
fi

# Copy desktop file to AppDir root
cp "$APPDIR/usr/share/applications/$DESKTOP_FILE_NAME.desktop" "$APPDIR/"

# Validate desktop file
echo "🔍 Validating desktop file..."
desktop-file-validate "$APPDIR/$DESKTOP_FILE_NAME.desktop" || echo "Desktop file validation warnings (non-fatal)"

# Create AppRun script
echo "📝 Creating AppRun script..."
cat > "$APPDIR/AppRun" << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
cd "${HERE}/usr/bin"
exec "./LoE-Launcher" "$@"
EOF

chmod +x "$APPDIR/AppRun"

# Download appimagetool
echo "📥 Downloading appimagetool..."
if ! wget -O appimagetool "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"; then
    echo "❌ Failed to download appimagetool"
    exit 1
fi

echo "📊 Downloaded appimagetool: $(ls -lh appimagetool)"
chmod +x appimagetool

# Set environment for CI/container environments
export APPIMAGE_EXTRACT_AND_RUN=1
echo "🔧 Set APPIMAGE_EXTRACT_AND_RUN=1 for CI environment"

# Test appimagetool
echo "🧪 Testing appimagetool..."
if ! ./appimagetool --version; then
    echo "⚠️  appimagetool version check failed, but continuing..."
fi

# Verify AppDir structure
echo "📁 AppDir structure:"
find "$APPDIR" -type f | head -10
echo "📁 AppDir required files check:"
[ -f "$APPDIR/AppRun" ] && echo "✅ AppRun exists" || echo "❌ AppRun missing"
[ -f "$APPDIR/$DESKTOP_FILE_NAME.desktop" ] && echo "✅ Desktop file exists" || echo "❌ Desktop file missing"

# Create the AppImage with verbose output
echo "🔨 Creating AppImage..."
ARCH=x86_64 ./appimagetool --verbose --no-appstream "$APPDIR" "Publish/Linux/LoE-Launcher.AppImage"

# Verify the AppImage was created
if [ -f "Publish/Linux/LoE-Launcher.AppImage" ]; then
    chmod +x "Publish/Linux/LoE-Launcher.AppImage"
    echo "✅ AppImage created successfully: $(ls -lh Publish/Linux/LoE-Launcher.AppImage)"
else
    echo "❌ AppImage creation failed"
    exit 1
fi

echo "✅ Linux AppImage build complete!"
echo "📦 Created: Publish/Linux/LoE-Launcher.AppImage"
