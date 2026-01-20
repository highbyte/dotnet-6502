<h1 align="center">Highbyte.DotNet6502.App.SadConsole</h1>

# Overview
Cross-platform desktop app written with [`SadConsole`](https://github.com/Thraka/SadConsole) terminal/ascii/console/game engine.

<img align="top" src="Screenshots/SadConsole_C64_Basic.png" width="25%" height="25%" title="SadConsole native app, C64 Basic" /> <img align="top" src="Screenshots/SadConsole_C64_Monitor.png" width="38%" height="38%" title="SadConsole native app, C64 Monitor" />

Technologies
  - UI: `SadConsole` UI controls.
  - Rendering: `Highbyte.DotNet6502.Impl.SadConsole`.
  - Input: `Highbyte.DotNet6502.Impl.SadConsole`.
  - Audio: `Highbyte.DotNet6502.Impl.NAudio`. Synthesizer via `NAudio` and playback via `OpenAL`.

See [here](DESKTOP_APPS.md) how to download and run pre-built executables.

# Features

## System: C64 
- A directory containing the C64 ROM files (Kernal, Basic, Chargen) is supplied by the user. Defaults are set in the appsettings.json file, and possible to change in the UI. Also a auto-download option exists (license required).

- Renderer provider `Video commands` -> target `Skia commands`
  - Character mode (normal).
  - Only video mode that works in C64 character mode (not multicolor) with built-in characters set from ROM is supported. 

- Audio via [NAudio](https://github.com/naudio/NAudio) synthesizer.

## System: Generic computer 
TODO

## Monitor
Press button or toggle with F12.
TODO

## Stats
Press button or toggle with F11.
TODO

# How to run locally for development
For development system requirements, see details [here](DEVELOP.md#Requirements)

## Prerequisites, compatibility, and troubleshooting
See [here](APPS_SADCONSOLE_TROUBLESHOOT.md)

## Visual Studio 2026 or 2022 (Windows)
Open solution `dotnet-6502.sln`.
Set project `Highbyte.DotNet6502.App.SadConsole` as startup, and start with F5.

## VSCode

TODO
