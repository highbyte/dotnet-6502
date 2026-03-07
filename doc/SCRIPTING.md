<h1 align="center">Lua Scripting</h1>

# Overview

The Avalonia Desktop application supports Lua scripting for automating emulator interaction, monitoring CPU/memory state, and controlling the emulator at runtime. Scripts are loaded from a configurable directory and executed as coroutines alongside the emulator's frame loop.

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
    "EnableScriptsAtStart": false
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `false` | Master switch for the scripting system. |
| `ScriptDirectory` | string | `""` | Directory to load `.lua` files from. Absolute path, or relative to the application working directory. |
| `MaxExecutionWarningMs` | int | `5` | Log a warning if a script hook takes longer than this many milliseconds. Set to `0` to disable. |
| `MaxInstructionsPerResume` | int | `1000000` | Maximum Lua VM instructions per coroutine resume. Protects against runaway scripts. Set to `0` to disable. |
| `EnableScriptsAtStart` | bool | `false` | Whether scripts start enabled when loaded. When `false`, scripts are loaded but must be enabled manually from the Scripts tab. |

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

## Standard Lua libraries

The following standard Lua modules are available: `string`, `math`, `table`, and the soft-sandbox base functions (`print`, `type`, `tostring`, `tonumber`, `pairs`, `ipairs`, etc.). File I/O and OS functions are not available.

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

# Technical details

## Scripting engine

