# TCP Remote Control

## Overview

The emulator supports a persistent TCP remote control endpoint that lets external processes inspect and drive a running emulator instance in real time. It is designed for automation, AI agent integration, and tooling that needs ad-hoc access without embedding a Lua script inside the emulator process.

Key design points:
- **Persistent TCP connection** — one client at a time; the server accepts a new client after the previous one disconnects.
- **Newline-delimited JSON** — every request is a single JSON object terminated by `\n`; every response is a single JSON object terminated by `\n`.
- **Platform-agnostic** — the same protocol works against both the Avalonia Desktop app and the headless console app.
- **Non-exclusive** — user input from keyboard/joystick and remote input coexist; neither locks the other out.
- **Frame-synchronized input** — joystick, keyboard, and memory-write commands are queued and executed at the next frame boundary so they do not race with the CPU.

---

## Starting the Emulator with Remote Control

### Avalonia Desktop

```sh
# Start on a fixed port
dotnet run --project src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Desktop -- --remote-port 6510

# Or via the published binary
./Highbyte.DotNet6502.App.Avalonia.Desktop --remote-port 6510
```

When the server is listening the **Debug & Remoting tab** shows a *Remote Control Server* section with the status `Listening on :6510`. When a client connects a blue banner appears at the bottom of the window: `• Remote Control Connected (port 6510)`.

### Headless

```sh
dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless -- \
  --remote-port 6510 \
  --system C64 --start

# Allow the emu.quit command (disabled by default on headless too unless opted in)
dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless -- \
  --remote-port 6510 --allow-remote-quit \
  --system C64 --start
```

---

## Protocol

### Request format

```json
{"id": 1, "cmd": "emu.state"}
{"id": 2, "cmd": "mem.read", "addr": "C000", "len": 16}
```

`id` is optional. If supplied it is echoed back in the response, which lets clients correlate responses when pipelining multiple requests.

### Response format

Success:
```json
{"id": 1, "ok": true, "state": "Running", "system": "C64"}
```

Failure:
```json
{"id": 99, "ok": false, "error": "Emulator not running"}
```

Null-valued fields are omitted from the response.

---

## Command Reference

### `emu.state`

Returns the current emulator state and selected system name.

**Request**
```json
{"id": 1, "cmd": "emu.state"}
```

**Response fields**

| Field    | Type   | Description                                              |
|----------|--------|----------------------------------------------------------|
| `state`  | string | `Uninitialized`, `Running`, or `Paused`                  |
| `system` | string | Currently selected system, e.g. `C64` or `Generic`      |

**Response example**
```json
{"id": 1, "ok": true, "state": "Uninitialized", "system": "C64"}
```

---

### `emu.start`

Starts the emulator. Equivalent to clicking the **Start** button in the UI.

```json
{"id": 2, "cmd": "emu.start"}
```
```json
{"id": 2, "ok": true}
```

---

### `emu.stop`

Stops the emulator and resets it to the `Uninitialized` state.

```json
{"id": 3, "cmd": "emu.stop"}
```
```json
{"id": 3, "ok": true}
```

---

### `emu.pause`

Pauses emulation without resetting state.

```json
{"id": 4, "cmd": "emu.pause"}
```
```json
{"id": 4, "ok": true}
```

---

### `emu.reset`

Stops and immediately restarts the emulator.

```json
{"id": 5, "cmd": "emu.reset"}
```
```json
{"id": 5, "ok": true}
```

---

### `emu.quit`

Terminates the host application. **Headless only** — requires `--allow-remote-quit`. Returns an error in the Avalonia Desktop app.

```json
{"id": 6, "cmd": "emu.quit"}
```
```json
{"id": 6, "ok": true}
```

---

### `cpu.get`

Returns all CPU registers. The emulator must be running or paused.

```json
{"id": 7, "cmd": "cpu.get"}
```

**Response fields**

| Field   | Type   | Description                        |
|---------|--------|------------------------------------|
| `pc`    | string | Program Counter as 4-digit hex     |
| `a`     | int    | Accumulator                        |
| `x`     | int    | X register                         |
| `y`     | int    | Y register                         |
| `sp`    | int    | Stack Pointer                      |
| `flags` | string | Processor status bits: `NV-BDIZC`  |

