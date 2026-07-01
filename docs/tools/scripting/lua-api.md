# Lua API

## Yield primitives

Scripts run as coroutines. Use one of these to suspend execution and let the emulator continue:

| Function | Description |
|----------|-------------|
| `emu.frameadvance()` | Yield until the next emulator frame. The script is frozen while the emulator is paused. Use this for per-frame logic tied to emulation. |
| `emu.yield()` | Yield until the next timer tick (~60 Hz). Keeps ticking even while the emulator is paused or stopped. Use this when the script needs to observe or control emulator state changes. |

A script must call one of these in its main loop. Scripts that return without yielding are marked as **Completed** (or **HookOnly** if they registered event hooks).

## Emulator queries

| Function | Returns | Description |
|----------|---------|-------------|
| `emu.framecount()` | number | Number of emulator frames executed since scripts were loaded (1-based). |
| `emu.time()` | number | Wall-clock seconds elapsed since scripts were loaded. |
| `emu.state()` | string | Current emulator state: `"running"`, `"paused"`, `"stopped"`, or `"unknown"`. |
| `emu.host()` | string | Host application type: `"headless"`, `"desktop"`, or `"browser"`. Use this to write scripts that behave differently per host — e.g. call `emu.quit()` only when headless. |
| `emu.systems()` | table | List of available system names (e.g. `{"C64", "Generic"}`). |
| `emu.selected_system()` | string | Currently selected system name. |
| `emu.selected_variant()` | string | Currently selected system variant name. |

## Emulator control

Control operations are deferred — they take effect after the current frame completes.

| Function | Description |
|----------|-------------|
| `emu.start()` | Request emulator start or resume. |
| `emu.pause()` | Request emulator pause. |
| `emu.stop()` | Request emulator stop. |
| `emu.reset()` | Request emulator stop + restart. |
| `emu.select(name [, variant])` | Request system selection. The emulator must be stopped. |
| `emu.quit()` | Stop the emulator and terminate the host application. Useful for automation pipelines (CI/CD, batch runs) where the app should exit automatically when the script is done. |
| `emu.config_valid()` | Check whether the currently selected system's configuration is valid. Returns `true` if valid, or `false` plus a table of error strings if not. Use this before `emu.start()` to detect misconfiguration early (e.g. missing ROM files). |

## CPU registers (`cpu`)

All properties are read-only and return safe defaults (`0` or `false`) before a system is started.

| Property | Type | Description |
|----------|------|-------------|
| `cpu.pc` | int | Program Counter (0–65535) |
| `cpu.a` | int | Accumulator (0–255) |
| `cpu.x` | int | Index register X (0–255) |
| `cpu.y` | int | Index register Y (0–255) |
| `cpu.sp` | int | Stack Pointer (0–255) |
| `cpu.carry` | bool | Carry flag |
| `cpu.zero` | bool | Zero flag |
| `cpu.negative` | bool | Negative flag |
| `cpu.overflow` | bool | Overflow flag |
| `cpu.interrupt_disable` | bool | Interrupt disable flag |
| `cpu.decimal_mode` | bool | Decimal mode flag |

## Memory access (`mem`)

| Function | Description |
|----------|-------------|
| `mem.read(address)` | Read a byte from emulator memory. Returns 0–255. Address is masked to 16-bit range. |
| `mem.write(address, value)` | Write a byte to emulator memory. Address is masked to 16-bit, value to 8-bit. |

Memory reads and writes go through the same address decoding as the emulated CPU, including I/O registers. For example, on the C64, `mem.read(0xD012)` reads the VIC-II raster line register and `mem.write(0xD020, 1)` sets the border color to white.

## Input (`input`)

The `input` table provides access to keyboard and joystick state. Scripts can both read the current input state and inject synthetic input for automation.

Script-injected inputs are **merged** with real user input: a script can add key presses or joystick actions the user isn't pressing, but cannot suppress or override user input.

Script-injected inputs are **ephemeral** — they must be re-injected every frame in your script loop. The scripting engine clears all injected state at the start of each frame before your scripts run.

