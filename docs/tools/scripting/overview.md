# Overview of scripting

The emulator supports Lua scripting for automating emulator interaction, monitoring CPU/memory state, and controlling the emulator at runtime. Scripts are loaded from a configurable directory and executed as coroutines alongside the emulator's frame loop. Scripting is currently wired in the Avalonia Desktop and Avalonia Browser host applications; the architecture supports extension to other host applications (SilkNet, SadConsole) without changes to the scripting core.

Implementation library: [`Highbyte.DotNet6502.Scripting.MoonSharp`](../../libraries/core/dotnet6502-scripting-moonsharp.md).

## Scripting styles

Two scripting styles are supported and can be combined within a single script:

- **Linear loop (BizHawk-style):** Write a top-level `while true do ... emu.frameadvance() end` loop. The script reads like a sequential program that suspends and resumes each frame.
- **Event hooks:** Define global callback functions (e.g. `on_before_frame()`, `on_started()`) that the emulator calls at specific points.

## Scripts tab UI

The **Scripts** tab in the application shows all loaded scripts with their current state. Each row displays:

- **Status dot** — green (running), red (system-disabled), or grey (user-disabled / completed).
- **Enable/disable checkbox** — toggle individual scripts on or off at runtime.
- **Reload button** (↻) — re-reads the script from disk, recompiles, and runs it. Available when the script is not actively running (i.e. user-disabled, completed, or errored).
- **Script name, status, yield type, and registered hooks.**

The tab header shows a count of active scripts and, if any scripts were disabled by the system (syntax or runtime errors), a red count of disabled scripts.

## Where to go next

- [Configuration](configuration.md) — `appsettings.json` settings.
- [Lua API](lua-api.md) — full reference for `emu`, `cpu`, `mem`, `input`, `log`, `file`, `http`, `store`, `tcp`.
- [Examples](examples.md) — runnable scripts that combine multiple APIs.
