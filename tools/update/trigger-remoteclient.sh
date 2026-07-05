#!/usr/bin/env bash
# Show the "update available" output for the REMOTECLIENT app locally.
#
#   App: src/apps/Highbyte.DotNet6502.App.RemoteClient
#        (Homebrew formula / Scoop; one-shot TCP client for the remote-control server).
#
# NOTE: RemoteClient has NO automatic startup check/log - its stdout is the server response, kept
#       clean for automation - so it only reports on the explicit flags. This sets up the managed
#       simulation and runs `--check-update` so you can see the reported command. `--update` and
#       `--version` also work (run the dll directly with those flags).
#
# Usage:
#   tools/update/trigger-remoteclient.sh          # set up + run --check-update
#   tools/update/trigger-remoteclient.sh --reset  # remove simulation + restore a normal build

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_common.sh"

APP_LABEL="RemoteClient"
PROJECT_DIR="$REPO_ROOT/src/apps/Highbyte.DotNet6502.App.RemoteClient"
DLL_NAME="Highbyte.DotNet6502.App.RemoteClient.dll"

run_app() {
    local bin_dir="$1"
    echo "RemoteClient has no automatic check (explicit flags only). Running --check-update:"
    echo
    dotnet "${bin_dir}/${DLL_NAME}" --check-update
}

update_main "$@"
