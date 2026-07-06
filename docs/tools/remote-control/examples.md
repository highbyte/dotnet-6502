# Examples

End-to-end automation patterns for typical C64 tasks. The first section uses the [`dotnet-6502-remote`](remote-client.md) CLI; the following sections show equivalent recipes using raw TCP from Bash and PowerShell.

For the protocol and full command reference, see [TCP protocol](tcp-protocol.md).

## Common automation workflows

These show end-to-end patterns using the `dotnet-6502-remote` CLI. The same command sequence translates directly to JSON requests over the raw TCP connection.

### Preflight: check the server version

As a best practice, start a scripted control session by confirming the client and the emulator it drives are the same release. `--check-server-version` exits `0` on a match and `3` on a mismatch (printing the update commands to stderr), so a script can bail out early:

```sh
# Abort the script if the endpoint version doesn't match this client
dotnet-6502-remote --port 6510 --check-server-version || exit 1

# ... rest of the automation ...
```

This is a version-mismatch warning only, not full protocol compatibility handling. See [Server version preflight](remote-client.md#server-version-preflight).

### Typing a BASIC command and running it

Poll `c64.isbasicstarted` before sending text — the C64 BASIC ROM takes several frames to initialize after `emu.start`.

```sh
dotnet-6502-remote emu.start

# Poll until BASIC is ready (repeat until isbasicstarted is true)
dotnet-6502-remote c64.isbasicstarted
# → {"ok":true,"isbasicstarted":true}

# Type the BASIC command in LOWERCASE (lowercase input → uppercase on C64 screen)
dotnet-6502-remote c64.type --text 'load"*",8,1'
dotnet-6502-remote keyboard.press --key return
dotnet-6502-remote keyboard.release --key return
```

Shell poll loop:

```sh
until dotnet-6502-remote c64.isbasicstarted | grep -q '"isbasicstarted":true'; do
  sleep 0.5
done
dotnet-6502-remote c64.type --text "list"
dotnet-6502-remote keyboard.press --key return
dotnet-6502-remote keyboard.release --key return
```

### Writing and running machine code

There are two ways to jump to machine code. Setting the PC directly is simpler and works on any system.

#### Method 1: Set PC directly (recommended for agents)

`cpu.set --pc` sets the program counter at the next frame boundary. No BASIC, no keyboard input, no PETSCII — just write code and point the CPU at it.

```sh
dotnet-6502-remote emu.start

# Write machine code to $C000 as comma-separated decimal bytes
# Example: LDA #$42 (169,66), STA $D020 (141,32,208), RTS (96)
dotnet-6502-remote mem.write --addr C000 --data 169,66,141,32,208,96

# Verify the bytes were written
dotnet-6502-remote mem.read --addr C000 --len 6

# Jump to the routine — takes effect at next frame boundary
dotnet-6502-remote cpu.set --pc C000

# Confirm the CPU is executing inside the routine
dotnet-6502-remote cpu.get

# Capture the result visually
dotnet-6502-remote screenshot --output /tmp/result.png
```

!!! note
    `cpu.set --pc` does not push a return address. If the routine ends with `RTS`, it will pop whatever is on the stack (potentially crashing). Use this method for programs that loop forever, or end with a `JMP` or `BRK`. Use the `SYS` method below when the routine is designed to return to BASIC via `RTS`.

#### Method 2: Via BASIC SYS command (C64 only)

This approach uses the C64 BASIC interpreter to call the routine, so BASIC must be running and the routine must end with `RTS` to return cleanly to BASIC.

```sh
dotnet-6502-remote emu.start

dotnet-6502-remote mem.write --addr C000 --data 169,66,141,32,208,96
dotnet-6502-remote mem.read --addr C000 --len 6

# Wait until BASIC is ready before using the keyboard buffer
dotnet-6502-remote c64.isbasicstarted   # repeat until "isbasicstarted":true

# Type SYS in LOWERCASE — "sys 49152" displays as SYS 49152 on the C64 screen
dotnet-6502-remote c64.type --text "sys 49152"
dotnet-6502-remote keyboard.press --key return
dotnet-6502-remote keyboard.release --key return

dotnet-6502-remote cpu.get
dotnet-6502-remote screenshot --output /tmp/result.png
```

### Restoring a snapshot, stepping, and screenshotting

Deterministically restore a saved state, advance a known number of frames, and capture the result. Pair this with starting the host paused — e.g. `--load-snapshot state.d6502snap --remote-port 6510` (no `--start`), or a fresh `--remote-port` server you `emu.loadsnapshot` into. Snapshot paths are resolved on the **emulator host**; relative paths use the host's shared snapshot directory.

```sh
# Restore full machine state (manifest picks the system); leaves the emulator paused
dotnet-6502-remote emu.loadsnapshot --path state.d6502snap

# Advance exactly one frame and render it (rejected if the emulator is Running)
dotnet-6502-remote emu.runframes --count 1

# Capture the rendered frame (works on both Avalonia and headless hosts)
dotnet-6502-remote screenshot --output /tmp/result.png
```

To save a snapshot of the current state instead:

```sh
dotnet-6502-remote emu.savesnapshot --path state.d6502snap
```

### Discovering valid key names at runtime

```sh
dotnet-6502-remote keyboard.getall
# → {"ok":true,"data":["space","return","a","b",...,"f7","crsrdown","crsrright","stop",...]}
```

Use this before calling `keyboard.press` to confirm the exact spelling of a key for the currently running system. Key names are case-insensitive in the protocol.

---

## Bash / shell script examples

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
# Write 1 to address $D020 (border color)
echo '{"id":1,"cmd":"mem.write","addr":"D020","data":[1]}' \
  | nc -G2 localhost 6510

# Read it back
echo '{"id":2,"cmd":"mem.read","addr":"D020","len":1}' \
  | nc -G2 localhost 6510 \
  | jq '.data[0]'
# → 1
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

## PowerShell examples

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
