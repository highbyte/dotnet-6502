#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

HEADLESS_PROJECT="src/apps/Highbyte.DotNet6502.App.Headless/Highbyte.DotNet6502.App.Headless.csproj"
REMOTE_PROJECT="src/apps/Highbyte.DotNet6502.App.RemoteClient/Highbyte.DotNet6502.App.RemoteClient.csproj"
HEADLESS_OUT="$REPO_ROOT/src/apps/Highbyte.DotNet6502.App.Headless/bin/Debug/net10.0"
REMOTE_OUT="$REPO_ROOT/src/apps/Highbyte.DotNet6502.App.RemoteClient/bin/Debug/net10.0"
HEADLESS_DLL="$HEADLESS_OUT/Highbyte.DotNet6502.App.Headless.dll"
REMOTE_DLL="$REMOTE_OUT/Highbyte.DotNet6502.App.RemoteClient.dll"
APPSETTINGS_PATH="$HEADLESS_OUT/appsettings.Development.json"

find_python() {
  if command -v python3 >/dev/null 2>&1; then
    echo "python3"
    return
  fi
  if command -v python >/dev/null 2>&1; then
    echo "python"
    return
  fi
  echo "Python 3 is required." >&2
  exit 1
}

PYTHON_BIN="$(find_python)"
REMOTE_PORT="$("$PYTHON_BIN" - <<'PY'
import socket
s = socket.socket()
s.bind(("127.0.0.1", 0))
print(s.getsockname()[1])
s.close()
PY
)"

TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/swiftlink-smoke.XXXXXX")"
PRG_PATH="$TMP_DIR/swiftlink-smoke.prg"
ECHO_PORT_FILE="$TMP_DIR/echo.port"
ECHO_LOG="$TMP_DIR/echo.log"
ECHO_STDOUT="$TMP_DIR/echo.stdout.log"
HEADLESS_LOG="$TMP_DIR/headless.log"
APPSETTINGS_BACKUP="$TMP_DIR/appsettings.Development.backup.json"

ECHO_PID=""
HEADLESS_PID=""
HAD_APPSETTINGS=0
SUCCESS=0

cleanup() {
  set +e
  if [[ -f "$REMOTE_DLL" ]]; then
    dotnet "$REMOTE_DLL" --port "$REMOTE_PORT" emu.quit >/dev/null 2>&1 || true
  fi
  if [[ -n "$HEADLESS_PID" ]] && kill -0 "$HEADLESS_PID" >/dev/null 2>&1; then
    kill "$HEADLESS_PID" >/dev/null 2>&1 || true
    wait "$HEADLESS_PID" >/dev/null 2>&1 || true
  fi
  if [[ -n "$ECHO_PID" ]] && kill -0 "$ECHO_PID" >/dev/null 2>&1; then
    kill "$ECHO_PID" >/dev/null 2>&1 || true
    wait "$ECHO_PID" >/dev/null 2>&1 || true
  fi
  if [[ "$HAD_APPSETTINGS" -eq 1 ]]; then
    cp "$APPSETTINGS_BACKUP" "$APPSETTINGS_PATH"
  else
    rm -f "$APPSETTINGS_PATH"
  fi
  if [[ "$SUCCESS" -eq 1 ]]; then
    rm -rf "$TMP_DIR"
  else
    echo "Smoke artifacts kept at $TMP_DIR" >&2
  fi
}
trap cleanup EXIT

if [[ -f "$APPSETTINGS_PATH" ]]; then
  cp "$APPSETTINGS_PATH" "$APPSETTINGS_BACKUP"
  HAD_APPSETTINGS=1
fi

echo "==> Building Headless app and remote client"
dotnet build "$REPO_ROOT/$HEADLESS_PROJECT" >/dev/null
dotnet build "$REPO_ROOT/$REMOTE_PROJECT" >/dev/null

echo "==> Starting local TCP echo server"
"$PYTHON_BIN" "$SCRIPT_DIR/tcp_echo_server.py" \
  --host 127.0.0.1 \
  --port 0 \
  --port-file "$ECHO_PORT_FILE" \
  --log-file "$ECHO_LOG" \
  >"$ECHO_STDOUT" 2>&1 &
