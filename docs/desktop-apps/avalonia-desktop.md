# Avalonia Desktop app

Cross-platform desktop app written with [Avalonia UI](https://avaloniaui.net/). Shares almost all code (including UI) with the [Avalonia Browser app](../web-apps/avalonia-browser.md).

![Avalonia Desktop app, C64 Basic](../assets/screenshots/AvaloniaDesktop_C64_Basic.png){ width="25%" }
![Avalonia Desktop app, C64 scroll](../assets/screenshots/AvaloniaDesktop_C64_raster_scroll.png){ width="25%" }
![Avalonia Desktop app, C64 monitor](../assets/screenshots/AvaloniaDesktop_C64_Monitor.png){ width="25%" }

Technologies:

- UI: `Avalonia` UI controls.
- Rendering: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md).
- Input: [`Highbyte.DotNet6502.Impl.Avalonia`](../libraries/implementation/avalonia.md) + [`Highbyte.DotNet6502.Impl.SilkNet.SDL`](../libraries/implementation/silknet-sdl.md) (joystick).
- Audio: [`Highbyte.DotNet6502.Impl.NAudio`](../libraries/implementation/naudio.md), playback via `OpenAL`. Two C64 audio providers available: a sample-based one (good but not perfect accuracy — the default) and a command-stream synthesizer one (low CPU but inaccurate). See [C64 audio](../systems/c64/libraries.md#audio).

## Installation

See [Desktop apps installation](installation.md) for package manager and manual download instructions.

## Features

### System: C64

- A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user. Defaults are set in `appsettings.json` and possible to change in the UI. An auto-download option also exists (license required). See [Systems / C64 / ROMs](../systems/c64/roms.md) for details.

--8<-- "avalonia-c64-renderers.md"

- Input using `Avalonia` (keyboard) + `SDL` (joystick). Keyboard uses `Avalonia.Input.PhysicalKey`
  (W3C `code`), so both `US` and `Swedish` C64 keyboard layouts work; layout is auto-detected from
  the host (Win32 KLID / macOS `TIS*`) and can be overridden in the C64 config dialog. See
  [Systems / C64 / Keyboard mapping](../systems/c64/keyboard.md) for the full host-agnostic mapping.
- Audio via [NAudio](https://github.com/naudio/NAudio). Defaults to the sample-based SID
  provider; switch to the command-stream provider in the C64 config dialog if you need
  lower CPU. The SID emulation mode (`Auto` / `Fast`) is selectable in the same dialog.
- Optional SwiftLink cartridge support with `RawTcp` and `HayesModem` transport modes. This is
  the native host currently recommended for SwiftLink-based software such as Compunet Reborn. See
  [Systems / C64 / SwiftLink support](../systems/c64/swiftlink.md).

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

The desktop app can be launched from the command line with arguments for automated startup (system
selection, program / disk loading, C64 BASIC paste), scripting, logging, the debug adapter, and
remote control.

--8<-- "cli-arguments-reference.md"

### Examples

```sh
# Run a Lua script (script owns all setup and lifecycle)
./Highbyte.DotNet6502.App.Avalonia.Desktop --script scripts/example_c64_basic_readwrite.lua

# Start C64 and load a local .prg file via CLI (no script)
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --loadPrg game.prg --runLoadedProgram

# Start C64, fetch a .prg over HTTP, and run it
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadPrgUrl https://example.com/game.prg --runLoadedProgram

# Start C64, paste BASIC source from a local file and run it
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --basicFile hello.bas --runBasic

# Start C64 PAL, mount a .d64, paste LOAD"*",8,1 + RUN, set keyboard-joystick to port 2
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --systemVariant C64PAL --start --waitForSystemReady --loadD64 "/path/to/SomeGame.d64" --diskMount --runLoadedProgram --keyboardJoystickEnabled --keyboardJoystickNumber 2

# Start C64, direct-load the first PRG from a .d64 image (no disk mount) and RUN it
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadD64 "/path/to/SomeGame.d64" --d64Program "*" --runLoadedProgram

# Start C64, fetch a .d64 over HTTP, direct-load the first PRG (no disk mount) and RUN it
./Highbyte.DotNet6502.App.Avalonia.Desktop --system C64 --start --waitForSystemReady --loadD64Url https://example.com/game.d64 --d64Program "*" --runLoadedProgram

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
