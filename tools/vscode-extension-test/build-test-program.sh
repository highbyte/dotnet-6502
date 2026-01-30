#!/bin/bash

# Build script for test-program.asm using ca65 assembler and ld65 linker
# Usage: ./build-test-program.sh

set -e  # Exit immediately if a command exits with a non-zero status

# cc65Path="$HOME/Documents/C64/cc65/bin/"
cc65Path="" # Assume cc65 tools are in the system PATH
ca65Exe="${cc65Path}ca65"
ld65Exe="${cc65Path}ld65"
cl65Exe="${cc65Path}cl65"
asmFile="test-program.asm"
outputFile="${asmFile%.asm}.o"
#binaryFile="${asmFile%.asm}.bin"  # Only used if generating binaries without the .prg 2 byte load address header
prgFile="${asmFile%.asm}.prg" # Commodore .prg file with 2 byte load address header
labelFile="${asmFile%.asm}.lbl"
debugFile="${asmFile%.asm}.dbg"
mapFile="${asmFile%.asm}.map"
startAddress="0x0600"

echo -e "\033[36mBuilding $asmFile with cc65...\033[0m"

# Check if Assembler exists
if ! command -v "$cl65Exe" > /dev/null 2>&1; then
    echo -e "\033[31mERROR: Assembler not found: $cl65Exe\033[0m"
    echo -e "\033[33mPlease update the path in this script or ensure cc65 is in your PATH.\033[0m"
    exit 1
fi

# Check if source file exists
if [ ! -f "$asmFile" ]; then
    echo -e "\033[31mERROR: Source file not found: $asmFile\033[0m"
    exit 1
fi

# Remove old output file if it exists
if [ -f "$outputFile" ]; then
    rm "$outputFile"
fi

# Assemble + Link with lc65 using a one-liner (using cc65 driver)
echo "Running: $cl65Exe -g $asmFile -o $prgFile -C c64-asm.cfg --start-addr $startAddress -Wl \"-Ln,$labelFile\" -Wl \"--dbgfile,$debugFile\" -Wl \"-m,$mapFile\""
"$cl65Exe" -g "$asmFile" -o "$prgFile" -C c64-asm.cfg --start-addr "$startAddress" -Wl "-Ln,$labelFile" -Wl "--dbgfile,$debugFile" -Wl "-m,$mapFile"

# Assemble ca65
#echo "Running: $ca65Exe -g \"$asmFile\" -o \"$outputFile\"" 
#"$ca65Exe" -g "$asmFile" -o "$outputFile"

# Link ld65 (requires external __LOADADDR__ symbol in the source)
#echo "Running: $ld65Exe \"$outputFile\" -o \"$prgFile\" -C c64-asm.cfg --start-addr $startAddress --dbgfile \"$debugFile\" -Ln \"$labelFile\" -m \"$mapFile\""
#"$ld65Exe" "$outputFile" -o "$prgFile" -C c64-asm.cfg --start-addr "$startAddress" --dbgfile "$debugFile" -Ln "$labelFile" -m "$mapFile"

# Check if assembly succeeded
if [ $? -eq 0 ] && [ -f "$prgFile" ]; then
    # Use appropriate stat syntax based on OS
    if [[ "$OSTYPE" == "darwin"* ]]; then
        FILE_SIZE=$(stat -f%z "$prgFile")
    else
        FILE_SIZE=$(stat -c%s "$prgFile")
    fi
    echo ""
    echo -e "\033[32mSUCCESS! Built $prgFile ($FILE_SIZE bytes)\033[0m"
    echo ""
    echo -e "\033[36mYou can now debug this file in VSCode:\033[0m"
    echo "  1. Open this folder in the Extension Development Host"
    echo "  2. Create/use launch.json with \"program\": \"\${workspaceFolder}/$prgFile\""
    echo "  3. Press F5 to start debugging"
else
    echo ""
    echo -e "\033[31mERROR: Assembly failed!\033[0m"
    exit 1
fi