```json
{"id": 7, "ok": true, "pc": "E5CD", "a": 0, "x": 0, "y": 0, "sp": 255, "flags": "----I--C"}
```

---

### `mem.read`

Reads bytes from the system's address space. The emulator must be running or paused.

**Parameters**

| Parameter | Type   | Description                                 |
|-----------|--------|---------------------------------------------|
| `addr`    | string | Start address as a hex string (e.g. `C000`) |
| `len`     | int    | Number of bytes to read (1–4096)            |

```json
{"id": 8, "cmd": "mem.read", "addr": "C000", "len": 4}
```
```json
{"id": 8, "ok": true, "data": [0, 169, 0, 133]}
```

---

### `mem.write`

Writes bytes into the system's address space. Executed at the next frame boundary.

**Parameters**

| Parameter | Type         | Description                                    |
|-----------|--------------|------------------------------------------------|
| `addr`    | string       | Start address as a hex string                  |
| `data`    | array of int | Byte values to write (0–255 each)              |

```json
{"id": 9, "cmd": "mem.write", "addr": "C000", "data": [169, 42, 133, 254]}
```
```json
{"id": 9, "ok": true}
```

---

### `joystick.set`

Sets joystick direction and fire button state for the next frame. Any combination of directions may be active simultaneously. Executed at the next frame boundary.

**Parameters**

| Parameter | Type | Description                    |
|-----------|------|--------------------------------|
| `port`    | int  | Joystick port: `1` or `2`      |
| `up`      | bool | Up direction                   |
| `down`    | bool | Down direction                 |
| `left`    | bool | Left direction                 |
| `right`   | bool | Right direction                |
| `fire`    | bool | Fire button                    |

Only the fields you include are changed; omitted fields are not touched.

When using `dotnet-6502-remote`, you can explicitly clear an action with `--no-up`, `--no-down`, `--no-left`, `--no-right`, or `--no-fire`. The client also accepts `--up false` style boolean values. This is mainly useful when multiple `joystick.set` updates are queued before the same frame boundary; across frames, `joystick.set` is already non-persistent and must be resent every frame to remain active.

```json
{"id": 10, "cmd": "joystick.set", "port": 1, "up": true, "fire": false}
```
```json
{"id": 10, "ok": true}
```

---

### `joystick.press`

Presses and holds joystick actions on the selected port. Held joystick actions stay active across frames until explicitly released with `joystick.release` or `joystick.releaseall`. Executed at the next frame boundary.

**Parameters**

| Parameter | Type | Description                    |
|-----------|------|--------------------------------|
| `port`    | int  | Joystick port: `1` or `2`      |
| `up`      | bool | Hold Up direction when `true`  |
| `down`    | bool | Hold Down direction when `true`|
| `left`    | bool | Hold Left direction when `true`|
| `right`   | bool | Hold Right direction when `true`|
| `fire`    | bool | Hold Fire button when `true`   |

Only fields explicitly set to `true` are applied; omitted or `false` fields are ignored.

```json
{"id": 10, "cmd": "joystick.press", "port": 1, "up": true, "fire": true}
```
```json
{"id": 10, "ok": true}
```

---

### `joystick.release`

Releases held joystick actions on the selected port. Executed at the next frame boundary.

**Parameters**

| Parameter | Type | Description                          |
|-----------|------|--------------------------------------|
| `port`    | int  | Joystick port: `1` or `2`            |
| `up`      | bool | Release Up direction when `true`     |
| `down`    | bool | Release Down direction when `true`   |
| `left`    | bool | Release Left direction when `true`   |
| `right`   | bool | Release Right direction when `true`  |
| `fire`    | bool | Release Fire button when `true`      |

Only fields explicitly set to `true` are applied; omitted or `false` fields are ignored.

```json
{"id": 11, "cmd": "joystick.release", "port": 1, "up": true}
```
```json
{"id": 11, "ok": true}
```

---

### `joystick.releaseall`

Releases all held joystick actions on one port. Executed at the next frame boundary.

**Parameters**

