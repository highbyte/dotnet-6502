# Trigger the "update available" log in the TERMINAL (TUI) app locally (Windows/Scoop).
#
#   App: src/apps/Terminal/Highbyte.DotNet6502.App.Terminal (Scoop; Terminal.Gui TUI host).
#
# Observe: the update line is logged to the in-app "Logs" pane about a second after startup (not to
#          stdout - the TUI owns the screen). Open the Logs pane to see it. The explicit flags also work
#          and print to stdout before the TUI starts, e.g. `dotnet <dll> --check-update`.
#
# Usage:
#   .\tools\update\trigger-terminal.ps1              # set up + launch the TUI
#   .\tools\update\trigger-terminal.ps1 --relaunch   # relaunch reusing the existing setup
#   .\tools\update\trigger-terminal.ps1 --reset      # remove simulation + restore a normal build

. "$PSScriptRoot\_common.ps1"

$AppLabel   = 'Terminal (TUI)'
$ProjectDir = Join-Path $RepoRoot 'src\apps\Terminal\Highbyte.DotNet6502.App.Terminal'
$DllName    = 'Highbyte.DotNet6502.App.Terminal.dll'

function Invoke-App {
    param($BinDir)
    Write-Host "Launching $AppLabel. The 'update available' line appears in the in-app Logs pane after ~1s."
    Write-Host 'Open the Logs pane to see it.'
    dotnet (Join-Path $BinDir $DllName)
}

Invoke-UpdateTrigger @args
