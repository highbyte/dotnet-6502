#!/bin/bash
# Interactive helper to cut a GitHub release for the VSCode extension.
# - Checks release access and prepares the commit hook before changing files.
# - Suggests next patch version (bumps the patch digit of the latest vscode-v* tag,
#   preserving any pre-release suffix).
# - Validates the new version is unique and strictly higher than any existing.
# - If [Unreleased] in CHANGELOG.md has content and no section for the new version
#   yet exists, offers to move that content into a new versioned section, commit,
#   and push.
# - Creates a GitHub release with the standard Marketplace-changelog link.
# - Marks as pre-release when the major version is 0 (i.e. base < 1.0.0)
#   or when the version has a pre-release suffix (e.g. -alpha).
#
# Usage:
#   tools/vscode-extension/release.sh [--check-tools] [--dry-run] [--force]
#
#   --check-tools  Run the full preflight and exit without changing release files.
#   --dry-run  Print planned changes without committing, pushing, or publishing.
#   --force    Skip the pre-release safety checks (on-master / clean tree /
#              in-sync-with-origin). Use with care.

set -euo pipefail

CHECK_TOOLS_ONLY=false
DRY_RUN=false
FORCE=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --check-tools) CHECK_TOOLS_ONLY=true; shift ;;
        --dry-run) DRY_RUN=true; shift ;;
        --force)   FORCE=true; shift ;;
        -h|--help)
            while IFS= read -r line; do
                [[ "$line" == '#!'* ]] && continue
                [[ "$line" == '#'* ]] || break
                line="${line#\#}"; printf '%s\n' "${line# }"
            done < "${BASH_SOURCE[0]}"
            exit 0
            ;;
        *) echo "Unknown arg: $1" >&2; exit 2 ;;
    esac
done

# Check tools before fetching, prompting, or rewriting the changelog. Keep
# preflight independent of --force and --dry-run.
echo "==> Checking release tools:"
if (( BASH_VERSINFO[0] < 3 || (BASH_VERSINFO[0] == 3 && BASH_VERSINFO[1] < 2) )); then
    echo "Bash 3.2 or newer is required; found $BASH_VERSION. Upgrade Bash." >&2
    exit 2
fi
echo "    Bash $BASH_VERSION"

MISSING_TOOLS=false
for tool in gh git python3 awk sed dirname cut grep date mktemp cp diff rm; do
    if ! command -v "$tool" >/dev/null; then
        MISSING_TOOLS=true
        case "$tool" in
            gh) hint="Install GitHub CLI (macOS: brew install gh)." ;;
            git) hint="Install Git (macOS: brew install git)." ;;
            python3) hint="Install Python 3 (macOS: brew install python@3.14)." ;;
            *) hint="Install the system utility and ensure it is on PATH." ;;
        esac
        echo "Required tool missing: $tool. $hint" >&2
    fi
done
if $MISSING_TOOLS; then exit 2; fi

if ! GIT_VERSION="$(git --version)"; then
    echo "Git cannot run. Repair your Git installation (macOS: brew install git)." >&2
    exit 2
fi
echo "    $GIT_VERSION"
if ! GH_VERSION="$(gh --version)"; then
    echo "GitHub CLI cannot run. Repair gh (macOS: brew install gh)." >&2
    exit 2
fi
echo "    ${GH_VERSION%%$'\n'*}"
if ! GH_LIST_HELP="$(gh release list --help)" ||
    [[ "$GH_LIST_HELP" != *'--json'* || "$GH_LIST_HELP" != *'--jq'* ]]; then
    echo "GitHub CLI must support gh release list --json and --jq. Upgrade gh (macOS: brew upgrade gh)." >&2
    exit 2
fi
if ! python3 - <<'PY'
import sys
version = '.'.join(map(str, sys.version_info[:3]))
if sys.version_info < (3, 6):
    sys.exit('Python 3.6 or newer is required; found ' + version + '. Upgrade Python 3.')
