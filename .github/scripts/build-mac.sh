#!/bin/bash

# .github/scripts/build-mac.sh
# Builds LoE Launcher for macOS with Velopack in GitHub Actions

set -e

if [ -z "$VERSION" ]; then
    VERSION=$(git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0")
fi
VERSION="${VERSION#v}" # Strip leading 'v'
echo "🔹 Building Version: $VERSION"
# ------------------------

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/osx-arm64/publish"
VELOPACK_OUTPUT_DIR="Publish/Mac"

echo "🚀 Starting build for macOS..."

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "$VELOPACK_OUTPUT_DIR"

# Build + publish
echo "🔨 Publishing project..."
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net8.0 \
    -r osx-arm64 \
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
    -p:AssemblyVersion="$VERSION" \
    -p:FileVersion="$VERSION" \
    -p:InformationalVersion="$VERSION" \
    --verbosity minimal

# Make sure it worked
if [ ! -f "$OUTPUT_DIR/LoE-Launcher" ]; then
    echo "❌ Build failed — couldn't find output executable"
    exit 1
fi

# Velopack packaging
echo "📦 Packaging with Velopack..."
vpk pack \
    --packId "LoE-Launcher" \
    --packVersion "$VERSION" \
    --packDir "$OUTPUT_DIR" \
    --outputDir "$VELOPACK_OUTPUT_DIR" \
    --channel Stable \
    --mainExe "LoE-Launcher"

echo "✅ macOS build complete!"
echo "📦 Created: $VELOPACK_OUTPUT_DIR"