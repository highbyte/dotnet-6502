#!/usr/bin/env bash
# Shared helpers for the tools/update/trigger-*.sh dev scripts.
#
# These scripts make the "a newer version is available" behavior fire locally for one of the
# brew/scoop-distributed apps, WITHOUT a real Homebrew/Scoop install, by faking the two things a
# managed install provides:
#   1. a version stamp OLDER than the latest GitHub release (so an update looks available), and
#   2. a managed-install signal: an `install-channel` marker next to the binary + a fake `brew`
#      that "confirms" the package is installed (HOMEBREW_PREFIX points at it).
# The real shared Highbyte.DotNet6502.Updates checker then runs exactly as on a real install; only
# the version stamp, marker, and brew are simulated.
#
# A sourcing trigger-<app>.sh must, AFTER sourcing this file:
#   - set APP_LABEL     (human name, e.g. "Avalonia Desktop")
#   - set PROJECT_DIR   (absolute path to the app project dir; build from $REPO_ROOT)
#   - set DLL_NAME      (built dll filename)
#   - define run_app()  (launches/observes the app; $1 = bin dir; HOMEBREW_PREFIX already exported)
#   - call update_main "$@"
#
# ASCII-only on purpose (a multibyte char next to a $var mis-parses under LC_CTYPE=C). Run from a
# real terminal (GUI/TUI apps need a window server / TTY).
#
# Env overrides: OLD_VERSION (default 0.1.0-alpha).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OLD_VERSION="${OLD_VERSION:-0.1.0-alpha}"
FAKEBREW_ROOT="${TMPDIR:-/tmp}/dotnet6502-dev-fakebrew"

# Update-checker storage lives under .NET's LocalApplicationData (see AppStoragePaths).
case "$(uname -s)" in
    Darwin) UPDATES_DIR="$HOME/Library/Application Support/Highbyte/DotNet6502/cache/updates" ;;
    *)      UPDATES_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/Highbyte/DotNet6502/cache/updates" ;;
esac
CHECK_CACHE="$UPDATES_DIR/update-check.json"
DISMISSED_FILE="$UPDATES_DIR/dismissed-version.txt"
PENDING_FILE="$UPDATES_DIR/pending-update.txt"
APPLY_LOG="$UPDATES_DIR/last-update.log"

_project_file() { ls "$PROJECT_DIR"/*.csproj 2>/dev/null | head -1; }

_find_binary_dir() {
    # The stamped binary's directory is the app's AppContext.BaseDirectory, where the marker must live.
    find "$PROJECT_DIR/bin" -name "$DLL_NAME" 2>/dev/null | head -1 | xargs -I{} dirname {}
}

update_reset() {
    echo "Removing update simulation and restoring a plain dev build for ${APP_LABEL}..."
    local bin_dir
    bin_dir="$(_find_binary_dir || true)"
    [[ -n "${bin_dir:-}" ]] && rm -f "${bin_dir}/install-channel"
    rm -rf "$FAKEBREW_ROOT"
    rm -f "$CHECK_CACHE" "$DISMISSED_FILE" "$PENDING_FILE" "$APPLY_LOG"
    dotnet build "$(_project_file)" -v quiet >/dev/null
    echo "Done. ${APP_LABEL} is back to normal (unstamped, not managed)."
}

update_setup() {
    echo "Building ${APP_LABEL} stamped with old version v${OLD_VERSION}..."
    dotnet build "$(_project_file)" -p:Version="$OLD_VERSION" -v quiet >/dev/null
    BIN_DIR="$(_find_binary_dir)"
    if [[ -z "$BIN_DIR" ]]; then
        echo "Could not locate the built binary (${DLL_NAME})." >&2
        exit 1
    fi

    # 1. Marker next to the binary - the "managed" signal the Homebrew formula/Scoop emit.
    printf 'homebrew\n' > "${BIN_DIR}/install-channel"

    # 2. Fake `brew` that confirms the package on `brew list [--cask] --versions <pkg>`. Echoing all
    #    args guarantees the package name is present regardless of formula/cask arg position.
    #    Also fakes `upgrade` so --update / the GUI one-click can exercise the upgrade step. Set
    #    FAKE_UPGRADE_EXIT=1 to make the upgrade FAIL (to test --update failure / the amber
    #    "update didn't complete" banner on next launch).
    mkdir -p "$FAKEBREW_ROOT/bin"
    local upgrade_exit="${FAKE_UPGRADE_EXIT:-0}"
    cat > "$FAKEBREW_ROOT/bin/brew" <<FAKEBREW
#!/bin/bash
# Throwaway fake brew for the dev update scripts.
case "\$1" in
    list)    echo "\$@"; exit 0 ;;                                 # detection: confirm the package is installed
    upgrade) echo "==> (fake) brew \$*"; echo "Upgrade finished (exit ${upgrade_exit})."; exit ${upgrade_exit} ;;
    *)       exit 1 ;;
esac
FAKEBREW
    chmod +x "$FAKEBREW_ROOT/bin/brew"

    # 3. Clear the update-check cache AND dismissed-version memory so the check definitely reports available.
    rm -f "$CHECK_CACHE" "$DISMISSED_FILE" "$PENDING_FILE" "$APPLY_LOG"
}

update_main() {
    local mode="${1:-}"

    case "$mode" in
        --reset)
            update_reset
            exit 0
            ;;
        --relaunch)
            # Reuse the existing stamped build + marker + fake brew, and DO NOT clear the
            # dismissed-version memory (relevant to the Avalonia banner's per-version dismissal).
            BIN_DIR="$(_find_binary_dir || true)"
            if [[ -z "${BIN_DIR:-}" || ! -f "${BIN_DIR}/install-channel" || ! -x "$FAKEBREW_ROOT/bin/brew" ]]; then
                echo "Nothing set up yet - run without --relaunch first." >&2
                exit 1
            fi
            export HOMEBREW_PREFIX="$FAKEBREW_ROOT"
            run_app "$BIN_DIR"
            exit 0
            ;;
        "")
            ;;
        *)
            echo "Unknown option: $mode" >&2
            echo "Usage: $0 [--relaunch | --reset]" >&2
            exit 2
            ;;
    esac

    update_setup
    export HOMEBREW_PREFIX="$FAKEBREW_ROOT"
    run_app "$BIN_DIR"

    echo
    echo "Tip: run '$0 --reset' to restore a normal dev build."
}