print('    Python ' + version)
PY
then
    exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHANGELOG="$SCRIPT_DIR/CHANGELOG.md"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# Check authentication and repository access before a release can edit the
# changelog. These checks cannot guarantee a later network request or push.
if ! gh auth status >/dev/null 2>&1; then
    echo "GitHub CLI authentication failed. Run gh auth login, then retry." >&2
    exit 2
fi
if ! REPO_PERMISSION="$(gh repo view --json viewerPermission --jq .viewerPermission)"; then
    echo "Could not verify GitHub repository access. Check gh authentication and the origin remote." >&2
    exit 2
fi
case "$REPO_PERMISSION" in
    ADMIN|MAINTAIN|WRITE) echo "    GitHub repository access: $REPO_PERMISSION" ;;
    *) echo "GitHub repository write access is required; current permission: $REPO_PERMISSION." >&2; exit 2 ;;
esac

# A generated pre-commit hook can use a different Python than python3 on PATH.
# Inspect its interpreter and configuration, then install hook environments
# before touching the changelog so their setup cannot fail at commit time.
COMMIT_HOOK="$(git rev-parse --git-path hooks/pre-commit)"
if ! python3 - "$COMMIT_HOOK" "$REPO_ROOT/.pre-commit-config.yaml" <<'PY'
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys

hook = Path(sys.argv[1])
if not hook.is_file() or not os.access(str(hook), os.X_OK):
    print('    pre-commit: no executable Git hook installed')
    sys.exit(0)
text = hook.read_text(errors='replace')
if '# File generated by pre-commit:' not in text:
    print('    Custom commit hook: Python requirements not inspected')
    sys.exit(0)

match = re.search(r'^INSTALL_PYTHON=(.*)$', text, re.MULTILINE)
runner = shlex.split(match.group(1)) if match else []
if len(runner) != 1 or not os.access(runner[0], os.X_OK):
    # Match the generated hook's fallback to the pre-commit CLI on PATH.
    cli = shutil.which('pre-commit')
    if not cli:
        sys.exit('The installed Git hook cannot find pre-commit. Install it and run pre-commit install.')
    with open(cli) as source:
        shebang = source.readline().strip()
    runner = shlex.split(shebang[2:]) if shebang.startswith('#!') else []
    if not runner or not any('python' in part for part in runner):
        sys.exit('Cannot determine the Git hook Python. Run pre-commit install to refresh the hook.')

check = r'''
import re
import subprocess
import sys
try:
    from importlib.metadata import version
    from pre_commit.clientlib import load_config
    from pre_commit.languages.python import norm_version
    config = load_config(sys.argv[1])
    print('    pre-commit ' + version('pre_commit'), flush=True)
except Exception as error:
    sys.exit('Cannot load the installed pre-commit configuration: ' + str(error))

default = config.get('default_language_version', {}).get('python', 'default')
requirements = set()
for repo in config['repos']:
    for hook in repo['hooks']:
        if hook.get('language') not in (None, 'python'):
            continue
        required = hook.get('language_version', 'default')
        if required == 'default':
            required = default
        if required.startswith('python') or hook.get('language') == 'python':
            requirements.add(required)

for required in sorted(requirements):
    interpreter = norm_version(required) or sys.executable
    try:
        actual = subprocess.check_output(
            [interpreter, '-c', 'import sys; print(".".join(map(str, sys.version_info[:3])))'],
            stderr=subprocess.STDOUT, universal_newlines=True,
        ).strip()
        expected = re.fullmatch(r'python(\d+(?:\.\d+)*)', required)
        if expected:
            parts = tuple(map(int, expected.group(1).split('.')))
            if tuple(map(int, actual.split('.')))[:len(parts)] != parts:
                raise ValueError('found Python ' + actual)
    except (OSError, subprocess.CalledProcessError, ValueError) as error:
        hint = 'Install ' + required + ' and ensure it is on PATH.'
        if re.fullmatch(r'python\d+\.\d+', required):
            hint += ' On macOS: brew install python@' + required[len('python'):]
        sys.exit('Commit hook requires ' + required + ': ' + str(error) + '\n' + hint)
    print('    Commit hook ' + required + ': Python ' + actual, flush=True)
'''
result = subprocess.run(runner + ['-', sys.argv[2]], input=check, universal_newlines=True)
if result.returncode:
    sys.exit(result.returncode)
