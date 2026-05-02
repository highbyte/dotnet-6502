# Avalonia Desktop app

Cross-platform desktop app written with [Avalonia UI](https://avaloniaui.net/). Shares almost all code (including UI) with the [Avalonia Browser app](../web-apps/avalonia-browser.md).

<img align="top" src="../assets/screenshots/AvaloniaDesktop_C64_Basic.png" width="25%" height="25%" title="Avalonia Desktop app, C64 Basic" />
<img align="top" src="../assets/screenshots/AvaloniaDesktop_C64_raster_scroll.png" width="25%" height="25%" title="Avalonia Desktop app, C64 scroll" />
<img align="top" src="../assets/screenshots/AvaloniaDesktop_C64_Monitor.png" width="25%" height="25%" title="Avalonia Desktop app, C64 monitor" />

Technologies:

- UI: `Avalonia` UI controls.
- Rendering: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md).
- Input: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md) + [`Highbyte.DotNet6502.Impl.SilkNet.SDL`](../libraries/implementation/silknet-sdl.md) (joystick).
- Audio: [`Highbyte.DotNet6502.Impl.NAudio`](../libraries/implementation/naudio.md). Synthesizer via `NAudio` and playback via `OpenAL`.

## Installation

See [Desktop apps installation](installation.md) for package manager and manual download instructions.

## Features

### System: C64

- A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user. Defaults are set in `appsettings.json` and possible to change in the UI. An auto-download option also exists (license required). See [Systems / C64 / ROMs](../systems/c64/roms.md) for details.

- Renderer provider `Rasterizer` → target `Avalonia 2-layer bitmap`
    - Character mode (normal and multi-color).
    - Bitmap mode (normal and bitmap mode).
    - Sprites (normal and multi-color).
    - Rendering of raster lines for border and background colors.

- Renderer provider `Video commands` → target `Skia commands`
    - Character mode (normal).

- Input using `Avalonia` (keyboard) + `SDL` (joystick).
- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

### System: Generic computer

The example 6502 machine code that is loaded and run by default for the *Generic* computer is an assembled version of [this 6502 assembly code](https://github.com/highbyte/dotnet-6502/blob/master/samples/Assembler/Generic/hostinteraction_scroll_text_and_cycle_colors.asm).

### Lua scripting

The Avalonia Desktop app supports Lua scripting via MoonSharp. See [Tools / Scripting](../tools/scripting/overview.md) for the full guide.

### VS Code debug adapter

The app exposes a TCP-based debug adapter for source-level debugging from VS Code. See [Tools / VSCode debugger / Debugging](../tools/vscode-debugger/debugging.md).

### Remote control

The app can expose a TCP remote control endpoint that lets external processes drive the running emulator. See [Tools / Remote control](../tools/remote-control/overview.md).

### CLI arguments

The desktop app can be launched from the command line with arguments to control logging, scripting, debug adapter, and remote control. See [Tools / CLI arguments](../tools/cli-arguments.md) for the full reference.

### UI

#### Menu

Start and stop of selected system.

Configuration options of selected system.

#### Monitor

An Avalonia implementation of the [machine code monitor](../libraries/core/dotnet6502-monitor.md) is available by pressing F12.

#### Stats

A toggleable stats window by pressing F11.

## How to run locally for development

For development system requirements, see [Development](../home/development.md).

### Prerequisites, compatibility, and troubleshooting

See [Avalonia Desktop app troubleshooting](avalonia-desktop-troubleshooting.md).

### Visual Studio 2026 / 2022 (Windows)

Open solution `dotnet-6502.sln`. Set project `Highbyte.DotNet6502.App.Avalonia.Desktop` as startup, and start with F5.

### VSCode

TODO

## See also

- [Avalonia Desktop app automation](avalonia-desktop-automation.md) — UI automation and accessibility.
- [Avalonia Desktop app troubleshooting](avalonia-desktop-troubleshooting.md) — platform-specific notes.
