# TCP protocol

The remote control endpoint speaks newline-delimited JSON over a single persistent TCP connection. Every request is a single JSON object terminated by `\n`; every response is a single JSON object terminated by `\n`.

For how to start the server, see [Overview](overview.md). For automation patterns, see [Examples](examples.md).

## Request format

```json
{"id": 1, "cmd": "emu.state"}
{"id": 2, "cmd": "mem.read", "addr": "C000", "len": 16}
```

`id` is optional. If supplied it is echoed back in the response, which lets clients correlate responses when pipelining multiple requests.

## Response format

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

| Field     | Type   | Description                                              |
|-----------|--------|----------------------------------------------------------|
| `state`   | string | `Uninitialized`, `Running`, or `Paused`                  |
| `system`  | string | Currently selected system, e.g. `C64` or `Generic`       |
| `variant` | string | Currently selected configuration variant, e.g. `C64NTSC` |

**Response example**

```json
{"id": 1, "ok": true, "state": "Running", "system": "C64", "variant": "C64NTSC"}
```

### `emu.start`

Starts the emulator. Equivalent to clicking the **Start** button in the UI.

When the emulator is `Paused`, this command **resumes** it rather than reinitializing — the existing system runner is reused. Use `emu.reset` if you want a fresh start from the `Paused` state.

> There is no separate `emu.resume` command; `emu.start` serves both roles.

```json
{"id": 2, "cmd": "emu.start"}
```

```json
{"id": 2, "ok": true}
```

### `emu.stop`

Stops the emulator and resets it to the `Uninitialized` state.

```json
{"id": 3, "cmd": "emu.stop"}
```

```json
{"id": 3, "ok": true}
```

### `emu.pause`

Pauses emulation without resetting state.

```json
{"id": 4, "cmd": "emu.pause"}
```

```json
{"id": 4, "ok": true}
```

### `emu.reset`

Stops and immediately restarts the emulator.

```json
{"id": 5, "cmd": "emu.reset"}
```

```json
{"id": 5, "ok": true}
```

### `emu.quit`

Terminates the host application. **Headless only** — requires `--allow-remote-quit`. Returns an error in the Avalonia Desktop app.

```json
{"id": 6, "cmd": "emu.quit"}
```

```json
{"id": 6, "ok": true}
```

### `emu.systems`

Returns the names of all available systems the emulator supports.

```json
{"id": 6, "cmd": "emu.systems"}
```

**Response fields**

| Field  | Type            | Description            |
|--------|-----------------|------------------------|
| `data` | array of string | Available system names |

```json
{"id": 6, "ok": true, "data": ["C64", "Generic"]}
```

### `emu.selectsystem`

Selects the active system. **The emulator must be stopped** (`Uninitialized` state) before calling this command; use `emu.stop` first if needed.

**Parameters**

| Parameter | Type   | Description              |
|-----------|--------|--------------------------|
| `name`    | string | System name, e.g. `C64`  |

```json
{"id": 7, "cmd": "emu.selectsystem", "name": "C64"}
```

```json
{"id": 7, "ok": true}
```

After selecting a system, call `emu.variants` to see which configuration variants are available, then `emu.selectvariant` to pick one before starting with `emu.start`.

### `emu.variants`

Returns the available configuration variants for the currently selected system (e.g. `C64NTSC`, `C64PAL`).

```json
{"id": 8, "cmd": "emu.variants"}
```

**Response fields**

| Field  | Type            | Description                  |
|--------|-----------------|------------------------------|
| `data` | array of string | Configuration variant names  |

```json
{"id": 8, "ok": true, "data": ["C64NTSC", "C64PAL"]}
```

### `emu.selectvariant`

Selects a configuration variant for the current system. **The emulator must be stopped** (`Uninitialized` state).

**Parameters**

| Parameter | Type   | Description                   |
|-----------|--------|-------------------------------|
| `name`    | string | Variant name, e.g. `C64NTSC`  |

```json
{"id": 9, "cmd": "emu.selectvariant", "name": "C64NTSC"}
```

```json
{"id": 9, "ok": true}
```

### `emu.savesnapshot`

Saves the current full emulator state to a `.d6502snap` snapshot file. The path is resolved **on the machine the emulator runs on** (the server), not the client. Absolute paths are used as-is; relative paths are resolved from the server's shared snapshot directory. The selected system must support snapshots.

**Parameters**

| Parameter | Type   | Description                                  |
|-----------|--------|----------------------------------------------|
| `path`    | string | Server-side destination path for the snapshot. Relative paths use the server's shared snapshot directory. |

```json
{"id": 10, "cmd": "emu.savesnapshot", "path": "state.d6502snap"}
```

```json
{"id": 10, "ok": true}
```

### `emu.loadsnapshot`

Restores full emulator state from a `.d6502snap` snapshot file (read from the **server-side** path). Absolute paths are used as-is; relative paths are resolved from the server's shared snapshot directory. The snapshot's manifest determines the system, so no `emu.selectsystem` is needed first. The emulator is left **paused** after restore — send `emu.start` to resume.

**Parameters**

