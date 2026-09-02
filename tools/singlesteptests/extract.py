#!/usr/bin/env python3
"""Vendor a pinned subset of the SingleStepTests 65x02 corpus for the CPU bus-cycle tests.

The corpus (https://github.com/SingleStepTests/65x02, MIT) has 10,000 randomly generated
vectors per opcode, each with the full before/after CPU and memory state and every bus cycle
(address, value, read/write). One file per opcode is ~5 MB, so the whole thing cannot be
committed. This script downloads the per-opcode files for the requested sets at a pinned commit,
keeps the first N vectors of each opcode, and writes one gzip'd JSON-lines file per set into the
test fixture directory together with the upstream LICENSE and a manifest. The selection is
deterministic (first N), so re-running with the same commit and N reproduces the fixtures.

Usage:
  tools/singlesteptests/extract.py [--commit SHA] [--per-opcode N] [--sets 6502,wdc65c02]
"""
import argparse
import gzip
import hashlib
import json
import os
import sys
import urllib.request

DEFAULT_COMMIT = "2f6980a2d95757486c7bee24355c360e40e2a224"
DEFAULT_SETS = ["6502", "wdc65c02"]
RAW = "https://raw.githubusercontent.com/SingleStepTests/65x02/{commit}/{path}"
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "tests", "Highbyte.DotNet6502.Tests", "Fixtures", "SingleStepTests")


def fetch(commit, path, attempts=3):
    last = None
    for _ in range(attempts):
        try:
            with urllib.request.urlopen(RAW.format(commit=commit, path=path), timeout=120) as r:
                return r.read()
        except urllib.error.HTTPError:
            raise
        except Exception as e:  # transient network trouble: retry
            last = e
    raise last


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--commit", default=DEFAULT_COMMIT)
    ap.add_argument("--per-opcode", type=int, default=20)
    ap.add_argument("--sets", default=",".join(DEFAULT_SETS))
    args = ap.parse_args()
    sets = [s for s in args.sets.split(",") if s]
    out_dir = os.path.abspath(OUT_DIR)
    os.makedirs(out_dir, exist_ok=True)

    with open(os.path.join(out_dir, "LICENSE"), "wb") as f:
        f.write(fetch(args.commit, "LICENSE"))

    manifest = {"commit": args.commit, "perOpcode": args.per_opcode, "sets": {}}
    for s in sets:
        out_path = os.path.join(out_dir, f"{s}.jsonl.gz")
        count = 0
        with gzip.open(out_path, "wt", encoding="utf-8", compresslevel=9) as out:
            for opcode in range(256):
                path = f"{s}/v1/{opcode:02x}.json"
                try:
                    data = fetch(args.commit, path)
                except urllib.error.HTTPError as e:
                    if e.code == 404:
                        print(f"{s}: no file for {opcode:02x}, skipping", file=sys.stderr)
                        continue
                    raise
                if not data.strip():
                    # Upstream ships empty files for instructions that cannot be single-stepped
                    # (the WDC set's WAI/STP).
                    print(f"{s} {opcode:02x}: empty upstream file, skipping", file=sys.stderr)
                    continue
                vectors = json.loads(data)
                for v in vectors[: args.per_opcode]:
                    out.write(json.dumps(v, separators=(",", ":")) + "\n")
                    count += 1
                print(f"{s} {opcode:02x}: {min(len(vectors), args.per_opcode)} of {len(vectors)}", file=sys.stderr)

    # The manifest describes every set file present, so sets can be regenerated one at a time.
    for name in sorted(os.listdir(out_dir)):
        if not name.endswith(".jsonl.gz"):
            continue
        out_path = os.path.join(out_dir, name)
        with gzip.open(out_path, "rt", encoding="utf-8") as f:
            count = sum(1 for line in f if line.strip())
        digest = hashlib.sha256(open(out_path, "rb").read()).hexdigest()
        manifest["sets"][name[: -len(".jsonl.gz")]] = {"file": name, "vectors": count, "sha256": digest}

    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