| Parameter | Type | Description               |
|-----------|------|---------------------------|
| `port`    | int  | Joystick port: `1` or `2` |

```json
{"id": 12, "cmd": "joystick.releaseall", "port": 1}
```
```json
{"id": 12, "ok": true}
```

---

### `keyboard.press`

Holds down a named key. The key stays down until `keyboard.release` or `keyboard.releaseall` is sent. Executed at the next frame boundary.

**Parameters**

| Parameter | Type   | Description                                              |
|-----------|--------|----------------------------------------------------------|
| `key`     | string | Key name (case-insensitive), e.g. `space`, `return`, `a` |

```json
{"id": 11, "cmd": "keyboard.press", "key": "space"}
```
```json
{"id": 11, "ok": true}
```

---

### `keyboard.release`

Releases a previously pressed key. Executed at the next frame boundary.

**Parameters**

| Parameter | Type   | Description  |
|-----------|--------|--------------|
| `key`     | string | Key name     |

```json
{"id": 12, "cmd": "keyboard.release", "key": "space"}
```
```json
{"id": 12, "ok": true}
```

---

### `keyboard.releaseall`

Releases all injected keys at once. Useful for cleanup after automation. Executed at the next frame boundary.

```json
{"id": 13, "cmd": "keyboard.releaseall"}
```
```json
{"id": 13, "ok": true}
```

---

### `keyboard.iskeydown`

Returns whether a key is currently pressed (by the user or by a prior `keyboard.press`).

**Parameters**

| Parameter | Type   | Description  |
|-----------|--------|--------------|
| `key`     | string | Key name     |

**Response fields**

| Field    | Type | Description                      |
|----------|------|----------------------------------|
| `isdown` | bool | `true` if the key is down        |

```json
{"id": 14, "cmd": "keyboard.iskeydown", "key": "space"}
```
```json
{"id": 14, "ok": true, "isdown": false}
```

---

### `keyboard.getall`

Returns the list of all valid key names for the currently running system.

**Response fields**

| Field  | Type            | Description                   |
|--------|-----------------|-------------------------------|
| `data` | array of string | Valid key names for this system |

```json
{"id": 15, "cmd": "keyboard.getall"}
```
```json
{"id": 15, "ok": true, "data": ["space","return","a","b",...]}
```

---

### `screenshot`

Captures the current display as a Base64-encoded PNG. Returns an error in headless mode (no renderer).

```json
{"id": 19, "cmd": "screenshot"}
```

**Response fields**

| Field    | Type   | Description                        |
|----------|--------|------------------------------------|
| `format` | string | Always `"png"`                     |
| `data`   | string | Base64-encoded PNG bytes           |

```json
{"id": 19, "ok": true, "format": "png", "data": "iVBORw0KGgoAAAANSUhEUgAA..."}
```

---

### `ui.message`

Displays a message in the emulator UI. In the Avalonia Desktop app it appears in the **Log tab** prefixed with `[Remote]`. In headless mode it is written to stdout.

**Parameters**

| Parameter | Type   | Description                                           |
|-----------|--------|-------------------------------------------------------|
| `text`    | string | Message text                                          |
| `level`   | string | `info` (default), `warning`, or `error`               |

```json
{"id": 20, "cmd": "ui.message", "text": "Watch out — enemy spawns left!", "level": "info"}
```
```json
{"id": 20, "ok": true}
```

---

### `c64.type`

Pastes a string of text into the C64's keyboard buffer character by character. Characters are converted from ASCII to PETSCII automatically and fed into the buffer across frames — the C64 BASIC interpreter processes them exactly as if the user typed them. Newline (`\n`) maps to C64 Return.

**C64 only.** Returns an error on other systems.

**Parameters**

| Parameter | Type   | Description                                           |
|-----------|--------|-------------------------------------------------------|
| `text`    | string | Text to paste, e.g. `LOAD"*",8,1\nRUN\n`             |

```json
{"id": 16, "cmd": "c64.type", "text": "LOAD\"*\",8,1\n"}
```
```json
{"id": 16, "ok": true}
```

---

### `c64.isbasicstarted`

