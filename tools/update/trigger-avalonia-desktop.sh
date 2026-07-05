#!/usr/bin/env bash
# Trigger the "update available" banner + About dialog in the Avalonia DESKTOP app locally.
#
#   App: src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop
#        ("DotNet 6502 Emulator" GUI; Homebrew cask on macOS / formula on Linux; Scoop on Windows).
#
# Observe: a green banner appears at the top of the window a moment after it opens; click the
#          left-panel "About" button for the version, the brew command, and the What's new link.
#
# Usage:
#   tools/update/trigger-avalonia-desktop.sh              # set up + launch (banner shows)
#   tools/update/trigger-avalonia-desktop.sh --relaunch   # relaunch keeping the dismissed-version
#                                                          # memory, to verify a dismissed banner
#                                                          # stays hidden across a restart
#   tools/update/trigger-avalonia-desktop.sh --reset      # remove simulation + restore a normal build
#
# Run from a real GUI session (Terminal.app); Avalonia needs a window server.

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

APP_LABEL="Avalonia Desktop"
PROJECT_DIR="$REPO_ROOT/src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop"
DLL_NAME="Highbyte.DotNet6502.App.Avalonia.Desktop.dll"

run_app() {
    local bin_dir="$1"
    echo "Launching ${APP_LABEL}. Expect a green top banner: 'Update available: v${OLD_VERSION} -> v<latest>'."
    echo "Click the left-panel 'About' button for the version, the brew command, and What's new."
    dotnet "${bin_dir}/${DLL_NAME}" -- --console-log
}

update_main "$@"
