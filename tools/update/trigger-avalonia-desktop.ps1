# Trigger the "update available" banner + About dialog in the Avalonia DESKTOP app locally (Windows/Scoop).
#
#   App: src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop
#        ("DotNet 6502 Emulator" GUI; on Windows distributed via Scoop).
#
# Observe: a green banner appears at the top of the window a moment after it opens; click the
#          left-panel "About" button for the version, the `scoop update` command, and What's new.
#          Click "Update now" to test the one-click self-update (quits -> fake scoop update -> relaunch).
#
# Usage:
#   .\tools\update\trigger-avalonia-desktop.ps1              # set up + launch (banner shows)
#   .\tools\update\trigger-avalonia-desktop.ps1 --relaunch   # relaunch keeping the pending-update memory
#                                                            # (to see the amber "didn't complete" banner)
#   .\tools\update\trigger-avalonia-desktop.ps1 --reset      # remove simulation + restore a normal build
#   $env:FAKE_UPGRADE_EXIT=1; .\tools\update\trigger-avalonia-desktop.ps1   # make the fake upgrade fail

. "$PSScriptRoot\_common.ps1"

$AppLabel   = 'Avalonia Desktop'
$ProjectDir = Join-Path $RepoRoot 'src\apps\Avalonia\Highbyte.DotNet6502.App.Avalonia.Desktop'
$DllName    = 'Highbyte.DotNet6502.App.Avalonia.Desktop.dll'

function Invoke-App {
    param($BinDir)
    Write-Host "Launching $AppLabel. Expect a green top banner: 'Update available: v$OldVersion -> v<latest>'."
    Write-Host "Click the left-panel 'About' button for the version, the scoop command, and What's new."
    dotnet (Join-Path $BinDir $DllName) -- --console-log
}

Invoke-UpdateTrigger @args
