# Build script for test-program.asm using ca65 assembler and ld65 linker
# Usage: ./build-test-program.ps1

$ErrorActionPreference = "Stop"

$cc65Path = "$env:USERPROFILE/Documents/C64/cc65/bin"
$ca65Exe = "$cc65Path/ca65.exe"
$ld65Exe = "$cc65Path/ld65.exe"
$cl65Exe = "$cc65Path/cl65.exe"
$asmFile = "test-program.asm"
$outputFile = $asmFile -replace "\.asm$", ".o"
#$binaryFile = $asmFile -replace "\.asm$", ".bin"  # Only used if generating binaries without the .prg 2 byte load address header
$prgFile = $asmFile -replace "\.asm$", ".prg" # Commodore .prg file with 2 byte load address header
$labelFile = $asmFile -replace "\.asm$", ".lbl"
$debugFile = $asmFile -replace "\.asm$", ".dbg"
$mapFile = $asmFile -replace "\.asm$", ".map"
$startAddress = "0x0600"

Write-Host "Building $asmFile with cc65..." -ForegroundColor Cyan

# Check if Assembler exists
if (-not (Test-Path $ca65Exe)) {
    Write-Host "ERROR: Assembler not found at: $ca65Exe" -ForegroundColor Red
    Write-Host "Please update the path in this script." -ForegroundColor Yellow
    exit 1
}

# Check if source file exists
if (-not (Test-Path $asmFile)) {
    Write-Host "ERROR: Source file not found: $asmFile" -ForegroundColor Red
    exit 1
}

# Remove old output file if it exists
if (Test-Path $outputFile) {
    Remove-Item $outputFile
}

# Assemble + Link with lc65 using a one-liner (using cc65 driver)
Write-Host "Running: $cl65Exe -g $asmFile -o $prgFile -C c64-asm.cfg --start-addr $startAddress -Wl ""-Ln,$labelFile"" -Wl ""--dbgfile,$debugFile"" -Wl ""-m,$mapFile"""
& $cl65Exe -g $asmFile -o $prgFile -C c64-asm.cfg --start-addr $startAddress -Wl "-Ln,$labelFile" -Wl "--dbgfile,$debugFile" -Wl "-m,$mapFile"

# Assemble ca65
#Write-Host "Running: $ca65Exe -g ""$asmFile"" -o ""$outputFile""" 
#& $ca65Exe -g "$asmFile" -o "$outputFile"

# Link ld65 (requires external __LOADADDR__ symbol in the source)
#Write-Host "Running: $ld65Exe ""$outputFile"" -o ""$prgFile"" -C c64-asm.cfg --start-addr $startAddress --dbgfile ""$debugFile"" -Ln ""$labelFile"" -m ""$mapFile"""
#& $ld65Exe "$outputFile" -o "$prgFile" -C c64-asm.cfg --start-addr $startAddress --dbgfile "$debugFile" -Ln "$labelFile" -m "$mapFile"

return

# Check if assembly succeeded
if ($LASTEXITCODE -eq 0 -and (Test-Path $prgFile)) {
    $fileInfo = Get-Item $prgFile
    Write-Host ""
    Write-Host "SUCCESS! Built $prgFile ($($fileInfo.Length) bytes)" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now debug this file in VSCode:" -ForegroundColor Cyan
    Write-Host "  1. Open this folder in the Extension Development Host"
    Write-Host "  2. Create/use launch.json with `"program`": `"`${workspaceFolder}/$prgFile`""
    Write-Host "  3. Press F5 to start debugging"
} else {
    Write-Host ""
    Write-Host "ERROR: Assembly failed!" -ForegroundColor Red
    exit 1
}