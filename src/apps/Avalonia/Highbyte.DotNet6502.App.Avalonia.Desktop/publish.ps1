# Publish Avalonia Desktop app as a self-contained, single-file executable
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
$ProjectFile = Join-Path $ScriptDir "Highbyte.DotNet6502.App.Avalonia.Desktop.csproj"
$OutputDir = Join-Path $ScriptDir "publish"

# Default runtime based on current OS and architecture
if (-not $Runtime) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
            $Runtime = "win-arm64"
        } else {
            $Runtime = "win-x64"
        }
    } elseif ($IsMacOS) {
        if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
            $Runtime = "osx-arm64"
        } else {
            $Runtime = "osx-x64"
        }
    } else {
        $Runtime = "linux-x64"
    }
}

$OutputPath = Join-Path $OutputDir $Runtime

Write-Host "Publishing Avalonia Desktop app..."
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

# On Windows, bundle native libraries into single file (works reliably there)
# On macOS/Linux, keep native libraries external due to native library loading issues
if ($Runtime -like "win-*") {
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

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
