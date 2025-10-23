#!/bin/bash

# .github/scripts/build-mac.sh
# Builds LoE Launcher for macOS in GitHub Actions

set -e

PROJECT_DIR="LoE-Launcher"
PROJECT_FILE="$PROJECT_DIR/LoE-Launcher.csproj"
OUTPUT_DIR="$PROJECT_DIR/bin/Release/net8.0/osx-arm64/publish"
APP_NAME="LoE Launcher"
BUNDLE_ID="com.legendsofequestria.loelauncher"

echo "🚀 Starting build for macOS..."

# Clean previous output
rm -rf "$PROJECT_DIR/bin/Release"
rm -rf "Publish/Mac"

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
    --verbosity minimal

# Make sure it worked
if [ ! -f "$OUTPUT_DIR/LoE-Launcher" ]; then
    echo "❌ Build failed — couldn't find output executable"
    exit 1
fi

# Set up .app bundle structure
echo "📦 Creating .app bundle..."
mkdir -p "Publish/Mac"
APP_BUNDLE="Publish/Mac/$APP_NAME.app"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Get absolute paths for reliable copying
CURRENT_DIR="$(pwd)"
FULL_OUTPUT_DIR="$CURRENT_DIR/$OUTPUT_DIR"
FULL_APP_BUNDLE="$CURRENT_DIR/$APP_BUNDLE"

# Copy everything into the MacOS directory
echo "📁 Copying files into app bundle..."
cd "$FULL_OUTPUT_DIR"

for file in * .[^.]*; do
    [ -f "$file" ] && cp "$file" "$FULL_APP_BUNDLE/Contents/MacOS/"
done

# Copy all directories
for dir in */ .[^.]*/; do
    [ -d "$dir" ] && cp -r "$dir" "$FULL_APP_BUNDLE/Contents/MacOS/"
done

cd "$CURRENT_DIR"

echo "📁 Copied $(find "$APP_BUNDLE/Contents/MacOS/" -type f | wc -l) files into bundle"

# Include icon if it exists
ICON_FILE="Assets/Icon.icns"
if [ -f "$ICON_FILE" ]; then
    echo "🖼️  Adding icon..."
    cp "$ICON_FILE" "$APP_BUNDLE/Contents/Resources/"
    ICON_NAME="Icon.icns"
else
    echo "ℹ️  No icon included"
    ICON_NAME=""
fi

