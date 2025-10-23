#!/bin/bash

# .github/scripts/build-flatpak.sh
# Builds LoE Launcher for Linux as Flatpak in GitHub Actions

set -e

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/linux-x64/publish"

echo "🚀 Starting build for Linux Flatpak..."

# Install ICU dependencies for .NET
echo "📦 Installing ICU dependencies..."
if command -v dnf >/dev/null 2>&1; then
    # Fedora-based container
    dnf install -y libicu-devel
elif command -v zypper >/dev/null 2>&1; then
    # openSUSE-based container  
    zypper install -y libicu-devel
elif command -v pacman >/dev/null 2>&1; then
    # Arch-based container
    pacman -S --noconfirm icu
else
    echo "⚠️  Unknown package manager, trying to continue without ICU install..."
fi

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "Publish/Flatpak"
mkdir -p "Publish/Flatpak"

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

echo "📁 Copying files to Flatpak build directory..."
cp -r "$OUTPUT_DIR"/* "Publish/Flatpak/"
chmod +x "Publish/Flatpak/LoE-Launcher"

# Create wrapper script
echo "📝 Creating wrapper script..."
cat > loe-launcher-wrapper.sh << 'EOF'
#!/bin/bash
# Create writable directory
mkdir -p /var/data/loe-launcher
# Copy all files to writable location  
cp -r /app/bin/* /var/data/loe-launcher/
chmod +x /var/data/loe-launcher/LoE-Launcher
# Run from writable directory
cd /var/data/loe-launcher
exec ./LoE-Launcher "$@"
EOF

chmod +x loe-launcher-wrapper.sh

echo "📁 Copied $(find "Publish/Flatpak/" -type f | wc -l) files to Flatpak build directory"
echo "✅ Flatpak build preparation complete!"
echo "📦 Files prepared in: Publish/Flatpak/"