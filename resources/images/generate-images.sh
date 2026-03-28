#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname)" != "Darwin" ]]; then
  echo "Error: This script currently only supports macOS." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE="$SCRIPT_DIR/logo.png"
SOURCE_SIMPLE="$SCRIPT_DIR/logo-simple.png"

if [[ ! -f "$SOURCE" ]]; then
  echo "Error: Source file not found: $SOURCE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_SIMPLE" ]]; then
  echo "Error: Source file not found: $SOURCE_SIMPLE" >&2
  exit 1
fi

generate() {
  local size="$1"
  local output="$SCRIPT_DIR/logo-${size}.png"
  sips --resampleHeightWidth "$size" "$size" "$SOURCE" --out "$output" > /dev/null
  echo "Generated: logo-${size}.png"
}

generate 256
generate 128
generate 64

# Generate favicon.png (64x64) from logo-simple.png
sips --resampleHeightWidth 64 64 "$SOURCE_SIMPLE" --out "$SCRIPT_DIR/favicon.png" > /dev/null
echo "Generated: favicon.png"

# Generate favicon.ico (64x64) from logo-simple.png
sips --resampleHeightWidth 64 64 "$SOURCE_SIMPLE" --setProperty format com.microsoft.ico --out "$SCRIPT_DIR/favicon.ico" > /dev/null
echo "Generated: favicon.ico"