Returns whether C64 BASIC has finished initializing. Checks the `TXTAB` pointer at `$002B–$002C`; it equals `$0801` once BASIC is ready. Use this to poll before sending `c64.type` commands.

**C64 only.**

**Response fields**

| Field            | Type | Description                              |
|------------------|------|------------------------------------------|
| `isbasicstarted` | bool | `true` once BASIC initialization is done |

```json
{"id": 17, "cmd": "c64.isbasicstarted"}
```
```json
{"id": 17, "ok": true, "isbasicstarted": true}
```

---

### `c64.getbasicsource`

Returns the current BASIC program in memory as a human-readable string with line numbers. Returns an empty string if BASIC has not initialized yet.

**C64 only.**

**Response fields**

| Field  | Type   | Description                          |
|--------|--------|--------------------------------------|
| `data` | string | BASIC source text with line numbers  |

```json
{"id": 18, "cmd": "c64.getbasicsource"}
```
```json
{"id": 18, "ok": true, "data": "10 PRINT \"HELLO\"\n20 GOTO 10\n"}
```

---

## `dotnet-6502-remote` Client

A thin CLI wrapper is provided that hides the JSON framing, handles connection setup, and formats output for human or pipeline consumption.

### Installation

```sh
# Run directly from source
dotnet run --project src/apps/Highbyte.DotNet6502.App.RemoteClient -- --help

# Or build and add to PATH
dotnet build src/apps/Highbyte.DotNet6502.App.RemoteClient -c Release
# binary: src/apps/Highbyte.DotNet6502.App.RemoteClient/bin/Release/net10.0/dotnet-6502-remote
```

### Global options

| Option          | Default     | Description                  |
|-----------------|-------------|------------------------------|
| `--host <host>` | `127.0.0.1` | Server hostname or IP        |
| `--port <port>` | `6510`      | TCP port                     |
| `--help`        |             | Print usage and exit         |

### Usage examples

```sh
# Check emulator state
dotnet-6502-remote emu.state

# Start the emulator
dotnet-6502-remote --port 6510 emu.start

# Read 16 bytes from $C000
dotnet-6502-remote mem.read --addr C000 --len 16

# Write bytes to $C000
dotnet-6502-remote mem.write --addr C000 --data 169,42,133,254

# Get CPU registers
dotnet-6502-remote cpu.get

# Set joystick port 1: up + fire
dotnet-6502-remote joystick.set --port 1 --up --fire

# Hold joystick port 1 up + fire until release
dotnet-6502-remote joystick.press --port 1 --up --fire

# Release held joystick up on port 1
dotnet-6502-remote joystick.release --port 1 --up

# Release all held joystick actions on port 1
dotnet-6502-remote joystick.releaseall --port 1

# Clear joystick port 1 up + fire explicitly for the next frame only
dotnet-6502-remote joystick.set --port 1 --no-up --fire false

# Press and release the Return key
dotnet-6502-remote keyboard.press --key return
dotnet-6502-remote keyboard.release --key return

# Paste text into the C64 keyboard buffer (C64 only)
dotnet-6502-remote c64.type --text "LOAD\"*\",8,1"

# Take a screenshot and save to a file
dotnet-6502-remote screenshot --output /tmp/screen.png

# Display a message in the Log tab
dotnet-6502-remote ui.message --text "Checkpoint reached" --level info

# Quit the headless emulator (requires --allow-remote-quit on server)
dotnet-6502-remote emu.quit
```

Exit codes: `0` = success, `1` = server returned an error or connection failed, `2` = bad arguments.

---

## Bash / Shell Script Examples

These examples use `nc` (netcat) and standard Unix tools. On macOS, `nc` is available by default; on Linux, install `netcat-openbsd`.

### Single command

```sh
echo '{"id":1,"cmd":"emu.state"}' | nc -G2 localhost 6510
```

On Linux (where `-G` is not available):

```sh
echo '{"id":1,"cmd":"emu.state"}' | nc -q1 localhost 6510
```

### Multi-command session (one connection, multiple requests)

```sh
(
  echo '{"id":1,"cmd":"emu.start"}'
  sleep 2
  echo '{"id":2,"cmd":"emu.state"}'
  echo '{"id":3,"cmd":"cpu.get"}'
  echo '{"id":4,"cmd":"mem.read","addr":"0400","len":8}'
  sleep 0.1
) | nc localhost 6510
```

