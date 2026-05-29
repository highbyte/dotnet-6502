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

PAYLOAD_START=0x30
PAYLOAD_COUNT=16

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

TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/swiftlink-irq-smoke.XXXXXX")"
PRG_PATH="$TMP_DIR/swiftlink-irq-smoke.prg"
SERVER_PORT_FILE="$TMP_DIR/server.port"
SERVER_LOG="$TMP_DIR/server.log"
SERVER_STDOUT="$TMP_DIR/server.stdout.log"
SERVER_TRIGGER="$TMP_DIR/server.trigger"
HEADLESS_LOG="$TMP_DIR/headless.log"
APPSETTINGS_BACKUP="$TMP_DIR/appsettings.Development.backup.json"

SERVER_PID=""
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
  if [[ -n "$SERVER_PID" ]] && kill -0 "$SERVER_PID" >/dev/null 2>&1; then
    kill "$SERVER_PID" >/dev/null 2>&1 || true
    wait "$SERVER_PID" >/dev/null 2>&1 || true
  fi
  if [[ "$HAD_APPSETTINGS" -eq 1 ]]; then
    cp "$APPSETTINGS_BACKUP" "$APPSETTINGS_PATH"
  else
    rm -f "$APPSETTINGS_PATH"
  fi
  if [[ "$SUCCESS" -eq 1 ]]; then
    rm -rf "$TMP_DIR"
  else
    echo "IRQ smoke artifacts kept at $TMP_DIR" >&2
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

echo "==> Starting local TCP burst server"
"$PYTHON_BIN" "$SCRIPT_DIR/tcp_burst_server.py" \
  --host 127.0.0.1 \
  --port 0 \
  --port-file "$SERVER_PORT_FILE" \
  --log-file "$SERVER_LOG" \
  --trigger-file "$SERVER_TRIGGER" \
  --start-byte "$PAYLOAD_START" \
  --count "$PAYLOAD_COUNT" \
  >"$SERVER_STDOUT" 2>&1 &
SERVER_PID=$!

for _ in {1..40}; do
  [[ -f "$SERVER_PORT_FILE" ]] && break
  sleep 0.25
done

if [[ ! -f "$SERVER_PORT_FILE" ]]; then
  echo "Burst server did not publish a port." >&2
  exit 1
fi
SERVER_PORT="$(tr -d '[:space:]' <"$SERVER_PORT_FILE")"

echo "==> Generating SwiftLink IRQ smoke PRG"
"$PYTHON_BIN" "$SCRIPT_DIR/write_irq_smoke_prg.py" "$PRG_PATH"

cat >"$APPSETTINGS_PATH" <<JSON
{
  "Highbyte.DotNet6502.C64.Headless": {
    "SwiftLinkHost": {
      "TcpHost": "127.0.0.1",
      "TcpPort": $SERVER_PORT,
      "ConnectOnBoot": true
    },
    "SystemConfig": {
      "SwiftLink": {
        "Enabled": true,
        "CartridgeIOAddress": "DE00",
        "ReceiveMode": "FastBuffered"
      }
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
  if [[ -f "$SERVER_STDOUT" ]] && grep -q "connected " "$SERVER_STDOUT"; then
    break
  fi
  sleep 0.25
done

if ! [[ -f "$SERVER_STDOUT" ]] || ! grep -q "connected " "$SERVER_STDOUT"; then
  echo "SwiftLink transport did not connect to the burst server." >&2
  echo "Headless log: $HEADLESS_LOG" >&2
  echo "Server log: $SERVER_STDOUT" >&2
  exit 1
fi

echo "==> Loading IRQ-driven PRG and starting it through remote control"
remote_cmd c64.loadprg --file "$PRG_PATH" >/dev/null
remote_cmd cpu.set --pc C000 >/dev/null

echo "==> Triggering burst send"
touch "$SERVER_TRIGGER"

echo "==> Waiting for interrupt handler to capture burst"
DONE_VALUE=""
COUNT_VALUE=""
for _ in {1..120}; do
  if DONE_JSON="$(remote_cmd mem.read --addr C101 --len 1 2>/dev/null)"; then
    DONE_VALUE="$(printf '%s' "$DONE_JSON" | "$PYTHON_BIN" -c 'import json,sys; data=json.load(sys.stdin).get("data", []); print(data[0] if data else "")' 2>/dev/null || true)"
  fi
  if COUNT_JSON="$(remote_cmd mem.read --addr C100 --len 1 2>/dev/null)"; then
    COUNT_VALUE="$(printf '%s' "$COUNT_JSON" | "$PYTHON_BIN" -c 'import json,sys; data=json.load(sys.stdin).get("data", []); print(data[0] if data else "")' 2>/dev/null || true)"
  fi
  if [[ "$DONE_VALUE" == "170" && "$COUNT_VALUE" == "$PAYLOAD_COUNT" ]]; then
    break
  fi
  sleep 0.25
done

if [[ "$DONE_VALUE" != "170" || "$COUNT_VALUE" != "$PAYLOAD_COUNT" ]]; then
  echo "IRQ smoke did not finish as expected. done=${DONE_VALUE:-<empty>} count=${COUNT_VALUE:-<empty>}." >&2
  echo "Headless log: $HEADLESS_LOG" >&2
  exit 1
fi

BUFFER_JSON="$(remote_cmd mem.read --addr C200 --len "$PAYLOAD_COUNT")"
ACTUAL_HEX="$(printf '%s' "$BUFFER_JSON" | "$PYTHON_BIN" -c 'import json,sys; data=json.load(sys.stdin).get("data", []); print("".join(f"{b:02X}" for b in data))')"
EXPECTED_HEX="$("$PYTHON_BIN" - <<PY
start = $PAYLOAD_START
count = $PAYLOAD_COUNT
print("".join(f"{(start + i) & 0xFF:02X}" for i in range(count)))
PY
)"

if [[ "$ACTUAL_HEX" != "$EXPECTED_HEX" ]]; then
  echo "Captured IRQ buffer mismatch." >&2
  echo "Expected: $EXPECTED_HEX" >&2
  echo "Actual:   $ACTUAL_HEX" >&2
  exit 1
fi

if ! grep -q "$EXPECTED_HEX" "$SERVER_LOG"; then
  echo "Burst server did not log the expected payload." >&2
  echo "Server log: $SERVER_LOG" >&2
  exit 1
fi

SUCCESS=1
echo "==> SwiftLink IRQ smoke test passed"
