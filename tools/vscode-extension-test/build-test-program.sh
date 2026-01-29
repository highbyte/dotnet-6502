#!/bin/bash

# Build script for test-program.asm using ACME assembler
# Usage: ./build-test-program.sh

set -e  # Exit immediately if a command exits with a non-zero status

ACME_EXE="$HOME/Documents/C64/ACME/acme"
ASM_FILE="test-program.asm"
OUTPUT_FILE="test-program.prg"

# Check if ACME exists
if [ ! -f "$ACME_EXE" ]; then
    echo -e "\033[31mERROR: ACME not found at: $ACME_EXE\033[0m"
    echo -e "\033[33mPlease update the path in this script.\033[0m"
    exit 1
fi

# Check if source file exists
if [ ! -f "$ASM_FILE" ]; then
    echo -e "\033[31mERROR: Source file not found: $ASM_FILE\033[0m"
    exit 1
fi

# Remove old output file if it exists
if [ -f "$OUTPUT_FILE" ]; then
    rm "$OUTPUT_FILE"
fi

# Assemble with ACME
# -f format (cbm = Commodore with load address)
# -o output file
# --vicelabels generates label file for VICE debugger (optional)
echo "Building $ASM_FILE with ACME..."
echo "Running: $ACME_EXE -f cbm -o $OUTPUT_FILE $ASM_FILE"
$ACME_EXE -f cbm -o "$OUTPUT_FILE" "$ASM_FILE"

# Check if assembly succeeded
if [ $? -eq 0 ] && [ -f "$OUTPUT_FILE" ]; then
    # Use appropriate stat syntax based on OS
    if [[ "$OSTYPE" == "darwin"* ]]; then
        FILE_SIZE=$(stat -f%z "$OUTPUT_FILE")
    else
        FILE_SIZE=$(stat -c%s "$OUTPUT_FILE")
    fi
    echo -e "\033[32mSUCCESS! Built $OUTPUT_FILE ($FILE_SIZE bytes)\033[0m"
    echo ""
    echo -e "\033[36mYou can now debug this file in VSCode:\033[0m"
    echo "  1. Open this folder in the Extension Development Host"
    echo "  2. Create/use launch.json with \"program\": \"\${workspaceFolder}/$OUTPUT_FILE\""
    echo "  3. Press F5 to start debugging"
else
    echo -e "\033[31mERROR: Assembly failed!\033[0m"
    exit 1
fi