ECHO_PID=$!

for _ in {1..40}; do
  [[ -f "$ECHO_PORT_FILE" ]] && break
  sleep 0.25
done

if [[ ! -f "$ECHO_PORT_FILE" ]]; then
  echo "Echo server did not publish a port." >&2
  exit 1
fi
ECHO_PORT="$(tr -d '[:space:]' <"$ECHO_PORT_FILE")"

echo "==> Generating SwiftLink smoke PRG"
"$PYTHON_BIN" "$SCRIPT_DIR/write_smoke_prg.py" "$PRG_PATH"

cat >"$APPSETTINGS_PATH" <<JSON
{
  "Highbyte.DotNet6502.C64.Headless": {
    "SwiftLinkTcpHost": "127.0.0.1",
    "SwiftLinkTcpPort": $ECHO_PORT,
    "SwiftLinkConnectOnBoot": true,
      "SystemConfig": {
      "SwiftLinkEnabled": true,
      "SwiftLinkCartridgeIOAddress": "DE00",
      "SwiftLinkReceiveMode": "FastBuffered"
    }
  }
}
JSON

echo "==> Starting Headless app"
DOTNET_ENVIRONMENT=Development \
dotnet "$HEADLESS_DLL" \
  --system C64 \
  --start \
  --waitForSystemReady \
  --remote-port "$REMOTE_PORT" \
  --allow-remote-quit \
  -l Warning \
  >"$HEADLESS_LOG" 2>&1 &
HEADLESS_PID=$!

remote_cmd() {
  dotnet "$REMOTE_DLL" --port "$REMOTE_PORT" "$@"
}

echo "==> Waiting for remote control endpoint"
for _ in {1..80}; do
  if remote_cmd emu.state >/dev/null 2>&1; then
    break
  fi
  sleep 0.25
done

if ! remote_cmd emu.state >/dev/null 2>&1; then
  echo "Remote control endpoint did not become ready." >&2
  echo "Headless log: $HEADLESS_LOG" >&2
  exit 1
fi

echo "==> Waiting for SwiftLink TCP connection"
for _ in {1..80}; do
  if [[ -f "$ECHO_STDOUT" ]] && grep -q "connected " "$ECHO_STDOUT"; then
    break
  fi
  sleep 0.25
done

if ! [[ -f "$ECHO_STDOUT" ]] || ! grep -q "connected " "$ECHO_STDOUT"; then
  echo "SwiftLink transport did not connect to the local echo server." >&2
  echo "Headless log: $HEADLESS_LOG" >&2
  echo "Echo server log: $ECHO_STDOUT" >&2
  exit 1
fi

echo "==> Loading PRG and starting it through remote control"
remote_cmd c64.loadprg --file "$PRG_PATH" >/dev/null
remote_cmd cpu.set --pc C000 >/dev/null

echo "==> Waiting for echoed byte to reach C64 memory"
VALUE=""
for _ in {1..80}; do
  if MEM_JSON="$(remote_cmd mem.read --addr C100 --len 1 2>/dev/null)"; then
    VALUE="$(printf '%s' "$MEM_JSON" | "$PYTHON_BIN" -c 'import json,sys; data=json.load(sys.stdin).get("data", []); print(data[0] if data else "")' 2>/dev/null || true)"
    if [[ "$VALUE" == "65" ]]; then
      break
    fi
  fi
  sleep 0.25
done

if [[ "$VALUE" != "65" ]]; then
  echo "Expected C64 memory \$C100 to contain 65, got '${VALUE:-<empty>}'." >&2
  echo "Headless log: $HEADLESS_LOG" >&2
  exit 1
fi

if ! grep -q '\b41\b' "$ECHO_LOG"; then
  echo "Echo server did not record the transmitted 41 byte." >&2
  echo "Echo log: $ECHO_LOG" >&2
  exit 1
fi

SUCCESS=1
echo "==> SwiftLink smoke test passed"