### Parse response with `jq`

```sh
# Print just the state field
echo '{"id":1,"cmd":"emu.state"}' | nc -G2 localhost 6510 | jq -r '.state'

# Read memory and format as hex
echo '{"id":1,"cmd":"mem.read","addr":"0400","len":40}' \
  | nc -G2 localhost 6510 \
  | jq '.data | map(. | tostring) | join(",")'

# Save screenshot to PNG
echo '{"id":1,"cmd":"screenshot"}' \
  | nc -G2 localhost 6510 \
  | jq -r '.data' \
  | base64 --decode > /tmp/screen.png
```

### Automation loop: poll state until emulator is running

```sh
#!/usr/bin/env bash
PORT=6510
HOST=localhost

echo "Starting emulator..."
echo '{"cmd":"emu.start"}' | nc -G2 "$HOST" "$PORT" > /dev/null

for i in $(seq 1 30); do
  STATE=$(echo '{"cmd":"emu.state"}' | nc -G2 "$HOST" "$PORT" | jq -r '.state')
  echo "  state: $STATE"
  if [ "$STATE" = "Running" ]; then
    echo "Emulator is running."
    break
  fi
  sleep 0.5
done
```

### Write a POKE and verify it

```sh
# Write 42 ($2A) to address $C100
echo '{"id":1,"cmd":"mem.write","addr":"D020","data":[1]}' \
  | nc -G2 localhost 6510

# Read it back
echo '{"id":2,"cmd":"mem.read","addr":"C100","len":1}' \
  | nc -G2 localhost 6510 \
  | jq '.data[0]'
# → 42
```

### Persistent helper function

Add this to your `~/.bashrc` or `~/.zshrc`:

```sh
emu() {
  local port="${EMU_PORT:-6510}"
  local host="${EMU_HOST:-localhost}"
  echo "$1" | nc -G2 "$host" "$port"
}

# Usage:
emu '{"cmd":"emu.state"}'
emu '{"cmd":"mem.read","addr":"C000","len":16}'
```

---

## PowerShell Examples

### Single command

```powershell
$json = '{"id":1,"cmd":"emu.state"}'
$tcp  = [System.Net.Sockets.TcpClient]::new("127.0.0.1", 6510)
$stream = $tcp.GetStream()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($json + "`n")
$stream.Write($bytes, 0, $bytes.Length)

$reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8)
$response = $reader.ReadLine()
$tcp.Close()

$response | ConvertFrom-Json
```

### Reusable helper function

```powershell
function Invoke-EmuCommand {
    param(
        [string]$Command,
        [string]$Host = "127.0.0.1",
        [int]$Port = 6510
    )
    $tcp    = [System.Net.Sockets.TcpClient]::new($Host, $Port)
    $stream = $tcp.GetStream()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Command + "`n")
        $stream.Write($bytes, 0, $bytes.Length)
        $reader = [System.IO.StreamReader]::new($stream)
        return $reader.ReadLine() | ConvertFrom-Json
    }
    finally {
        $tcp.Close()
    }
}

# Examples
Invoke-EmuCommand '{"id":1,"cmd":"emu.state"}'
Invoke-EmuCommand '{"id":2,"cmd":"emu.start"}'
Invoke-EmuCommand '{"id":3,"cmd":"cpu.get"}'
Invoke-EmuCommand '{"id":4,"cmd":"mem.read","addr":"C000","len":4}'
```

### Multi-command session (one TCP connection)

```powershell
function Invoke-EmuSession {
    param(
        [string[]]$Commands,
        [string]$Host = "127.0.0.1",
        [int]$Port = 6510
    )
    $tcp    = [System.Net.Sockets.TcpClient]::new($Host, $Port)
    $stream = $tcp.GetStream()
    $reader = [System.IO.StreamReader]::new($stream)
    try {
        foreach ($cmd in $Commands) {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($cmd + "`n")
            $stream.Write($bytes, 0, $bytes.Length)
            $response = $reader.ReadLine() | ConvertFrom-Json
            Write-Output $response
        }
    }
    finally {
        $tcp.Close()
    }
}

