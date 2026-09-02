#!/usr/bin/env bash
# Run a BenchmarkDotNet suite on a baseline ref (default: master) and on the current HEAD, then
# print a side-by-side comparison of mean times and allocations. Flags any benchmark where HEAD is
# >= 5% slower than baseline or where HEAD allocates and baseline did not.
#
# Covers the CPU hot-path suite and the integrated C64 instruction/frame suites by default; set
# PERF_COMPARE_FILTER to a space-separated list of BenchmarkDotNet --filter globs to change that.
# Benchmarks are keyed on their full name (class, method and parameters), so parameterised
# suites compare row by row.
#
# Requirements:
#   - dotnet SDK.
#   - Clean working tree (script switches branches with `git switch --detach`).
#   - python3 (for the comparison step).
#
# Usage:
#   tools/perf-compare.sh [BASELINE_REF]
#     BASELINE_REF = git ref to compare against (default: master)
#   PERF_COMPARE_FILTER='*HotPathBenchmarks*' tools/perf-compare.sh   # only the CPU suite

set -euo pipefail

BASELINE_REF="${1:-master}"
FILTER="${PERF_COMPARE_FILTER:-*HotPathBenchmarks* *C64ExecuteFrameBenchmark* *C64ExecuteInstructionBenchmark*}"
PROJECT="benchmarks/Highbyte.DotNet6502.Benchmarks/Highbyte.DotNet6502.Benchmarks.csproj"
ARTIFACTS_DIR="BenchmarkDotNet.Artifacts"
OUT_DIR="$(mktemp -d -t perf-compare.XXXXXX)"
trap 'rm -rf "$OUT_DIR"' EXIT

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

if ! git diff-index --quiet HEAD --; then
  echo "perf-compare: working tree is dirty -- commit or stash before running." >&2
  exit 2
fi

head_ref=$(git rev-parse --abbrev-ref HEAD)
if [[ "$head_ref" = "HEAD" ]]; then
  head_ref=$(git rev-parse HEAD)
fi

run_benchmark() {
  local label="$1"
  local outdir="$OUT_DIR/$label"
  mkdir -p "$outdir"
  echo "perf-compare: running benchmarks on $label ($(git rev-parse --short HEAD))..."
  rm -rf "$ARTIFACTS_DIR"
  # shellcheck disable=SC2086 -- FILTER is a deliberate list of globs.
  dotnet run -c Release --project "$PROJECT" -- \
    --filter $FILTER \
    --exporters fulljson \
    --artifacts "$ARTIFACTS_DIR" \
    >/dev/null
  local found=0
  for f in "$ARTIFACTS_DIR"/results/*-report-full.json; do
    [[ -f "$f" ]] || continue
    cp "$f" "$outdir/"
    found=1
  done
  if [[ "$found" -eq 0 ]]; then
    echo "perf-compare: no benchmark JSON produced for $label" >&2
    exit 3
  fi
}

echo "perf-compare: baseline = $BASELINE_REF, head = $head_ref, filter = $FILTER"

git switch --detach "$BASELINE_REF"
run_benchmark baseline

git switch --detach "$head_ref"
run_benchmark head

# Restore the original branch (best effort -- detached state means we re-checkout).
git switch "$head_ref" 2>/dev/null || true

python3 - "$OUT_DIR/baseline" "$OUT_DIR/head" <<'PY'
import glob
import json
import os
import sys

baseline_dir, head_dir = sys.argv[1], sys.argv[2]

def short_name(full_name):
    # "Ns.Sub.Class.Method(Param: X)" -> "Class.Method(Param: X)"
    head, sep, params = full_name.partition('(')
    parts = head.split('.')
    return '.'.join(parts[-2:]) + sep + params

def load(directory):
    rows = {}
    for path in sorted(glob.glob(os.path.join(directory, '*-report-full.json'))):
        with open(path, encoding='utf-8-sig') as f:
            report = json.load(f)
        for b in report.get('Benchmarks', []):
            stats = b.get('Statistics') or {}
            memory = b.get('Memory') or {}
            rows[b['FullName']] = {
                'mean_ns': stats.get('Mean'),
                'alloc_b': memory.get('BytesAllocatedPerOperation'),
            }
    return rows

def fmt_time(ns):
    if ns is None:
        return '?'
    for unit, div in (('s', 1e9), ('ms', 1e6), ('us', 1e3)):
        if ns >= div:
            return f"{ns / div:,.2f} {unit}"
    return f"{ns:,.2f} ns"

baseline = load(baseline_dir)
head = load(head_dir)

REGRESSION_RATIO = 1.05
fail = False

header = f"{'Benchmark':<70} {'baseline':>12} {'head':>12} {'ratio':>7} {'alloc Δ':>9}"
print(header)
print('-' * len(header))

for full_name, h in head.items():
    name = short_name(full_name)
    b = baseline.get(full_name)
    if b is None:
        print(f"{name:<70} {'(new)':>12} {fmt_time(h['mean_ns']):>12}")
        continue
    ratio = None
    ratio_s = '?'
    if b['mean_ns'] and h['mean_ns']:
        ratio = h['mean_ns'] / b['mean_ns']
        ratio_s = f"{ratio:.3f}"
    b_alloc = b['alloc_b'] or 0
    h_alloc = h['alloc_b'] or 0
    delta = h_alloc - b_alloc
    alloc_s = f"{delta:+.0f}B" if delta else '0'
    print(f"{name:<70} {fmt_time(b['mean_ns']):>12} {fmt_time(h['mean_ns']):>12} {ratio_s:>7} {alloc_s:>9}")
    if ratio is not None and ratio >= REGRESSION_RATIO:
        print(f"  REGRESSION: {name} is {((ratio - 1) * 100):.1f}% slower")
        fail = True
    if b_alloc == 0 and h_alloc > 0:
        print(f"  REGRESSION: {name} introduces {h_alloc:.0f}B of allocations")
        fail = True

for full_name in baseline:
    if full_name not in head:
        print(f"{short_name(full_name):<70} {'(removed)':>12}")

sys.exit(1 if fail else 0)
PY
