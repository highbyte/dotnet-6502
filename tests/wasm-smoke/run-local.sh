#!/usr/bin/env bash
# Local replay of .github/workflows/wasm-aot-verify.yml. Publishes the chosen
# WASM app(s) with the same Release/AOT flags as CI, then runs the matching
# Playwright smoke spec against the published wwwroot.
#
# Usage: ./run-local.sh {blazor|avalonia-browser|all}

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
VERSION="0.0.0-local"

usage() {
  echo "Usage: $0 {blazor|avalonia-browser|all}" >&2
  exit 2
}

[ $# -eq 1 ] || usage

publish() {
  local name="$1" project="$2"
  local out="$BUILD_DIR/$name"
  echo "==> Publishing $name (Release, AOT) -> $out"
  rm -rf "$out"
  dotnet publish "$REPO_ROOT/$project" \
    -c Release \
    -p:Version="$VERSION" \
    -p:GHPages=true \
    -p:GHPagesBase=/ \
    -p:GHPagesInjectBrotliLoader=false \
    -o "$out"
}

apply_version_placeholder() {
  local root="$1"
  echo "==> Replacing {{APP_VERSION}} placeholders in $root"
  find "$root" -type f \( -name "*.html" -o -name "*.js" \) ! -name "*.br" ! -name "*.gz" -print0 \
    | while IFS= read -r -d '' f; do
        if [[ "$OSTYPE" == "darwin"* ]]; then
          sed -i '' "s/{{APP_VERSION}}/$VERSION/g" "$f"
        else
          sed -i "s/{{APP_VERSION}}/$VERSION/g" "$f"
        fi
      done
}

prepare_playwright() {
  echo "==> Ensuring Playwright + Chromium installed"
  cd "$SCRIPT_DIR"
  [ -d node_modules ] || npm install
  npx playwright install chromium >/dev/null
}

run_spec() {
  local name="$1" spec="$2"
  local root="$BUILD_DIR/$name/wwwroot"
  echo "==> Running Playwright spec for $name"
  cd "$SCRIPT_DIR"
  WASM_SITE_ROOT="$root" npx playwright test "$spec"
}

run_blazor() {
  publish blazor src/apps/BlazorWASM/Highbyte.DotNet6502.App.WASM/Highbyte.DotNet6502.App.WASM.csproj
  run_spec blazor blazor.spec.ts
}

run_avalonia_browser() {
  publish avalonia-browser src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/Highbyte.DotNet6502.App.Avalonia.Browser.csproj
  apply_version_placeholder "$BUILD_DIR/avalonia-browser/wwwroot"
  run_spec avalonia-browser avalonia-browser.spec.ts
}

prepare_playwright

case "$1" in
  blazor)            run_blazor ;;
  avalonia-browser)  run_avalonia_browser ;;
  all)               run_blazor; run_avalonia_browser ;;
  *)                 usage ;;
esac

echo "==> Smoke test passed"