The `input` table is always registered, even when no input provider is active (e.g. on systems without input support). Functions that query state return `false` or `nil` gracefully in that case.

### Keyboard

Key names are **system-dependent** — each system defines its own valid key names. Call `input.available_keys()` to discover the valid names for the current system. On the C64, key names include `"a"`, `"space"`, `"return"`, `"f1"`, `"crsrright"`, `"stop"`, `"lira"`, etc.

| Function | Returns | Description |
|----------|---------|-------------|
| `input.key_press(name)` | — | Inject a key press for the current frame. The key will be considered "down" for this frame only; scripts must re-inject each frame if the key should remain held. |
| `input.key_release(name)` | — | Release a previously injected key. This only affects keys injected by the script, not user input. |
| `input.key_release_all()` | — | Release all keys injected by the script for this frame. |
| `input.is_key_down(name)` | boolean | Returns `true` if the key is currently pressed, whether by the user or by the script. Returns `false` if no input provider is active. |
| `input.available_keys()` | table | Returns a 1-indexed table of valid key name strings for the current system. Returns an empty table if no input provider is active. |

### Joystick

Joystick action names are **standardized** across all systems: `"up"`, `"down"`, `"left"`, `"right"`, `"fire"`. Scripts use the same strings regardless of which system is running.

| Function | Returns | Description |
|----------|---------|-------------|
| `input.joystick_set(port, action, pressed)` | — | Inject a joystick action on the given port (1-based) for the current frame. `action` is one of `"up"`, `"down"`, `"left"`, `"right"`, `"fire"`. |
| `input.joystick_action(port, action)` | boolean | Returns `true` if the joystick action is active on the given port, whether by the user or by the script. Returns `false` if no input provider is active. |
| `input.joystick_count()` | number | Returns the number of joystick ports on the current system (e.g. `2` on C64). Returns `0` if no input provider is active. |
| `input.available_joystick_actions()` | table | Returns a 1-indexed table of valid joystick action strings. Always `{"up", "down", "left", "right", "fire"}`. Returns an empty table if no input provider is active. |

### Input notes & examples

The script's **top-level code** runs immediately when the script is enabled, before the first frame executes. The input provider is only wired once the first `on_before_frame()` fires. Therefore, input functions should only be called from hooks (`on_started`, `on_before_frame`) or after at least one `emu.frameadvance()`.

Use `emu.time()` to measure durations in real seconds rather than counting frames — this keeps timing correct regardless of the system's frame rate (PAL vs NTSC).

**Keyboard — press A, B, C in sequence:**

```lua
-- Press A for 0.5s, pause 0.3s, press B for 0.5s, pause 0.3s, press C for 0.5s
local sequence = {
    { key = "a", hold = 0.5 },
    { key = nil, hold = 0.3 },
    { key = "b", hold = 0.5 },
    { key = nil, hold = 0.3 },
    { key = "c", hold = 0.5 },
}

local step = 0
local step_start = 0

function on_before_frame()
    local now = emu.time()

    if step == 0 then
        step = 1
        step_start = now
    end

    if step > #sequence then return end

    local entry = sequence[step]
    if entry.key then
        input.key_press(entry.key)
    end

    if now - step_start >= entry.hold then
        step = step + 1
        if step <= #sequence then
            step_start = now
        end
    end
end
```

**Joystick — timed action sequence with repeats:**

```lua
-- Repeat 3 times: left 0.2s → pause 0.2s → right 0.2s → pause 0.2s → fire 0.5s → pause 0.5s
local PORT    = 1
local REPEATS = 3

local sequence = {
    { action = "left",  hold = 0.2 },
    { action = nil,     hold = 0.2 },
    { action = "right", hold = 0.2 },
    { action = nil,     hold = 0.2 },
    { action = "fire",  hold = 0.5 },
    { action = nil,     hold = 0.5 },
}

local step = 0
local step_start = 0
local rep = 0

function on_before_frame()
    local now = emu.time()

    if step == 0 then
        rep = 1 ; step = 1 ; step_start = now
    end

    if rep > REPEATS then return end

    local entry = sequence[step]
    if entry.action then
        input.joystick_set(PORT, entry.action, true)
    end

    if now - step_start >= entry.hold then
        step = step + 1
        if step > #sequence then
            rep = rep + 1 ; step = 1
        end
        step_start = now
    end
end
```

