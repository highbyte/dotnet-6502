#!/usr/bin/env bash
# Trigger the "update available" log in the TERMINAL (TUI) app locally.
#
#   App: src/apps/Terminal/Highbyte.DotNet6502.App.Terminal
#        (Homebrew formula / Scoop; Terminal.Gui TUI host).
#
# Observe: the update line is logged to the in-app "Logs" pane about a second after startup (not to
#          stdout - the TUI owns the screen). Open the Logs pane to see it. The explicit flags also
#          work and print to stdout before the TUI starts, e.g.:
#            dotnet <dll> --check-update
#
# Usage:
#   tools/update/trigger-terminal.sh              # set up + launch the TUI
#   tools/update/trigger-terminal.sh --relaunch   # relaunch reusing the existing setup
#   tools/update/trigger-terminal.sh --reset      # remove simulation + restore a normal build
#
# Run from a real terminal (the TUI needs a TTY).

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

APP_LABEL="Terminal (TUI)"
PROJECT_DIR="$REPO_ROOT/src/apps/Terminal/Highbyte.DotNet6502.App.Terminal"
DLL_NAME="Highbyte.DotNet6502.App.Terminal.dll"

run_app() {
    local bin_dir="$1"
    echo "Launching ${APP_LABEL}. The 'update available' line appears in the in-app Logs pane after ~1s."
    echo "Open the Logs pane to see it."
    dotnet "${bin_dir}/${DLL_NAME}"
    return 0
}

update_main "$@"
