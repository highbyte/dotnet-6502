#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname)" != "Darwin" ]]; then
  echo "Error: This script currently only supports macOS." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

SOURCE="$SCRIPT_DIR/logo.png"
SOURCE_SOLID_BG="$SCRIPT_DIR/logo-solid-bg.png"
SOURCE_SIMPLE="$SCRIPT_DIR/logo-simple.png"
SOURCE_SIMPLE_SOLID_BG="$SCRIPT_DIR/logo-simple-solid-bg.png"
SOURCE_SIMPLE_TEXT="$SCRIPT_DIR/logo-simple-text.png"
SOURCE_SIMPLE_TEXT_SOLID_BG="$SCRIPT_DIR/logo-simple-text-solid-bg.png"

#SIZES=(256 128 64)
SIZES=(128)

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

if [[ ! -f "$SOURCE_SIMPLE_TEXT" ]]; then
  echo "Error: Source file not found: $SOURCE_SIMPLE_TEXT" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_SIMPLE_TEXT_SOLID_BG" ]]; then
  echo "Error: Source file not found: $SOURCE_SIMPLE_TEXT_SOLID_BG" >&2
  exit 1
fi

generate() {
  local source="$1"
  local prefix="$2"
  local size="$3"
  local output="$SCRIPT_DIR/${prefix}-${size}.png"
  sips --resampleHeightWidth "$size" "$size" "$source" --out "$output" > /dev/null
  echo "Generated: ${prefix}-${size}.png"
}

for size in "${SIZES[@]}"; do
  generate "$SOURCE" "logo" "$size"
done

for size in "${SIZES[@]}"; do
  generate "$SOURCE_SOLID_BG" "logo-solid-bg" "$size"
done

for size in "${SIZES[@]}"; do
  generate "$SOURCE_SIMPLE" "logo-simple" "$size"
done

for size in "${SIZES[@]}"; do
  generate "$SOURCE_SIMPLE_SOLID_BG" "logo-simple-solid-bg" "$size"
done

for size in "${SIZES[@]}"; do
  generate "$SOURCE_SIMPLE_TEXT" "logo-simple-text" "$size"
done

for size in "${SIZES[@]}"; do
  generate "$SOURCE_SIMPLE_TEXT_SOLID_BG" "logo-simple-text-solid-bg" "$size"
done

# Generate favicon.png (64x64)
#FAVICON_SOURCE="$SOURCE_SIMPLE_SOLID_BG"
FAVICON_SOURCE="$SOURCE_SIMPLE"
sips --resampleHeightWidth 64 64 "$FAVICON_SOURCE" --out "$SCRIPT_DIR/favicon.png" > /dev/null
echo "Generated: favicon.png"
# Generate favicon.ico (64x64) from logo-simple.png
sips --resampleHeightWidth 64 64 "$FAVICON_SOURCE" --setProperty format com.microsoft.ico --out "$SCRIPT_DIR/favicon.ico" > /dev/null
echo "Generated: favicon.ico"

# Generate AppIcon.icns for macOS 
APPLE_ICON_IMAGE="$SOURCE_SIMPLE"
#APPLE_ICON_IMAGE="$SOURCE_SIMPLE_SOLID_BG"
#APPLE_ICON_IMAGE="$SOURCE_SIMPLE_TEXT_SOLID_BG"
ICONSET_DIR="$SCRIPT_DIR/AppIcon.iconset"
mkdir -p "$ICONSET_DIR"
sips --resampleHeightWidth 16   16   "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_16x16.png"      > /dev/null
sips --resampleHeightWidth 32   32   "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_16x16@2x.png"   > /dev/null
sips --resampleHeightWidth 32   32   "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_32x32.png"      > /dev/null
sips --resampleHeightWidth 64   64   "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_32x32@2x.png"   > /dev/null
sips --resampleHeightWidth 128  128  "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_128x128.png"    > /dev/null
sips --resampleHeightWidth 256  256  "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_128x128@2x.png" > /dev/null
sips --resampleHeightWidth 256  256  "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_256x256.png"    > /dev/null
sips --resampleHeightWidth 512  512  "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_256x256@2x.png" > /dev/null
sips --resampleHeightWidth 512  512  "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_512x512.png"    > /dev/null
sips --resampleHeightWidth 1024 1024 "$APPLE_ICON_IMAGE" --out "$ICONSET_DIR/icon_512x512@2x.png" > /dev/null
iconutil -c icns "$ICONSET_DIR" -o "$SCRIPT_DIR/AppIcon.icns"
rm -rf "$ICONSET_DIR"
echo "Generated: AppIcon.icns"
