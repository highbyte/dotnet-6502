#!/usr/bin/env python3
import sys

from _safe_path import safe_path


PROGRAM_BYTES = bytes(
    [
        0x00,
        0xC0,
        0xA9,
        0x41,
        0x8D,
        0x00,
        0xDE,
        0xAD,
        0x01,
        0xDE,
        0x29,
        0x08,
        0xF0,
        0xF9,
        0xAD,
        0x00,
        0xDE,
        0x8D,
        0x00,
        0xC1,
        0x4C,
        0x12,
        0xC0,
    ]
)


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: write_smoke_prg.py <output.prg>", file=sys.stderr)
        return 2

    output_path = safe_path(sys.argv[1])
    output_path.write_bytes(PROGRAM_BYTES)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
