#!/usr/bin/env bash
# Wait for the SonarCloud branch analysis on the current commit, then report
# open Sonar issues at or above a severity threshold. Exits non-zero if any
# blocking issue is present, so it can be used locally as a quality
# gate before declaring a long task done.
#
# Relies on the sonarscan-dotnet.yml workflow firing on push to feature/**.
#
# Requirements:
#   - The current branch must have been pushed (the workflow runs on push).
#   - gh CLI authenticated (`gh auth status`).
#   - curl, jq.
#   - SONAR_TOKEN env for private projects; anonymous works for public ones.
#
# Usage:
#   tools/sonar-check.sh [MIN_SEVERITY]
#     MIN_SEVERITY = INFO | MINOR | MAJOR | CRITICAL | BLOCKER   (default: MAJOR)

set -euo pipefail

MIN_SEVERITY="${1:-MAJOR}"
PROJECT_KEY="${SONAR_PROJECT_KEY:-highbyte_dotnet-6502}"
SONAR_HOST="${SONAR_HOST_URL:-https://sonarcloud.io}"
WORKFLOW_FILE="sonarscan-dotnet.yml"

case "$MIN_SEVERITY" in
  INFO|MINOR|MAJOR|CRITICAL|BLOCKER) ;;
  *) echo "MIN_SEVERITY must be INFO|MINOR|MAJOR|CRITICAL|BLOCKER (got $MIN_SEVERITY)" >&2; exit 2 ;;
esac

for tool in gh curl jq git; do
  command -v "$tool" >/dev/null || { echo "Required tool missing: $tool" >&2; exit 2; }
done

BRANCH=$(git rev-parse --abbrev-ref HEAD)
SHA=$(git rev-parse HEAD)
echo "==> Branch: $BRANCH  sha: ${SHA:0:12}  threshold: $MIN_SEVERITY"

# Fail fast if the current commit hasn't been pushed. The Sonar workflow runs
# only on push, so a run for an unpushed sha cannot exist — without this check
# the script would spin for 60s waiting for a run that never appears.
if ! REMOTE_SHA=$(git rev-parse --verify --quiet "refs/remotes/origin/${BRANCH}"); then
  echo "Branch '$BRANCH' has not been pushed to origin." >&2
  echo "Push it first:  git push -u origin $BRANCH" >&2
  exit 2
fi
if [[ "$REMOTE_SHA" != "$SHA" ]]; then
  echo "Local HEAD (${SHA:0:12}) differs from origin/$BRANCH (${REMOTE_SHA:0:12})." >&2
  echo "The latest commit has not been pushed yet. Push first, then re-run:" >&2
  echo "  git push" >&2
  exit 2
fi

# Find the Sonar workflow run for the current commit. GitHub may take a few
# seconds to register the run after a push, so retry briefly.
RUN_ID=""
for _ in $(seq 1 12); do
  RUN_ID=$(gh run list --workflow="$WORKFLOW_FILE" --branch="$BRANCH" \
    --json databaseId,headSha --limit 20 \
    | jq -r --arg sha "$SHA" '.[] | select(.headSha == $sha) | .databaseId' \
    | head -n1)
  [[ -n "$RUN_ID" ]] && break
  sleep 5
done

if [[ -z "$RUN_ID" ]]; then
  echo "No $WORKFLOW_FILE run found for sha ${SHA:0:12} on branch $BRANCH after 60s." >&2
  echo "Hint: push the branch first; the workflow triggers on push to feature/**." >&2
  exit 2
fi

echo "==> Waiting for Sonar workflow run $RUN_ID ..."
if ! gh run watch "$RUN_ID" --exit-status >/dev/null; then
  echo "Sonar workflow failed; inspect the run in GitHub Actions." >&2
  exit 1
fi

# Server-side processing finishes a moment after the workflow. Poll briefly.
auth=()
[[ -n "${SONAR_TOKEN:-}" ]] && auth=(-u "${SONAR_TOKEN}:")

# inNewCodePeriod=true restricts the query to issues introduced on this branch
# since it diverged from master — i.e., what *this branch* added. Pre-existing
# issues on master are not the gate's concern. Set SONAR_INCLUDE_PREEXISTING=1
# to disable this filter (useful for auditing total branch debt).
NEW_CODE_FILTER="&inNewCodePeriod=true"
[[ "${SONAR_INCLUDE_PREEXISTING:-0}" == "1" ]] && NEW_CODE_FILTER=""

API="${SONAR_HOST}/api/issues/search?componentKeys=${PROJECT_KEY}&branch=${BRANCH}&statuses=OPEN&resolved=false&ps=500${NEW_CODE_FILTER}"

issues_json=""
for _ in $(seq 1 12); do
  # ${auth[@]+"${auth[@]}"} expands to the array elements only if non-empty;
  # plain "${auth[@]}" trips set -u "unbound variable" on empty arrays.
  issues_json=$(curl -fsS ${auth[@]+"${auth[@]}"} "$API" 2>/dev/null || true)
  if [[ -n "$issues_json" ]] && echo "$issues_json" | jq -e '.issues' >/dev/null 2>&1; then
    break
  fi
  sleep 5
done

if [[ -z "$issues_json" ]] || ! echo "$issues_json" | jq -e '.issues' >/dev/null 2>&1; then
  echo "Failed to fetch Sonar issues from $SONAR_HOST. Set SONAR_TOKEN for private projects." >&2
  exit 2
fi

echo "$issues_json" | jq -r --arg min "$MIN_SEVERITY" '
  def sev_rank: {"INFO":0,"MINOR":1,"MAJOR":2,"CRITICAL":3,"BLOCKER":4};
  [.issues[] | select(sev_rank[.severity] >= sev_rank[$min])] as $blocking
  | if ($blocking | length) == 0 then
      "==> No open Sonar issues at >= \($min). Clean."
    else
      "==> \($blocking | length) open Sonar issue(s) at >= \($min):\n"
      + (
          $blocking
          | sort_by(sev_rank[.severity])
          | reverse
          | map("  [\(.severity)] \(.component | sub("^[^:]+:"; "")):\(.line // "?")  \(.rule)\n      \(.message)")
          | join("\n")
        )
    end
'

count=$(echo "$issues_json" | jq --arg min "$MIN_SEVERITY" '
  def sev_rank: {"INFO":0,"MINOR":1,"MAJOR":2,"CRITICAL":3,"BLOCKER":4};
  [.issues[] | select(sev_rank[.severity] >= sev_rank[$min])] | length
')

[[ "$count" -eq 0 ]]