The scripting system uses [MoonSharp](https://www.moonsharp.org/), a Lua interpreter written entirely in C#. MoonSharp runs Lua 5.2-compatible code without native dependencies.

The engine is implemented in three layers:

| Layer | Project | Description |
|-------|---------|-------------|
| Abstraction | `Highbyte.DotNet6502.Scripting` | Defines `IScriptingEngine`, `ScriptingEngine`, `IScriptingEngineAdapter`, `ScriptStatus`, `ScriptingConfig`, and the adapter DTOs (`AdapterScriptHandle`, `AdapterScriptState`, `AdapterResumeResult`). No dependency on MoonSharp. |
| MoonSharp adapter | `Highbyte.DotNet6502.Scripting.MoonSharp` | `MoonSharpScriptingEngineAdapter` implements `IScriptingEngineAdapter` using MoonSharp. Contains the Lua proxy classes (`LuaCpuProxy`, `LuaMemProxy`, `LuaLogProxy`). `MoonSharpScriptingConfigurator` is the factory entry point. |
| Host | `Highbyte.DotNet6502.App.Avalonia.Core` | The Avalonia host app (`AvaloniaHostApp`) wires the engine into the emulator lifecycle and UI (`MainViewModel`). |

### ScriptingEngine + IScriptingEngineAdapter design

`ScriptingEngine` (concrete class, in the abstraction project) implements `IScriptingEngine` and contains all engine-agnostic orchestration: script file tracking, enable/disable state, hook routing, `ScriptStatus` building, and event firing. It delegates all Lua-VM-specific operations to an `IScriptingEngineAdapter`.

`IScriptingEngineAdapter` covers the operations that differ per Lua engine: VM initialization, script compilation, coroutine creation and resume, yield-type detection, hook function caching, and hook invocation.

`MoonSharpScriptingEngineAdapter` is the MoonSharp-specific implementation. It wraps each `.lua` file in a MoonSharp `Coroutine` (held in a `MoonSharpScriptHandle`). The Lua environment uses a soft-sandbox preset (`CoreModules.Preset_SoftSandbox | CoreModules.String | CoreModules.Math | CoreModules.Table`).

To support a different Lua engine, implement `IScriptingEngineAdapter` and a matching `AdapterScriptHandle` subclass, then pass the adapter to `new ScriptingEngine(adapter, config, loggerFactory)`. No changes to `IScriptingEngine`, `NoScriptingEngine`, or any host-app code are required.

### Portability: NLua validation

The `IScriptingEngineAdapter` interface was validated against [NLua](https://github.com/NLua/NLua) (the most popular .NET Lua library, wrapping the native Lua C API via KeraLua) to confirm all methods map cleanly. Key implementation notes for a future NLua adapter:

- **VM setup** — `new Lua()` replaces `new Script(...)`. Object globals use `lua["name"] = obj` (NLua exposes all public members by default; use `[LuaHide]` to opt out, inverse of MoonSharp's `[MoonSharpUserData]` opt-in). Functions use `lua.RegisterFunction(...)`.
- **Script loading** — `lua.LoadFile(filePath)` returns a `LuaFunction` chunk; `lua.NewThread(fn, out thread)` creates the coroutine. The handle would wrap the resulting `KeraLua.Lua` thread state.
- **Coroutine resume** — `threadState.Resume(mainState, 0, out nResults)` on the `KeraLua.Lua` thread object. Yield/dead/error state is read from the `LuaStatus` enum return value.
- **`ForceSuspended` (runaway protection)** — MoonSharp's `AutoYieldCounter` has no direct equivalent. Implement via `threadState.SetHook(CountHook, LuaHookMask.Count, N)`: set a `bool WasKilledByHook` flag on the handle _before_ calling `threadState.Error(...)` inside the hook to distinguish it from genuine runtime errors (both surface as `LuaStatus.ErrRun`).
- **`emu.frameadvance()` yield** — for Lua 5.4 correctness, implement at KeraLua level using `threadState.YieldK(1, ctx, continuation)` rather than `RegisterFunction` + `Yield()`, since NLua's `RegisterFunction` wraps methods as `lua_CFunction` without continuation support.
- **Hook cache** — cache `LuaFunction` references (from `lua.GetFunction(name)`) instead of MoonSharp `DynValue` references.
- **Proxy classes** — `LuaCpuProxy`, `LuaMemProxy`, `LuaLogProxy` are MoonSharp-specific. A NLua adapter would provide equivalent proxy classes with NLua-compatible attribute conventions.

## Script lifecycle

1. **Load** -- On startup (when scripting is enabled), all `.lua` files in the configured directory are compiled. Files with syntax errors are recorded and shown as system-disabled in the UI.
2. **Initial resume** -- Each coroutine is resumed once. This executes top-level code (variable initialization, hook function definitions, etc.) until the script yields or returns. Hook function registrations are detected by comparing global function state before and after the initial resume.
3. **Per-frame execution** -- On each emulator frame, coroutines that yielded via `emu.frameadvance()` are resumed, and `on_before_frame` / `on_after_frame` hooks are invoked.
4. **Tick execution** -- A separate timer (~60 Hz) resumes coroutines that yielded via `emu.yield()`. This timer keeps firing even while the emulator is paused or stopped.
5. **Disable/enable** -- Individual scripts can be toggled from the Scripts tab. Disabled scripts have their coroutine resumes and hook invocations skipped.
6. **Reload** -- A script can be reloaded from disk. The old coroutine and hook registrations are cleaned up, the file is recompiled, and a new coroutine is created and resumed. The script retains its position in the list.

## Threading model

All script execution runs on the **Avalonia UI thread**. There are no threading issues between Lua memory access and the emulator's own CPU execution.

The emulator uses two periodic timers, both of which dispatch their callbacks to the UI thread via `Dispatcher.UIThread.InvokeAsync`:

- **Update timer** -- fires at the emulated system's refresh rate (e.g. ~50 Hz for PAL C64). Each tick runs the full frame sequence synchronously:
  1. `InvokeBeforeFrame()` -- resumes `emu.frameadvance()` coroutines, then calls `on_before_frame` hooks. Scripts may call `mem.read`/`mem.write` here.
  2. `ProcessInputBeforeFrame()` -- processes user input.
  3. `ExecuteOneFrame()` -- runs the emulated CPU for one frame's worth of cycles. The CPU reads and writes emulator memory here.
  4. `InvokeAfterFrame()` -- calls `on_after_frame` hooks. Scripts may call `mem.read`/`mem.write` here.

- **Scripting tick timer** -- fires at ~60 Hz independently. Resumes `emu.yield()` coroutines (which may also access memory).

Because both timers dispatch to the same UI thread, their callbacks are serialized by the Avalonia dispatcher queue and can never execute concurrently. This means:

- Lua `mem.read`/`mem.write` and CPU memory access never overlap.
- No locks or synchronization are needed around memory access.
- A script's `emu.frameadvance()` loop interleaves cleanly with emulator frames: the script runs before (or after) the CPU, never during.
