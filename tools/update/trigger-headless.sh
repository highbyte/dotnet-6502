#!/usr/bin/env bash
# Trigger the "update available" log in the HEADLESS app locally.
#
#   App: src/apps/Highbyte.DotNet6502.App.Headless
#        (Homebrew formula / Scoop; CLI/automation host that logs to the console).
#
# Observe: launched here as a remote server (--remote-port) so it stays alive; the update line prints
#          to the console log about a second after startup:
#            info: UpdateCheck[0] A newer version ... Run 'brew upgrade dotnet-6502-headless' to update.
#          Press Ctrl+C to stop. (A very short-lived headless run may exit before the async check
#          finishes - by design, since the check is non-blocking; this keeps it alive to observe.)
#
# Usage:
#   tools/update/trigger-headless.sh              # set up + launch (remote server, logs to console)
#   tools/update/trigger-headless.sh --relaunch   # relaunch reusing the existing setup
#   tools/update/trigger-headless.sh --reset      # remove simulation + restore a normal build

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

APP_LABEL="Headless"
PROJECT_DIR="$REPO_ROOT/src/apps/Highbyte.DotNet6502.App.Headless"
DLL_NAME="Highbyte.DotNet6502.App.Headless.dll"

run_app() {
    local bin_dir="$1"
    echo "Launching ${APP_LABEL} as a remote server (stays alive). Watch the console for the"
    echo "  info: UpdateCheck[0] A newer version ... line. Press Ctrl+C to stop."
    dotnet "${bin_dir}/${DLL_NAME}" --remote-port 6599
}

update_main "$@"
