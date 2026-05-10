# Avalonia Browser app

## Overview

Cross-platform browser app written with [Avalonia UI](https://avaloniaui.net/). Shares almost all code (including UI) with the [Avalonia Desktop app](../desktop-apps/avalonia-desktop.md).

![Avalonia Browser WebAssembly app, C64 Basic](../assets/screenshots/AvaloniaBrowser_C64_Basic.png){ width="33%" }
![Avalonia Browser WebAssembly app, C64 Montezuma's Revenge](../assets/screenshots/AvaloniaBrowser_C64_Montezuma.png){ width="33%" }
![Avalonia Browser WebAssembly app, C64 monitor](../assets/screenshots/AvaloniaBrowser_C64_Monitor.png){ width="33%" }

Technologies:

- UI: `Avalonia` UI controls.
- Rendering: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md).
- Input: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md) + [`Highbyte.DotNet6502.Impl.Browser`](../libraries/implementation/browser.md) (gamepad).
- Audio: [`Highbyte.DotNet6502.Impl.NAudio`](../libraries/implementation/naudio.md). Synthesizer via `NAudio` and playback via WebAudio JS interop.

Live version: <https://highbyte.se/dotnet-6502/app2>

## Install

The browser app runs entirely in the browser — no installation required. Open the [live version](https://highbyte.se/dotnet-6502/app2) and start the emulator from the UI.

To self-host, see [Run from command line](#run-from-command-line) below.

## Features

### System: C64

- Via the C64 config UI you have to upload binaries for the ROMs that a C64 uses (Kernal, Basic, Chargen). Or use the convenient auto-download functionality (with a license notice). For details on ROM files, see [Systems / C64 / ROMs](../systems/c64/roms.md).

--8<-- "avalonia-c64-renderers.md"

- Input using `Avalonia`.
- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

### System: Generic computer

--8<-- "avalonia-generic-computer.md"

### Lua scripting

The browser app supports the same Lua scripting API as the Avalonia Desktop app, except for filesystem and TCP access (the browser sandbox does not allow them; the key/value store falls back to `localStorage`). For the full guide, see [Tools / Scripting](../tools/scripting/overview.md).

### URL query parameters

The Avalonia Browser app supports URL-driven startup automation. This is the browser counterpart to the Avalonia Desktop app's [CLI arguments](../desktop-apps/avalonia-desktop.md#cli-arguments): instead of passing `--system` or `--script`, you encode the request in the page URL query string.

Query parameter names are case-insensitive. Boolean flags treat an empty value, `1`, `true`, and `yes` as true.

| Query parameter | Purpose | Notes |
| --- | --- | --- |
| `system` | Pre-select a system such as `C64` or `Generic`. | Mirrors desktop `--system`. |
| `systemVariant` | Pre-select a system variant. | Requires `system`. Mirrors desktop `--systemVariant`. |
| `start` | Auto-start the selected system. | Requires `system`. Mirrors desktop `--start`. |
| `waitForSystemReady` | Wait until the system reports ready. | Requires `system` and `start`. Mirrors desktop `--waitForSystemReady`. |
| `loadPrgUrl` | Fetch a `.prg` over HTTP and load it into memory. | Requires `system` and `start`. Browser equivalent of desktop `--loadPrg`, but uses a URL instead of a local file path. Relative URLs are resolved from the app origin. |
| `runLoadedProgram` | Start executing the loaded PRG from its load address. | Requires `loadPrgUrl`. Mirrors desktop `--runLoadedProgram`. |
| `script` | Run an inline Lua script supplied as base64url-encoded UTF-8 text. | Browser only. Mutually exclusive with all system-driven parameters below. Disabled by default; gated by `Scripting.AllowUrlScripts`. |
| `scriptUrl` | Fetch a Lua script from a relative or absolute URL and run it. | Same behavior as `script`, but avoids URL-length limits. Disabled by default; gated by `Scripting.AllowUrlScripts`. |

Validation rules are intentionally forgiving: invalid combinations are ignored and the normal UI still loads.

1. `systemVariant` requires `system`.
2. `start` and `waitForSystemReady` require `system`.
3. `waitForSystemReady` requires `start`.
4. `loadPrgUrl` requires `system` and `start`.
5. `runLoadedProgram` requires `loadPrgUrl`.
6. `script` and `scriptUrl` are mutually exclusive.
7. `script` and `scriptUrl` are also mutually exclusive with `system`, `start`, `waitForSystemReady`, `loadPrgUrl`, and `runLoadedProgram`.

Examples:

```text
# Start C64 PAL and wait until the machine is ready
?system=C64&systemVariant=PAL&start=1&waitForSystemReady=1

# Load and run a bundled PRG
?system=C64&start=1&waitForSystemReady=1&loadPrgUrl=prg/c64/smooth_scroller_and_raster.prg&runLoadedProgram=1

# Run an inline Lua script (base64url for: log.info('hello'))
?script=bG9nLmluZm8oJ2hlbGxvJyk

# Run a Lua script fetched over HTTP
?scriptUrl=scripts/example_emulator_control.lua
```

Important differences from desktop automation:

- `loadPrgUrl` and `scriptUrl` use browser HTTP fetch semantics, so normal browser origin and CORS rules apply.
- URL-driven Lua is **disabled by default**. Enable **Allow URL-driven scripts (script / scriptUrl query params)** in the browser app's general settings, save, then reload the page.
- URL-driven scripts do not behave exactly like desktop `--script`: the browser app still selects the configured default system first, then enables the injected script. The script can still take over by calling APIs such as `emu.select(...)` and `emu.start()`.

## How to run locally for development

For development system requirements, see [Development](../home/development.md).

### Visual Studio (Windows)

Open solution `dotnet-6502.sln`. Set project `Highbyte.DotNet6502.App.Avalonia.Browser` as startup, and start with F5.

!!! important
    Running a Debug build of the Avalonia Browser app is very slow. To get acceptable performance a published release build with AOT is required. The Avalonia Desktop app has ok performance in Debug mode, so using the Desktop app when developing and testing locally is recommended.

### Run from command line

#### Run Debug build (very slow)

```sh
cd ./src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser
dotnet run
```

Open browser at <http://localhost:5000>.

#### Run optimized Publish build (AOT)

To serve the published build, the example below uses the .NET global tool `dotnet-serve`. Install with `dotnet tool install --global dotnet-serve`.

```powershell
cd ./src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser
if(Test-Path ./bin/Publish/) { del ./bin/Publish/ -r -force }
dotnet publish -c Release -o ./bin/Publish/
dotnet serve -o:/ --directory ./bin/Publish/wwwroot/
```

A browser is automatically opened.