print('==> Preparing commit hook environment:', flush=True)
result = subprocess.run(runner + ['-m', 'pre_commit', 'install-hooks'])
if result.returncode:
    sys.exit('Commit hook setup failed before release changes. Fix the error above and retry.')
PY
then
    echo "Release tool checks failed; no release files have been changed." >&2
    exit 2
fi
echo "==> Release tool checks passed."
if $CHECK_TOOLS_ONLY; then exit 0; fi

dry() {
    if $DRY_RUN; then
        echo "  [dry-run] $*"
    else
        "$@"
    fi
}

# Prompt with a default the user can accept by pressing Enter or override by
# typing a new value. Caller is responsible for displaying what the default is
# (on its own line above the prompt) — keeps the prompt itself short.
#
# (Earlier attempts used Python's readline.set_startup_hook to prefill the
# default in-line for character-level editing, but macOS Python links against
# libedit, where set_startup_hook is a no-op.)
prompt_with_default() {
    local prompt="$1" default="$2" input
    # Read from /dev/tty in case stdout is being captured by $(...).
    read -r -p "$prompt" input < /dev/tty
    echo "${input:-$default}"
}

# Simple y/N prompt with selectable default. $2 is the default ("y" or "n").
confirm() {
    local prompt="$1" default="${2:-n}" yn=""
    local hint
    if [[ "$default" == "y" ]]; then hint="[Y/n]"; else hint="[y/N]"; fi
    read -r -p "$prompt $hint " yn < /dev/tty
    yn="${yn:-$default}"
    [[ "$yn" =~ ^[Yy]$ ]]
}

# Compare two dotted versions ($1 vs $2). Echoes -1, 0, or 1.
version_cmp() {
    local a="$1" b="$2"
    awk -v a="$a" -v b="$b" 'BEGIN {
        n1 = split(a, x, "."); n2 = split(b, y, ".")
        n = (n1 > n2) ? n1 : n2
        for (i = 1; i <= n; i++) {
            xi = (i <= n1) ? x[i]+0 : 0
            yi = (i <= n2) ? y[i]+0 : 0
            if (xi < yi) { print -1; exit }
            if (xi > yi) { print  1; exit }
        }
        print 0
    }'
}

# ----- Pre-release safety checks -----
if ! $FORCE; then
    BRANCH=$(git rev-parse --abbrev-ref HEAD)
    if [[ "$BRANCH" != "master" ]]; then
        echo "Refusing to release from branch '$BRANCH' (must be master). Use --force to override." >&2
        exit 2
    fi
    if [[ -n "$(git status --porcelain)" ]]; then
        echo "Working tree is not clean. Commit or stash first. Use --force to override." >&2
        exit 2
    fi
    git fetch origin master --quiet
    if [[ "$(git rev-parse HEAD)" != "$(git rev-parse origin/master)" ]]; then
        echo "Local master is not in sync with origin/master. Pull/push first. Use --force to override." >&2
        exit 2
    fi
fi

# ----- Find latest vscode-v* release and build suggestion -----
LATEST_TAG="$(gh release list --limit 100 --json tagName,createdAt \
    --jq '[.[] | select(.tagName | startswith("vscode-v"))] | sort_by(.createdAt) | reverse | .[0].tagName // ""')"

if [[ -z "$LATEST_TAG" ]]; then
    echo "==> No prior vscode-v* release found."
    SUGGESTED_TAG="vscode-v0.1.0"
