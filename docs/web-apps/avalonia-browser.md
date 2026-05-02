# Avalonia Browser app

## Overview

Cross-platform browser app written with [Avalonia UI](https://avaloniaui.net/). Shares almost all code (including UI) with the [Avalonia Desktop app](../desktop-apps/avalonia-desktop.md).

<img align="top" src="../assets/screenshots/AvaloniaBrowser_C64_Basic.png" width="33%" height="33%" title="Avalonia Browser WebAssembly app, C64 Basic" />
<img align="top" src="../assets/screenshots/AvaloniaBrowser_C64_Montezuma.png" width="33%" height="33%" title="Avalonia Browser WebAssembly app, C64 Montezuma's Revenge" />
<img align="top" src="../assets/screenshots/AvaloniaBrowser_C64_Monitor.png" width="33%" height="33%" title="Avalonia Browser WebAssembly app, C64 monitor" />

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

- Renderer provider `Rasterizer` → target `Avalonia 2-layer bitmap`
    - Character mode (normal and multi-color).
    - Bitmap mode (normal and bitmap mode).
    - Sprites (normal and multi-color).
    - Rendering of raster lines for border and background colors.

- Renderer provider `Video commands` → target `Skia commands`
    - Character mode (normal).

- Input using `Avalonia`.
- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

### System: Generic computer

The example 6502 machine code that is loaded and run by default for the *Generic* computer is an assembled version of [this 6502 assembly code](https://github.com/highbyte/dotnet-6502/blob/master/samples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.asm).

### Lua scripting

The browser app supports the same Lua scripting API as the Avalonia Desktop app, except for filesystem and TCP access (the browser sandbox does not allow them; the key/value store falls back to `localStorage`). For the full guide, see [Tools / Scripting](../tools/scripting/overview.md).

## How to run locally for development

For development system requirements, see [Development](../home/development.md).

### Visual Studio 2026 / 2022 (Windows)

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