For complete demonstrations, see `example_input_kb.lua` (keyboard) and `example_input_joystick.lua` (joystick).

## Logging (`log`)

Log messages are prefixed with `[Lua:filename.lua]` in the application log output.

| Function | Log level |
|----------|-----------|
| `log.info(msg)` | Information |
| `log.debug(msg)` | Debug |
| `log.warn(msg)` | Warning |
| `log.error(msg)` | Error |

## Event hooks

Define any of these as global functions in your script. The emulator calls them at the corresponding points. A single script can register multiple hooks.

| Hook | Arguments | When called |
|------|-----------|-------------|
| `on_before_frame()` | none | Before each emulator frame executes. |
| `on_after_frame()` | none | After each emulator frame completes. |
| `on_started()` | none | Emulator started or resumed. |
| `on_paused()` | none | Emulator paused. |
| `on_stopped()` | none | Emulator stopped. |
| `on_system_selected(name)` | system name | System selection changed. |
| `on_variant_selected(name)` | variant name | System variant changed. |

## File I/O (`file`)

Available when `AllowFileIO: true`. The `file` global is not registered when `AllowFileIO` is `false`, so scripts that use it will fail with a runtime error on platforms without filesystem access (e.g. WASM/browser).

All paths are relative to `FileBaseDirectory` (which defaults to `ScriptDirectory`). Paths that attempt to escape the base directory (e.g. `../`) are blocked and treated as non-existent files or raise a runtime error on write attempts.

### Read operations (always allowed when `AllowFileIO: true`)

| Function | Returns | Description |
|----------|---------|-------------|
| `file.read(name)` | string or nil | Reads the entire contents of a text file. Returns `nil` if the file does not exist or the path is unsafe. |
| `file.read_bytes(name)` | table or nil | Reads a file as raw bytes. Returns a 1-indexed Lua table of integers (0–255), or `nil` if the file does not exist. Useful for inspecting binary data; for loading a binary directly into emulator memory, prefer `emu.load()`. |
| `file.exists(name)` | boolean | Returns `true` if the file exists within the base directory. |
| `file.list([pattern])` | table | Returns a 1-indexed table of filenames in the base directory matching an optional glob pattern (default: `"*"`). Only filenames are returned, not full paths. |

### Write operations (require `AllowFileWrite: true`)

If `AllowFileWrite` is `false`, calling any write operation raises a Lua runtime error and the script is auto-disabled.

| Function | Description |
|----------|-------------|
| `file.write(name, text)` | Writes (overwrites) a text file. Creates the file if it does not exist. |
| `file.append(name, text)` | Appends text to a file. Creates the file if it does not exist. |
| `file.delete(name)` | Deletes a file. No-op if the file does not exist. |

## Binary loading (`emu.load`)

Available when `AllowFileIO: true`. Loads a binary file from `FileBaseDirectory` directly into emulator memory, entirely on the C# side — no Lua byte array handling required, making it efficient even for large files.

| Function | Description |
|----------|-------------|
| `emu.load(name)` | Reads the 2-byte little-endian load address from the file header (C64 `.prg` format) and loads the remaining bytes at that address. |
| `emu.load(name, true)` | Same as above, and also sets CPU PC to the load address after loading. |
| `emu.load(name, address)` | Loads the entire file as raw binary at the given address, without header parsing. |
| `emu.load(name, address, true)` | Same as above, and also sets CPU PC to the load address after loading. |

The operation is deferred (like `emu.start()` etc.) and takes effect after the current frame. Path confinement rules are the same as for `file.*`.

## Snapshots (`emu.save_snapshot` / `emu.load_snapshot`)

Available when `AllowFileIO: true`. Save and restore the **full emulator state** (CPU, memory, machine-specific chips, and attached disk/cartridge media) to a `.d6502snap` file. Paths are confined to `FileBaseDirectory`, the same as `file.*`. These mirror the remote-control `emu.savesnapshot` / `emu.loadsnapshot` commands.

