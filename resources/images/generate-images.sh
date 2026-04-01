#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname)" != "Darwin" ]]; then
  echo "Error: This script currently only supports macOS." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE="$SCRIPT_DIR/logo.png"
SOURCE_SIMPLE="$SCRIPT_DIR/logo-simple.png"
SOURCE_SOLID_BG="$SCRIPT_DIR/logo-solid-bg.png"

if [[ ! -f "$SOURCE" ]]; then
  echo "Error: Source file not found: $SOURCE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_SIMPLE" ]]; then
  echo "Error: Source file not found: $SOURCE_SIMPLE" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_SOLID_BG" ]]; then
  echo "Error: Source file not found: $SOURCE_SOLID_BG" >&2
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

# Generate logo-solid-bg variants from logo-solid-bg.png
for size in 256 128 64; do
  sips --resampleHeightWidth "$size" "$size" "$SOURCE_SOLID_BG" --out "$SCRIPT_DIR/logo-solid-bg-${size}.png" > /dev/null
  echo "Generated: logo-solid-bg-${size}.png"
done

# Generate favicon.png (64x64) from logo-simple.png
sips --resampleHeightWidth 64 64 "$SOURCE_SIMPLE" --out "$SCRIPT_DIR/favicon.png" > /dev/null
echo "Generated: favicon.png"

# Generate favicon.ico (64x64) from logo-simple.png
sips --resampleHeightWidth 64 64 "$SOURCE_SIMPLE" --setProperty format com.microsoft.ico --out "$SCRIPT_DIR/favicon.ico" > /dev/null
echo "Generated: favicon.ico"

# Generate AppIcon.icns for macOS from logo-simple.png
ICONSET_DIR="$SCRIPT_DIR/AppIcon.iconset"
mkdir -p "$ICONSET_DIR"
sips --resampleHeightWidth 16   16   "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_16x16.png"      > /dev/null
sips --resampleHeightWidth 32   32   "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_16x16@2x.png"   > /dev/null
sips --resampleHeightWidth 32   32   "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_32x32.png"      > /dev/null
sips --resampleHeightWidth 64   64   "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_32x32@2x.png"   > /dev/null
sips --resampleHeightWidth 128  128  "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_128x128.png"    > /dev/null
sips --resampleHeightWidth 256  256  "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_128x128@2x.png" > /dev/null
sips --resampleHeightWidth 256  256  "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_256x256.png"    > /dev/null
sips --resampleHeightWidth 512  512  "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_256x256@2x.png" > /dev/null
sips --resampleHeightWidth 512  512  "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_512x512.png"    > /dev/null
sips --resampleHeightWidth 1024 1024 "$SOURCE_SIMPLE" --out "$ICONSET_DIR/icon_512x512@2x.png" > /dev/null
iconutil -c icns "$ICONSET_DIR" -o "$SCRIPT_DIR/AppIcon.icns"
rm -rf "$ICONSET_DIR"
echo "Generated: AppIcon.icns"
