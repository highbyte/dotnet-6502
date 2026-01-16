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
    DEFAULT_RUNTIME="linux-x64"
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

if [[ $? -eq 0 ]]; then
    echo ""
    echo "✅ Published successfully to: $OUTPUT_DIR/$RUNTIME"
    ls -la "$OUTPUT_DIR/$RUNTIME"
else
    echo ""
    echo "❌ Publish failed"
    exit 1
fi