| Function | Description |
|----------|-------------|
| `emu.save_snapshot(name)` | Captures the current machine state and writes it to `name`. Requires `AllowFileWrite: true`. Runs **inline** — the file exists immediately after the call returns, and any failure raises a Lua runtime error. The currently selected system must support snapshots. |
| `emu.load_snapshot(name)` | Restores machine state from `name`. The snapshot's manifest determines the system, so no `emu.select()` is needed first. Runs **deferred** (takes effect after the current frame), because restoring rebuilds the system. |

After a successful `emu.load_snapshot()`, the emulator is left **paused** (consistent with the remote and command-line load paths). This means a script blocked on `emu.frameadvance()` will not resume until something calls `emu.start()` again, because the frame pump is stopped while paused. To observe state immediately after a load, drive the wait with `emu.yield()` (tick-based, independent of the frame pump) rather than `emu.frameadvance()`:

```lua
emu.save_snapshot("quicksave.d6502snap")
-- ... later ...
emu.load_snapshot("quicksave.d6502snap")
emu.yield()                       -- let the deferred restore apply (emulator is now paused)
print("restored, state = " .. emu.state())   -- "paused"
emu.start()                       -- resume from the restored state
```

## HTTP (`http`)

Available when `AllowHttpRequests: true`. The `http` global is not registered when `AllowHttpRequests` is `false`.

All methods return a **response table** with the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `ok` | boolean | `true` if the request succeeded (HTTP 2xx) and no network error occurred. |
| `status` | number | HTTP status code (e.g. `200`, `404`). `0` on network or timeout failure. |
| `body` | string, table, or nil | Response body. String for `get` / `post` / `post_json`. 1-indexed byte table for `get_bytes`. `nil` on failure or for `download`. |
| `error` | string or nil | Error description on failure. `nil` on success. |

### Methods

| Function | Description |
|----------|-------------|
| `http.get(url [, headers])` | GET request. Returns the response body as a string in `body`. |
| `http.get_bytes(url [, headers])` | GET request. Returns the response body as a 1-indexed Lua table of byte values (0–255) in `body`. Useful for binary data; for loading it directly into emulator memory see `mem.write`. |
| `http.post(url, body, content_type [, headers])` | POST request with an explicit content type (e.g. `"application/x-www-form-urlencoded"`). Returns the response body as a string in `body`. |
| `http.post_json(url, json_body [, headers])` | POST request with `Content-Type: application/json`. Shorthand for `http.post(url, body, "application/json")`. |
| `http.download(url, filename [, headers])` | GET request that streams the response body directly to a file in the `file` sandbox. Requires `AllowFileIO: true` and `AllowFileWrite: true`. The `body` field is `nil` in the response; use `file.read` / `file.read_bytes` to access the saved file afterwards. |

The optional `headers` argument is a Lua table of key-value string pairs, e.g. `{["Authorization"] = "Bearer token", ["Accept"] = "application/json"}`.

### Async behaviour

All HTTP calls are **non-blocking and async**. When a script calls `http.get(url)`, the coroutine is suspended immediately and the emulator continues running. The script resumes automatically on the next frame once the response arrives, with the response table returned as the value of the `http.*` call. From the Lua script's point of view the call is still a simple synchronous expression:

```lua
local resp = http.get(url)   -- suspends until response arrives; script sees a normal return value
```

This works the same on both desktop and browser/WASM hosts. Because the emulator keeps running during the HTTP wait, `on_started()` hooks that make HTTP calls will complete *after* the emulator has already started. If your script depends on data fetched in `on_started` being available before the first emulated frame runs, use `emu.pause()` at the start of the hook and `emu.start()` after the last HTTP call.

### HTTP examples