# Create Info.plist
echo "📄 Writing Info.plist..."
cat > "$APP_BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>LoE-Launcher-Wrapper</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSUIElement</key>
    <false/>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
    $(if [ -n "$ICON_NAME" ]; then echo "
    <key>CFBundleIconFile</key>
    <string>$ICON_NAME</string>"; fi)
</dict>
</plist>
EOF

# Make sure the binary is executable
echo "🔒 Making launcher executable..."
chmod +x "$APP_BUNDLE/Contents/MacOS/LoE-Launcher"

# Add wrapper to set working dir
echo "📝 Creating wrapper script..."
cat > "$APP_BUNDLE/Contents/MacOS/LoE-Launcher-Wrapper" << 'EOF'
#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"
export DOTNET_BUNDLE_EXTRACT_BASE_DIR="$SCRIPT_DIR"
exec ./LoE-Launcher "$@"
EOF

chmod +x "$APP_BUNDLE/Contents/MacOS/LoE-Launcher-Wrapper"

# Create entitlements file for signing
echo "📄 Creating entitlements..."
cat > "entitlements.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.network.client</key>
    <true/>
</dict>
</plist>
EOF

# Code signing
if [ -n "$MACOS_CERTIFICATE_P12" ] && [ -n "$MACOS_CERTIFICATE_PASSWORD" ]; then
    echo "🔐 Setting up code signing..."
    
    # Decode and install certificate
    echo "$MACOS_CERTIFICATE_P12" | base64 --decode > certificate.p12
    
    # Create temporary keychain
    KEYCHAIN_PATH="$RUNNER_TEMP/build.keychain-db"
    KEYCHAIN_PASSWORD="$(openssl rand -base64 32)"
    
    security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
    security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
    security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
    
    # Import certificate
    security import certificate.p12 -P "$MACOS_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
    security list-keychain -d user -s "$KEYCHAIN_PATH"
    
    # Find signing identity
    SIGNING_IDENTITY=$(security find-identity -v -p codesigning "$KEYCHAIN_PATH" | head -1 | grep '"' | sed -e 's/[^"]*"//' -e 's/".*//')
    
    if [ -n "$SIGNING_IDENTITY" ]; then
        echo "🔐 Code signing with: $SIGNING_IDENTITY"
        
        # Sign the app
        codesign --force --deep --sign "$SIGNING_IDENTITY" \
            --options runtime \
            --entitlements entitlements.plist \
            --timestamp \
            "$APP_BUNDLE"
        
        echo "✅ App bundle signed successfully!"
        
        # Verify signing
        codesign --verify --verbose "$APP_BUNDLE"
        
        # Set up notarization if credentials available
        if [ -n "$NOTARIZATION_APPLE_ID" ] && [ -n "$NOTARIZATION_PASSWORD" ] && [ -n "$NOTARIZATION_TEAM_ID" ]; then
            echo "📋 Setting up notarization profile..."
            xcrun notarytool store-credentials "notarytool-profile" \
                --apple-id "$NOTARIZATION_APPLE_ID" \
                --team-id "$NOTARIZATION_TEAM_ID" \
                --password "$NOTARIZATION_PASSWORD" \
                --keychain "$KEYCHAIN_PATH"
        fi
    else
        echo "⚠️  Could not find signing identity, skipping code signing"
    fi
    
    # Clean up certificate file
    rm -f certificate.p12
else
    echo "ℹ️  Code signing certificates not available, skipping signing"
fi

# Remove extended attributes
echo "🔓 Removing all extended attributes..."
xattr -cr "$APP_BUNDLE" 2>/dev/null || true
echo "✅ Quarantine and translocation attributes removed!"

echo "📀 Creating professional DMG installer..."

# Check if create-dmg is available
if command -v create-dmg >/dev/null 2>&1; then
    echo "  Using create-dmg for professional DMG creation..."
    
    cd "Publish/Mac"
    create-dmg \
        --volname "LoE Launcher" \
        --volicon "../../Assets/Icon.icns" \
        --window-pos 200 120 \
        --window-size 800 450 \
        --icon-size 128 \
        --icon "$APP_NAME.app" 200 190 \
        --hide-extension "$APP_NAME.app" \
        --app-drop-link 600 190 \
        --background "../../Assets/dmg-background.png" \
        --format UDBZ \
        "LoE-Launcher-Mac.dmg" \
        "$APP_NAME.app"
    
    cd ../..
    echo "✅ Professional DMG created with create-dmg!"
    
elif [ -f "Assets/dmg-background.png" ]; then
    echo "  Creating DMG..."
    
    DMG_NAME="LoE-Launcher-Mac.dmg"
    TEMP_DMG="temp-${DMG_NAME}"
    
    # Create temporary DMG
    hdiutil create -volname "LoE Launcher" \
        -srcfolder "Publish/Mac/$APP_NAME.app" \
        -ov -format UDRW \
        "Publish/Mac/$TEMP_DMG"
    
    # Mount the temporary DMG
    DEVICE=$(hdiutil attach -readwrite -noverify -noautoopen "Publish/Mac/$TEMP_DMG" | \
             egrep '^/dev/' | sed 1q | awk '{print $1}')
    
    # Set up the DMG appearance
    sleep 2  # Give time for mount
    
    # Create Applications symlink
    ln -s /Applications "/Volumes/LoE Launcher/Applications"
    
    # Copy background image
    mkdir -p "/Volumes/LoE Launcher/.background"
    cp "Assets/dmg-background.png" "/Volumes/LoE Launcher/.background/"
    
    # Create custom .DS_Store for layout
    osascript << EOD
tell application "Finder"
    tell disk "LoE Launcher"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set the bounds of container window to {400, 100, 1200, 550}
        set viewOptions to the icon view options of container window
        set arrangement of viewOptions to not arranged
        set icon size of viewOptions to 128
        set background picture of viewOptions to file ".background:dmg-background.png"
        set position of item "$APP_NAME.app" to {200, 190}
        set position of item "Applications" to {600, 190}
        close
        open
        update without registering applications
        delay 2
    end tell
end tell
EOD
    
    # Make the background folder invisible
    chflags hidden "/Volumes/LoE Launcher/.background"
    
    # Unmount with retry logic
    echo "🔄 Unmounting DMG..."
    for i in {1..5}; do
        if hdiutil detach "${DEVICE}" 2>/dev/null; then
            echo "✅ Successfully unmounted on attempt $i"
            break
        fi
        echo "⏳ Unmount attempt $i failed, waiting..."
        sleep 2
        # Force kill any processes using the mount
        lsof +D "/Volumes/LoE Launcher" 2>/dev/null | tail -n +2 | awk '{print $2}' | xargs kill -9 2>/dev/null || true
        sleep 1
    done
    
    # Final force unmount if needed
    hdiutil detach "${DEVICE}" -force 2>/dev/null || true
    
    # Convert to compressed DMG
    hdiutil convert "Publish/Mac/$TEMP_DMG" \
        -format UDZO \
        -imagekey zlib-level=9 \
        -o "Publish/Mac/$DMG_NAME"
    
    # Clean up
    rm -f "Publish/Mac/$TEMP_DMG"
    
    echo "✅ Enhanced DMG created with custom background!"
    
else
    # Fallback to your original method
    echo "  Using basic DMG creation (consider adding a background image)..."
    
    DMG_NAME="LoE-Launcher-Mac.dmg"
    DMG_DIR="Publish/Mac/dmg-contents"
    mkdir -p "$DMG_DIR"
    
    cp -R "$APP_BUNDLE" "$DMG_DIR/"
    ln -s /Applications "$DMG_DIR/Applications"
    
    if hdiutil create -volname "LoE Launcher" \
        -srcfolder "$DMG_DIR" \
        -ov -format UDZO \
        -imagekey zlib-level=9 \
        "Publish/Mac/$DMG_NAME"; then
        
        echo "✅ Basic DMG created successfully!"
        echo "💡 Tip: Add 'Assets/dmg-background.png' for a professional look!"
    else
        echo "❌ DMG creation failed"
    fi
fi

# Sign the DMG
if [ -n "$SIGNING_IDENTITY" ] && [ -f "Publish/Mac/LoE-Launcher-Mac.dmg" ]; then
    echo "🔐 Signing DMG..."
    codesign --force --sign "$SIGNING_IDENTITY" \
        --timestamp \
        "Publish/Mac/LoE-Launcher-Mac.dmg"
    echo "✅ DMG signed successfully!"
    
    # Notarize if credentials are available
    if [ -n "$NOTARIZATION_APPLE_ID" ] && [ -n "$NOTARIZATION_PASSWORD" ] && [ -n "$NOTARIZATION_TEAM_ID" ]; then
        echo "📋 Starting notarization process..."
        echo "⏳ This typically takes 5-30 minutes..."
        
        # Submit for notarization
        if xcrun notarytool submit "Publish/Mac/LoE-Launcher-Mac.dmg" \
            --keychain-profile "notarytool-profile" \
            --wait; then
            
            echo "✅ Notarization successful!"
            
            # Staple the notarization ticket
            xcrun stapler staple "Publish/Mac/LoE-Launcher-Mac.dmg"
            echo "✅ Notarization ticket stapled to DMG!"
        else
            echo "⚠️  Notarization failed, but DMG is still signed"
        fi
    fi
fi

# Also create zip as backup
echo "🗜️  Creating ZIP backup..."
cd "Publish/Mac"
zip -r "LoE-Launcher-Mac.zip" "$APP_NAME.app" -x "*.DS_Store" "*__MACOSX*"
cd ../..

# Clean up temporary files
rm -f entitlements.plist

echo "✅ macOS build complete!"
echo "📦 Created: Publish/Mac/LoE-Launcher-Mac.dmg"
echo "📦 Created: Publish/Mac/LoE-Launcher-Mac.zip (backup)"

if [ -n "$SIGNING_IDENTITY" ]; then
    echo "🔐 App is code signed and ready for distribution!"
    if [ -n "$NOTARIZATION_APPLE_ID" ]; then
        echo "📋 App has been submitted for notarization!"
    fi
else
    echo "ℹ️  App is unsigned - users will see security warnings"
    echo "💡 Add signing certificates to GitHub Secrets for automatic signing"
fi