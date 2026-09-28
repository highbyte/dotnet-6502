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


def extract_set(commit, set_name, per_opcode, out_dir):
    """Writes one gzip'd JSON-lines file with the first N vectors of every opcode in a set."""
    out_path = os.path.join(out_dir, f"{set_name}.jsonl.gz")
    with gzip.open(out_path, "wt", encoding="utf-8", compresslevel=9) as out:
        for opcode in range(256):
            vectors = fetch_opcode(commit, set_name, opcode)
            if vectors is None:
                continue
            for v in vectors[:per_opcode]:
                out.write(json.dumps(v, separators=(",", ":")) + "\n")
            print(f"{set_name} {opcode:02x}: {min(len(vectors), per_opcode)} of {len(vectors)}", file=sys.stderr)


def fetch_opcode(commit, set_name, opcode):
    """Returns the vectors of one opcode file, or None when upstream has no usable file for it."""
    path = f"{set_name}/v1/{opcode:02x}.json"
    try:
        data = fetch(commit, path)
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print(f"{set_name}: no file for {opcode:02x}, skipping", file=sys.stderr)
            return None
        raise
    if not data.strip():
        # Upstream ships empty files for instructions that cannot be single-stepped
        # (the WDC set's WAI/STP).
        print(f"{set_name} {opcode:02x}: empty upstream file, skipping", file=sys.stderr)
        return None
    return json.loads(data)


def write_manifest(commit, per_opcode, out_dir):
    """Describes every set file present, so sets can be regenerated one at a time."""
    manifest = {"commit": commit, "perOpcode": per_opcode, "sets": {}}
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
    return manifest


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--commit", default=DEFAULT_COMMIT)
    ap.add_argument("--per-opcode", type=int, default=20)
    ap.add_argument("--sets", default=",".join(DEFAULT_SETS))
    args = ap.parse_args()
    out_dir = os.path.abspath(OUT_DIR)
    os.makedirs(out_dir, exist_ok=True)

    with open(os.path.join(out_dir, "LICENSE"), "wb") as f:
        f.write(fetch(args.commit, "LICENSE"))

    for set_name in [s for s in args.sets.split(",") if s]:
        extract_set(args.commit, set_name, args.per_opcode, out_dir)

    print(json.dumps(write_manifest(args.commit, args.per_opcode, out_dir), indent=2))


if __name__ == "__main__":
    main()
