#!/bin/bash

# Publish Avalonia Desktop app as a self-contained, single-file executable
# Native libraries are kept external (not bundled) for compatibility on macOS/Linux
# On Windows, native libraries are bundled into the single file
# Usage: ./publish.sh [runtime] [--include-pdb]
# Example: ./publish.sh osx-arm64
#          ./publish.sh linux-x64 --include-pdb
#          ./publish.sh win-x64

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/Highbyte.DotNet6502.App.Avalonia.Desktop.csproj"
OUTPUT_DIR="$SCRIPT_DIR/publish"

# Default runtime based on current OS
if [[ "$OSTYPE" == "darwin"* ]]; then
    # Check if running on Apple Silicon or Intel
    if [[ $(uname -m) == "arm64" ]]; then
        DEFAULT_RUNTIME="osx-arm64"
    else
        DEFAULT_RUNTIME="osx-x64"
    fi
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # Check if running on ARM64 or x64
    if [[ $(uname -m) == "aarch64" ]]; then
        DEFAULT_RUNTIME="linux-arm64"
    else
        DEFAULT_RUNTIME="linux-x64"
    fi
else
    DEFAULT_RUNTIME="win-x64"
fi

# Parse arguments
RUNTIME="$DEFAULT_RUNTIME"
INCLUDE_PDB=false

for arg in "$@"; do
    if [[ "$arg" == "--include-pdb" ]]; then
        INCLUDE_PDB=true
    elif [[ "$arg" != --* ]]; then
        RUNTIME="$arg"
    fi
done

echo "Publishing Avalonia Desktop app..."
echo "  Runtime: $RUNTIME"
echo "  Output:  $OUTPUT_DIR/$RUNTIME"
echo "  Include PDB: $INCLUDE_PDB"
echo ""

# Build publish arguments
PUBLISH_ARGS=(
    "$PROJECT_FILE"
    --configuration Release
    --runtime "$RUNTIME"
    --self-contained true
    -p:PublishSingleFile=true
    --output "$OUTPUT_DIR/$RUNTIME"
)

# Bundle native libraries into single file.
# Note: Doesn't seem to be working, the app crashes on startup if enabled
#PUBLISH_ARGS+=(-p:IncludeNativeLibrariesForSelfExtract=true)

# Exclude PDB files unless --include-pdb is specified
if [[ "$INCLUDE_PDB" == false ]]; then
    PUBLISH_ARGS+=(-p:DebugType=None -p:DebugSymbols=false)
fi

# Run dotnet publish
dotnet publish "${PUBLISH_ARGS[@]}"

if [[ $? -ne 0 ]]; then
    echo ""
    echo "❌ Publish failed"
    exit 1
fi

echo ""
echo "✅ Published successfully to: $OUTPUT_DIR/$RUNTIME"

# Create .app bundle for macOS
if [[ "$RUNTIME" == osx-* ]]; then
    APP_NAME="DotNet6502.app"
    APP_DIR="$OUTPUT_DIR/$RUNTIME/$APP_NAME"
    MACOS_DIR="$APP_DIR/Contents/MacOS"
    RESOURCES_DIR="$APP_DIR/Contents/Resources"

    echo ""
    echo "Creating macOS .app bundle: $APP_NAME"

    # Create bundle structure
    mkdir -p "$MACOS_DIR"
    mkdir -p "$RESOURCES_DIR"

    # Copy Info.plist
    cp "$SCRIPT_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"

    # Copy icon
    cp "$SCRIPT_DIR/AppIcon.icns" "$RESOURCES_DIR/AppIcon.icns"

    # Move all published files into MacOS directory
    # (exclude the .app bundle itself to avoid recursion)
    for item in "$OUTPUT_DIR/$RUNTIME/"*; do
        if [[ "$(basename "$item")" != "$APP_NAME" ]]; then
            mv "$item" "$MACOS_DIR/"
        fi
    done

    echo "✅ macOS .app bundle created: $APP_DIR"
fi

ls -la "$OUTPUT_DIR/$RUNTIME"