```lua
-- GET: call a REST API
local resp = http.get("https://api.example.com/status")
if resp.ok then
    log.info("Response: " .. resp.body)
else
    log.error("HTTP " .. resp.status .. ": " .. (resp.error or "?"))
end

-- GET bytes: download binary data and copy into emulator memory
local resp = http.get_bytes("https://example.com/data.bin")
if resp.ok then
    for i, b in ipairs(resp.body) do
        mem.write(0xC000 + i - 1, b)
    end
end

-- POST JSON
local resp = http.post_json("https://api.example.com/save", '{"score":42}')
log.info("Saved: " .. tostring(resp.ok))

-- Download directly to the file sandbox, then load into memory via emu.load
local resp = http.download("https://example.com/game.prg", "game.prg")
if resp.ok then
    emu.load("game.prg")  -- reads PRG header and loads at the embedded address
end

-- Custom headers
local resp = http.get("https://api.example.com/private", {
    ["Authorization"] = "Bearer my-token"
})
```

## Key/value store (`store`)

Available when `AllowStore: true`. The `store` global is not registered when `AllowStore` is `false`.

The store provides simple persistent key/value storage. Values are always strings. The storage backend depends on the environment:

- **Desktop:** each key is stored as a file named `<key>` inside `{ScriptDirectory}/{StoreSubDirectory}` (default: `scripts/.store/`). The directory is created automatically on the first write.
- **Browser/WASM:** each key is stored in `localStorage` under the prefix `dotnet6502.store.`.

Keys must be valid filenames (no path separators, no `..`). Attempting to use an invalid key raises a Lua runtime error.

| Function | Returns | Description |
|----------|---------|-------------|
| `store.get(key)` | string or nil | Returns the stored value for `key`, or `nil` if the key does not exist. |
| `store.set(key, value)` | — | Stores `value` under `key`, overwriting any existing entry. |
| `store.delete(key)` | — | Removes the entry for `key`. No-op if the key does not exist. |
| `store.exists(key)` | boolean | Returns `true` if an entry exists for `key`. |
| `store.list()` | table | Returns a 1-indexed Lua table of all stored keys. |

### Store examples

```lua
-- Save and retrieve a string value
store.set("high_score", "12345")
local score = store.get("high_score")
log.info("High score: " .. (score or "none"))

-- Persist a flag across sessions
if not store.exists("intro_shown") then
    log.info("Showing intro for the first time")
    store.set("intro_shown", "1")
end

-- Save downloaded content for later use
local resp = http.get("https://example.com/data.txt")
if resp.ok then
    store.set("cached_data", resp.body)
end

-- List all stored keys
local keys = store.list()
for _, k in ipairs(keys) do
    log.info("Store key: " .. k .. " = " .. (store.get(k) or ""))
end

-- Clean up
store.delete("high_score")
```

## TCP client (`tcp`)

Available when `AllowTcpClient: true`. The `tcp` global is not registered when `AllowTcpClient` is `false`. TCP client support is **desktop only** — it is not available in browser/WASM builds because `System.Net.Sockets.TcpClient` is not supported in WebAssembly.

The TCP API is designed for low-latency per-frame communication. The connection persists across frames.

### Binary data representation

MoonSharp strings are .NET `System.String` (UTF-16 code units), **not** raw byte arrays. Any byte value > 127 would be silently reinterpreted as a Unicode code point, corrupting binary payloads. All binary I/O in the TCP API therefore uses **1-indexed Lua tables of numbers (0–255)** — the same convention as `file.read_bytes()` and `http.get_bytes()`.

`conn:send()` accepts either format:

- A **string** — encoded to UTF-8 bytes before sending. Safe for text-only protocols.
- A **1-indexed byte table** — sent verbatim as raw bytes. Use for binary protocols.

`conn:receive(n)` always returns a 1-indexed byte table regardless of the data content.

### Lua 5.2 bitwise arithmetic

MoonSharp implements Lua 5.2. The Lua 5.3 bitwise operators (`&`, `|`, `>>`, `<<`) are **not** available. Use integer arithmetic instead:

| Lua 5.3 | Lua 5.2 equivalent |
|---------|--------------------|
| `n & 0xFF` | `n % 256` |
| `(n >> 8) & 0xFF` | `math.floor(n / 256) % 256` |
| `(n >> 16) & 0xFF` | `math.floor(n / 65536) % 256` |
| `(n >> 24) & 0xFF` | `math.floor(n / 16777216) % 256` |

