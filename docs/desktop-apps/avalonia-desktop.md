# Avalonia Desktop app

Cross-platform desktop app written with [Avalonia UI](https://avaloniaui.net/). Shares almost all code (including UI) with the [Avalonia Browser app](../web-apps/avalonia-browser.md).

![Avalonia Desktop app, C64 Basic](../assets/screenshots/AvaloniaDesktop_C64_Basic.png){ width="25%" }
![Avalonia Desktop app, C64 scroll](../assets/screenshots/AvaloniaDesktop_C64_raster_scroll.png){ width="25%" }
![Avalonia Desktop app, C64 monitor](../assets/screenshots/AvaloniaDesktop_C64_Monitor.png){ width="25%" }

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

--8<-- "avalonia-c64-renderers.md"

- Input using `Avalonia` (keyboard) + `SDL` (joystick).
- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

### System: Generic computer

--8<-- "avalonia-generic-computer.md"

### Lua scripting

The Avalonia Desktop app supports Lua scripting via MoonSharp. See [Tools / Scripting](../tools/scripting/overview.md) for the full guide.

### VS Code debug adapter

The app exposes a TCP-based debug adapter for source-level debugging from VS Code. See [Tools / VSCode debugger / Debugging](../tools/vscode-debugger/debugging.md).

### Remote control

The app can expose a TCP remote control endpoint that lets external processes drive the running emulator. See [Tools / Remote control](../tools/remote-control/overview.md).

### UI

#### Menu

Start and stop of selected system.

Configuration options of selected system.

#### Monitor

An Avalonia implementation of the [machine code monitor](../libraries/core/dotnet6502-monitor.md) is available by pressing F12.

#### Stats

A toggleable stats window by pressing F11.

## CLI arguments

The desktop app can be launched from the command line with arguments to control logging, scripting, debug adapter, and remote control.

--8<-- "cli-arguments-reference.md"

### Examples

```sh
# Run a Lua script (script owns all setup and lifecycle)
./Highbyte.DotNet6502.App.Avalonia.Desktop --script scripts/example_c64_basic_readwrite.lua

# Start C64 and load a .prg file via CLI (no script)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadPrg game.prg --runLoadedProgram

# Start with debug adapter for VS Code, waiting for client
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --enableExternalDebug --debug-port 6502 --debug-wait

# Start with debug adapter bound to all interfaces (use only on trusted networks)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --enableExternalDebug --debug-port 6502 --debug-bind-address 0.0.0.0

# Start with remote control server on port 6510 (loopback only)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --remote-port 6510

# Start with remote control server accessible from the network (trusted networks only)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --remote-port 6510 --remote-bind-address 0.0.0.0
```

## How to run locally for development

For development system requirements, see [Development](../home/development.md).

### Prerequisites, compatibility, and troubleshooting

See [Avalonia Desktop app troubleshooting](avalonia-desktop-troubleshooting.md).

### Visual Studio (Windows)

Open solution `dotnet-6502.sln`. Set project `Highbyte.DotNet6502.App.Avalonia.Desktop` as startup, and start with F5.

### VSCode

TODO

## See also

- [Avalonia Desktop app automation](avalonia-desktop-automation.md) — UI automation and accessibility.
- [Avalonia Desktop app troubleshooting](avalonia-desktop-troubleshooting.md) — platform-specific notes.
