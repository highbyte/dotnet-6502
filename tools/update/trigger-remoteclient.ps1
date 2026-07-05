# Show the "update available" output for the REMOTECLIENT app locally (Windows/Scoop).
#
#   App: src/apps/Highbyte.DotNet6502.App.RemoteClient (Scoop; one-shot TCP client).
#
# NOTE: RemoteClient has NO automatic startup check/log - its stdout is the server response, kept clean
#       for automation - so it only reports on the explicit flags. This sets up the managed simulation and
#       runs `--check-update` so you can see the reported command. `--update` and `--version` also work
#       (run the dll directly with those flags).
#
# Usage:
#   .\tools\update\trigger-remoteclient.ps1          # set up + run --check-update
#   .\tools\update\trigger-remoteclient.ps1 --reset  # remove simulation + restore a normal build

. "$PSScriptRoot\_common.ps1"

$AppLabel   = 'RemoteClient'
$ProjectDir = Join-Path $RepoRoot 'src\apps\Highbyte.DotNet6502.App.RemoteClient'
$DllName    = 'Highbyte.DotNet6502.App.RemoteClient.dll'

function Invoke-App {
    param($BinDir)
    Write-Host 'RemoteClient has no automatic check (explicit flags only). Running --check-update:'
    Write-Host ''
    dotnet (Join-Path $BinDir $DllName) --check-update
}

Invoke-UpdateTrigger @args