| Parameter | Type   | Description                             |
|-----------|--------|-----------------------------------------|
| `path`    | string | Server-side path of the snapshot to load. Relative paths use the server's shared snapshot directory. |

```json
{"id": 11, "cmd": "emu.loadsnapshot", "path": "state.d6502snap"}
```

```json
{"id": 11, "ok": true}
```

### `emu.runframes`

Deterministically advances the emulator by a fixed number of frames and renders the result (so a following `screenshot` reflects it). Intended for automation: load a snapshot (paused), step a known number of frames, then screenshot. **The emulator must be stopped/paused** — stepping is rejected while it is `Running`, because the real-time run loop would make the step non-deterministic.

**Parameters**

| Parameter | Type | Description                          |
|-----------|------|--------------------------------------|
| `count`   | int  | Number of frames to step (default 1) |

```json
{"id": 12, "cmd": "emu.runframes", "count": 1}
```

```json
{"id": 12, "ok": true}
```

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
| `flags` | string | 8-char processor status: each position is the flag letter (`N`, `V`, `U`, `B`, `D`, `I`, `Z`, `C`) or `-` when clear |

```json
{"id": 7, "ok": true, "pc": "E5CD", "a": 0, "x": 0, "y": 0, "sp": 255, "flags": "----I--C"}
```

### `cpu.set`

Sets one or more CPU registers. At least one parameter must be supplied. Executed at the next frame boundary, so the emulator must be `Running`.

**Parameters** (all optional; omitted registers are left unchanged)

| Parameter | Type   | Description                                                                       |
|-----------|--------|-----------------------------------------------------------------------------------|
| `pc`      | string | Program Counter as a hex string, e.g. `C000`                                      |
| `a`       | int    | Accumulator (0–255)                                                               |
| `x`       | int    | X register (0–255)                                                                |
| `y`       | int    | Y register (0–255)                                                                |
| `sp`      | int    | Stack Pointer (0–255)                                                             |
| `flags`   | string | Processor status — 8-char string in `NVUBDIZC` format, same as `cpu.get` output  |

For `flags`, each character is the flag letter when set or `-` when clear. Example: `"----I---"` sets only the InterruptDisable flag. You can copy the `flags` value directly from a `cpu.get` response.

```json
{"id": 1, "cmd": "cpu.set", "a": 42, "x": 0, "flags": "------Z-"}
```

```json
{"id": 1, "ok": true}
```

Set only the program counter:

```json
{"id": 2, "cmd": "cpu.set", "pc": "C000"}
```

```json
{"id": 2, "ok": true}
```

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

### `mem.loadbin`

Loads raw binary data into the system's address space at a specified address. Unlike `mem.write` (which takes a JSON integer array), this command accepts a base64-encoded byte string — convenient for larger payloads and binary files. Bytes are written directly with no header interpretation. Executed at the next frame boundary.

For C64 PRG files that include a 2-byte load-address header, use `c64.loadprg` instead.

**Parameters**

| Parameter | Type   | Description                                 |
|-----------|--------|---------------------------------------------|
| `addr`    | string | Start address as a hex string (e.g. `0801`) |
| `data`    | string | Base64-encoded raw bytes                    |

```json
{"id": 10, "cmd": "mem.loadbin", "addr": "C000", "data": "qrtM"}
```

```json
{"id": 10, "ok": true}
```

When using `dotnet-6502-remote`, pass `--file <path>` and the client reads and encodes the file:

```sh
dotnet-6502-remote mem.loadbin --addr 0801 --file /path/to/binary.bin
```

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

### `joystick.press`

Presses and holds joystick actions on the selected port. Held joystick actions stay active across frames until explicitly released with `joystick.release` or `joystick.releaseall`. Executed at the next frame boundary.

**Parameters**

| Parameter | Type | Description                     |
|-----------|------|---------------------------------|
| `port`    | int  | Joystick port: `1` or `2`       |
| `up`      | bool | Hold Up direction when `true`   |
| `down`    | bool | Hold Down direction when `true` |
| `left`    | bool | Hold Left direction when `true` |
| `right`   | bool | Hold Right direction when `true` |
| `fire`    | bool | Hold Fire button when `true`    |

Only fields explicitly set to `true` are applied; omitted or `false` fields are ignored.

```json
{"id": 10, "cmd": "joystick.press", "port": 1, "up": true, "fire": true}
```

```json
{"id": 10, "ok": true}
```

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

### `keyboard.releaseall`

Releases all injected keys at once. Useful for cleanup after automation. Executed at the next frame boundary.

```json
{"id": 13, "cmd": "keyboard.releaseall"}
```

```json
{"id": 13, "ok": true}
```

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

### `keyboard.getall`

Returns the list of all valid key names for the currently running system.

**Response fields**

| Field  | Type            | Description                     |
|--------|-----------------|---------------------------------|
| `data` | array of string | Valid key names for this system |

```json
{"id": 15, "cmd": "keyboard.getall"}
```

```json
{"id": 15, "ok": true, "data": ["space","return","a","b",...]}
```

#### C64 key name reference

