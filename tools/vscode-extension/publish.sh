#!/bin/bash

# Build and package the VSCode extension as a .vsix file
# Requires Node.js and npm to be installed
# Usage: ./publish.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/publish"

echo "Building VSCode extension..."
echo "  Output: $OUTPUT_DIR"
echo ""

# Install dependencies if node_modules is missing
if [[ ! -d "$SCRIPT_DIR/node_modules" ]]; then
    echo "Installing npm dependencies..."
    npm install --prefix "$SCRIPT_DIR"
    if [[ $? -ne 0 ]]; then
        echo ""
        echo "❌ npm install failed"
        exit 1
    fi
    echo ""
fi

# Compile TypeScript
echo "Compiling TypeScript..."
npm run compile --prefix "$SCRIPT_DIR"
if [[ $? -ne 0 ]]; then
    echo ""
    echo "❌ TypeScript compilation failed"
    exit 1
fi
echo ""

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Package the extension
echo "Packaging extension..."
npx --prefix "$SCRIPT_DIR" vsce package --out "$OUTPUT_DIR"
if [[ $? -ne 0 ]]; then
    echo ""
    echo "❌ Packaging failed"
    exit 1
fi

echo ""
VSIX_FILE=$(ls -t "$OUTPUT_DIR"/*.vsix 2>/dev/null | head -1)
echo "✅ Packaged successfully: $VSIX_FILE"
echo ""
echo "To install manually in VS Code:"
echo "  code --install-extension \"$VSIX_FILE\""
echo ""
echo "To uninstall:"
echo "  code --uninstall-extension highbyte.dotnet-6502-debugger"
