# Trigger the "update available" log in the HEADLESS app locally (Windows/Scoop).
#
#   App: src/apps/Highbyte.DotNet6502.App.Headless (Scoop; CLI/automation host that logs to the console).
#
# Observe: launched here as a remote server (--remote-port) so it stays alive; the update line prints to
#          the console log about a second after startup:
#            info: UpdateCheck[0] A newer version ... Run 'scoop update dotnet-6502-headless' to update.
#          Press Ctrl+C to stop. (A very short-lived headless run may exit before the async check
#          finishes - by design, since the check is non-blocking; this keeps it alive to observe.)
#
# Usage:
#   .\tools\update\trigger-headless.ps1              # set up + launch (remote server, logs to console)
#   .\tools\update\trigger-headless.ps1 --relaunch   # relaunch reusing the existing setup
#   .\tools\update\trigger-headless.ps1 --reset      # remove simulation + restore a normal build

. "$PSScriptRoot\_common.ps1"

$AppLabel   = 'Headless'
$ProjectDir = Join-Path $RepoRoot 'src\apps\Highbyte.DotNet6502.App.Headless'
$DllName    = 'Highbyte.DotNet6502.App.Headless.dll'

function Invoke-App {
    param($BinDir)
    Write-Host "Launching $AppLabel as a remote server (stays alive). Watch the console for the"
    Write-Host '  info: UpdateCheck[0] A newer version ... line. Press Ctrl+C to stop.'
    dotnet (Join-Path $BinDir $DllName) --remote-port 6599
}

Invoke-UpdateTrigger @args