Call `keyboard.getall` at runtime for the authoritative list. Common C64 key names:

| Key name | Physical key |
|----------|-------------|
| `space` | Space bar |
| `return` | Return (Enter) |
| `a`–`z` | Letter keys A–Z |
| `0`–`9` | Digit keys |
| `+` `-` `*` `/` `:` `;` `=` `.` `,` `@` | Punctuation keys |
| `lira` | Pound sign (£) |
| `leftarrow` | ← left-arrow key (top-left of keyboard) |
| `rightarrow` | ↑ up-arrow key |
| `stop` | RUN/STOP |
| `cbm` | Commodore key |
| `ctrl` | CTRL |
| `lshift` / `rshift` | Left / Right Shift |
| `home` | CLR/HOME |
| `delete` | INST/DEL |
| `crsrdown` | Cursor Down |
| `crsrright` | Cursor Right |
| `f1` `f3` `f5` `f7` | Function keys F1 / F3 / F5 / F7 |

> Cursor Up = `crsrdown` + `lshift` held. Cursor Left = `crsrright` + `lshift` held. F2/F4/F6/F8 = corresponding F-key + `lshift` held.

### `screenshot`

Captures the current display as a Base64-encoded PNG. Works in both Avalonia and headless hosts (composited from the current system's render frame); returns an error if no system is running.

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

### `c64.type`

Pastes a string of text into the C64's keyboard buffer character by character. Characters are converted from ASCII to PETSCII automatically and fed into the buffer across frames — the C64 BASIC interpreter processes them exactly as if the user typed them. Newline (`\n`) maps to C64 Return.

**C64 only.** Returns an error on other systems.

**Parameters**

| Parameter | Type   | Description                                           |
|-----------|--------|-------------------------------------------------------|
| `text`    | string | Text to paste, e.g. `load"*",8,1\nrun\n`              |

```json
{"id": 16, "cmd": "c64.type", "text": "load\"*\",8,1\n"}
```

```json
{"id": 16, "ok": true}
```

#### PETSCII case mapping

!!! warning
    **Use lowercase letters in the `text` parameter to produce uppercase letters on the C64 screen.**

The C64 boots into *upper/graphics* character mode. In this mode the PETSCII code range `$41–$5A` (produced by lowercase `a`–`z` input) renders as the uppercase Latin alphabet A–Z, while `$C1–$DA` (produced by uppercase `A`–`Z` input) renders as graphics characters.

| Input character | PETSCII code | Displayed on C64 screen |
|-----------------|--------------|--------------------------|
| `a`–`z` (lowercase) | `$41–$5A` | **A–Z** (uppercase letters) |
| `A`–`Z` (uppercase) | `$C1–$DA` | Graphics / symbols |
| `0`–`9`, punctuation | Same as ASCII | Digits and punctuation |

**Practical rule:** write all BASIC keywords and commands in lowercase in the `text` value.

```sh
# Correct — displays LOAD"*",8,1 on the C64 screen
dotnet-6502-remote c64.type --text 'load"*",8,1'

# Wrong — each letter maps to a graphics character, BASIC gives SYNTAX ERROR
dotnet-6502-remote c64.type --text 'LOAD"*",8,1'

# Correct — displays SYS 49152 and executes the machine code at $C000
dotnet-6502-remote c64.type --text "sys 49152"
```

Newline (`\n`) in the text is equivalent to pressing Return on the C64 keyboard.

### `c64.loadprg`

Loads a Commodore 64 PRG file into memory. The first two bytes of the data are the little-endian load address; the remaining bytes are written to memory starting at that address. Executed at the next frame boundary, so the emulator must be `Running`.

**C64 only.** Returns an error on other systems.

**Parameters**

| Parameter | Type   | Description                                       |
|-----------|--------|---------------------------------------------------|
| `data`    | string | Base64-encoded PRG file bytes (address + payload) |

```json
{"id": 16, "cmd": "c64.loadprg", "data": "AMCqu8w="}
```

```json
{"id": 16, "ok": true}
```

When using `dotnet-6502-remote`, pass `--file <path.prg>` and the client reads and encodes the file for you:

```sh
dotnet-6502-remote c64.loadprg --file /path/to/program.prg
```

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

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ External process (AI agent, script, dotnet-6502-remote) │
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
│  │    TcpRemoteControlServer           │ │  ← listens on configurable bind address
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
| Read-only queries (`emu.state`, `emu.systems`, `emu.variants`, `cpu.get`, `mem.read`, `screenshot`, `ui.message`, `c64.isbasicstarted`, `c64.getbasicsource`) | Session thread (direct) |
| `emu.start/stop/pause/reset/quit`, `emu.selectsystem`, `emu.selectvariant`, `emu.savesnapshot`, `emu.loadsnapshot`, `emu.runframes` | UI thread via dispatcher |
| `mem.write`, `mem.loadbin`, `cpu.set`, `c64.loadprg`, `joystick.set/press/release/releaseall`, `keyboard.press/release/releaseall`, `c64.type` | Frame boundary via action queue |
| `keyboard.iskeydown`, `keyboard.getall` | Session thread (direct read) |
