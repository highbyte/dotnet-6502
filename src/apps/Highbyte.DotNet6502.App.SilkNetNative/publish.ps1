# Publish SilkNetNative app as a self-contained, single-file executable
# Native libraries are kept external (not bundled) for compatibility on macOS/Linux
# On Windows, native libraries are bundled into the single file
# Usage: .\publish.ps1 [-Runtime <runtime>] [-IncludePdb]
# Example: .\publish.ps1 -Runtime win-x64
#          .\publish.ps1 -Runtime win-arm64 -IncludePdb
#          .\publish.ps1 -Runtime linux-x64
#          .\publish.ps1 -Runtime osx-arm64

param(
    [Parameter(Mandatory=$false)]
    [string]$Runtime,
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludePdb
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "Highbyte.DotNet6502.App.SilkNetNative.csproj"
$OutputDir = Join-Path $ScriptDir "publish"

# Default runtime based on current OS and architecture
if (-not $Runtime) {
    # Detect OS with fallbacks for different PowerShell versions
    $isWin = $IsWindows -eq $true -or $env:OS -eq "Windows_NT"
    $isArm64 = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64

    if ($isWin) {
        $Runtime = if ($isArm64) { "win-arm64" } else { "win-x64" }
    } else {
        # Only check for macOS on non-Windows systems
        $isMac = $IsMacOS -eq $true -or ((Get-Command uname -ErrorAction SilentlyContinue) -and (uname) -eq "Darwin")
        if ($isMac) {
            $Runtime = if ($isArm64) { "osx-arm64" } else { "osx-x64" }
        } else {
            $Runtime = if ($isArm64) { "linux-arm64" } else { "linux-x64" }
        }
    }
}

$OutputPath = Join-Path $OutputDir $Runtime

Write-Host "Publishing SilkNetNative app..."
Write-Host "  Runtime: $Runtime"
Write-Host "  Output:  $OutputPath"
Write-Host "  Include PDB: $IncludePdb"
Write-Host ""

# Build publish arguments
$publishArgs = @(
    $ProjectFile
    "--configuration", "Release"
    "--runtime", $Runtime
    "--self-contained", "true"
    "-p:PublishSingleFile=true"
    "--output", $OutputPath
)

# Bundle native libraries into single file.
# Note: Doesn't seem to be working, the app crashes on startup if enabled
#$publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"

# Exclude PDB files unless -IncludePdb is specified
if (-not $IncludePdb) {
    $publishArgs += "-p:DebugType=None"
    $publishArgs += "-p:DebugSymbols=false"
}

# Run dotnet publish
& dotnet publish @publishArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Published successfully to: $OutputPath" -ForegroundColor Green
    Get-ChildItem $OutputPath | Format-Table Name, Length -AutoSize
} else {
    Write-Host ""
    Write-Host "❌ Publish failed" -ForegroundColor Red
    exit 1
}
