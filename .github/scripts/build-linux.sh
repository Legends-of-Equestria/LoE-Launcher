#!/bin/bash

# .github/scripts/build-linux.sh
# Builds LoE Launcher for Linux with Velopack in GitHub Actions

set -e

if [ -z "$VERSION" ]; then
    VERSION=$(git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0")
fi

# Strip leading 'v' (v1.0.0 -> 1.0.0)
VERSION="${VERSION#v}"

# Check if VERSION is a valid number (e.g. 1.0.0). If not (e.g. 'main'), default to 0.0.0
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+ ]]; then
    echo "⚠️  Version '$VERSION' is not numeric. Defaulting to 0.0.0 for build safety."
    VERSION="0.0.0"
fi

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/linux-x64/publish"
VELOPACK_OUTPUT_DIR="Publish/Linux"

echo "🚀 Starting build for Linux..."

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "$VELOPACK_OUTPUT_DIR"

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

echo "✅ Linux build complete!"
echo "📦 Created: "$VELOPACK_OUTPUT_DIR
