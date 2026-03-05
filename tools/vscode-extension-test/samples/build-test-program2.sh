#!/bin/bash

# Build script for test-program.asm using ACME assembler
# Usage: ./build-test-program.sh

set -e  # Exit immediately if a command exits with a non-zero status

acmePath="$HOME/Documents/C64/ACME/"
#acmePath="" # Assume ACME is in the system PATH
acmeExe="${acmePath}acme"
asmFile="test-program2.asm"
binaryFile="${asmFile%.asm}.prg"

# Check if ACME exists
if ! command -v "$acmeExe" > /dev/null 2>&1; then
    echo -e "\033[31mERROR: ACME not found: $acmeExe\033[0m"
    echo -e "\033[33mPlease update the path in this script or ensure ACME is in your PATH.\033[0m"
    exit 1
fi

# Check if source file exists
if [ ! -f "$asmFile" ]; then
    echo -e "\033[31mERROR: Source file not found: $asmFile\033[0m"
    exit 1
fi

# Remove old output file if it exists
if [ -f "$binaryFile" ]; then
    rm "$binaryFile"
fi

# Assemble with ACME
# -f format (cbm = Commodore with load address)
# -o output file
# --vicelabels generates label file for VICE debugger (optional)
echo "Building $asmFile with ACME..."
echo "Running: $acmeExe -f cbm -o $binaryFile $asmFile"
$acmeExe -f cbm -o "$binaryFile" "$asmFile"
# Check if assembly succeeded
if [ $? -eq 0 ] && [ -f "$binaryFile" ]; then
    # Use appropriate stat syntax based on OS
    if [[ "$OSTYPE" == "darwin"* ]]; then
        FILE_SIZE=$(stat -f%z "$binaryFile")
    else
        FILE_SIZE=$(stat -c%s "$binaryFile")
    fi
    echo -e "\033[32mSUCCESS! Built $binaryFile ($FILE_SIZE bytes)\033[0m"
    echo ""
    echo -e "\033[36mYou can now debug this file in VSCode:\033[0m"
    echo "  1. Open this folder in the Extension Development Host"
    echo "  2. Create/use launch.json with \"program\": \"\${workspaceFolder}/$OUTPUT_FILE\""
    echo "  3. Press F5 to start debugging"
else
    echo -e "\033[31mERROR: Assembly failed!\033[0m"
    exit 1
fi