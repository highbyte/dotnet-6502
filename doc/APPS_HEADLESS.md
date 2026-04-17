<h1 align="center">Highbyte.DotNet6502.App.Headless</h1>

# Overview
Cross-platform console app that runs the emulator without any UI, rendering, audio, or user input. Driven entirely by CLI arguments and Lua scripts — useful for automation, scripting, and CI workflows.

Technologies
  - UI: none.
  - Rendering: none (null renderer).
  - Input: none (null input handler).
  - Audio: none (null audio handler).
  - Scripting: Lua via [MoonSharp](https://www.moonsharp.org/).

# Features

## Systems
- **C64** — requires ROM files (Kernal, Basic, Chargen). See [C64 ROM files](#c64-rom-files) below.
- **Generic computer** — built-in example 6502 programs (Snake, Scroll, HelloWorld).

## C64 ROM files

The C64 system requires three ROM files: **Kernal**, **Basic**, and **Character generator (Chargen)**.

> [!IMPORTANT]
> You may need a license from Commodore/Cloanto (or own a real C64) to use C64 ROM files.

### Where to get them

The ROM files can be downloaded from [zimmers.net](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/):

| ROM | File | Direct link |
|-----|------|-------------|
| Kernal | `kernal.901227-03.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/kernal.901227-03.bin) |
| Basic | `basic.901226-01.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/basic.901226-01.bin) |
| Chargen | `characters.901225-01.bin` | [download](https://www.zimmers.net/anonftp/pub/cbm/firmware/computers/c64/characters.901225-01.bin) |

### Where to put them

By default `appsettings.json` expects them in `%HOME%/Downloads/C64` (i.e. `~/Downloads/C64` on macOS/Linux, `%USERPROFILE%\Downloads\C64` on Windows). The directory and filenames can be changed in `appsettings.json`:

```json
"Highbyte.DotNet6502.C64.Headless": {
  "SystemConfig": {
    "ROMDirectory": "%HOME%/Downloads/C64",
    "ROMs": [
      { "Name": "basic",   "File": "basic.901226-01.bin" },
      { "Name": "kernal",  "File": "kernal.901227-03.bin" },
      { "Name": "chargen", "File": "characters.901225-01.bin" }
    ]
  }
}
```

## CLI arguments

### Scripting mode (`--script` / `--scriptDir`)

When a Lua script is supplied the script owns the full emulator lifecycle — it calls `emu.start()`, loads programs, and quits when done.

| Argument | Description |
|---|---|
| `--script <path>` | Load and run a Lua script (can be specified multiple times) |
| `--scriptDir <path>` | Override the script directory from `appsettings.json` |
| `--log-level <level>` / `-l <level>` | Set console log level (Trace/Debug/Information/Warning/Error) |
| `--enableExternalDebug` | Enable VS Code debug adapter (DAP) over TCP |
| `--debug-port <port>` | TCP port for the debug adapter (default: 6502) |
| `--debug-wait` | Wait for a debug client to connect before starting |

> [!IMPORTANT]
> `--script` and `--scriptDir` are **mutually exclusive** with `--system`, `--systemVariant`, `--start`, `--waitForSystemReady`, `--loadPrg`, and `--runLoadedProgram`. The script is responsible for all emulator setup and lifecycle. Combining them is an error.

### Automated startup mode (`--start`)

Used when driving the emulator from the command line without a script — primarily by the VS Code debugger extension.

| Argument | Description |
|---|---|
| `--system <name>` | Select a system (e.g. `C64`, `Generic`) |
| `--systemVariant <name>` | Select a system variant. Requires `--system`. |
| `--start` | Auto-start the emulator after selection |
| `--waitForSystemReady` | Wait until the system reports ready before continuing. Requires `--start`. |
| `--loadPrg <path>` | Load a `.prg` file into memory. Requires `--start`. |
| `--runLoadedProgram` | Run the loaded `.prg` file after loading. Requires `--start` and `--loadPrg`. |
| `--log-level <level>` / `-l <level>` | Set console log level (Trace/Debug/Information/Warning/Error) |
| `--enableExternalDebug` | Enable VS Code debug adapter (DAP) over TCP |
| `--debug-port <port>` | TCP port for the debug adapter (default: 6502) |
| `--debug-wait` | Wait for a debug client to connect before starting |

## Lua scripting
The same Lua scripting API available in the Avalonia apps is fully supported here. Scripts are the primary way to control the emulator. Example scripts are included in the `scripts/` directory:

| Script | Description |
|---|---|
| `example_emulator_control.lua` | Query state, pause/resume/reset, state-change event hooks |
| `example_c64_basic_readwrite.lua` | Write a BASIC program via keyboard buffer and read it back |
| `example_c64_border_cycle.lua` | Cycle C64 border color via memory writes |
| `example_c64_download_and_run_prg.lua` | Download and run a `.prg` file over HTTP |
| `example_c64_load_d64.lua` | Load a `.d64` disk image |
| `example_c64_screenshot.lua` | Save a screenshot to disk |
| `example_frameadvance.lua` | Advance frames and read frame count |
| `example_input_kb.lua` | Inject keyboard input |
| `example_input_joystick.lua` | Inject joystick input |
| `example_monitor.lua` | Interact with the machine code monitor |
| `example_file_io.lua` | Read and write files from Lua |
| `example_http.lua` | Make HTTP requests from Lua |
| `example_tcp_client.lua` | Connect to a TCP server from Lua |
| `example_store.lua` | Persist data between script runs using the key-value store |
| `example_quit.lua` | Quit the emulator from a script |

See [Scripting](SCRIPTING.md) for the full Lua API reference.

## External debug adapter
The headless app supports the [Debug Adapter Protocol (DAP)](https://microsoft.github.io/debug-adapter-protocol/) over TCP, allowing VS Code to attach a debugger while the emulator runs headless.

See the [VS Code debugger extension](../tools/vscode-extension/README.md) for details.

# Example usage

Examples below use `dotnet-6502-headless` as installed via Homebrew or Scoop.
- Manual download (self-contained binary): replace with `./Highbyte.DotNet6502.App.Headless` (or `Highbyte.DotNet6502.App.Headless.exe` on Windows)
- Running from source: replace with `dotnet run --project src/apps/Highbyte.DotNet6502.App.Headless --`

Start a C64 and run a Lua script (script owns all setup and lifecycle):
```
dotnet-6502-headless --script scripts/example_c64_basic_readwrite.lua
```

Start with debug adapter listening on port 6502, waiting for client (no script):
```
dotnet-6502-headless --system C64 --start --enableExternalDebug --debug-port 6502 --debug-wait
```

Example console output:
```
09:12:01 info: Program[0] Starting headless emulator.
09:12:01 info: Program[0] Creating configuration object.
09:12:01 info: Program[0] Initializing logging.
09:12:01 info: Program[0] Reading emulator config.
09:12:01 info: HeadlessHostApp[0] Creating headless host app.
09:12:01 info: HeadlessHostApp[0] Headless host app initialized.
09:12:01 info: HeadlessHostApp[0] Headless emulator running. Press Ctrl+C to exit.
09:12:01 info: Script[0] Waiting for C64 emulator to start...
09:12:02 info: Script[0] C64 emulator started. Waiting for BASIC to initialize...
09:12:03 info: Script[0] BASIC ready. Typing BASIC program...
09:12:05 info: Script[0] Retrieved BASIC source:
09:12:05 info: Script[0] 10 PRINT "HELLO FROM LUA"
                          20 GOTO 10
09:12:05 info: Script[0] Round-trip check PASSED: both lines found in retrieved source.
```

# Configuration

Edit `appsettings.json` in the app directory to configure ROM paths and scripting settings:

```json
{
  "Highbyte.DotNet6502.Scripting": {
    "Enabled": true,
    "ScriptDirectory": "scripts"
  },
  "Highbyte.DotNet6502.C64.Headless": {
    "SystemConfig": {
      "ROMDirectory": "%HOME%/Downloads/C64"
    }
  }
}
```

# Download and install

See [INSTALL_HEADLESS.md](INSTALL_HEADLESS.md) for download links and installation instructions for Windows, Linux, and macOS.

# How to run locally for development
For development system requirements, see [here](DEVELOP.md#Requirements).

## Visual Studio 2026 or 2022 (Windows)
Open solution `dotnet-6502.sln`.
Set project `Highbyte.DotNet6502.App.Headless` as startup and start with F5.

## VSCode
A launch configuration is included. Open the repo in VSCode and use the `Headless` launch profile.
