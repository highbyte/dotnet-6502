# Build script for test-program.asm using ACME assembler
# Usage: .\build-test-program.ps1

$ErrorActionPreference = "Stop"

$acmePath = "$env:USERPROFILE/Documents/C64/ACME"
$acmeExe = "$acmePath/acme.exe"
$asmFile = "test-program2.asm"
$binaryFile = $asmFile -replace "\.asm$", ".prg"


Write-Host "Building $asmFile with ACME..." -ForegroundColor Cyan

# Check if ACME exists
if (-not (Test-Path $acmeExe)) {
    Write-Host "ERROR: ACME not found at: $acmeExe" -ForegroundColor Red
    Write-Host "Please update the path in this script." -ForegroundColor Yellow
    exit 1
}

# Check if source file exists
if (-not (Test-Path $asmFile)) {
    Write-Host "ERROR: Source file not found: $asmFile" -ForegroundColor Red
    exit 1
}

# Remove old output file if it exists
if (Test-Path $binaryFile) {
    Remove-Item $binaryFile
}

# Assemble with ACME
# -f format (cbm = Commodore with load address)
# -o output file
# --vicelabels generates label file for VICE debugger (optional)
Write-Host "Running: $acmeExe -f cbm -o $binaryFile $asmFile"
& $acmeExe -f cbm -o $binaryFile $asmFile

# Check if assembly succeeded
if ($LASTEXITCODE -eq 0 -and (Test-Path $binaryFile)) {
    $fileInfo = Get-Item $binaryFile
    Write-Host ""
    Write-Host "SUCCESS! Built $binaryFile ($($fileInfo.Length) bytes)" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now debug this file in VSCode:" -ForegroundColor Cyan
    Write-Host "  1. Open this folder in the Extension Development Host"
    Write-Host "  2. Create/use launch.json with `"program`": `"`${workspaceFolder}/$binaryFile`""
    Write-Host "  3. Press F5 to start debugging"
} else {
    Write-Host ""
    Write-Host "ERROR: Assembly failed!" -ForegroundColor Red
    exit 1
}
