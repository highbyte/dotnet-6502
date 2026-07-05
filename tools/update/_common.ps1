# Shared helpers for the tools/update/trigger-*.ps1 dev scripts (Windows / Scoop).
#
# Windows analog of _common.sh: makes the "a newer version is available" behavior fire locally for one
# of the Scoop-distributed apps WITHOUT a real Scoop install, by faking the two things a managed install
# provides:
#   1. a version stamp OLDER than the latest GitHub release (so an update looks available), and
#   2. a managed-install signal: an `install-channel` marker next to the binary + a fake `scoop.ps1`
#      shim that "confirms" the package is installed ($env:SCOOP points at it).
# The real shared Highbyte.DotNet6502.Updates checker then runs exactly as on a real Scoop install; only
# the version stamp, marker, and scoop are simulated.
#
# A sourcing trigger-<app>.ps1 must, AFTER dot-sourcing this file:
#   - set $AppLabel     (human name, e.g. "Avalonia Desktop")
#   - set $ProjectDir   (path to the app project dir; build from $RepoRoot)
#   - set $DllName      (built dll filename)
#   - define Invoke-App ($BinDir param; launches/observes the app; $env:SCOOP already set)
#   - call  Invoke-UpdateTrigger @args
#
# Run from a PowerShell prompt (Windows PowerShell 5.1 or PowerShell 7). Env overrides:
#   OLD_VERSION (default 0.1.0-alpha), FAKE_UPGRADE_EXIT=1 (make the fake upgrade fail).

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot      = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$OldVersion    = if ($env:OLD_VERSION) { $env:OLD_VERSION } else { '0.1.0-alpha' }
$FakeScoopRoot = Join-Path $env:TEMP 'dotnet6502-dev-fakescoop'

# Update-checker storage lives under .NET's LocalApplicationData = %LOCALAPPDATA% on Windows.
$UpdatesDir    = Join-Path $env:LOCALAPPDATA 'Highbyte\DotNet6502\cache\updates'
$CheckCache    = Join-Path $UpdatesDir 'update-check.json'
$DismissedFile = Join-Path $UpdatesDir 'dismissed-version.txt'
$PendingFile   = Join-Path $UpdatesDir 'pending-update.txt'
$ApplyLog      = Join-Path $UpdatesDir 'last-update.log'

function Get-ProjectFile {
    Get-ChildItem -Path $ProjectDir -Filter '*.csproj' | Select-Object -First 1 -ExpandProperty FullName
}

function Get-BinaryDir {
    # The stamped binary's directory is the app's AppContext.BaseDirectory, where the marker must live.
    $dll = Get-ChildItem -Path (Join-Path $ProjectDir 'bin') -Recurse -Filter $DllName -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($dll) { return $dll.Directory.FullName }
    return $null
}

function Reset-UpdateSim {
    Write-Host "Removing update simulation and restoring a plain dev build for $AppLabel..."
    $binDir = Get-BinaryDir
    if ($binDir) { Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $binDir 'install-channel') }
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $FakeScoopRoot
    Remove-Item -Force -ErrorAction SilentlyContinue $CheckCache, $DismissedFile, $PendingFile, $ApplyLog
    dotnet build (Get-ProjectFile) -v quiet | Out-Null
    Write-Host "Done. $AppLabel is back to normal (unstamped, not managed)."
}

function Initialize-UpdateSim {
    Write-Host "Building $AppLabel stamped with old version v$OldVersion..."
    dotnet build (Get-ProjectFile) -p:Version=$OldVersion -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

    $script:BinDir = Get-BinaryDir
    if (-not $script:BinDir) { Write-Error "Could not locate the built binary ($DllName)."; exit 1 }

    # 1. Marker next to the binary - the "managed" signal Scoop's post_install emits.
    Set-Content -Path (Join-Path $script:BinDir 'install-channel') -Value 'scoop' -Encoding ascii

    # 2. Fake scoop.ps1 shim (Scoop is a PowerShell tool). Confirms the package on `scoop list <pkg>`
    #    and fakes `scoop update <pkg>`. Set FAKE_UPGRADE_EXIT=1 to make the upgrade FAIL.
    $shims = Join-Path $FakeScoopRoot 'shims'
    New-Item -ItemType Directory -Force -Path $shims | Out-Null
    $upgradeExit = if ($env:FAKE_UPGRADE_EXIT) { $env:FAKE_UPGRADE_EXIT } else { '0' }
    $fakeScoop = @"
`$cmd = if (`$args.Count -gt 0) { `$args[0] } else { '' }
switch (`$cmd) {
    'list'   { `$args -join ' '; exit 0 }
    'update' { "==> (fake) scoop `$(`$args -join ' ')"; "Upgrade finished (exit $upgradeExit)."; exit $upgradeExit }
    default  { exit 1 }
}
"@
    Set-Content -Path (Join-Path $shims 'scoop.ps1') -Value $fakeScoop -Encoding ascii

    # 3. Clear the update-check cache + dismissed/pending memory so the check definitely reports available.
    Remove-Item -Force -ErrorAction SilentlyContinue $CheckCache, $DismissedFile, $PendingFile, $ApplyLog
}

function Invoke-UpdateTrigger {
    param([string]$Mode)

    if ($Mode -eq '--reset') { Reset-UpdateSim; return }

    if ($Mode -eq '--relaunch') {
        # Reuse the existing stamped build + marker + fake scoop, and DO NOT clear the pending-update
        # memory - so you can confirm a dismissed banner stays hidden / the amber notice appears.
        $script:BinDir = Get-BinaryDir
        if ((-not $script:BinDir) -or
            (-not (Test-Path (Join-Path $script:BinDir 'install-channel'))) -or
            (-not (Test-Path (Join-Path $FakeScoopRoot 'shims\scoop.ps1')))) {
            Write-Error "Nothing set up yet - run without --relaunch first."; exit 1
        }
        $env:SCOOP = $FakeScoopRoot
        Invoke-App $script:BinDir
        return
    }

    if ($Mode) { Write-Error "Unknown option: $Mode`nUsage: <script> [--relaunch | --reset]"; exit 2 }

    Initialize-UpdateSim
    $env:SCOOP = $FakeScoopRoot
    Invoke-App $script:BinDir

    Write-Host ''
    Write-Host 'Tip: run this script with --reset to restore a normal dev build.'
}
