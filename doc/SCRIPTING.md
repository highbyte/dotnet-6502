<h1 align="center">Lua Scripting</h1>

# Overview

The emulator supports Lua scripting for automating emulator interaction, monitoring CPU/memory state, and controlling the emulator at runtime. Scripts are loaded from a configurable directory and executed as coroutines alongside the emulator's frame loop. Scripting is currently wired in the Avalonia Desktop host application; the architecture supports extension to other host applications (SilkNet, SadConsole) without changes to the scripting core.

Two scripting styles are supported and can be combined within a single script:

- **Linear loop (BizHawk-style):** Write a top-level `while true do ... emu.frameadvance() end` loop. The script reads like a sequential program that suspends and resumes each frame.
- **Event hooks:** Define global callback functions (e.g. `on_before_frame()`, `on_started()`) that the emulator calls at specific points.

# Configuration

Scripting is configured in `appsettings.json` under the `"Highbyte.DotNet6502.Scripting"` section:

```json
"Highbyte.DotNet6502.Scripting": {
    "Enabled": true,
    "ScriptDirectory": "scripts",
    "MaxExecutionWarningMs": 5,
    "MaxInstructionsPerResume": 1000000,
    "EnableScriptsAtStart": false,
    "AllowFileIO": true,
    "AllowFileWrite": false,
    "AllowHttpRequests": true,
    "AllowStore": true,
    "StoreSubDirectory": ".store",
    "AllowTcpClient": false
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Master switch for the scripting system. |
| `ScriptDirectory` | string | `""` | Directory to load `.lua` files from. Absolute path, or relative to the application working directory. |
| `MaxExecutionWarningMs` | int | `5` | Log a warning if a script hook takes longer than this many milliseconds. Set to `0` to disable. |
| `MaxInstructionsPerResume` | int | `1000000` | Maximum Lua VM instructions per coroutine resume. Protects against runaway scripts. Set to `0` to disable. |
| `EnableScriptsAtStart` | bool | `false` | Whether scripts start enabled when loaded. When `false`, scripts are loaded but must be enabled manually from the Scripts tab. |
| `AllowFileIO` | bool | `true` | Whether the `file` global and `emu.load()` are available to Lua scripts. Set to `false` in environments without filesystem access (e.g. WASM/browser). |
| `AllowFileWrite` | bool | `false` | Whether scripts may write, append, or delete files via the `file` global. Read operations are always permitted when `AllowFileIO` is `true`. |
| `FileBaseDirectory` | string | `null` | Base directory for all file I/O. When `null` or empty, defaults to `ScriptDirectory`. All script-supplied paths are resolved relative to this directory; traversal outside it (e.g. `../`) is blocked. |
| `AllowHttpRequests` | bool | `true` | Whether the `http` global is available to Lua scripts. When `true`, scripts may make outbound HTTP GET and POST requests to arbitrary URLs. Default is `true`. |
| `AllowStore` | bool | `true` | Whether the `store` global is available to Lua scripts. Provides a cross-platform key/value store. On desktop, backed by files in `StoreSubDirectory`. In browser, backed by `localStorage`. Default is `true`. |
| `StoreSubDirectory` | string | `".store"` | Subdirectory within `ScriptDirectory` used for the filesystem store backend (desktop only). Default is `".store"`. |
| `AllowTcpClient` | bool | `false` | Whether the `tcp` global is available to Lua scripts. Desktop only — forced `false` in browser/WASM builds. Default is `false`. |

# Scripts tab UI

The **Scripts** tab in the application shows all loaded scripts with their current state. Each row displays:

- **Status dot** -- green (running), red (system-disabled), or grey (user-disabled / completed).
- **Enable/disable checkbox** -- toggle individual scripts on or off at runtime.
- **Reload button** (&#x21bb;) -- re-reads the script from disk, recompiles, and runs it. Available when the script is not actively running (i.e. user-disabled, completed, or errored).
- **Script name, status, yield type, and registered hooks.**

The tab header shows a count of active scripts and, if any scripts were disabled by the system (syntax or runtime errors), a red count of disabled scripts.

# Lua API reference

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
| `emu.systems()` | table | List of available system names (e.g. `{"C64", "Generic"}`). |
| `emu.selected_system()` | string | Currently selected system name. |
| `emu.selected_variant()` | string | Currently selected system variant name. |

## Emulator control

Control operations are deferred -- they take effect after the current frame completes.

| Function | Description |
|----------|-------------|
| `emu.start()` | Request emulator start or resume. |
| `emu.pause()` | Request emulator pause. |
| `emu.stop()` | Request emulator stop. |
| `emu.reset()` | Request emulator stop + restart. |
| `emu.select(name [, variant])` | Request system selection. The emulator must be stopped. |

## CPU registers (`cpu`)

All properties are read-only and return safe defaults (`0` or `false`) before a system is started.

| Property | Type | Description |
|----------|------|-------------|
| `cpu.pc` | int | Program Counter (0-65535) |
| `cpu.a` | int | Accumulator (0-255) |
| `cpu.x` | int | Index register X (0-255) |
| `cpu.y` | int | Index register Y (0-255) |
| `cpu.sp` | int | Stack Pointer (0-255) |
| `cpu.carry` | bool | Carry flag |
| `cpu.zero` | bool | Zero flag |
| `cpu.negative` | bool | Negative flag |
| `cpu.overflow` | bool | Overflow flag |
| `cpu.interrupt_disable` | bool | Interrupt disable flag |
| `cpu.decimal_mode` | bool | Decimal mode flag |

## Memory access (`mem`)

| Function | Description |
|----------|-------------|
| `mem.read(address)` | Read a byte from emulator memory. Returns 0-255. Address is masked to 16-bit range. |
| `mem.write(address, value)` | Write a byte to emulator memory. Address is masked to 16-bit, value to 8-bit. |

Memory reads and writes go through the same address decoding as the emulated CPU, including I/O registers. For example, on the C64, `mem.read(0xD012)` reads the VIC-II raster line register and `mem.write(0xD020, 1)` sets the border color to white.

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

### Examples

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

### Examples

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

The TCP API is designed for low-latency per-frame communication. The connection persists across frames; 

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

### Async behaviour

`tcp.connect()`, `conn:send()`, and `conn:receive()` are all **non-blocking and async**: the coroutine suspends immediately and the emulator continues running frames. The script resumes automatically once the operation completes. From the Lua script's perspective each call looks like a normal synchronous expression:

```lua
local res = tcp.connect("127.0.0.1", 9000, 3000)  -- suspends until connected or timed out
local sr  = conn:send({1, 2, 3})                   -- suspends until sent
local lr  = conn:receive(4)                        -- suspends until 4 bytes arrive
```

### Examples

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

# Error handling

Scripts that encounter errors are automatically disabled:

- **Syntax errors** -- detected at load time. The script appears in the Scripts tab as system-disabled and cannot be toggled on.
- **Runtime errors** -- the script is stopped and marked as system-disabled. This applies to both coroutine execution and event hook invocations.
- **Instruction limit exceeded** -- if a coroutine resume exceeds `MaxInstructionsPerResume`, the script is force-suspended and system-disabled.

Disabled scripts can be fixed on disk and reloaded via the reload button in the Scripts tab without restarting the emulator.

# Examples

Example scripts are included in the `scripts/` directory:

| Script | Style | Description |
|--------|-------|-------------|
| `example_frameadvance.lua` | Linear loop | Logs CPU state every 60 frames and detects changes to the A register. |
| `example_monitor.lua` | Event hooks | Defines `on_before_frame` and `on_after_frame` hooks to log CPU state and watch the C64 raster line register. |
| `example_emulator_control.lua` | Linear loop + hooks | Demonstrates the emulator control API: queries state, pauses at frame 300, resumes after 3 seconds, and defines all state-change event hooks. |
| `example_border_cycle.lua` | Linear loop | Waits for the C64 system to be running, waits 3 seconds, then cycles the border color through all 16 C64 colors. |
| `example_file_io.lua` | Linear loop | Demonstrates the file I/O API: lists scripts, reads a text file, writes a CSV log, and shows `emu.load()` and `file.read_bytes()` usage. Requires `AllowFileIO: true`; write operations also require `AllowFileWrite: true`. |
| `example_http.lua` | Event hook | Demonstrates the HTTP API in `on_started()`: GET with and without custom headers, `post_json`, `post` with explicit content type, `get_bytes`, `download`, and error handling for unreachable hosts. Requires `AllowHttpRequests: true`. |
| `example_store.lua` | Linear loop + hooks | Demonstrates the key/value store API: persistent run counter, first-run flag, overwrite/verify, listing all keys, saving a CPU snapshot on `on_started`, and writing a frame checkpoint every 60 frames. Requires `AllowStore: true`. |
| `example_tcp_client.lua` | Linear loop | Demonstrates the TCP client API with a per-frame observation/action loop mimicking a Machine Learning / Reinforcement Learning server protocol (length-prefixed binary). Connects to a local TCP server, sends CPU state as an observation each frame, and applies the first byte of the server's response to the C64 border color register. Requires `AllowTcpClient: true`. Desktop only. |

# Technical details

## Scripting engine

The scripting system uses [MoonSharp](https://www.moonsharp.org/), a Lua interpreter written entirely in C#. MoonSharp runs Lua 5.2-compatible code without native dependencies.

The engine is implemented in three layers:

| Layer | Project | Description |
|-------|---------|-------------|
| Abstraction | `Highbyte.DotNet6502.Systems` (`Scripting/` subfolder) | Defines `IScriptingEngine`, `NoScriptingEngine`, `ScriptingEngine`, `IScriptingEngineAdapter`, `ScriptStatus`, `ScriptingConfig`, `IScriptStore`, and the adapter DTOs (`AdapterScriptHandle`, `AdapterScriptState`, `AdapterResumeResult`). Also contains `HostApp`, the base host-app class that integrates scripting into the emulator lifecycle. No dependency on MoonSharp. |
| MoonSharp adapter | `Highbyte.DotNet6502.Scripting.MoonSharp` | `MoonSharpScriptingEngineAdapter` implements `IScriptingEngineAdapter` using MoonSharp. Contains the Lua proxy classes (`LuaCpuProxy`, `LuaMemProxy`, `LuaLogProxy`, `LuaFileProxy`, `LuaHttpProxy`). Also contains `FileSystemScriptStore` and `DelegateScriptStore` (store backends). `MoonSharpScriptingConfigurator` is the factory entry point. |
| Host | `Highbyte.DotNet6502.App.Avalonia.Core` | `AvaloniaHostApp` overrides the platform-specific virtual hooks from `HostApp` to wire the `emu.yield()` tick timer and drain deferred script actions on the Avalonia UI thread. |

### ScriptingEngine + IScriptingEngineAdapter design

`ScriptingEngine` (concrete class, in `Systems/Scripting/`) implements `IScriptingEngine` and contains all engine-agnostic orchestration: script file tracking, enable/disable state, hook routing, `ScriptStatus` building, event firing, and the deferred-action queue used by `emu.start()` / `emu.stop()` etc. It delegates all Lua-VM-specific operations to an `IScriptingEngineAdapter`.

`IScriptingEngineAdapter` covers the operations that differ per Lua engine: VM initialization, script compilation, coroutine creation and resume, yield-type detection, hook function caching, and hook invocation.

`MoonSharpScriptingEngineAdapter` is the MoonSharp-specific implementation. It wraps each `.lua` file in a MoonSharp `Coroutine` (held in a `MoonSharpScriptHandle`). The Lua environment uses a soft-sandbox preset (`CoreModules.Preset_SoftSandbox | CoreModules.String | CoreModules.Math | CoreModules.Table`). The standard Lua `io` module is intentionally excluded; file I/O is provided instead through `LuaFileProxy`, a plain C# class backed by `System.IO` that enforces path confinement and write guards independently of any Lua library. `LuaFileProxy` is registered as a Lua `Table` of `DynValue.NewCallback` entries (the same pattern as the `emu` table) rather than as a MoonSharp `UserData` type, so that `file.list()` and `file.read_bytes()` can construct and return Lua `Table` objects directly. HTTP operations follow the same pattern: `LuaHttpProxy` holds a single `HttpClient` instance and is registered as the `http` Lua table; `http.download` routes through `LuaFileProxy.GetSafePath()` to ensure downloaded files stay within the file sandbox.

The `store` Lua table is registered when `AllowStore: true`. The adapter resolves the backend from `ScriptingConfig.StoreBackend`: if set (browser/WASM sets it to a `DelegateScriptStore` wrapping `localStorage` JSInterop calls), that backend is used directly; otherwise a `FileSystemScriptStore` is created automatically from `ScriptDirectory + StoreSubDirectory`. Both implement `IScriptStore` (defined in `Highbyte.DotNet6502.Systems`). `FileSystemScriptStore` validates keys as plain filenames (no path traversal) and creates the store directory on first write. `DelegateScriptStore` is a thin wrapper over four Lua lambdas, making it easy to adapt any backend (localStorage, IndexedDB, in-memory, etc.) without adding a new implementation class.

To support a different Lua engine, implement `IScriptingEngineAdapter` and a matching `AdapterScriptHandle` subclass, then pass the adapter to `new ScriptingEngine(adapter, config, loggerFactory)`. No changes to `IScriptingEngine`, `NoScriptingEngine`, or any host-app code are required.

### HostApp integration

The base class `HostApp<TInputHandlerContext, TAudioHandlerContext>` owns the scripting integration. A derived host app calls `SetScriptingEngine(engine)` once at startup (before the first `Start()`). `HostApp` then:

1. Calls `engine.SetHostApp(this)` so scripts can query and control the emulator via the `IHostApp` interface.
2. Calls `engine.LoadScripts()` to compile all `.lua` files and run initial resumes.
3. Calls the virtual `OnScriptingEngineSet()` so the derived host can start its platform-specific tick timer.

The base class calls scripting hooks directly in its concrete lifecycle methods (not in the virtual `On*` hooks):

| Lifecycle point | Call |
|---|---|
| `Start()` | `OnSystemStarted(system)` then `InvokeEvent("on_started")` |
| `Pause()` | `InvokeEvent("on_paused")` |
| `Stop()` | `InvokeEvent("on_stopped")` |
| `SelectSystem()` | `InvokeEvent("on_system_selected", name)` |
| `SelectSystemConfigurationVariant()` | `InvokeEvent("on_variant_selected", name)` |
| `RunEmulatorOneFrame()` | `InvokeBeforeFrame()` before the CPU frame, `InvokeAfterFrame()` after |
| `Close()` | `StopScriptingTimer()` |

The virtual `OnAfterStart`, `OnAfterPause`, `OnAfterStop`, etc. hooks remain empty integration points for derived classes; scripting is not invoked through them.

### Emulator control and deferred actions

Scripts call emulator control functions (`emu.start()`, `emu.pause()`, etc.) synchronously from Lua, but those operations (e.g. `IHostApp.Start()`) are asynchronous and must not execute during an active frame. `ScriptingEngine` maintains an internal pending-action queue. When `emu.start()` is called from Lua, a lambda is queued; the host drains it after the frame or timer tick by calling `DrainPendingScriptActionsAsync()` (a protected helper on `HostApp` that delegates to `ScriptingEngine.DrainPendingActionsAsync()`).

The `IScriptingEngineAdapter.InitializeVm` receives an `enqueueAction` delegate from `ScriptingEngine`, which adapter implementations use to queue deferred `IHostApp` calls without holding a direct reference to the queue.

### Portability: NLua validation

The `IScriptingEngineAdapter` interface was validated against [NLua](https://github.com/NLua/NLua) (the most popular .NET Lua library, wrapping the native Lua C API via KeraLua) to confirm all methods map cleanly. Key implementation notes for a future NLua adapter:

- **VM setup** — `new Lua()` replaces `new Script(...)`. Object globals use `lua["name"] = obj` (NLua exposes all public members by default; use `[LuaHide]` to opt out, inverse of MoonSharp's `[MoonSharpUserData]` opt-in). Functions use `lua.RegisterFunction(...)`.
- **Script loading** — `lua.LoadFile(filePath)` returns a `LuaFunction` chunk; `lua.NewThread(fn, out thread)` creates the coroutine. The handle would wrap the resulting `KeraLua.Lua` thread state.
- **Coroutine resume** — `threadState.Resume(mainState, 0, out nResults)` on the `KeraLua.Lua` thread object. Yield/dead/error state is read from the `LuaStatus` enum return value.
- **`ForceSuspended` (runaway protection)** — MoonSharp's `AutoYieldCounter` has no direct equivalent. Implement via `threadState.SetHook(CountHook, LuaHookMask.Count, N)`: set a `bool WasKilledByHook` flag on the handle _before_ calling `threadState.Error(...)` inside the hook to distinguish it from genuine runtime errors (both surface as `LuaStatus.ErrRun`).
- **`emu.frameadvance()` yield** — for Lua 5.4 correctness, implement at KeraLua level using `threadState.YieldK(1, ctx, continuation)` rather than `RegisterFunction` + `Yield()`, since NLua's `RegisterFunction` wraps methods as `lua_CFunction` without continuation support.
- **Hook cache** — cache `LuaFunction` references (from `lua.GetFunction(name)`) instead of MoonSharp `DynValue` references.
- **Proxy classes** — `LuaCpuProxy`, `LuaMemProxy`, `LuaLogProxy` are MoonSharp-specific. A NLua adapter would provide equivalent proxy classes with NLua-compatible attribute conventions.
- **File I/O and HTTP** — `LuaFileProxy` and `LuaHttpProxy` have no MoonSharp dependency (`System.IO` and `System.Net.Http` respectively) and can be reused as-is in a NLua adapter. The adapter would register their methods as individual NLua callbacks on Lua tables, exactly as the MoonSharp adapter does.
- **Key/value store** — `IScriptStore`, `FileSystemScriptStore`, and `DelegateScriptStore` have no MoonSharp dependency and can be reused as-is. The adapter would register the `store.get/set/delete/exists/list` callbacks on a Lua table in the same way.

## Script lifecycle

1. **Load** -- On startup (when scripting is enabled), all `.lua` files in the configured directory are compiled. Files with syntax errors are recorded and shown as system-disabled in the UI.
2. **Initial resume** -- Each coroutine is resumed once. This executes top-level code (variable initialization, hook function definitions, etc.) until the script yields or returns. Hook function registrations are detected by comparing global function state before and after the initial resume.
3. **Per-frame execution** -- On each emulator frame, coroutines that yielded via `emu.frameadvance()` are resumed, and `on_before_frame` / `on_after_frame` hooks are invoked.
4. **Tick execution** -- A separate timer (~60 Hz) resumes coroutines that yielded via `emu.yield()`. This timer keeps firing even while the emulator is paused or stopped.
5. **Disable/enable** -- Individual scripts can be toggled from the Scripts tab. Disabled scripts have their coroutine resumes and hook invocations skipped.
6. **Reload** -- A script can be reloaded from disk. The old coroutine and hook registrations are cleaned up, the file is recompiled, and a new coroutine is created and resumed. The script retains its position in the list.

## Threading model

The scripting threading model is intentionally simple: **all script execution must run on the same thread as the emulator frame loop**. This ensures Lua `mem.read`/`mem.write` and CPU memory access never overlap, with no locks or synchronization needed.

`HostApp` provides two protected helpers that derived host apps call from the appropriate thread:

- `InvokeScriptingTick()` — resumes `emu.yield()` coroutines (for the tick timer callback).
- `DrainPendingScriptActionsAsync()` — executes deferred emulator control actions (e.g. `emu.start()`) queued by scripts during the previous frame or tick.

`HostApp` also provides two virtual methods for derived classes that need a platform-specific tick timer (e.g. Avalonia):

- `OnScriptingEngineSet()` — called when `SetScriptingEngine()` completes; start the tick timer here. Game-loop-based hosts (SilkNet, SadConsole) typically do not need to override this.
- `StopScriptingTimer()` — called from `Close()`; stop and dispose the tick timer here. Only needed when `OnScriptingEngineSet()` was overridden.

### Avalonia (current implementation)

All script execution runs on the **Avalonia UI thread**. The emulator uses two periodic timers, both dispatching callbacks to the UI thread via `Dispatcher.UIThread.InvokeAsync`:

- **Update timer** -- fires at the emulated system's refresh rate (e.g. ~50 Hz for PAL C64). Each tick runs the full frame sequence synchronously:
  1. `InvokeBeforeFrame()` -- resumes `emu.frameadvance()` coroutines, then calls `on_before_frame` hooks.
  2. `ProcessInputBeforeFrame()` -- processes user input.
  3. `ExecuteOneFrame()` -- runs the emulated CPU for one frame's worth of cycles.
  4. `InvokeAfterFrame()` -- calls `on_after_frame` hooks.
  5. `DrainPendingScriptActionsAsync()` -- executes any emulator control operations queued by scripts.

- **Scripting tick timer** -- fires at ~60 Hz independently, also dispatched to the UI thread. Each tick calls `InvokeScriptingTick()` then `DrainPendingScriptActionsAsync()`.

Because both timers dispatch to the same UI thread, their callbacks are serialized by the Avalonia dispatcher queue and can never execute concurrently.

### SilkNet (future)

SilkNet host applications (e.g. `SilkNetHostApp`) run the emulator on the **game loop thread** managed by `IWindow.Run()`. The game loop already fires at ~60 Hz, so no separate tick timer is needed — `OnScriptingEngineSet()` does not need to be overridden.

The `OnUpdate` callback handles both frame execution and the scripting tick:
1. `RunEmulatorOneFrame()` — fires `InvokeBeforeFrame()`, the CPU frame, and `InvokeAfterFrame()` internally.
2. `InvokeScriptingTick()` — resumes `emu.yield()` coroutines.
3. `DrainPendingScriptActionsAsync()` — executes deferred emulator control operations.

Because all three steps run on the same game loop thread, no additional synchronization is needed.

### SadConsole (future)

SadConsole host applications (e.g. `SadConsoleHostApp`) drive the emulator from the **MonoGame/SadConsole update loop** (`UpdateSadConsole`, called by the game loop at ~60 Hz). As with SilkNet, the game loop itself is the tick mechanism and no separate timer is needed — `OnScriptingEngineSet()` does not need to be overridden.

The `UpdateSadConsole` method handles both frame execution and the scripting tick:
1. `RunEmulatorOneFrame()` — fires `InvokeBeforeFrame()`, the CPU frame, and `InvokeAfterFrame()` internally.
2. `InvokeScriptingTick()` — resumes `emu.yield()` coroutines.
3. `DrainPendingScriptActionsAsync()` — executes deferred emulator control operations.

Because all three steps run on the same game loop thread, no additional synchronization is needed.
