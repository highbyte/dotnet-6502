#!/usr/bin/env bash
# Run the HotPathBenchmarks suite on a baseline ref (default: master) and on the
# current HEAD, then print a side-by-side comparison of the mean times and
# allocations. Flags any benchmark where HEAD is >= 5% slower than baseline or
# where HEAD allocates and baseline did not.
#
# Requirements:
#   - dotnet SDK matching global.json.
#   - Clean working tree (script switches branches with `git switch --detach`).
#   - python3 (for the table-diff step).
#
# Usage:
#   tools/perf-compare.sh [BASELINE_REF]
#     BASELINE_REF = git ref to compare against (default: master)

set -euo pipefail

BASELINE_REF="${1:-master}"
PROJECT="benchmarks/Highbyte.DotNet6502.Benchmarks/Highbyte.DotNet6502.Benchmarks.csproj"
ARTIFACTS_DIR="BenchmarkDotNet.Artifacts/results"
OUT_DIR="$(mktemp -d -t perf-compare.XXXXXX)"
trap 'rm -rf "$OUT_DIR"' EXIT

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

if ! git diff-index --quiet HEAD --; then
  echo "perf-compare: working tree is dirty -- commit or stash before running." >&2
  exit 2
fi

head_ref=$(git rev-parse --abbrev-ref HEAD)
if [ "$head_ref" = "HEAD" ]; then
  head_ref=$(git rev-parse HEAD)
fi

run_benchmark() {
  local label="$1"
  local outfile="$OUT_DIR/$label.csv"
  echo "perf-compare: running benchmarks on $label..."
  rm -rf "$ARTIFACTS_DIR"
  dotnet run -c Release --project "$PROJECT" -- \
    --filter '*HotPathBenchmarks*' \
    --exporters github \
    >/dev/null
  # BenchmarkDotNet writes results as <namespace>.HotPathBenchmarks-report-github.md
  # plus a CSV. Grab the CSV for diffing.
  local csv
  csv=$(ls "$ARTIFACTS_DIR"/*HotPathBenchmarks-report.csv 2>/dev/null | head -1 || true)
  if [ -z "$csv" ]; then
    echo "perf-compare: no benchmark CSV produced for $label" >&2
    exit 3
  fi
  cp "$csv" "$outfile"
}

echo "perf-compare: baseline = $BASELINE_REF, head = $head_ref"

git switch --detach "$BASELINE_REF"
run_benchmark baseline

git switch --detach "$head_ref"
run_benchmark head

# Restore the original branch (best effort -- detached state means we re-checkout).
git switch "$head_ref" 2>/dev/null || true

python3 - "$OUT_DIR/baseline.csv" "$OUT_DIR/head.csv" <<'PY'
import csv
import sys

baseline_path, head_path = sys.argv[1], sys.argv[2]

def load(path):
    with open(path, newline='') as f:
        return {row['Method']: row for row in csv.DictReader(f)}

baseline = load(baseline_path)
head = load(head_path)

def parse_time(s):
    # BenchmarkDotNet writes durations like "12.34 ns" or "1.23 us".
    units = {'ns': 1.0, 'us': 1_000.0, 'ms': 1_000_000.0, 's': 1_000_000_000.0}
    if not s:
        return None
    parts = s.split()
    if len(parts) != 2 or parts[1] not in units:
        return None
    return float(parts[0].replace(',', '')) * units[parts[1]]

def parse_alloc(s):
    if not s or s == '-':
        return 0.0
    units = {'B': 1.0, 'KB': 1024.0, 'MB': 1024.0 ** 2}
    parts = s.split()
    if len(parts) != 2 or parts[1] not in units:
        return None
    return float(parts[0].replace(',', '')) * units[parts[1]]

REGRESSION_RATIO = 1.05
fail = False

header = f"{'Method':<45} {'baseline':>14} {'head':>14} {'ratio':>8} {'alloc Δ':>10}"
print(header)
print('-' * len(header))

for method, head_row in head.items():
    baseline_row = baseline.get(method)
    if baseline_row is None:
        print(f"{method:<45} {'(new)':>14} {head_row.get('Mean', ''):>14}")
        continue
    b_mean = parse_time(baseline_row.get('Mean', ''))
    h_mean = parse_time(head_row.get('Mean', ''))
    b_alloc = parse_alloc(baseline_row.get('Allocated', ''))
    h_alloc = parse_alloc(head_row.get('Allocated', ''))
    if b_mean is None or h_mean is None:
        ratio_s = '?'
        ratio = None
    else:
        ratio = h_mean / b_mean
        ratio_s = f"{ratio:.3f}"
    alloc_delta = ''
    if b_alloc is not None and h_alloc is not None:
        delta = h_alloc - b_alloc
        if delta != 0:
            alloc_delta = f"{delta:+.0f}B"
        else:
            alloc_delta = '0'
    print(f"{method:<45} {baseline_row.get('Mean',''):>14} {head_row.get('Mean',''):>14} {ratio_s:>8} {alloc_delta:>10}")
    if ratio is not None and ratio >= REGRESSION_RATIO:
        print(f"  REGRESSION: {method} is {((ratio - 1) * 100):.1f}% slower")
        fail = True
    if b_alloc == 0 and h_alloc and h_alloc > 0:
        print(f"  REGRESSION: {method} introduces {h_alloc:.0f}B of allocations")
        fail = True

sys.exit(1 if fail else 0)
PY
