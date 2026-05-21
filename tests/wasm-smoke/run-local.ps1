#!/usr/bin/env pwsh
# Local replay of .github/workflows/wasm-aot-verify.yml. Publishes the chosen
# WASM app(s) with the same Release/AOT flags as CI, then runs the matching
# Playwright smoke spec against the published wwwroot.
#
# Usage: ./run-local.ps1 {blazor|avalonia-browser|all}

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidateSet('blazor', 'avalonia-browser', 'all')]
    [string]$App
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
$BuildDir = Join-Path $ScriptDir 'build'
$Version = '0.0.0-local'

function Invoke-Publish {
    param([string]$Name, [string]$Project)
    $out = Join-Path $BuildDir $Name
    Write-Host "==> Publishing $Name (Release, AOT) -> $out"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    & dotnet publish (Join-Path $RepoRoot $Project) `
        -c Release `
        -p:Version=$Version `
        -p:GHPages=true `
        -p:GHPagesBase=/ `
        -p:GHPagesInjectBrotliLoader=false `
        -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}

function Invoke-VersionPlaceholder {
    param([string]$Root)
    Write-Host "==> Replacing {{APP_VERSION}} placeholders in $Root"
    $files = Get-ChildItem -Path $Root -Recurse -File -Include '*.html', '*.js' `
        | Where-Object { $_.Name -notmatch '\.(br|gz)$' }
    foreach ($f in $files) {
        $text = Get-Content -Raw -LiteralPath $f.FullName
        $text.Replace('{{APP_VERSION}}', $Version) `
            | Set-Content -NoNewline -LiteralPath $f.FullName
    }
}

function Initialize-Playwright {
    Write-Host "==> Ensuring Playwright + Chromium installed"
    Push-Location $ScriptDir
    try {
        if (-not (Test-Path 'node_modules')) {
            npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
        }
        npx playwright install chromium | Out-Null
    } finally { Pop-Location }
}

function Invoke-Spec {
    param([string]$Name, [string]$Spec)
    $root = Join-Path $BuildDir "$Name\wwwroot"
    Write-Host "==> Running Playwright spec for $Name"
    Push-Location $ScriptDir
    try {
        $env:WASM_SITE_ROOT = $root
        npx playwright test $Spec
        if ($LASTEXITCODE -ne 0) { throw "Playwright failed (exit $LASTEXITCODE)" }
    } finally { Pop-Location }
}

function Invoke-Blazor {
    Invoke-Publish -Name 'blazor' `
        -Project 'src/apps/BlazorWASM/Highbyte.DotNet6502.App.WASM/Highbyte.DotNet6502.App.WASM.csproj'
    Invoke-Spec -Name 'blazor' -Spec 'blazor.spec.ts'
}

function Invoke-AvaloniaBrowser {
    Invoke-Publish -Name 'avalonia-browser' `
        -Project 'src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/Highbyte.DotNet6502.App.Avalonia.Browser.csproj'
    Invoke-VersionPlaceholder -Root (Join-Path $BuildDir 'avalonia-browser\wwwroot')
    Invoke-Spec -Name 'avalonia-browser' -Spec 'avalonia-browser.spec.ts'
}

Initialize-Playwright

switch ($App) {
    'blazor'            { Invoke-Blazor }
    'avalonia-browser'  { Invoke-AvaloniaBrowser }
    'all'               { Invoke-Blazor; Invoke-AvaloniaBrowser }
}

Write-Host "==> Smoke test passed"