else
    echo "==> Latest release: $LATEST_TAG"
    VPART="${LATEST_TAG#vscode-v}"          # strip prefix → 0.2.4-alpha
    BASE="${VPART%%-*}"                     # base version → 0.2.4
    SUFFIX=""
    if [[ "$VPART" == *-* ]]; then SUFFIX="-${VPART#*-}"; fi
    MAJOR="$(echo "$BASE" | cut -d. -f1)"
    MINOR="$(echo "$BASE" | cut -d. -f2)"
    PATCH="$(echo "$BASE" | cut -d. -f3)"
    SUGGESTED_TAG="vscode-v${MAJOR}.${MINOR}.$((PATCH+1))${SUFFIX}"
fi

echo ""
echo "==> Suggested new tag: $SUGGESTED_TAG"
echo "    Press Enter to accept, or type a different tag to override (Ctrl-C cancels)."
NEW_TAG="$(prompt_with_default 'Tag: ' "$SUGGESTED_TAG")"

if [[ -z "$NEW_TAG" ]]; then
    echo "No tag entered. Aborting." >&2
    exit 1
fi

# ----- Validate format -----
if [[ ! "$NEW_TAG" =~ ^vscode-v[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$ ]]; then
    echo "Tag '$NEW_TAG' does not match expected format vscode-v<major>.<minor>.<patch>[-suffix]" >&2
    exit 2
fi

NEW_VERSION="${NEW_TAG#vscode-v}"
NEW_BASE="${NEW_VERSION%%-*}"
NEW_MAJOR="$(echo "$NEW_BASE" | cut -d. -f1)"

# ----- Uniqueness checks -----
if gh release view "$NEW_TAG" >/dev/null 2>&1; then
    echo "A release with tag '$NEW_TAG' already exists. Aborting." >&2
    exit 2
fi
if git rev-parse "refs/tags/$NEW_TAG" >/dev/null 2>&1; then
    echo "A local git tag '$NEW_TAG' already exists. Aborting." >&2
    exit 2
fi
if git ls-remote --tags origin "refs/tags/$NEW_TAG" 2>/dev/null | grep -q .; then
    echo "A remote git tag '$NEW_TAG' already exists. Aborting." >&2
    exit 2
fi

# ----- Highest-version check (strict-greater-than) -----
HIGHEST=""
while IFS= read -r t; do
    [[ -z "$t" ]] && continue
    base="${t#vscode-v}"; base="${base%%-*}"
    if [[ -z "$HIGHEST" ]] || [[ "$(version_cmp "$base" "$HIGHEST")" == "1" ]]; then
        HIGHEST="$base"
    fi
done < <(gh release list --limit 100 --json tagName --jq '.[] | .tagName | select(startswith("vscode-v"))')

if [[ -n "$HIGHEST" ]] && [[ "$(version_cmp "$NEW_BASE" "$HIGHEST")" != "1" ]]; then
    echo "Version $NEW_BASE is not strictly greater than highest existing version $HIGHEST. Aborting." >&2
    exit 2
fi

# ----- Pre-release rule: base < 1.0.0 OR has -suffix -----
PRERELEASE=false
if [[ "$NEW_MAJOR" -lt 1 ]] || [[ "$NEW_VERSION" == *-* ]]; then
    PRERELEASE=true
fi

# ----- CHANGELOG: ensure [Unreleased] is migrated to a new version section -----
# Delegates extraction + rewrite to Python (clean multi-line transformation; nothing
# subtle to debug in awk/sed). Modes:
#   extract  → prints the body lines of [Unreleased] (empty if section absent/empty)
#   rewrite  → moves [Unreleased] body into a new "## [NEW_BASE] - DATE" section,
#              leaving [Unreleased] empty. Writes in place to $1.
changelog_py() {
    python3 - "$@" <<'PY'
import sys, re
mode, path = sys.argv[1], sys.argv[2]
with open(path) as f:
    text = f.read()

# Find [Unreleased] body: everything between "## [Unreleased]" and the next "## " heading.
m = re.search(r'(?ms)^## \[Unreleased\][^\n]*\n(.*?)(?=^## |\Z)', text)
body = m.group(1) if m else ""

if mode == "extract":
    sys.stdout.write(body)
elif mode == "rewrite":
    new_base, release_date = sys.argv[3], sys.argv[4]
    new_header = f"## [{new_base}] - {release_date}"
    if m:
        # Strip trailing blank lines from the body but keep it intact otherwise.
        body_stripped = body.rstrip() + ("\n" if body.strip() else "")
        replacement = f"## [Unreleased]\n\n{new_header}\n{body_stripped}\n"
        text = text[:m.start()] + replacement + text[m.end():]
        with open(path, "w") as f:
            f.write(text)
PY
}

RELEASE_DATE="$(date +%Y-%m-%d)"
CHANGELOG_TOUCHED=false
if [[ -f "$CHANGELOG" ]]; then
    if grep -qE "^## \[$NEW_BASE\]" "$CHANGELOG"; then
        echo "==> CHANGELOG.md already has a section for [$NEW_BASE]; not modifying."
    else
        UNRELEASED_CONTENT="$(changelog_py extract "$CHANGELOG")"
        UNRELEASED_TRIMMED="$(printf '%s' "$UNRELEASED_CONTENT" | sed '/^[[:space:]]*$/d')"

        if [[ -z "$UNRELEASED_TRIMMED" ]]; then
            echo "==> CHANGELOG.md [Unreleased] section is empty; nothing to migrate."
        else
            echo ""
            echo "==> [Unreleased] section contents:"
            echo "---"
            printf '%s\n' "${UNRELEASED_CONTENT%$'\n'}"
            echo "---"
            if confirm "Move these lines into a new [$NEW_BASE] - $RELEASE_DATE section?" "y"; then
                if $DRY_RUN; then
                    TMP="$(mktemp)"; cp "$CHANGELOG" "$TMP"
                    changelog_py rewrite "$TMP" "$NEW_BASE" "$RELEASE_DATE"
                    echo "  [dry-run] CHANGELOG.md would be rewritten:"
                    diff -u "$CHANGELOG" "$TMP" || true
                    rm -f "$TMP"
                else
                    changelog_py rewrite "$CHANGELOG" "$NEW_BASE" "$RELEASE_DATE"
                    CHANGELOG_TOUCHED=true
                    echo "==> CHANGELOG.md updated."
                fi
            else
                echo "==> Skipped CHANGELOG migration (per your request)."
            fi
        fi
    fi
fi

# ----- Final confirmation -----
RELEASE_NOTES='**Extension Changelog**: https://marketplace.visualstudio.com/items/highbyte.dotnet-6502-debugger/changelog'

echo ""
echo "==> About to create release:"
echo "    Tag:         $NEW_TAG"
echo "    Title:       $NEW_TAG"
echo "    Pre-release: $PRERELEASE"
echo "    Notes:       $RELEASE_NOTES"
if $CHANGELOG_TOUCHED; then
    echo "    + Commit and push CHANGELOG.md update (\"Release $NEW_TAG\") to origin/master first"
fi
echo ""

if ! confirm "Proceed?" "y"; then
    echo "Aborted by user."
    if $CHANGELOG_TOUCHED && ! $DRY_RUN; then
        echo "Reverting local CHANGELOG.md change..."
        git checkout -- "$CHANGELOG"
    fi
    exit 1
fi

# ----- Commit + push CHANGELOG if it was edited -----
if $CHANGELOG_TOUCHED; then
    dry git add "$CHANGELOG"
    dry git commit -m "Release $NEW_TAG"
    dry git push origin master
fi

# ----- Create the release -----
PRERELEASE_FLAG=()
$PRERELEASE && PRERELEASE_FLAG=(--prerelease)

dry gh release create "$NEW_TAG" \
    --title "$NEW_TAG" \
    --notes "$RELEASE_NOTES" \
    "${PRERELEASE_FLAG[@]}"

echo ""
echo "✅ Release $NEW_TAG created."
echo "   The .github/workflows/release-vscode-extension.yml workflow will now publish to the VSCode Marketplace."