### Connecting

```lua
local res = tcp.connect(host, port [, timeout_ms])
```

- `host` — hostname or IP address string.
- `port` — TCP port number (1–65535).
- `timeout_ms` — connection timeout in milliseconds. Default: 5000.

Returns a **result table**:

| Field | Type | Description |
|-------|------|-------------|
| `ok` | boolean | `true` if the connection was established. |
| `data` | connection | Connection object (only present when `ok = true`). |
| `error` | string or nil | Error description on failure. `nil` on success. |

The call is **non-blocking and async** — the coroutine suspends until the connection is established or times out.

### Connection methods

Once connected, the `conn` object exposes three methods:

#### `conn:send(data)`

Send data over the connection. `data` may be a string or a 1-indexed byte table.

Returns `{ ok=boolean, error=string|nil }`.

#### `conn:receive(n)`

Receive exactly `n` bytes. Suspends asynchronously until all bytes arrive or an error occurs.

Returns `{ ok=boolean, data=table, error=string|nil }` where `data` is a 1-indexed byte table of `n` numbers (0–255).

#### `conn:receive("*l")`

Receive one line of text (reads until `\n`; the newline is stripped).

Returns `{ ok=boolean, data=string, error=string|nil }` where `data` is the line as a string.

#### `conn:close()`

Close the connection. Safe to call multiple times.

### TCP async behaviour

`tcp.connect()`, `conn:send()`, and `conn:receive()` are all **non-blocking and async**: the coroutine suspends immediately and the emulator continues running frames. The script resumes automatically once the operation completes. From the Lua script's perspective each call looks like a normal synchronous expression:

```lua
local res = tcp.connect("127.0.0.1", 9000, 3000)  -- suspends until connected or timed out
local sr  = conn:send({1, 2, 3})                  -- suspends until sent
local lr  = conn:receive(4)                       -- suspends until 4 bytes arrive
```

### TCP examples

```lua
-- Connect to a TCP server
local res = tcp.connect("127.0.0.1", 9000, 3000)
if not res.ok then
    log.error("Connection failed: " .. (res.error or "?"))
    return
end
local conn = res.data

-- Send a length-prefixed binary payload (byte table)
local payload = { cpu.a, cpu.x, cpu.y }
local prefix  = {
    #payload % 256,
    math.floor(#payload / 256) % 256,
    math.floor(#payload / 65536) % 256,
    math.floor(#payload / 16777216) % 256,
}
conn:send(prefix)
conn:send(payload)

-- Receive a length-prefixed response
local lr = conn:receive(4)
if lr.ok then
    local resp_len = lr.data[1] + lr.data[2]*256 + lr.data[3]*65536 + lr.data[4]*16777216
    if resp_len > 0 then
        local ar = conn:receive(resp_len)
        if ar.ok then
            mem.write(0xD020, ar.data[1] % 16)  -- apply first byte as C64 border color
        end
    end
end

-- Receive a text line (e.g. JSON or NDJSON)
local lr = conn:receive("*l")
if lr.ok then
    log.info("Server says: " .. lr.data)
end

-- Close when done
conn:close()
```

For a complete per-frame observation/action loop, see `example_tcp_client.lua`.

## Standard Lua libraries

The following standard Lua modules are available: `string`, `math`, `table`, and the soft-sandbox base functions (`print`, `type`, `tostring`, `tonumber`, `pairs`, `ipairs`, etc.). The standard Lua `io` and `os` modules are not available; use the `file` global and `emu.load()` instead.

## Error handling

Scripts that encounter errors are automatically disabled:

- **Syntax errors** — detected at load time. The script appears in the Scripts tab as system-disabled and cannot be toggled on.
- **Runtime errors** — the script is stopped and marked as system-disabled. This applies to both coroutine execution and event hook invocations.
- **Instruction limit exceeded** — if a coroutine resume exceeds `MaxInstructionsPerResume`, the script is force-suspended and system-disabled.

Disabled scripts can be fixed on disk and reloaded via the reload button in the Scripts tab without restarting the emulator.