# Example: start the emulator, wait, then read memory
$cmds = @(
    '{"id":1,"cmd":"emu.start"}',
    '{"id":2,"cmd":"emu.state"}',
    '{"id":3,"cmd":"mem.read","addr":"0400","len":8}'
)
Invoke-EmuSession -Commands $cmds
```

### Screenshot to file

```powershell
function Save-EmuScreenshot {
    param(
        [string]$OutputPath,
        [string]$Host = "127.0.0.1",
        [int]$Port = 6510
    )
    $resp = Invoke-EmuCommand '{"id":1,"cmd":"screenshot"}' -Host $Host -Port $Port
    if (-not $resp.ok) { throw "Screenshot failed: $($resp.error)" }
    $bytes = [Convert]::FromBase64String($resp.data)
    [System.IO.File]::WriteAllBytes($OutputPath, $bytes)
    Write-Host "Screenshot saved to $OutputPath"
}

Save-EmuScreenshot -OutputPath "$env:TEMP\screen.png"
```

### Poll until running

```powershell
Invoke-EmuCommand '{"cmd":"emu.start"}' | Out-Null

for ($i = 0; $i -lt 30; $i++) {
    $state = (Invoke-EmuCommand '{"cmd":"emu.state"}').state
    Write-Host "  state: $state"
    if ($state -eq "Running") { Write-Host "Emulator is running."; break }
    Start-Sleep -Milliseconds 500
}
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  External process (AI agent, script, dotnet-6502-remote) │
└──────────────────────┬──────────────────────────────────┘
                       │ TCP (newline-delimited JSON)
                       ▼
┌──────────────────────────────────────────┐
│  Highbyte.DotNet6502.Remoting (library)  │
│  ┌─────────────────────────────────────┐ │
│  │  RemoteControlController            │ │  ← owns server + session
│  │  RemoteCommandDispatcher            │ │  ← routes commands
│  │  IRemoteControlEnvironment          │ │  ← platform abstraction
│  │  IRemotableHostApp                  │ │  ← host app contract
│  └─────────────────────────────────────┘ │
│  ┌─────────────────────────────────────┐ │
│  │  Tcp/                               │ │
│  │    TcpRemoteControlServer           │ │  ← listens on loopback
│  │    RemoteControlSession             │ │  ← one per client
│  └─────────────────────────────────────┘ │
└───────────┬──────────────────────────────┘
            │
    ┌───────┴────────┐
    │                │
    ▼                ▼
 Avalonia        Headless
 Desktop          App
```

**Dispatch paths:**

| Command type              | Execution thread         |
|---------------------------|--------------------------|
| Read-only queries         | Session thread (direct)  |
| `emu.start/stop/pause/...`| UI thread via dispatcher |
| `mem.write`, `joystick.set`, `joystick.press/release/releaseall`, `keyboard.press/release/releaseall`, `c64.type` | Frame boundary via action queue |
| `keyboard.iskeydown`, `keyboard.getall` | Session thread (direct read) |

---

## Limitations

- **One client at a time.** A second connection attempt is accepted only after the first client disconnects.
- **`emu.quit` is disabled in Avalonia Desktop** by default. It is available in headless mode when `--allow-remote-quit` is passed.
- **`screenshot` returns an error in headless mode** because no renderer is active.
- **Loopback only.** The server binds to `127.0.0.1`; it is not reachable over the network.
- **`keyboard.press` holds a key until `keyboard.release` or `keyboard.releaseall`.** The client controls press duration by choosing when to release. Keys are applied at frame boundary and remain held until released.
- **`joystick.press` holds joystick actions until `joystick.release` or `joystick.releaseall`.** Use this for ergonomic hold/release remote control.
- **`c64.type` is C64-specific.** Other systems do not implement text paste and will return an error. The text is fed into the C64 keyboard buffer across frames — if the buffer is full the remaining characters wait until space is available.
- **Injected joystick actions from `joystick.set` are not persistent** — they must be resent every frame to hold a direction. Use `joystick.press` if you want stateful joystick hold/release behavior.
