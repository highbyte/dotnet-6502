# SadConsole app

Cross-platform desktop app written with [`SadConsole`](https://github.com/Thraka/SadConsole) terminal/ascii/console/game engine.

![SadConsole native app, C64 Basic](../assets/screenshots/SadConsole_C64_Basic.png){ width="25%" }
![SadConsole native app, C64 Monitor](../assets/screenshots/SadConsole_C64_Monitor.png){ width="38%" }

Technologies:

- UI: `SadConsole` UI controls.
- Rendering: [`Highbyte.DotNet6502.Impl.SadConsole`](../libraries/implementation/sadconsole.md).
- Input: [`Highbyte.DotNet6502.Impl.SadConsole`](../libraries/implementation/sadconsole.md).
- Audio: [`Highbyte.DotNet6502.Impl.NAudio`](../libraries/implementation/naudio.md). Synthesizer via `NAudio` and playback via `OpenAL`.

## Installation

Manual download, see section in [installation.md](installation.md)

## Features

### System: C64

- A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user. Defaults are set in the `appsettings.json` file, and possible to change in the UI. An auto-download option also exists (license required).

- Renderer provider `Video commands` -> target `Skia commands`
    - Character mode (normal).
    - Only video mode that works in C64 character mode (not multicolor) with built-in characters set from ROM is supported.

- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

### System: Generic computer

TODO

### Monitor

Press button or toggle with F12.

### Stats

Press button or toggle with F11.

## How to run locally for development

For development system requirements, see details under [Development](../home/development.md).

### Prerequisites, compatibility, and troubleshooting

See [SadConsole troubleshooting](sadconsole-troubleshooting.md).

### Visual Studio 2026 or 2022 (Windows)

Open solution `dotnet-6502.sln`.
Set project `Highbyte.DotNet6502.App.SadConsole` as startup, and start with F5.

### VSCode

TODO
