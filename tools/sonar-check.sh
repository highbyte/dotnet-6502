#!/usr/bin/env bash
# Wait for the SonarCloud analysis on the current commit, then report
# open Sonar issues at or above a severity threshold. Exits non-zero if any
# blocking issue is present, so it can be used locally as a quality
# gate before declaring a long task done.
#
# Requirements:
#   - The current branch must have been pushed.
#   - gh CLI authenticated (`gh auth status`).
#   - curl, jq.
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

# Fail fast if the current commit hasn't been pushed. A workflow run for an
# unpushed sha cannot exist, so waiting for one would only time out.
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

# Pull requests use Sonar's PR analysis; master and manually dispatched feature
# branches use branch analysis. If a feature branch has no open PR, start the
# existing workflow_dispatch trigger so the local gate remains useful before a
# PR is opened without restoring the duplicate push + pull_request scans.
PR_NUMBER=$(gh pr list --head "$BRANCH" --state open --limit 20 \
  --json number,headRefOid \
  | jq -r --arg sha "$SHA" '.[] | select(.headRefOid == $sha) | .number' \
  | head -n1)

if [[ -n "$PR_NUMBER" ]]; then
  EXPECTED_EVENT="pull_request"
  ANALYSIS_PARAMETER="pullRequest=${PR_NUMBER}"
  echo "==> Pull request: #$PR_NUMBER"
elif [[ "$BRANCH" == "master" ]]; then
  EXPECTED_EVENT="push"
  ANALYSIS_PARAMETER="branch=${BRANCH}"
else
  EXPECTED_EVENT="workflow_dispatch"
  ANALYSIS_PARAMETER="branch=${BRANCH}"

  EXISTING_RUN=$(gh run list --workflow="$WORKFLOW_FILE" --branch="$BRANCH" \
    --json headSha,event --limit 20 \
    | jq -r --arg sha "$SHA" \
      '.[] | select(.headSha == $sha and .event == "workflow_dispatch") | .headSha' \
    | head -n1)
  if [[ -z "$EXISTING_RUN" ]]; then
    echo "==> No open PR; starting a manual Sonar branch analysis ..."
    gh workflow run "$WORKFLOW_FILE" --ref "$BRANCH"
  fi
fi

# Find the event-specific Sonar workflow run for the current commit. GitHub may
# take a few seconds to register a PR or manually dispatched run, so retry.
RUN_ID=""
for _ in $(seq 1 12); do
  RUN_ID=$(gh run list --workflow="$WORKFLOW_FILE" --branch="$BRANCH" \
    --json databaseId,headSha,event --limit 20 \
    | jq -r --arg sha "$SHA" --arg event "$EXPECTED_EVENT" \
      '.[] | select(.headSha == $sha and .event == $event) | .databaseId' \
    | head -n1)
  [[ -n "$RUN_ID" ]] && break
  sleep 5
done

if [[ -z "$RUN_ID" ]]; then
  echo "No $EXPECTED_EVENT $WORKFLOW_FILE run found for sha ${SHA:0:12} on branch $BRANCH after 60s." >&2
  exit 2
fi

echo "==> Waiting for Sonar workflow run $RUN_ID ..."
if ! gh run watch "$RUN_ID" --exit-status >/dev/null; then
  echo "Sonar workflow failed; inspect the run in GitHub Actions." >&2
  exit 1
fi

# Server-side processing finishes a moment after the workflow. Poll briefly.

# inNewCodePeriod=true restricts the query to issues introduced on this branch
# since it diverged from master — i.e., what *this branch* added. Pre-existing
# issues on master are not the gate's concern. Set SONAR_INCLUDE_PREEXISTING=1
# to disable this filter (useful for auditing total branch debt).
NEW_CODE_FILTER="&inNewCodePeriod=true"
[[ "${SONAR_INCLUDE_PREEXISTING:-0}" == "1" ]] && NEW_CODE_FILTER=""

API="${SONAR_HOST}/api/issues/search?componentKeys=${PROJECT_KEY}&${ANALYSIS_PARAMETER}&statuses=OPEN&resolved=false&ps=500${NEW_CODE_FILTER}"

issues_json=""
for _ in $(seq 1 12); do
  issues_json=$(curl -fsS "$API" 2>/dev/null || true)
  if [[ -n "$issues_json" ]] && echo "$issues_json" | jq -e '.issues' >/dev/null 2>&1; then
    break
  fi
  sleep 5
done

if [[ -z "$issues_json" ]] || ! echo "$issues_json" | jq -e '.issues' >/dev/null 2>&1; then
  echo "Failed to fetch Sonar issues from $SONAR_HOST." >&2
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
