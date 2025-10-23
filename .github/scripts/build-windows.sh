#!/bin/bash

# .github/scripts/build-windows.sh
# Builds LoE Launcher for Windows as single-file executable in GitHub Actions

set -e

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/win-x86/publish"

echo "🚀 Starting build for Windows..."

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "Publish/Windows"

# Build + publish as single file
echo "🔨 Publishing project as single-file executable..."
# Add these properties to reduce AV triggers
dotnet publish "$PROJECT_FILE" \
    -c Release \
    -f net8.0 \
    -r win-x86 \
    --self-contained true \
    -p:UseAppHost=true \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:EnableCompressionInSingleFile=false \
    -p:PublishReadyToRun=false \
    -p:CopyOutputSymbolsToPublishDirectory=false \
    -p:PublishTrimmed=false \
    -p:AssemblyVersion=1.0.0.0 \
    -p:FileVersion=1.0.0.0 \
    -p:InformationalVersion=1.0.0 \
    --verbosity minimal

# Make sure it worked
if [ ! -f "$OUTPUT_DIR/LoE-Launcher.exe" ]; then
    echo "❌ Build failed — couldn't find output executable"
    exit 1
fi

# Set up output directory
echo "📦 Creating Windows output directory..."
mkdir -p "Publish/Windows"

# Copy the single executable
echo "📁 Copying executable..."
cp "$OUTPUT_DIR/LoE-Launcher.exe" "Publish/Windows/"

echo "📁 Windows executable size: $(ls -lh "Publish/Windows/LoE-Launcher.exe" | awk '{print $5}')"

echo "✅ Windows build complete!"
echo "📦 Created: Publish/Windows/LoE-Launcher.exe"