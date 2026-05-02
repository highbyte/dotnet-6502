# Scripting (MoonSharp)

Library: `Highbyte.DotNet6502.Scripting.MoonSharp`

For the user-facing scripting guide (Lua API, configuration, examples), see [Tools / Scripting](../../tools/scripting/overview.md).

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
- **`ForceSuspended` (runaway protection)** — MoonSharp's `AutoYieldCounter` has no direct equivalent. Implement via `threadState.SetHook(CountHook, LuaHookMask.Count, N)`: set a `bool WasKilledByHook` flag on the handle *before* calling `threadState.Error(...)` inside the hook to distinguish it from genuine runtime errors (both surface as `LuaStatus.ErrRun`).
- **`emu.frameadvance()` yield** — for Lua 5.4 correctness, implement at KeraLua level using `threadState.YieldK(1, ctx, continuation)` rather than `RegisterFunction` + `Yield()`, since NLua's `RegisterFunction` wraps methods as `lua_CFunction` without continuation support.
- **Hook cache** — cache `LuaFunction` references (from `lua.GetFunction(name)`) instead of MoonSharp `DynValue` references.
- **Proxy classes** — `LuaCpuProxy`, `LuaMemProxy`, `LuaLogProxy` are MoonSharp-specific. A NLua adapter would provide equivalent proxy classes with NLua-compatible attribute conventions.
- **File I/O and HTTP** — `LuaFileProxy` and `LuaHttpProxy` have no MoonSharp dependency (`System.IO` and `System.Net.Http` respectively) and can be reused as-is in a NLua adapter. The adapter would register their methods as individual NLua callbacks on Lua tables, exactly as the MoonSharp adapter does.
- **Key/value store** — `IScriptStore`, `FileSystemScriptStore`, and `DelegateScriptStore` have no MoonSharp dependency and can be reused as-is. The adapter would register the `store.get/set/delete/exists/list` callbacks on a Lua table in the same way.

## Script lifecycle

1. **Load** — On startup (when scripting is enabled), all `.lua` files in the configured directory are compiled. Files with syntax errors are recorded and shown as system-disabled in the UI.
2. **Initial resume** — Each coroutine is resumed once. This executes top-level code (variable initialization, hook function definitions, etc.) until the script yields or returns. Hook function registrations are detected by comparing global function state before and after the initial resume.
3. **Per-frame execution** — On each emulator frame, coroutines that yielded via `emu.frameadvance()` are resumed, and `on_before_frame` / `on_after_frame` hooks are invoked.
4. **Tick execution** — A separate timer (~60 Hz) resumes coroutines that yielded via `emu.yield()`. This timer keeps firing even while the emulator is paused or stopped.
5. **Disable/enable** — Individual scripts can be toggled from the Scripts tab. Disabled scripts have their coroutine resumes and hook invocations skipped.
6. **Reload** — A script can be reloaded from disk. The old coroutine and hook registrations are cleaned up, the file is recompiled, and a new coroutine is created and resumed. The script retains its position in the list.

## Threading model

The scripting threading model is intentionally simple: **all script execution must run on the same thread as the emulator frame loop**. This ensures Lua `mem.read` / `mem.write` and CPU memory access never overlap, with no locks or synchronization needed.

`HostApp` provides two protected helpers that derived host apps call from the appropriate thread:

- `InvokeScriptingTick()` — resumes `emu.yield()` coroutines (for the tick timer callback).
- `DrainPendingScriptActionsAsync()` — executes deferred emulator control actions (e.g. `emu.start()`) queued by scripts during the previous frame or tick.

`HostApp` also provides two virtual methods for derived classes that need a platform-specific tick timer (e.g. Avalonia):

- `OnScriptingEngineSet()` — called when `SetScriptingEngine()` completes; start the tick timer here. Game-loop-based hosts (SilkNet, SadConsole) typically do not need to override this.
- `StopScriptingTimer()` — called from `Close()`; stop and dispose the tick timer here. Only needed when `OnScriptingEngineSet()` was overridden.

### Avalonia (current implementation)

All script execution runs on the **Avalonia UI thread**. The emulator uses two periodic timers, both dispatching callbacks to the UI thread via `Dispatcher.UIThread.InvokeAsync`:

- **Update timer** — fires at the emulated system's refresh rate (e.g. ~50 Hz for PAL C64). Each tick runs the full frame sequence synchronously:
    1. `InvokeBeforeFrame()` — resumes `emu.frameadvance()` coroutines, then calls `on_before_frame` hooks.
    2. `ProcessInputBeforeFrame()` — processes user input.
    3. `ExecuteOneFrame()` — runs the emulated CPU for one frame's worth of cycles.
    4. `InvokeAfterFrame()` — calls `on_after_frame` hooks.
    5. `DrainPendingScriptActionsAsync()` — executes any emulator control operations queued by scripts.
- **Scripting tick timer** — fires at ~60 Hz independently, also dispatched to the UI thread. Each tick calls `InvokeScriptingTick()` then `DrainPendingScriptActionsAsync()`.

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
